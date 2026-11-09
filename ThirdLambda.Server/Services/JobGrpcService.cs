// Services/JobGrpcService.cs
using Grpc.Core;
using ThirdLambda.Core.Grpc.V1;
using ThirdLambda.Server.Data;

namespace ThirdLambda.Server.Services;

public class JobGrpcService(AppDbContext db) : JobService.JobServiceBase
{
    public override async Task<Job> GetJob(GetJobRequest request, ServerCallContext context)
    {
        var job = new ThirdLambda.Core.Grpc.V1.Job { Id = request.Id, Name = "Job " + request.Id, CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        return job;
    }

    public override async Task<ListJobsResponse> ListJobs(ListJobsRequest request, ServerCallContext context)
    {
        var size = request.PageSize is > 0 and <= 500 ? request.PageSize : 50;

        var response = new ListJobsResponse();
        return response;
    }
}