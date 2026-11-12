# Build from the repository root, because the Server depends on ThirdLambda.Core:
#   docker build -f ThirdLambda.Server/Dockerfile -t thirdlambda-server .

# The SDK stage runs on the build machine's architecture even for a cross-build, because
# the protoc binary used by Grpc.Tools is unreliable under emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Project files first, so source edits don't invalidate the restore layer.
COPY ThirdLambda.Core/ThirdLambda.Core.csproj ThirdLambda.Core/
COPY ThirdLambda.Server/ThirdLambda.Server.csproj ThirdLambda.Server/
RUN dotnet restore ThirdLambda.Server/ThirdLambda.Server.csproj -a $TARGETARCH

COPY ThirdLambda.Core/ ThirdLambda.Core/
COPY ThirdLambda.Server/ ThirdLambda.Server/
RUN dotnet publish ThirdLambda.Server/ThirdLambda.Server.csproj \
    --no-restore \
    -a $TARGETARCH \
    -c Release \
    -o /app \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The base image listens on 8080 by default; the Kestrel endpoints in appsettings.json
# replace that, and clearing it avoids a warning on every start.
ENV ASPNETCORE_HTTP_PORTS=

# Numeric so that Kubernetes can verify runAsNonRoot.
USER $APP_UID

EXPOSE 3000 8081

# The subcommand is the only argument, so Kubernetes `args` can switch between
# `serve` and `migrate` while the entrypoint stays the same.
ENTRYPOINT ["dotnet", "ThirdLambda.Server.dll"]
CMD ["serve"]
