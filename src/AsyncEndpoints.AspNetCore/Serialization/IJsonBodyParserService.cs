using AsyncEndpoints.Abstractions.Utilities;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.AspNetCore.Serialization;

public interface IJsonBodyParserService
{
	Task<MethodResult<T?>> ParseAsync<T>(HttpContext httpContext, CancellationToken cancellationToken = default);
}
