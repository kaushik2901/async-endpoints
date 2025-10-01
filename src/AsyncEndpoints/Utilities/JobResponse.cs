using System;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Represents the response object for a job in the AsyncEndpoints system.
/// </summary>
public sealed class JobResponse
{
	/// <summary>
	/// Gets or sets the unique identifier of the job.
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Gets or sets the name of the job.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current status of the job as a string.
	/// </summary>
	public string Status { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the number of times the job has been retried.
	/// </summary>
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retries allowed for the job.
	/// </summary>
	public int MaxRetries { get; set; }

	/// <summary>
	/// Gets or sets the date and time when the job was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets the date and time when the job processing started, if applicable.
	/// </summary>
	public DateTimeOffset? StartedAt { get; set; }

	/// <summary>
	/// Gets or sets the date and time when the job processing completed, if applicable.
	/// </summary>
	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the date and time when the job was last updated.
	/// </summary>
	public DateTimeOffset LastUpdatedAt { get; set; }

	/// <summary>
	/// Gets or sets the result of the job execution, if successful.
	/// </summary>
	public string Result { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the exception details if the job failed.
	/// </summary>
	public string Exception { get; set; } = string.Empty;
}