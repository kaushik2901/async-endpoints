using System.Collections.Generic;

namespace AsyncEndpoints.Handlers;

/// <summary>
/// Represents the context for an asynchronous request, containing the request object and associated HTTP context information.
/// </summary>
/// <typeparam name="TRequest">The type of the request object.</typeparam>
/// <param name="request">The original request object.</param>
/// <param name="headers">The HTTP headers from the original request.</param>
/// <param name="routeParams">The route parameters from the original request.</param>
/// <param name="query">The query parameters from the original request.</param>
public sealed class AsyncContext<TRequest>(
	TRequest request,
	IDictionary<string, List<string?>> headers,
	IDictionary<string, object?> routeParams,
	IEnumerable<KeyValuePair<string, List<string?>>> query)
{
	/// <summary>
	/// Gets the original request object.
	/// </summary>
	public TRequest Request { get; init; } = request;

	/// <summary>
	/// Gets the HTTP headers from the original request.
	/// </summary>
	public IDictionary<string, List<string?>> Headers { get; init; } = headers;

	/// <summary>
	/// Gets or sets the route parameters from the original request.
	/// </summary>
	public IDictionary<string, object?> RouteParams { get; set; } = routeParams;

	/// <summary>
	/// Gets the query parameters from the original request.
	/// </summary>
	public IEnumerable<KeyValuePair<string, List<string?>>> QueryParams { get; init; } = query;
}
