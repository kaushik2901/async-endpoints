using AsyncEndpoints.Abstractions.Jobs;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Core.Execution;

public sealed class JobDispatcher
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IHandlerRegistry _handlerRegistry;
	private readonly ILogger<JobDispatcher> _logger;

	public JobDispatcher(IServiceProvider serviceProvider, IHandlerRegistry handlerRegistry, ILogger<JobDispatcher> logger)
	{
		_serviceProvider = serviceProvider;
		_handlerRegistry = handlerRegistry;
		_logger = logger;
	}

	public async Task<DispatchResult> DispatchAsync(JobRecord record, CancellationToken ct = default)
	{
		var invoker = _handlerRegistry.GetInvoker(record.JobName);

		if (invoker is null)
		{
			_logger.LogWarning("No handler registered for job type {JobName}", record.JobName);
			return DispatchResult.Failure("No handler registered for job type: " + record.JobName);
		}

		try
		{
			await invoker(_serviceProvider, record, ct);
			return DispatchResult.Success();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error dispatching job {JobId} of type {JobName}", record.JobId, record.JobName);
			return DispatchResult.Failure(ex.Message);
		}
	}
}

public sealed record DispatchResult
{
	public bool IsSuccess { get; init; }
	public string? ErrorMessage { get; init; }

	public static DispatchResult Success() => new() { IsSuccess = true };
	public static DispatchResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
}
