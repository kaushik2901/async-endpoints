using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Job;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.RouteBuilder;

internal class AsyncEndpointRequestDelegate(IHttpContextAccessor httpContextAccessor, IJobStore jobStore)
{
    public async Task<IResult> HandleAsync<TRequest>(string jobName, TRequest request, CancellationToken token)
    {
        var id = JobIdHelper.GetJobId(httpContextAccessor.HttpContext);

        var existingJob = await jobStore.Get(id, token);
        if (existingJob != null) return Results.Accepted(existingJob.Id.ToString(), existingJob);

        var payload = JsonSerializer.Serialize(request);
        var job = Job.Job.Create(id, jobName, payload);

        await jobStore.Add(job, token);

        return Results.Accepted(job.Id.ToString(), job);
    }
}
