using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.Services;

public class HandlerExecutionService(IServiceScopeFactory serviceScopeFactory) : IHandlerExecutionService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task<MethodResult<object>> ExecuteHandlerAsync(string jobName, object request, Job job, CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var invoker = HandlerRegistrationTracker.GetInvoker(jobName);
        if (invoker == null)
        {
            return MethodResult<object>.Failure(new InvalidOperationException($"Handler registration not found for job name: {jobName}"));
        }

        var result = await invoker(scope.ServiceProvider, request, job, cancellationToken);

        return result;
    }
}