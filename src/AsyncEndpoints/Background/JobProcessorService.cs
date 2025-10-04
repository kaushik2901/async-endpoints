using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <summary>
/// Implements the IJobProcessorService interface to process individual jobs.
/// Handles the execution of job payloads, serialization/deserialization of requests and responses,
/// and updates job status and results in the job manager.
/// </summary>
public class JobProcessorService(ILogger<JobProcessorService> logger, IJobManager jobManager, IHandlerExecutionService handlerExecutionService, ISerializer serializer) : IJobProcessorService
{
	private readonly ILogger<JobProcessorService> _logger = logger;
	private readonly IJobManager _jobManager = jobManager;
	private readonly IHandlerExecutionService _handlerExecutionService = handlerExecutionService;
	private readonly ISerializer _serializer = serializer;

	/// <summary>
	/// Processes a single job asynchronously.
	/// </summary>
	/// <param name="job">The job to process.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ProcessAsync(Job job, CancellationToken cancellationToken)
	{
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
			_logger.LogError(ex, "Exception occurred during job processing: {JobName}, JobId: {JobId}", job.Name, job.Id);
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
		_logger.LogInformation("Processing job {JobId} with name {JobName}", job.Id, job.Name);

		try
		{
			var handlerRegistration = HandlerRegistrationTracker.GetHandlerRegistration(job.Name);
			if (handlerRegistration == null)
			{
				return MethodResult<string>.Failure(new InvalidOperationException($"Handler registration not found for job name: {job.Name}"));
			}

			var request = _serializer.Deserialize(job.Payload, handlerRegistration.RequestType);
			if (request == null)
			{
				return MethodResult<string>.Failure(new InvalidOperationException($"Failed to deserialize request payload for job: {job.Name}"));
			}

			var result = await _handlerExecutionService.ExecuteHandlerAsync(job.Name, request, job, cancellationToken);
			if (!result.IsSuccess)
			{
				return MethodResult<string>.Failure(result.Error);
			}

			var serializedResult = _serializer.Serialize(result.Data, handlerRegistration.ResponseType);

			return MethodResult<string>.Success(serializedResult);
		}
		catch (Exception ex)
		{
			return MethodResult<string>.Failure(ex);
		}
	}
}
