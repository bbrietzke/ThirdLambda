// ITerm.cs — iTerm2 proprietary / OSC escape-sequence helpers for .NET console apps.
//
// Supports:
//   • Progress bar           OSC 9 ; 4 ; state ; percent ST   (iTerm2 3.6.6+, also Windows Terminal, Ghostty, etc.)
//   • Tab / window titles    OSC 0 / 1 / 2
//   • Tab color              OSC 6 ; 1 ; bg ; {red|green|blue} ; brightness ; N ST
//   • Badge                  OSC 1337 ; SetBadgeFormat=<base64> ST
//   • Notifications          OSC 9 ; message ST
//   • Attention (dock bounce / fireworks), marks, user variables
//
// Every method is a no-op when output is redirected or the terminal isn't iTerm2
// (override with ITerm.Enabled = true/false). Inside tmux, sequences are wrapped in
// DCS passthrough automatically — requires `set -g allow-passthrough on` in tmux.conf.

using System.Text;

namespace ThirdLambda.Core;

public enum ProgressState
{
    Hidden = 0,
    Normal = 1,
    Error = 2,
    Indeterminate = 3,
    Warning = 4,
}

public enum AttentionMode
{
    /// <summary>Bounce the dock icon until the app is activated.</summary>
    Yes,
    /// <summary>Bounce the dock icon once.</summary>
    Once,
    /// <summary>Cancel a previous request.</summary>
    No,
    /// <summary>Show a fireworks animation at the cursor.</summary>
    Fireworks,
}

public static class ITerm
{
    private const string Esc = "\u001b";
    private const string St = Esc + "\\";   // String Terminator

    private static readonly object Gate = new();
    private static bool _cleanupRegistered;
    private static bool _progressActive;
    private static bool _tabColorSet;
    private static bool _badgeSet;

    /// <summary>Where sequences are written. Defaults to stdout; use Console.Error if stdout is piped data.</summary>
    public static TextWriter Output { get; set; } = Console.Out;

    /// <summary>True when running inside iTerm2 (also true when iTerm2 is the outer terminal of tmux/ssh, if env vars are forwarded).</summary>
    public static bool IsITerm { get; } =
        Env("TERM_PROGRAM") == "iTerm.app" || Env("LC_TERMINAL") == "iTerm2";

    /// <summary>True when running inside tmux (sequences get DCS-passthrough wrapping).</summary>
    public static bool IsTmux { get; } = !string.IsNullOrEmpty(Env("TMUX"));

    /// <summary>
    /// Master switch. Defaults to: iTerm2 detected AND output is a real terminal.
    /// Set it explicitly to force effects on/off (e.g. from a --no-fancy flag).
    /// </summary>
    public static bool Enabled { get; set; } = IsITerm && !Console.IsOutputRedirected;

    /// <summary>
    /// True if iTerm2 advertises OSC 9;4 support via the TERM_FEATURES "P" flag (3.6.6+).
    /// If TERM_FEATURES isn't present (e.g. over ssh), assumes support when iTerm2 is detected.
    /// </summary>
    public static bool SupportsProgress { get; } = DetectProgressSupport();

    // ───────────────────────────── Progress bar ─────────────────────────────

    /// <summary>Show a normal progress bar at <paramref name="percent"/> (0–100).</summary>
    public static void SetProgress(int percent) => SetProgress(ProgressState.Normal, percent);

    /// <summary>Show progress as a fraction of a total, e.g. SetProgress(37, 120).</summary>
    public static void SetProgress(long current, long total) =>
        SetProgress(ProgressState.Normal, total <= 0 ? 0 : (int)(current * 100 / total));

    /// <summary>Set the progress bar to an arbitrary state; percent is ignored for Hidden.</summary>
    public static void SetProgress(ProgressState state, int? percent = null)
    {
        if (!SupportsProgress) return;

        string seq = (state, percent) switch
        {
            (ProgressState.Hidden, _) => "9;4;0",
            (_, null) => $"9;4;{(int)state}",
            (_, int p) => $"9;4;{(int)state};{Math.Clamp(p, 0, 100)}",
        };

        if (WriteOsc(seq))
        {
            _progressActive = state != ProgressState.Hidden;
            if (_progressActive) EnsureCleanup();
        }
    }

    public static void SetProgressIndeterminate() => SetProgress(ProgressState.Indeterminate);
    public static void SetProgressError(int? percent = null) => SetProgress(ProgressState.Error, percent);
    public static void SetProgressWarning(int? percent = null) => SetProgress(ProgressState.Warning, percent);
    public static void ClearProgress() => SetProgress(ProgressState.Hidden);

    /// <summary>
    /// Scoped progress: clears the bar on Dispose. Call MarkFailed() to leave it red instead.
    /// <code>using var p = ITerm.BeginProgress(); p.Report(50);</code>
    /// </summary>
    public static ProgressScope BeginProgress(bool indeterminate = false) => new(indeterminate);

    // ───────────────────────────── Titles ─────────────────────────────

    /// <summary>Set the tab (icon) title — OSC 1.</summary>
    public static void SetTabTitle(string title) => WriteOsc("1;" + Sanitize(title));

    /// <summary>Set the window title — OSC 2.</summary>
    public static void SetWindowTitle(string title) => WriteOsc("2;" + Sanitize(title));

    /// <summary>Set both tab and window title — OSC 0.</summary>
    public static void SetTitle(string title) => WriteOsc("0;" + Sanitize(title));

    // ───────────────────────────── Tab color ─────────────────────────────

    /// <summary>Set the tab color from RGB components.</summary>
    public static void SetTabColor(byte red, byte green, byte blue)
    {
        bool wrote = WriteOsc($"6;1;bg;red;brightness;{red}")
                   & WriteOsc($"6;1;bg;green;brightness;{green}")
                   & WriteOsc($"6;1;bg;blue;brightness;{blue}");
        if (wrote) { _tabColorSet = true; EnsureCleanup(); }
    }

