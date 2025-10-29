using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Infrastructure.Observability;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
/// <summary>
/// Creates a service scope for handler execution to ensure proper dependency injection and disposal.
/// </summary>
public class HandlerExecutionService(ILogger<HandlerExecutionService> logger, IServiceScopeFactory serviceScopeFactory, IAsyncEndpointsObservability metrics) : IHandlerExecutionService
{
	private readonly ILogger<HandlerExecutionService> _logger = logger;
	private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
	private readonly IAsyncEndpointsObservability _metrics = metrics;

	/// <inheritdoc />
	public async Task<MethodResult<object>> ExecuteHandlerAsync(string jobName, object request, Job job, CancellationToken cancellationToken)
	{
		using var _ = _logger.BeginScope(new { JobId = job.Id, JobName = jobName, RequestType = request.GetType().Name });
		
		_logger.LogDebug("Starting handler execution for job: {JobName}, JobId: {JobId}", jobName, job.Id);

		await using var serviceScope = _serviceScopeFactory.CreateAsyncScope();

		var invoker = HandlerRegistrationTracker.GetInvoker(jobName);
		if (invoker == null)
		{
			_logger.LogError("Handler registration not found for job name: {JobName}", jobName);
			return MethodResult<object>.Failure(new InvalidOperationException($"Handler registration not found for job name: {jobName}"));
		}

		_logger.LogDebug("Found handler invoker for job: {JobName}, starting execution", jobName);

		try
		{
			_logger.LogDebug("Invoking handler for job: {JobName}, JobId: {JobId}", jobName, job.Id);
			var result = await invoker(serviceScope.ServiceProvider, request, job, cancellationToken);

			if (result.IsSuccess)
			{
				_logger.LogDebug("Handler execution successful for job: {JobName}, JobId: {JobId}", jobName, job.Id);
			}
			else
			{
				_logger.LogError("Handler execution failed for job: {JobName}, JobId: {JobId}, Error: {Error}",
					jobName, job.Id, result.Error?.Message);
				_metrics.RecordHandlerError(jobName, result.Error?.Code ?? "UNKNOWN_ERROR");
			}

			return result;
		}
		catch (Exception ex)
		{
			_metrics.RecordHandlerError(jobName, ex.GetType().Name);
			_logger.LogError(ex, "Exception occurred during handler execution for job: {JobName}, JobId: {JobId}", jobName, job.Id);
			return MethodResult<object>.Failure(new InvalidOperationException($"Handler execution failed: {ex.Message}", ex));
		}
	}
}
