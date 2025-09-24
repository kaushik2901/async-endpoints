using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Services;

public class JobProcessor(ILogger<JobProcessor> logger, IJobStore jobStore, IHandlerExecutionService handlerExecutionService, IOptions<JsonOptions> jsonOptions) : IJobProcessor
{
    private readonly ILogger<JobProcessor> _logger = logger;
    private readonly IJobStore _jobStore = jobStore;
    private readonly IHandlerExecutionService _handlerExecutionService = handlerExecutionService;
    private readonly IOptions<JsonOptions> _jsonOptions = jsonOptions;

    public async Task ProcessAsync(Job job, CancellationToken cancellationToken)
    {
        var originalJobStatus = job.Status;

        if (!await UpdateJobStatusWithRetry(job, JobStatus.InProgress, null, null, cancellationToken))
        {
            job.UpdateStatus(originalJobStatus);
            _logger.LogError("Failed to update job {JobId} status to InProgress", job.Id);
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
                _logger.LogInformation("Successfully processed job {JobId}", job.Id);
            else
                _logger.LogError("Failed to process job {JobId}: {Error}", job.Id, result.Error?.Message);
        }
        catch (Exception ex)
        {
            await HandleJobException(job, ex, cancellationToken);
        }
    }

    private async Task<MethodResult<string>> ProcessJobPayloadAsync(Job job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing job {JobId} with name {JobName}", job.Id, job.Name);

        try
        {
            // Deserialize the payload to the expected request type
            var handlerRegistration = HandlerRegistrationTracker.GetHandlerRegistration(job.Name);
            if (handlerRegistration == null)
            {
                return MethodResult<string>.Failure(new InvalidOperationException($"Handler registration not found for job name: {job.Name}"));
            }

            // Deserialize the payload to the expected request type
            var request = JsonSerializer.Deserialize(job.Payload, handlerRegistration.RequestType, _jsonOptions.Value.SerializerOptions);
            if (request == null)
            {
                return MethodResult<string>.Failure(new InvalidOperationException($"Failed to deserialize request payload for job: {job.Name}"));
            }

            // Execute handler using the AOT-optimized service
            var result = await _handlerExecutionService.ExecuteHandlerAsync(job.Name, request, job, cancellationToken);
            if (!result.IsSuccess)
            {
                return MethodResult<string>.Failure(result.Error!);
            }

            // Serialize the result to string
            var serializedResult = JsonSerializer.Serialize(result.Data, handlerRegistration.ResponseType, _jsonOptions.Value.SerializerOptions);

            return MethodResult<string>.Success(serializedResult);
        }
        catch (Exception ex)
        {
            return MethodResult<string>.Failure(ex);
        }
    }

    private async Task HandleJobException(Job job, Exception ex, CancellationToken cancellationToken)
    {
        _logger.LogError(ex, "Error processing job {JobId}", job.Id);

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
                _logger.LogInformation("Job {JobId} scheduled for retry {RetryCount}/{MaxRetries} at {RetryTime}",
                    job.Id, job.RetryCount, job.MaxRetries, delayUntil);
            }
        }
        else
        {
            // Mark as permanently failed
            job.UpdateStatus(JobStatus.Failed);
            job.SetException(ex.Message);

            await UpdateJobStatusWithRetry(job, JobStatus.Failed, null, ex.Message, cancellationToken);

            _logger.LogError("Job {JobId} failed permanently after {RetryCount} attempts", job.Id, job.RetryCount);
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
            var updateResult = await _jobStore.Update(job, cancellationToken);
            if (updateResult.IsSuccess)
            {
                return true;
            }

            _logger.LogWarning("Failed to update job {JobId} to {Status} (attempt {Attempt}/{MaxAttempts}): {Error}",
                job.Id, status, attempt, maxRetries, updateResult.Error?.Message);

            if (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
        }

        _logger.LogError("Failed to update job {JobId} status to {Status} after {MaxAttempts} attempts",
            job.Id, status, maxRetries);
        return false;
    }
}
