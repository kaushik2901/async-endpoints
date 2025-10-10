using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.Infrastructure.Serialization;

/// <inheritdoc />
public class JsonBodyParserService : IJsonBodyParserService
{
	private readonly ISerializer _serializer;

	public JsonBodyParserService(ISerializer serializer)
	{
		_serializer = serializer;
	}

	/// <inheritdoc />
	public async Task<MethodResult<T?>> ParseAsync<T>(HttpContext httpContext, CancellationToken cancellationToken = default)
	{
		try
		{
			if (httpContext.Request.Body is null || httpContext.Request.ContentLength == 0)
			{
				return MethodResult<T?>.Failure("Request has not body");
			}

			// Check content type
			var contentType = httpContext.Request.ContentType;
			if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
			{
				return MethodResult<T?>.Failure("Request content type must be application/json");
			}

			// Enable buffering so the stream can be read multiple times
			httpContext.Request.EnableBuffering();

			// Use the ISerializer to deserialize the JSON stream to the specified type
			var result = await _serializer.DeserializeAsync<T>(httpContext.Request.Body, cancellationToken: cancellationToken);

			// Reset the stream position so it can be read again by other parts of the pipeline
			httpContext.Request.Body.Position = 0;

			return MethodResult<T?>.Success(result);
		}
		catch (JsonException jsonEx)
		{
			return MethodResult<T?>.Failure(new InvalidOperationException($"Invalid JSON format in request body for type {typeof(T).Name}", jsonEx));
		}
		catch (NotSupportedException notSupportedEx)
		{
			return MethodResult<T?>.Failure(new InvalidOperationException($"JSON deserialization not supported for type: {typeof(T).Name}", notSupportedEx));
		}
		catch (Exception ex)
		{
			return MethodResult<T?>.Failure(new InvalidOperationException($"Unexpected error during JSON parsing for type {typeof(T).Name}", ex));
		}
	}
}
