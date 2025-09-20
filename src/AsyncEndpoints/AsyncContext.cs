using System.Collections.Generic;

namespace AsyncEndpoints;

public sealed class AsyncContext<TRequest>(TRequest request)
{
    public TRequest Request { get; init; } = request;
    public Dictionary<string, string> Headers { get; set; } = [];
}