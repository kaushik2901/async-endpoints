using AsyncEndpoints;
using AsyncEndpoints.Redis;
using RedisExampleCore;

var builder = WebApplication.CreateSlimBuilder(args);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(redisConnectionString)
    .AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default);

var app = builder.Build();

app.MapAsyncPost<ExampleJobRequest>("ExampleJob", "/jobs/submit");

await app.RunAsync();
