using AsyncEndpoints;
using AsyncEndpoints.Redis;
using RedisExampleCore;
using RedisExampleWorker;

var builder = WebApplication.CreateSlimBuilder(args);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsWorker()
    .AddAsyncEndpointsRedisStore(redisConnectionString)
    .AddAsyncEndpointHandler<ExampleJobHandler, ExampleJobRequest, ExampleJobResponse>("ExampleJob");

var app = builder.Build();

await app.RunAsync();