    /// <summary>Set the tab color from a hex string: "#ff8800", "ff8800" or "#f80".</summary>
    public static void SetTabColor(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
        if (h.Length != 6) throw new ArgumentException($"Invalid hex color '{hex}'.", nameof(hex));
        SetTabColor(
            Convert.ToByte(h[..2], 16),
            Convert.ToByte(h[2..4], 16),
            Convert.ToByte(h[4..6], 16));
    }

    /// <summary>Restore the profile's default tab color.</summary>
    public static void ResetTabColor()
    {
        if (WriteOsc("6;1;bg;*;default")) _tabColorSet = false;
    }

    // ───────────────────────────── Badge ─────────────────────────────

    /// <summary>
    /// Set the session badge (large translucent text in the top-right of the pane).
    /// Supports iTerm2 interpolation, e.g. "\(session.username)@\(session.hostname)".
    /// </summary>
    public static void SetBadge(string text)
    {
        if (WriteOsc("1337;SetBadgeFormat=" + Base64(text)))
        {
            _badgeSet = text.Length > 0;
            if (_badgeSet) EnsureCleanup();
        }
    }

    public static void ClearBadge() => SetBadge(string.Empty);

    // ───────────────────────────── Notifications & attention ─────────────────────────────

    /// <summary>Post a macOS notification (OSC 9). Requires notifications enabled in the iTerm2 profile.</summary>
    public static void Notify(string message)
    {
        var msg = Sanitize(message);
        // A message beginning with "4;" would be parsed as a progress command.
        if (msg.StartsWith("4;", StringComparison.Ordinal)) msg = " " + msg;
        WriteOsc("9;" + msg);
    }

    /// <summary>Bounce the dock icon, cancel that, or show fireworks at the cursor.</summary>
    public static void RequestAttention(AttentionMode mode = AttentionMode.Yes) =>
        WriteOsc("1337;RequestAttention=" + mode.ToString().ToLowerInvariant());

    /// <summary>Drop a navigation mark at the current line (Cmd-Shift-↑/↓ to jump between marks).</summary>
    public static void SetMark() => WriteOsc("1337;SetMark");

    /// <summary>Set a user variable, readable in iTerm2 as \(user.<paramref name="name"/>) in badges/titles/status bar.</summary>
    public static void SetUserVar(string name, string value) =>
        WriteOsc($"1337;SetUserVar={name}={Base64(value)}");

    // ───────────────────────────── Cleanup ─────────────────────────────

    /// <summary>Clear progress, reset tab color and badge if this class changed them.</summary>
    public static void ResetAll()
    {
        lock (Gate)
        {
            if (_progressActive) ClearProgress();
            if (_tabColorSet) ResetTabColor();
            if (_badgeSet) ClearBadge();
        }
    }

    // Make sure a crash or Ctrl+C doesn't leave the tab stuck with a progress bar / color.
    private static void EnsureCleanup()
    {
        if (_cleanupRegistered) return;
        lock (Gate)
        {
            if (_cleanupRegistered) return;
            _cleanupRegistered = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => ResetAll();
            Console.CancelKeyPress += (_, _) => ResetAll();
        }
    }

    // ───────────────────────────── Plumbing ─────────────────────────────

    private static bool WriteOsc(string body)
    {
        if (!Enabled) return false;

        string seq = Esc + "]" + body + St;
        if (IsTmux)
        {
            // DCS passthrough: ESC P tmux; <payload with every ESC doubled> ESC \
            seq = Esc + "Ptmux;" + seq.Replace(Esc, Esc + Esc) + St;
        }

        lock (Gate)
        {
            Output.Write(seq);
            Output.Flush();
        }
        return true;
    }

    private static string Base64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    // Strip control characters so user text can't terminate the sequence early.
    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            if (!char.IsControl(c)) sb.Append(c);
        return sb.ToString();
    }

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private static bool DetectProgressSupport()
    {
        if (!IsITerm) return false;
        var features = Env("TERM_FEATURES");
        if (string.IsNullOrEmpty(features)) return true; // can't tell; assume a modern iTerm2

        // TERM_FEATURES is a run of codes, each an uppercase letter followed by
        // optional lowercase letters/digits (e.g. "T3LrMSc7UUw9Ts3BFGsSyHNoSxFP").
        for (int i = 0; i < features.Length; i++)
        {
            if (!char.IsUpper(features[i])) continue;
            int j = i + 1;
            while (j < features.Length && (char.IsLower(features[j]) || char.IsDigit(features[j]))) j++;
            if (features[i] == 'P' && j == i + 1) return true;
            i = j - 1;
        }
        return false;
    }
}

/// <summary>Disposable progress handle; clears the bar on dispose unless marked failed.</summary>
public sealed class ProgressScope : IDisposable
{
    private bool _failed;
    private bool _disposed;

    internal ProgressScope(bool indeterminate)
    {
        if (indeterminate) ITerm.SetProgressIndeterminate();
        else ITerm.SetProgress(0);
    }

    public void Report(int percent) => ITerm.SetProgress(percent);
    public void Report(long current, long total) => ITerm.SetProgress(current, total);
    public void Indeterminate() => ITerm.SetProgressIndeterminate();
    public void Warn(int? percent = null) => ITerm.SetProgressWarning(percent);

    /// <summary>Turn the bar red and leave it visible after dispose.</summary>
    public void MarkFailed(int? percent = null)
    {
        _failed = true;
        ITerm.SetProgressError(percent);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_failed) ITerm.ClearProgress();
    }
}
