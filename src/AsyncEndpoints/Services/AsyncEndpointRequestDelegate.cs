using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Job;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.Services;

public class AsyncEndpointRequestDelegate(IJobStore jobStore) : IAsyncEndpointRequestDelegate
{
    public async Task<IResult> HandleAsync<TRequest>(
        string jobName,
        HttpContext httpContext,
        TRequest request,
        Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null,
        CancellationToken cancellationToken = default)
    {
        var handlerResponse = await HandleRequestDelegate(handler, httpContext, request, cancellationToken);
        if (handlerResponse != null) return handlerResponse;

        var payload = JsonSerializer.Serialize(request);
        return await HandleAsync(jobName, payload, httpContext, cancellationToken);
    }

    private async Task<IResult> HandleAsync(string jobName, string payload, HttpContext httpContext, CancellationToken token)
    {
        var id = JobIdHelper.GetJobId(httpContext);

        var existingJob = await jobStore.Get(id, token);
        if (existingJob != null) return Results.Accepted(existingJob.Id.ToString(), existingJob);
       
        var job = Job.Job.Create(id, jobName, payload);
        await jobStore.Add(job, token);

        return Results.Accepted(job.Id.ToString(), job);
    }

    private static async Task<IResult?> HandleRequestDelegate<TRequest>(Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler, HttpContext httpContext, TRequest request, CancellationToken token)
    {
        if (handler != null)
        {
            var handlerResponseTask = handler(httpContext, request, token);
            if (handlerResponseTask != null)
            {
                var handlerResponse = await handlerResponseTask;
                if (handlerResponse != null) return handlerResponse;
            }
        }

        return null;
    }
}
