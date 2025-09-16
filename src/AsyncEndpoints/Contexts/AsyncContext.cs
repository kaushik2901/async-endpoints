using System.Collections.Generic;

namespace AsyncEndpoints.Context;

public class AsyncContext<TRequest>(TRequest request)
{
    public TRequest Request { get; init; } = request;
    public Dictionary<string, string> Headers { get; set; } = [];
}