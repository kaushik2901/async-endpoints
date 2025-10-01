using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Serialization;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Services;

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

			if (result.IsSuccess)
			{
				_logger.LogInformation("Successfully processed job {JobId}", job.Id);
				await _jobManager.ProcessJobSuccess(job.Id, result.Data ?? string.Empty, cancellationToken);
			}
			else
			{
				_logger.LogError("Failed to process job {JobId}: {Error}", job.Id, result.Error?.Message);
				var serializedError = result.Error?.ToString() ?? "Unknown error occurred";
				await _jobManager.ProcessJobFailure(job.Id, serializedError, cancellationToken);
			}
		}
		catch (Exception ex)
		{
			var serializedException = ExceptionSerializer.Serialize(ex);
			await _jobManager.ProcessJobFailure(job.Id, serializedException, cancellationToken);
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
				return MethodResult<string>.Failure(result.Error!);
			}

			var serializedResult = _serializer.Serialize(result.Data!, handlerRegistration.ResponseType);

			return MethodResult<string>.Success(serializedResult);
		}
		catch (Exception ex)
		{
			return MethodResult<string>.Failure(ex);
		}
	}
}
