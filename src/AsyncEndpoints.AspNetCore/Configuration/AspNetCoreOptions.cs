namespace AsyncEndpoints.AspNetCore.Configuration;

public sealed class AspNetCoreOptions
{
	public string EndpointPrefix { get; set; } = "/jobs";
	public string DefaultResponseContentType { get; set; } = "application/json";
}
