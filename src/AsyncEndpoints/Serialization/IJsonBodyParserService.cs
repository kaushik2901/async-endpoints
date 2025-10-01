using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.Serialization;

public interface IJsonBodyParserService
{
	/// <summary>
	/// Parses the request body as JSON into the specified type without using reflection.
	/// Compatible with Native AOT.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the JSON to</typeparam>
	/// <param name="httpContext">The HTTP context containing the request body</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>A MethodResult containing the deserialized object of type T or an error</returns>
	Task<MethodResult<T?>> ParseAsync<T>(HttpContext httpContext, CancellationToken cancellationToken = default);
}