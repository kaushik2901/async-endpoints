using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Extensions;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.AspNetCore.Handlers;

[Obsolete("Use the new pipeline pattern with IJobSubmitter directly.")]
public sealed class AsyncEndpointRequestDelegate(ILogger<AsyncEndpointRequestDelegate> logger, IJobManager jobManager, ISerializer serializer, AsyncEndpointsResponseConfigurations responseConfigurations) : IAsyncEndpointRequestDelegate
{
	private readonly ILogger<AsyncEndpointRequestDelegate> _logger = logger;
	private readonly IJobManager _jobManager = jobManager;
	private readonly ISerializer _serializer = serializer;
	private readonly AsyncEndpointsResponseConfigurations _responseConfigurations = responseConfigurations;

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
		var payload = _serializer.Serialize(request, (System.Text.Json.JsonSerializerOptions?)null);
		_logger.LogDebug("Serialized request payload for job: {JobName}, payload length: {PayloadLength}", jobName, payload.Length);

		var jobId = httpContext.GetOrCreateJobId();
		var headers = httpContext.GetHeadersFromContext();

		var routeParams = new Dictionary<string, object?>();
		foreach (var rp in httpContext.GetRouteParamsFromContext())
			routeParams[rp.Key] = rp.Value;

		var queryParams = new List<KeyValuePair<string, List<string?>>>();
		foreach (var qp in httpContext.GetQueryParamsFromContext())
			queryParams.Add(new KeyValuePair<string, List<string?>>(qp.Key, qp.Value));

		var submitJobResult = await _jobManager.SubmitJob(jobName, payload, jobId, headers, routeParams, queryParams, cancellationToken);
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

			return Results.Problem(
				detail: submitJobResult.Error?.Message ?? "Job submission failed",
				title: "Job Submission Failed",
				statusCode: 500);
		}

		var job = submitJobResult.Data!;

		_logger.LogInformation("Successfully created job {JobId} for job: {JobName}", job.Id, jobName);

		return await _responseConfigurations.JobSubmittedResponseFactory(job.Id, httpContext);
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
