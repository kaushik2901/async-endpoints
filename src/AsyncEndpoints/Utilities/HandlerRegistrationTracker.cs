using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Handlers;
using AsyncEndpoints.JobProcessing;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Tracks registered handlers for async endpoints and provides methods to retrieve them.
/// This class maintains a registry of job name to handler mappings.
/// </summary>
public static class HandlerRegistrationTracker
{
	private static readonly ConcurrentDictionary<string, HandlerRegistration> _handlers = [];
	private static readonly ConcurrentDictionary<string, Func<IServiceProvider, object, Job, CancellationToken, Task<MethodResult<object>>>> _invokers = [];

	/// <summary>
	/// Registers a handler function for a specific job name.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request object.</typeparam>
	/// <typeparam name="TResponse">The type of the response object.</typeparam>
	/// <param name="jobName">The unique name of the job.</param>
	/// <param name="handlerFunc">The handler function to execute when the job is processed.</param>
	public static void Register<TRequest, TResponse>(string jobName,
		Func<IServiceProvider, TRequest, Job, CancellationToken, Task<MethodResult<TResponse>>> handlerFunc)
	{
		_handlers.TryAdd(jobName, new HandlerRegistration(jobName, typeof(TRequest), typeof(TResponse)));
		_invokers.TryAdd(jobName, (serviceProvider, request, job, cancellationToken) => Invoker(serviceProvider, request, job, handlerFunc, cancellationToken));
	}

	/// <summary>
	/// Gets the registration information for a handler with the specified job name.
	/// </summary>
	/// <param name="jobName">The unique name of the job.</param>
	/// <returns>The <see cref="HandlerRegistration"/> for the job, or null if not found.</returns>
	public static HandlerRegistration? GetHandlerRegistration(string jobName)
	{
		return _handlers.GetValueOrDefault(jobName);
	}

	/// <summary>
	/// Gets the invoker function for a handler with the specified job name.
	/// </summary>
	/// <param name="jobName">The unique name of the job.</param>
	/// <returns>A function that can invoke the handler, or null if not found.</returns>
	public static Func<IServiceProvider, object, Job, CancellationToken, Task<MethodResult<object>>>? GetInvoker(string jobName)
	{
		return _invokers.TryGetValue(jobName, out var invoker) ? invoker : null;
	}

	private static async Task<MethodResult<object>> Invoker<TRequest, TResponse>(IServiceProvider serviceProvider, object request, Job job, Func<IServiceProvider, TRequest, Job, CancellationToken, Task<MethodResult<TResponse>>> handlerFunc, CancellationToken cancellationToken)
	{
		var typedRequest = (TRequest)request;
		var result = await handlerFunc(serviceProvider, typedRequest, job, cancellationToken);
		return Convert(result);
	}

	private static MethodResult<object> Convert<TResponse>(MethodResult<TResponse> result)
	{
		if (result.IsSuccess) return MethodResult<object>.Success(result.Data!);

		return MethodResult<object>.Failure(result.Error!);
	}
}
