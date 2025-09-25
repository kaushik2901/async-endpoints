using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Services;

public class HandlerExecutionService(ILogger<HandlerExecutionService> logger, IServiceScopeFactory serviceScopeFactory) : IHandlerExecutionService
{
    private readonly ILogger<HandlerExecutionService> _logger = logger;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task<MethodResult<object>> ExecuteHandlerAsync(string jobName, object request, Job job, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing handler for job: {JobName}, JobId: {JobId}", jobName, job.Id);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var invoker = HandlerRegistrationTracker.GetInvoker(jobName);
        if (invoker == null)
        {
            _logger.LogError("Handler registration not found for job name: {JobName}", jobName);
            return MethodResult<object>.Failure(new InvalidOperationException($"Handler registration not found for job name: {jobName}"));
        }

        _logger.LogDebug("Found handler invoker for job: {JobName}, starting execution", jobName);

        var result = await invoker(scope.ServiceProvider, request, job, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogDebug("Handler execution successful for job: {JobName}, JobId: {JobId}", jobName, job.Id);
        }
        else
        {
            _logger.LogError("Handler execution failed for job: {JobName}, JobId: {JobId}, Error: {Error}",
                jobName, job.Id, result.Error?.Message);
        }

        return result;
    }
}