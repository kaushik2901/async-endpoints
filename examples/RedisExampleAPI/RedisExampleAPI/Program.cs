using AsyncEndpoints.Extensions;
using AsyncEndpoints.Redis.Extensions;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RedisExampleCore;

var builder = WebApplication.CreateSlimBuilder(args);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services
	.AddAsyncEndpoints()
	.AddAsyncEndpointsRedisStore(redisConnectionString)
	.AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default);

// Configure OpenTelemetry with proper service name
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "RedisExampleAPI", serviceVersion: "1.0.0"))
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

app.MapAsyncGetJobDetails();

app.MapAsyncPost("empty-body-success", "/empty-body/success");
app.MapAsyncPost("empty-body-failure", "/empty-body/failure");
app.MapAsyncPost<ExampleRequest>("with-body-success", "/with-body/success");
app.MapAsyncPost<ExampleRequest>("with-body-failure", "/with-body/failure");

// PUT endpoints
app.MapAsyncPut("empty-body-success", "/empty-body/success-put");
app.MapAsyncPut("empty-body-failure", "/empty-body/failure-put");
app.MapAsyncPut<ExampleRequest>("with-body-success", "/with-body/success-put");
app.MapAsyncPut<ExampleRequest>("with-body-failure", "/with-body/failure-put");

// PATCH endpoints
app.MapAsyncPatch("empty-body-success", "/empty-body/success-patch");
app.MapAsyncPatch("empty-body-failure", "/empty-body/failure-patch");
app.MapAsyncPatch<ExampleRequest>("with-body-success", "/with-body/success-patch");
app.MapAsyncPatch<ExampleRequest>("with-body-failure", "/with-body/failure-patch");

// DELETE endpoints
app.MapAsyncDelete("empty-body-success", "/empty-body/success-delete");
app.MapAsyncDelete("empty-body-failure", "/empty-body/failure-delete");
app.MapAsyncDelete<ExampleRequest>("with-body-success", "/with-body/success-delete");
app.MapAsyncDelete<ExampleRequest>("with-body-failure", "/with-body-failure-delete");

await app.RunAsync();
