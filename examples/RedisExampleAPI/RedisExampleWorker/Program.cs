using AsyncEndpoints.Extensions;
using AsyncEndpoints.Redis.Extensions;
using Microsoft.AspNetCore.Builder;
using RedisExampleCore;
using RedisExampleWorker;

var builder = WebApplication.CreateSlimBuilder(args);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services
	.AddAsyncEndpoints()
	.AddAsyncEndpointsWorker(options =>
	{
		options.JobTimeoutMinutes = 1;
		options.RecoveryCheckIntervalSeconds = 120;
	})
	.AddAsyncEndpointsRedisStore(redisConnectionString)
	.AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default)
	.AddAsyncEndpointHandler<ExampleJobHandler, ExampleJobRequest, ExampleJobResponse>("ExampleJob");

var app = builder.Build();

await app.RunAsync();
