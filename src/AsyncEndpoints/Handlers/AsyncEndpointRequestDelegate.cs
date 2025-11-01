using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Handlers;

/// <inheritdoc />
public sealed class AsyncEndpointRequestDelegate(ILogger<AsyncEndpointRequestDelegate> logger, IJobManager jobManager, ISerializer serializer, AsyncEndpointsConfigurations configurations) : IAsyncEndpointRequestDelegate
{
	private readonly ILogger<AsyncEndpointRequestDelegate> _logger = logger;
	private readonly IJobManager _jobManager = jobManager;
	private readonly ISerializer _serializer = serializer;
	private readonly AsyncEndpointsConfigurations _configurations = configurations;

	/// <inheritdoc />
	public async Task<IResult> HandleAsync<TRequest>(
		string jobName,
		HttpContext httpContext,
		TRequest request,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null,
		CancellationToken cancellationToken = default)
	{
		using var _ = _logger.BeginScope(new { JobName = jobName, RequestType = typeof(TRequest).Name });

		_logger.LogInformation("Handling async request for job: {JobName}", jobName);

		var handlerResponse = await HandleRequestDelegate(handler, httpContext, request, cancellationToken);
		if (handlerResponse != null)
		{
			_logger.LogDebug("Handler provided direct response for job: {JobName}", jobName);
			return handlerResponse;
		}

		_logger.LogDebug("Serializing request payload for job: {JobName}", jobName);
		var payload = _serializer.Serialize(request);
		_logger.LogDebug("Serialized request payload for job: {JobName}, payload length: {PayloadLength}", jobName, payload.Length);

		var submitJobResult = await _jobManager.SubmitJob(jobName, payload, httpContext, cancellationToken);
		if (!submitJobResult.IsSuccess)
		{
			_logger.LogError("Failed to submit job {JobName}: {ErrorMessage}", jobName, submitJobResult.Error?.Message);

			if (submitJobResult.Error?.Exception != null)
			{
				_logger.LogCritical("Exception occurred while submitting job {JobName}: Type={ExceptionType}, Message={ExceptionMessage}, StackTrace={StackTrace}",
					jobName,
					submitJobResult.Error.Exception.Type,
					submitJobResult.Error.Exception.Message,
					submitJobResult.Error.Exception.StackTrace);
			}

			return await _configurations.ResponseConfigurations.JobSubmissionErrorResponseFactory(
				submitJobResult.Error,
				httpContext);
		}

		var job = submitJobResult.Data!;

		_logger.LogInformation("Successfully created job {JobId} for job: {JobName}", job.Id, jobName);

		return await _configurations.ResponseConfigurations.JobSubmittedResponseFactory(job, httpContext);
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
