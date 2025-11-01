using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Infrastructure.Observability;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
/// <summary>
/// Provides functionality for processing individual jobs by executing their handlers and managing job lifecycle updates.
/// </summary>
public class JobProcessorService(ILogger<JobProcessorService> logger, IJobManager jobManager, IHandlerExecutionService handlerExecutionService, ISerializer serializer, IAsyncEndpointsObservability metrics) : IJobProcessorService
{
	private readonly ILogger<JobProcessorService> _logger = logger;
	private readonly IJobManager _jobManager = jobManager;
	private readonly IHandlerExecutionService _handlerExecutionService = handlerExecutionService;
	private readonly ISerializer _serializer = serializer;
	private readonly IAsyncEndpointsObservability _metrics = metrics;

	/// <inheritdoc />
	public async Task ProcessAsync(Job job, CancellationToken cancellationToken)
	{
		using var _ = _logger.BeginScope(new { JobId = job.Id, JobName = job.Name });

		_logger.LogDebug("Starting job processing for job {JobId} with name {JobName}", job.Id, job.Name);

		using var activity = _metrics.StartJobProcessActivity(job.GetType().Name, job);

		using var durationTimer = _metrics.TimeJobProcessingDuration(job.Name, "processing");

		try
		{
			var result = await ProcessJobPayloadAsync(job, cancellationToken);
			if (!result.IsSuccess)
			{
				activity?.SetStatus(ActivityStatusCode.Error, result.Error.Message);
				activity?.SetTag("error.type", result.Error.Code);

				_logger.LogError("Failed to process job {JobId}: {Error}", job.Id, result.Error.Message);

				var processJobFailureResult = await _jobManager.ProcessJobFailure(job.Id, result.Error, cancellationToken);
				if (!processJobFailureResult.IsSuccess)
				{
					activity?.SetStatus(ActivityStatusCode.Error, processJobFailureResult.Error.Message);

					_logger.LogError("Failed to update job status for failure {JobId}: {Error}", job.Id, processJobFailureResult.Error.Message);
					return;
				}

				return;
			}

			var processJobSuccessResult = await _jobManager.ProcessJobSuccess(job.Id, result.Data, cancellationToken);
			if (!processJobSuccessResult.IsSuccess)
			{
				activity?.SetStatus(ActivityStatusCode.Error, processJobSuccessResult.Error.Message);

				_logger.LogError("Failed to update job status for success {JobId}: {Error}", job.Id, processJobSuccessResult.Error.Message);
				return;
			}

			_logger.LogInformation("Successfully processed job {JobId}", job.Id);
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag("error.type", ex.GetType().Name);

			_logger.LogError(ex, "Exception occurred during job processing");
		}
	}

	/// <summary>
	/// Processes the payload of a job by deserializing the request, executing the handler, and serializing the result.
	/// </summary>
	/// <param name="job">The job containing the payload to process.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{T}"/> containing the serialized result of the operation.</returns>
	private async Task<MethodResult<string>> ProcessJobPayloadAsync(Job job, CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogDebug("Starting payload processing for job {JobId} with name {JobName}", job.Id, job.Name);

			var handlerRegistration = HandlerRegistrationTracker.GetHandlerRegistration(job.Name);
			if (handlerRegistration == null)
			{
				_logger.LogError("Handler registration not found for job name: {JobName}", job.Name);
				return MethodResult<string>.Failure(new InvalidOperationException($"Handler registration not found for job name: {job.Name}"));
			}

			var request = _serializer.Deserialize(job.Payload, handlerRegistration.RequestType);
			if (request == null)
			{
				_logger.LogError("Failed to deserialize request payload for job: {JobName}", job.Name);
				return MethodResult<string>.Failure(new InvalidOperationException($"Failed to deserialize request payload for job: {job.Name}"));
			}

			_logger.LogDebug("Executing handler for job {JobId}", job.Id);

			// Start handler execution activity if tracing is enabled
			// Since HandlerRegistration doesn't store the handler type, we'll use the response type as a fallback
			var handlerType = handlerRegistration.ResponseType?.Name ?? "Unknown";
			using var handlerActivity = _metrics.StartHandlerExecuteActivity(job.Name, job.Id, handlerType);

			// Use timer for handler execution duration
			using var handlerDurationTimer = _metrics.TimeHandlerExecution(job.Name, handlerType);

			var result = await _handlerExecutionService.ExecuteHandlerAsync(job.Name, request, job, cancellationToken);
			if (!result.IsSuccess)
			{
				_metrics.RecordHandlerError(job.Name, result.Error.Code);
				handlerActivity?.SetStatus(ActivityStatusCode.Error, result.Error.Message);
				handlerActivity?.SetTag("error.type", result.Error.Code);

				_logger.LogError("Handler execution failed for job {JobId}: {Error}", job.Id, result.Error.Message);
				return MethodResult<string>.Failure(result.Error);
			}

			var serializedResult = _serializer.Serialize(result.Data, handlerRegistration.ResponseType ?? typeof(object));

			_logger.LogDebug("Successfully processed payload for job {JobId}", job.Id);
			return MethodResult<string>.Success(serializedResult);
		}
		catch (Exception ex)
		{
			_metrics.RecordHandlerError(job.Name, ex.GetType().Name);

			_logger.LogError(ex, "Exception during payload processing for job {JobId}", job.Id);
			return MethodResult<string>.Failure(ex);
		}
	}
}
