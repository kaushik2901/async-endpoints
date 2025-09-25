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

/// <summary>
/// Implements the IJobProcessorService interface to process individual jobs.
/// Handles the execution of job payloads, serialization/deserialization of requests and responses,
/// and updates job status and results in the job store.
/// </summary>
public class JobProcessorService(ILogger<JobProcessorService> logger, IJobStore jobStore, IHandlerExecutionService handlerExecutionService, IOptions<JsonOptions> jsonOptions) : IJobProcessorService
{
    private readonly ILogger<JobProcessorService> _logger = logger;
    private readonly IJobStore _jobStore = jobStore;
    private readonly IHandlerExecutionService _handlerExecutionService = handlerExecutionService;
    private readonly IOptions<JsonOptions> _jsonOptions = jsonOptions;

    public async Task ProcessAsync(Job job, CancellationToken cancellationToken)
    {
        var updateJobStatusResult = await _jobStore.UpdateJobStatus(job.Id, JobStatus.InProgress, cancellationToken);
        if (!updateJobStatusResult.IsSuccess)
        {
            _logger.LogError("Failed to update job {JobId} status to InProgress", job.Id);
            return;
        }

        try
        {
            var result = await ProcessJobPayloadAsync(job, cancellationToken);

            if (result.IsSuccess)
                _logger.LogInformation("Successfully processed job {JobId}", job.Id);
            else
                _logger.LogError("Failed to process job {JobId}: {Error}", job.Id, result.Error?.Message);

            var jobUpdateResult = await UpdateJob(job, result, cancellationToken);

            if (jobUpdateResult.IsSuccess)
                _logger.LogInformation("Successfully updated job {JobId}", job.Id);
            else
                _logger.LogError("Failed to update job {JobId}: {Error}", job.Id, result.Error?.Message);
        }
        catch (Exception ex)
        {
            var serializedException = ExceptionSerializer.Serialize(ex);
            await _jobStore.UpdateJobException(job.Id, serializedException, cancellationToken);
        }
    }

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

            var request = JsonSerializer.Deserialize(job.Payload, handlerRegistration.RequestType, _jsonOptions.Value.SerializerOptions);
            if (request == null)
            {
                return MethodResult<string>.Failure(new InvalidOperationException($"Failed to deserialize request payload for job: {job.Name}"));
            }

            var result = await _handlerExecutionService.ExecuteHandlerAsync(job.Name, request, job, cancellationToken);
            if (!result.IsSuccess)
            {
                return MethodResult<string>.Failure(result.Error!);
            }

            var serializedResult = JsonSerializer.Serialize(result.Data, handlerRegistration.ResponseType, _jsonOptions.Value.SerializerOptions);

            return MethodResult<string>.Success(serializedResult);
        }
        catch (Exception ex)
        {
            return MethodResult<string>.Failure(ex);
        }
    }

    private async Task<MethodResult> UpdateJob(Job job, MethodResult<string> processJobResult, CancellationToken cancellationToken)
    {
        if (processJobResult.IsSuccess)
        {
            return await _jobStore.UpdateJobResult(job.Id, processJobResult.Data ?? string.Empty, cancellationToken);
        }

        return await _jobStore.UpdateJobException(job.Id, processJobResult.Error?.ToString() ?? string.Empty, cancellationToken);
    }
}
