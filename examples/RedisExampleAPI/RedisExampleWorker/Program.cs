using AsyncEndpoints.Extensions;
using AsyncEndpoints.Redis.Extensions;
using Microsoft.AspNetCore.Builder;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RedisExampleCore;
using RedisExampleWorker;

var builder = WebApplication.CreateSlimBuilder(args);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services
	.AddAsyncEndpoints()
	.AddAsyncEndpointsWorker()
	.AddAsyncEndpointsRedisStore(redisConnectionString)
	.AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default)
	.AddAsyncEndpointHandler<NoBodySuccessHandler, string>("empty-body-success")
	.AddAsyncEndpointHandler<NoBodyFailureHandler, string>("empty-body-failure")
	.AddAsyncEndpointHandler<WithBodySuccessHandler, ExampleRequest, ExampleResponse>("with-body-success")
	.AddAsyncEndpointHandler<WithBodyFailureHandler, ExampleRequest, ExampleResponse>("with-body-failure");

// Configure OpenTelemetry with proper service name
builder.Services.AddOpenTelemetry()
	.ConfigureResource(resource => resource
		.AddService(serviceName: "RedisExampleWorker", serviceVersion: "1.0.0"))
	.WithTracing(tracingBuilder =>
	{
		tracingBuilder
			.AddAspNetCoreInstrumentation()
			.AddHttpClientInstrumentation()
			.AddOtlpExporter()
			.AddSource("AsyncEndpoints");
	})
	.WithMetrics(metricsBuilder =>
	{
		metricsBuilder
			.AddAspNetCoreInstrumentation()
			.AddHttpClientInstrumentation()
			.AddOtlpExporter()
			.AddMeter("AsyncEndpoints");
	})
	.WithLogging(loggingBuilder =>
	{
		loggingBuilder
			.AddOtlpExporter();
	});

var app = builder.Build();

await app.RunAsync();
