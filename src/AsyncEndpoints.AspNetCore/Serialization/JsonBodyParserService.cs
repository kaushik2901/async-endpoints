using AsyncEndpoints.Abstractions.Common;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.AspNetCore.Serialization;

public class JsonBodyParserService(ISerializer serializer, ILogger<JsonBodyParserService> logger) : IJsonBodyParserService
{
	private readonly ISerializer _serializer = serializer;
	private readonly ILogger<JsonBodyParserService> _logger = logger;

	public async Task<MethodResult<T?>> ParseAsync<T>(HttpContext httpContext, CancellationToken cancellationToken = default)
	{
		var requestType = typeof(T).Name;
		_logger.LogDebug("Starting JSON parsing for type {RequestType}", requestType);

		try
		{
			if (httpContext.Request.Body is null || httpContext.Request.ContentLength == 0)
			{
				_logger.LogDebug("Request has no body for type {RequestType}", requestType);
				return MethodResult<T?>.Failure("Request has not body");
			}

			var contentType = httpContext.Request.ContentType;
			if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
			{
				_logger.LogWarning("Request content type is not application/json for type {RequestType}: {ContentType}", requestType, contentType);
				return MethodResult<T?>.Failure("Request content type must be application/json");
			}

			_logger.LogDebug("Request content type validated as JSON for type {RequestType}", requestType);

			httpContext.Request.EnableBuffering();

			T? result;
			var typeInfo = (JsonTypeInfo<T>?)AsyncEndpointsAspNetCoreJsonSerializationContext.Default.GetTypeInfo(typeof(T));
			if (typeInfo is not null)
			{
				result = await _serializer.DeserializeAsync(httpContext.Request.Body, typeInfo, cancellationToken);
			}
			else
			{
				var coreTypeInfo = (JsonTypeInfo<T>?)AsyncEndpointsJsonSerializationContext.Default.GetTypeInfo(typeof(T));
				if (coreTypeInfo is not null)
				{
					result = await _serializer.DeserializeAsync(httpContext.Request.Body, coreTypeInfo, cancellationToken);
				}
				else
				{
					result = await _serializer.DeserializeAsync<T>(httpContext.Request.Body, (JsonSerializerOptions?)null, cancellationToken);
				}
			}

			httpContext.Request.Body.Position = 0;

			_logger.LogDebug("Successfully parsed JSON for type {RequestType}", requestType);
			return MethodResult<T?>.Success(result);
		}
		catch (JsonException jsonEx)
		{
			_logger.LogError(jsonEx, "Invalid JSON format in request body for type {RequestType}", requestType);
			return MethodResult<T?>.Failure(new InvalidOperationException($"Invalid JSON format in request body for type {typeof(T).Name}", jsonEx));
		}
		catch (NotSupportedException notSupportedEx)
		{
			_logger.LogError(notSupportedEx, "JSON deserialization not supported for type {RequestType}", requestType);
			return MethodResult<T?>.Failure(new InvalidOperationException($"JSON deserialization not supported for type: {typeof(T).Name}", notSupportedEx));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error during JSON parsing for type {RequestType}", requestType);
			return MethodResult<T?>.Failure(new InvalidOperationException($"Unexpected error during JSON parsing for type {typeof(T).Name}", ex));
		}
	}
}
