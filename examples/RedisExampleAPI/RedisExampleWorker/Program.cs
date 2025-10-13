using AsyncEndpoints.Extensions;
using AsyncEndpoints.Redis.Extensions;
using Microsoft.AspNetCore.Builder;
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

var app = builder.Build();

await app.RunAsync();
