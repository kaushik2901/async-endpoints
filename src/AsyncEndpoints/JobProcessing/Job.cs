using System;
using System.Collections.Generic;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.JobProcessing;

/// <summary>
/// Represents an asynchronous job in the AsyncEndpoints system.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Job"/> class with a specific time.
/// </remarks>
/// <param name="currentTime">The current time to use for timestamps.</param>
public sealed class Job(DateTimeOffset currentTime)
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Job"/> class with the current time.
	/// </summary>
	public Job() : this(DateTimeOffset.UtcNow)
	{
	}

	/// <summary>
	/// Gets the unique identifier of the job.
	/// </summary>
	public Guid Id { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the name of the job.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current status of the job.
	/// </summary>
	public JobStatus Status { get; set; } = JobStatus.Queued;

	/// <summary>
	/// Gets or sets the collection of HTTP headers associated with the job.
	/// </summary>
	public Dictionary<string, List<string?>> Headers { get; set; } = [];

	/// <summary>
	/// Gets or sets the route parameters associated with the job.
	/// </summary>
	public Dictionary<string, object?> RouteParams { get; set; } = [];

	/// <summary>
	/// Gets or sets the query parameters associated with the job.
	/// </summary>
	public List<KeyValuePair<string, List<string?>>> QueryParams { get; set; } = [];

	/// <summary>
	/// Gets the payload data for the job.
	/// </summary>
	public string Payload { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the result of the job execution, if successful.
	/// </summary>
	public string? Result { get; set; } = null;

	/// <summary>
	/// Gets or sets the error details if the job failed.
	/// </summary>
	public AsyncEndpointError? Error { get; set; } = null;

	/// <summary>
	/// Gets or sets the number of times the job has been retried.
	/// </summary>
	public int RetryCount { get; set; } = 0;

	/// <summary>
	/// Gets or sets the maximum number of retries allowed for the job.
	/// </summary>
	public int MaxRetries { get; set; } = AsyncEndpointsConstants.MaximumRetries;

	/// <summary>
	/// Gets or sets the time until which the job is scheduled for retry.
	/// </summary>
	public DateTime? RetryDelayUntil { get; set; } = null;

	/// <summary>
	/// Gets or sets the ID of the worker currently processing this job, if any.
	/// </summary>
	public Guid? WorkerId { get; set; } = null;

	/// <summary>
	/// Gets or sets the date and time when the job was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; } = currentTime;

	/// <summary>
	/// Gets or sets the date and time when the job processing started, if applicable.
	/// </summary>
	public DateTimeOffset? StartedAt { get; set; } = null;

	/// <summary>
	/// Gets or sets the date and time when the job processing completed, if applicable.
	/// </summary>
	public DateTimeOffset? CompletedAt { get; set; } = null;

	/// <summary>
	/// Gets or sets the date and time when the job was last updated.
	/// </summary>
	public DateTimeOffset LastUpdatedAt { get; set; } = currentTime;

	/// <summary>
	/// Gets a value indicating whether the job has been canceled.
	/// </summary>
	public bool IsCanceled => Status == JobStatus.Canceled;

	/// <summary>
	/// Creates a new job with the specified parameters.
	/// </summary>
	/// <param name=\"id\">The unique identifier for the job.</param>
	/// <param name=\"name\">The name of the job.</param>
	/// <param name=\"payload\">The payload data for the job.</param>
	/// <param name=\"dateTimeProvider\">Provider for current date and time.</param>
	/// <returns>A new <see cref=\"Job\"/> instance.</returns>
	public static Job Create(Guid id, string name, string payload, IDateTimeProvider dateTimeProvider)
	{
		var now = dateTimeProvider.DateTimeOffsetNow;
		return new Job
		{
			Id = id,
			Name = name,
			Payload = payload,
			CreatedAt = now,
			LastUpdatedAt = now
		};
	}

	/// <summary>
	/// Creates a new job with the specified parameters including HTTP context information.
	/// </summary>
	/// <param name="id">The unique identifier for the job.</param>
	/// <param name="name">The name of the job.</param>
	/// <param name="payload">The payload data for the job.</param>
	/// <param name="headers">The HTTP headers associated with the original request.</param>
	/// <param name="routeParams">The route parameters associated with the original request.</param>
	/// <param name="queryParams">The query parameters associated with the original request.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	/// <returns>A new <see cref="Job"/> instance.</returns>
	public static Job Create(
		Guid id,
		string name,
		string payload,
		Dictionary<string, List<string?>> headers,
		Dictionary<string, object?> routeParams,
		List<KeyValuePair<string, List<string?>>> queryParams,
		IDateTimeProvider dateTimeProvider)
	{
		var now = dateTimeProvider.DateTimeOffsetNow;
		return new Job
		{
			Id = id,
			Name = name,
			Payload = payload,
			Headers = headers,
			RouteParams = routeParams,
			QueryParams = queryParams,
			CreatedAt = now,
			LastUpdatedAt = now
		};
	}

	/// <summary>
	/// Updates the status of the job and updates the last updated timestamp.
	/// </summary>
	/// <param name="status">The new status to set for the job.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	public void UpdateStatus(JobStatus status, IDateTimeProvider dateTimeProvider)
	{
		// Validate legal state transitions
		if (!IsValidStateTransition(Status, status))
		{
			throw new InvalidOperationException($"Invalid state transition from {Status} to {status}");
		}

		Status = status;
		var now = dateTimeProvider.DateTimeOffsetNow;
		LastUpdatedAt = now;

		switch (status)
		{
			case JobStatus.InProgress:
				StartedAt = now;
				break;
			case JobStatus.Completed:
			case JobStatus.Failed:
			case JobStatus.Canceled:
				CompletedAt = now;
				break;
		}
	}

	/// <summary>
	/// Validates if a state transition is legal.
	/// </summary>
	/// <param name="from">The current status of the job.</param>
	/// <param name="to">The target status to transition to.</param>
	/// <returns>True if the state transition is valid, otherwise false.</returns>
	private static bool IsValidStateTransition(JobStatus from, JobStatus to)
	{
		// Define legal state transitions
		return (from, to) switch
		{
			// Allow jobs to transition directly to completed/failed from queued (e.g. for immediate processing)
			(JobStatus.Queued, JobStatus.Completed) => true,
			(JobStatus.Queued, JobStatus.Failed) => true,
			(JobStatus.Queued, JobStatus.Canceled) => true,
			(JobStatus.Queued, JobStatus.InProgress) => true,
			(JobStatus.Queued, JobStatus.Scheduled) => true, // For retries with delay
			(JobStatus.Scheduled, JobStatus.Queued) => true, // When scheduled job becomes available
			(JobStatus.Scheduled, JobStatus.Canceled) => true,
			(JobStatus.InProgress, JobStatus.Completed) => true,
			(JobStatus.InProgress, JobStatus.Failed) => true,
			(JobStatus.InProgress, JobStatus.Canceled) => true,
			(JobStatus.Failed, JobStatus.Queued) => true, // For retries without delay
			(JobStatus.Failed, JobStatus.Scheduled) => true, // For retries with delay
			(JobStatus.Failed, JobStatus.Canceled) => true,
			(JobStatus.Completed, JobStatus.Canceled) => true, // Completed jobs can be canceled if needed
			_ => from == to // Allow same state updates for timestamp refreshes
		};
	}

	/// <summary>
	/// Sets the result of the job and updates the status to completed.
	/// </summary>
	/// <param name="result">The result of the job execution.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	public void SetResult(string result, IDateTimeProvider dateTimeProvider)
	{
		Result = result;
		UpdateStatus(JobStatus.Completed, dateTimeProvider);
	}

	/// <summary>
	/// Sets the error details for the job and updates the status to failed.
	/// </summary>
	/// <param name="error">The error that occurred during job execution.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	public void SetError(AsyncEndpointError error, IDateTimeProvider dateTimeProvider)
	{
		Error = error;
		UpdateStatus(JobStatus.Failed, dateTimeProvider);
	}

	/// <summary>
	/// Sets the error details for the job and updates the status to failed.
	/// </summary>
	/// <param name="error">The error message that occurred during job execution.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	public void SetError(string error, IDateTimeProvider dateTimeProvider)
	{
		Error = AsyncEndpointError.FromMessage(error);
		UpdateStatus(JobStatus.Failed, dateTimeProvider);
	}

	/// <summary>
	/// Increments the retry count for the job.
	/// </summary>
	public void IncrementRetryCount()
	{
		RetryCount++;
	}

	/// <summary>
	/// Sets the retry delay time for the job.
	/// </summary>
	/// <param name="delayUntil">The time until which the job is scheduled for retry.</param>
	public void SetRetryTime(DateTime delayUntil)
	{
		RetryDelayUntil = delayUntil;
	}
}
