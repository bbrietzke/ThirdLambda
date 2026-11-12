// Services/JobGrpcService.cs
using Grpc.Core;
using ThirdLambda.Core.Grpc.V1;

namespace ThirdLambda.Server.Services;

public class JobGrpcService : JobService.JobServiceBase
{
    public override Task<Job> GetJob(GetJobRequest request, ServerCallContext context) =>
        throw new RpcException(new Status(StatusCode.Unimplemented, "GetJob is not implemented yet."));

    public override Task<ListJobsResponse> ListJobs(ListJobsRequest request, ServerCallContext context) =>
        throw new RpcException(new Status(StatusCode.Unimplemented, "ListJobs is not implemented yet."));
}
