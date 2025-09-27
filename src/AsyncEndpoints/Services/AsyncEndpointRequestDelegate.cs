using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Services;

public sealed class AsyncEndpointRequestDelegate(ILogger<AsyncEndpointRequestDelegate> logger, IJobManager jobManager, IOptions<JsonOptions> jsonOptions) : IAsyncEndpointRequestDelegate
{
    private readonly ILogger<AsyncEndpointRequestDelegate> _logger = logger;
    private readonly IJobManager _jobManager = jobManager;
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

        var submitJobResult = await _jobManager.SubmitJob(jobName, payload, httpContext, cancellationToken);
        if (submitJobResult.IsSuccess)
        {
            // TODO: Handler error properly
            return Results.Problem(submitJobResult.Error!.Message);
        }

        var job = submitJobResult.Data!;
        var jobResponse = JobResponseMapper.ToResponse(job);

        _logger.LogInformation("Created job {JobId} for job: {JobName}", job.Id, jobName);

        return Results.Accepted("", jobResponse);
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
