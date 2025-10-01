using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Services;

/// <summary>
/// Provides functionality for retrieving job responses by job ID.
/// </summary>
public sealed class JobResponseService(ILogger<JobResponseService> logger, IJobStore jobStore) : IJobResponseService
{
	private readonly ILogger<JobResponseService> _logger = logger;
	private readonly IJobStore _jobStore = jobStore;

	/// <summary>
	/// Retrieves a job response by its ID.
	/// </summary>
	/// <param name="jobId">The unique identifier of the job.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>An <see cref="IResult"/> representing the HTTP response containing the job information.</returns>
	public async Task<IResult> GetJobResponseAsync(Guid jobId, CancellationToken cancellationToken)
	{
		_logger.LogInformation("Retrieving job response for job ID: {JobId}", jobId);

		var result = await _jobStore.GetJobById(jobId, cancellationToken);

		if (!result.IsSuccess || result.Data == null)
		{
			_logger.LogWarning("Job with ID {JobId} not found", jobId);
			return Results.NotFound(new { Message = $"Job with ID {jobId} not found" });
		}

		var job = result.Data;
		var jobResponse = JobResponseMapper.ToResponse(job);

		_logger.LogInformation("Successfully retrieved job response for job ID: {JobId}", jobId);

		return Results.Ok(jobResponse);
	}
}