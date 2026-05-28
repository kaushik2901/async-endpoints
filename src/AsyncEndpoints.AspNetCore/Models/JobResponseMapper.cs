using AsyncEndpoints.Core.Legacy.JobProcessing;

namespace AsyncEndpoints.AspNetCore.Models;

[Obsolete("Use the new pipeline's JobRecord-based response instead.")]
public static class JobResponseMapper
{
	public static JobResponse ToResponse(Job job)
	{
		return new JobResponse
		{
			Id = job.Id,
			Name = job.Name,
			Status = job.Status.ToString(),
			RetryCount = job.RetryCount,
			MaxRetries = job.MaxRetries,
			CreatedAt = job.CreatedAt,
			StartedAt = job.StartedAt,
			CompletedAt = job.CompletedAt,
			LastUpdatedAt = job.LastUpdatedAt,
			Result = job.Result ?? string.Empty,
			Error = job.Error,
		};
	}
}
