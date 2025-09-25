using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Services;

public sealed class AsyncEndpointRequestDelegate(ILogger<AsyncEndpointRequestDelegate> logger, IJobStore jobStore, IOptions<JsonOptions> jsonOptions) : IAsyncEndpointRequestDelegate
{
    private readonly ILogger<AsyncEndpointRequestDelegate> _logger = logger;
    private readonly IJobStore _jobStore = jobStore;
    private readonly IOptions<JsonOptions> _jsonOptions = jsonOptions;

    public async Task<IResult> HandleAsync<TRequest>(
        string jobName,
        HttpContext httpContext,
        TRequest request,
        Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling async request for job: {JobName}", jobName);

        var handlerResponse = await HandleRequestDelegate(handler, httpContext, request, cancellationToken);
        if (handlerResponse != null)
        {
            _logger.LogDebug("Handler provided direct response for job: {JobName}", jobName);
            return handlerResponse;
        }

        var payload = JsonSerializer.Serialize(request, _jsonOptions.Value.SerializerOptions);
        _logger.LogDebug("Serialized request payload for job: {JobName}", jobName);

        var job = await HandleAsync(jobName, payload, httpContext, cancellationToken);
        var jobResponse = JobResponseMapper.ToResponse(job);

        _logger.LogInformation("Created job {JobId} for job: {JobName}", job.Id, jobName);

        return Results.Accepted("", jobResponse);
    }

    private async Task<Job> HandleAsync(string jobName, string payload, HttpContext httpContext, CancellationToken token)
    {
        _logger.LogDebug("Processing job creation for: {JobName}", jobName);

        var id = httpContext.GetOrCreateJobId();

        var result = await _jobStore.Get(id, token);
        if (result.IsSuccess && result.Data != null)
        {
            _logger.LogDebug("Found existing job {JobId} for job: {JobName}", id, jobName);
            return result.Data;
        }

        var headers = httpContext.GetHeadersFromContext();
        var routeParams = httpContext.GetRouteParamsFromContext();
        var queryParams = httpContext.GetQueryParamsFromContext();

        var job = Job.Create(id, jobName, payload, headers, routeParams, queryParams);
        _logger.LogDebug("Creating new job {JobId} for job: {JobName}", id, jobName);

        await _jobStore.Add(job, token);

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
