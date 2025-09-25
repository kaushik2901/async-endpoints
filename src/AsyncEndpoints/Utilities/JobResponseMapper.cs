using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Provides methods for mapping Job entities to response objects.
/// </summary>
public static class JobResponseMapper
{
    /// <summary>
    /// Converts a Job entity to a JobResponse object.
    /// </summary>
    /// <param name="job">The job entity to convert.</param>
    /// <returns>A <see cref="JobResponse"/> containing the job information.</returns>
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
            Result = job.Result,
            Exception = job.Exception
        };
    }
}