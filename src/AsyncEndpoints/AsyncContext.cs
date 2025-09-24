using System.Collections.Generic;

namespace AsyncEndpoints;

public sealed class AsyncContext<TRequest>(
    TRequest request,
    IDictionary<string, List<string>> headers,
    IDictionary<string, object?> routeParams,
    IEnumerable<KeyValuePair<string, List<string>>> query)
{
    public TRequest Request { get; init; } = request;
    public IDictionary<string, List<string>> Headers { get; init; } = headers;
    public IDictionary<string, object?> RouteParams { get; set; } = routeParams;
    public IEnumerable<KeyValuePair<string, List<string>>> QueryParams { get; init; } = query;
}