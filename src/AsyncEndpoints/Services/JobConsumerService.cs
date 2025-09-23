using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Services;

public class JobConsumerService(ILogger<JobConsumerService> logger, IJobStore jobStore)
{
    public async Task ConsumeJobsAsync(ChannelReader<Job> readerJobChannel, SemaphoreSlim semaphoreSlim, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in readerJobChannel.ReadAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                await semaphoreSlim.WaitAsync(stoppingToken);

                try
                {
                    await ProcessJobAsync(job, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing job {JobId}", job.Id);
                }
                finally
                {
                    semaphoreSlim.Release();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Consumer task failed");
            throw; // Let the background service handle the failure
        }
    }

    private async Task ProcessJobAsync(Job job, CancellationToken cancellationToken)
    {
        var originalJobStatus = job.Status;

        if (!await UpdateJobStatusWithRetry(job, JobStatus.InProgress, null, null, cancellationToken))
        {
            job.UpdateStatus(originalJobStatus);
            logger.LogError("Failed to update job {JobId} status to InProgress", job.Id);
            return;
        }

        try
        {
            var result = await ProcessJobPayloadAsync(job, cancellationToken);

            if (result.IsSuccess)
            {
                await UpdateJobStatusWithRetry(job, JobStatus.Completed, result.Data?.ToString(), null, cancellationToken);
            }
            else
            {
                await UpdateJobStatusWithRetry(job, JobStatus.Failed, null, result.Error?.ToString() ?? "Unknown error", cancellationToken);
            }

            if (result.IsSuccess)
                logger.LogInformation("Successfully processed job {JobId}", job.Id);
            else
                logger.LogError("Failed to process job {JobId}: {Error}", job.Id, result.Error?.Message);
        }
        catch (Exception ex)
        {
            await HandleJobException(job, ex, cancellationToken);
        }
    }

    private async Task<bool> UpdateJobStatusWithRetry(Job job, JobStatus status, string? result = null, string? exception = null, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;

        job.UpdateStatus(status);
        if (result != null) job.SetResult(result);
        if (exception != null) job.SetException(exception);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var updateResult = await jobStore.Update(job, cancellationToken);
            if (updateResult.IsSuccess)
            {
                return true;
            }

            logger.LogWarning("Failed to update job {JobId} to {Status} (attempt {Attempt}/{MaxAttempts}): {Error}",
                job.Id, status, attempt, maxRetries, updateResult.Error?.Message);

            if (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
        }

        logger.LogError("Failed to update job {JobId} status to {Status} after {MaxAttempts} attempts",
            job.Id, status, maxRetries);
        return false;
    }

    private async Task HandleJobException(Job job, Exception ex, CancellationToken cancellationToken)
    {
        logger.LogError(ex, "Error processing job {JobId}", job.Id);

        if (job.RetryCount < job.MaxRetries)
        {
            job.IncrementRetryCount();

            // Calculate exponential backoff with jitter
            var baseDelay = TimeSpan.FromMinutes(Math.Pow(2, job.RetryCount - 1)); // 1, 2, 4, 8 minutes
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 30000)); // 0-30 seconds
            var delayUntil = DateTime.UtcNow.Add(baseDelay).Add(jitter);

            // Set retry time and queue for future processing
            job.SetRetryTime(delayUntil);
            job.UpdateStatus(JobStatus.Scheduled); // New status for delayed jobs
            job.SetException(ex.Message);

            var updateSuccess = await UpdateJobStatusWithRetry(job, JobStatus.Scheduled, null, ex.Message, cancellationToken);

            if (updateSuccess)
            {
                logger.LogInformation("Job {JobId} scheduled for retry {RetryCount}/{MaxRetries} at {RetryTime}",
                    job.Id, job.RetryCount, job.MaxRetries, delayUntil);
            }
        }
        else
        {
            // Mark as permanently failed
            job.UpdateStatus(JobStatus.Failed);
            job.SetException(ex.Message);

            await UpdateJobStatusWithRetry(job, JobStatus.Failed, null, ex.Message, cancellationToken);

            logger.LogError("Job {JobId} failed permanently after {RetryCount} attempts", job.Id, job.RetryCount);
        }
    }

    private async Task<MethodResult<object?>> ProcessJobPayloadAsync(Job job, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing job {JobId} with name {JobName}", job.Id, job.Name);

        // In a real implementation, we would:
        // 1. Deserialize the payload based on the job name
        // 2. Resolve the appropriate handler from the service provider
        // 3. Call the handler with the deserialized payload
        // 4. Store the result in the job

        // For now, we'll simulate the process
        try
        {
            // Simulate some work
            await Task.Delay(1000, cancellationToken);

            // Simulate a successful result
            return MethodResult<object?>.Success(new { Message = "Job processed successfully", JobId = job.Id });
        }
        catch (Exception ex)
        {
            return MethodResult<object?>.Failure(ex);
        }
    }
}
