using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
/// <summary>
/// Provides functionality for processing individual jobs by executing their handlers and managing job lifecycle updates.
/// </summary>
public class JobProcessorService(ILogger<JobProcessorService> logger, IJobManager jobManager, IHandlerExecutionService handlerExecutionService, ISerializer serializer) : IJobProcessorService
{
	private readonly ILogger<JobProcessorService> _logger = logger;
	private readonly IJobManager _jobManager = jobManager;
	private readonly IHandlerExecutionService _handlerExecutionService = handlerExecutionService;
	private readonly ISerializer _serializer = serializer;

	/// <inheritdoc />
	public async Task ProcessAsync(Job job, CancellationToken cancellationToken)
	{
		using var _ = _logger.BeginScope(new { JobId = job.Id, JobName = job.Name });
		
		_logger.LogDebug("Starting job processing for job {JobId} with name {JobName}", job.Id, job.Name);

		try
		{
			var result = await ProcessJobPayloadAsync(job, cancellationToken);
			if (!result.IsSuccess)
			{
				_logger.LogError("Failed to process job {JobId}: {Error}", job.Id, result.Error.Message);

				var processJobFailureResult = await _jobManager.ProcessJobFailure(job.Id, result.Error, cancellationToken);
				if (!processJobFailureResult.IsSuccess)
				{
					_logger.LogError("Failed to update job status for failure {JobId}: {Error}", job.Id, processJobFailureResult.Error.Message);
					return;
				}

				return;
			}

			var processJobSuccessResult = await _jobManager.ProcessJobSuccess(job.Id, result.Data, cancellationToken);
			if (!processJobSuccessResult.IsSuccess)
			{
				_logger.LogError("Failed to update job status for success {JobId}: {Error}", job.Id, processJobSuccessResult.Error.Message);
				return;
			}

			_logger.LogInformation("Successfully processed job {JobId}", job.Id);
		}
		catch (Exception ex)
		{
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

			_logger.LogDebug("Deserializing payload for job {JobId}", job.Id);
			var request = _serializer.Deserialize(job.Payload, handlerRegistration.RequestType);
			if (request == null)
			{
				_logger.LogError("Failed to deserialize request payload for job: {JobName}", job.Name);
				return MethodResult<string>.Failure(new InvalidOperationException($"Failed to deserialize request payload for job: {job.Name}"));
			}

			_logger.LogDebug("Executing handler for job {JobId}", job.Id);
			var result = await _handlerExecutionService.ExecuteHandlerAsync(job.Name, request, job, cancellationToken);
			if (!result.IsSuccess)
			{
				_logger.LogError("Handler execution failed for job {JobId}: {Error}", job.Id, result.Error.Message);
				return MethodResult<string>.Failure(result.Error);
			}

			_logger.LogDebug("Serializing result for job {JobId}", job.Id);
			var serializedResult = _serializer.Serialize(result.Data, handlerRegistration.ResponseType);

			_logger.LogDebug("Successfully processed payload for job {JobId}", job.Id);
			return MethodResult<string>.Success(serializedResult);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception during payload processing for job {JobId}", job.Id);
			return MethodResult<string>.Failure(ex);
		}
	}
}
