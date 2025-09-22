using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Services;

public sealed class AsyncEndpointRequestDelegate(IJobStore jobStore, IOptions<JsonOptions> jsonOptions) : IAsyncEndpointRequestDelegate
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

        var payload = JsonSerializer.Serialize(request, jsonOptions.Value.SerializerOptions);
        var job = await HandleAsync(jobName, payload, httpContext, cancellationToken);

        return Results.Accepted("", job);
    }

    private async Task<Job> HandleAsync(string jobName, string payload, HttpContext httpContext, CancellationToken token)
    {
        var id = httpContext.GetOrCreateJobId();

        var result = await jobStore.Get(id, token);
        if (result.IsSuccess && result.Data != null) return result.Data;

        var job = Job.Create(id, jobName, payload);
        await jobStore.Add(job, token);

        return job;
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
