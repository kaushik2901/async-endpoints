using System;

namespace AsyncEndpoints.Handlers;

/// <summary>
/// Represents a registration of a handler for processing async endpoint requests.
/// </summary>
public sealed class HandlerRegistration(string jobName, Type requestType, Type responseType)
{
	/// <summary>
	/// Gets or sets the unique name of the job associated with this handler.
	/// </summary>
	public string JobName { get; set; } = jobName;

	/// <summary>
	/// Gets or sets the type of the request object that this handler processes.
	/// </summary>
	public Type RequestType { get; set; } = requestType;

	/// <summary>
	/// Gets or sets the type of the response object that this handler returns.
	/// </summary>
	public Type ResponseType { get; set; } = responseType;
}
