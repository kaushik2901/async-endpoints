using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Utilities;

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
            Result = job.Result,
            Exception = job.Exception
        };
    }
}