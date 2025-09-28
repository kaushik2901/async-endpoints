using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Services;

/// <summary>
/// Handles asynchronous endpoint requests by creating background jobs for processing.
/// </summary>
public sealed class AsyncEndpointRequestDelegate(ILogger<AsyncEndpointRequestDelegate> logger, IJobManager jobManager, ISerializer serializer) : IAsyncEndpointRequestDelegate
{
    private readonly ILogger<AsyncEndpointRequestDelegate> _logger = logger;
    private readonly IJobManager _jobManager = jobManager;
    private readonly ISerializer _serializer = serializer;

    /// <summary>
    /// Handles an asynchronous request by creating a job and returning an immediate response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request object.</typeparam>
    /// <param name="jobName">The unique name of the job, used to identify the specific handler.</param>
    /// <param name="httpContext">The HTTP context containing the request information.</param>
    /// <param name="request">The request object to process asynchronously.</param>
    /// <param name="handler">Optional custom handler function to process the request.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="IResult"/> representing the HTTP response.</returns>
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

        var payload = _serializer.Serialize(request);
        _logger.LogDebug("Serialized request payload for job: {JobName}", jobName);

        var submitJobResult = await _jobManager.SubmitJob(jobName, payload, httpContext, cancellationToken);
        if (!submitJobResult.IsSuccess)
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
