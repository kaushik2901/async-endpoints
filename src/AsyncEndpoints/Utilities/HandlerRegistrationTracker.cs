using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Utilities;

public static class HandlerRegistrationTracker
{
    private static readonly ConcurrentDictionary<string, HandlerRegistration> _handlers = [];
    private static readonly ConcurrentDictionary<string, Func<IServiceProvider, object, CancellationToken, Task<MethodResult<object>>>> _invokers = [];

    public static void Register<TRequest, TResponse>(string jobName,
        Func<IServiceProvider, TRequest, CancellationToken, Task<MethodResult<TResponse>>> handlerFunc)
    {
        _handlers.TryAdd(jobName, new HandlerRegistration(jobName, typeof(TRequest), typeof(TResponse)));
        _invokers.TryAdd(jobName, (serviceProvider, request, cancellationToken) => Invoker(serviceProvider, request, handlerFunc, cancellationToken));
    }

    public static HandlerRegistration? GetHandlerRegistration(string jobName)
    {
        return _handlers.GetValueOrDefault(jobName);
    }

    public static Func<IServiceProvider, object, CancellationToken, Task<MethodResult<object>>>? GetInvoker(string jobName)
    {
        return _invokers.TryGetValue(jobName, out var invoker) ? invoker : null;
    }

    private static async Task<MethodResult<object>> Invoker<TRequest, TResponse>(IServiceProvider serviceProvider, object request, Func<IServiceProvider, TRequest, CancellationToken, Task<MethodResult<TResponse>>> handlerFunc, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var result = await handlerFunc(serviceProvider, typedRequest, cancellationToken);
        return Convert(result);
    }

    private static MethodResult<object> Convert<TResponse>(MethodResult<TResponse> result)
    {
        if (result.IsSuccess) return MethodResult<object>.Success(result.Data!);

        return MethodResult<object>.Failure(result.Error!);
    }
}