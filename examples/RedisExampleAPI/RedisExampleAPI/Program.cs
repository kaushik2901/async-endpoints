using AsyncEndpoints.Extensions;
using AsyncEndpoints.Redis.Extensions;
using RedisExampleCore;

var builder = WebApplication.CreateSlimBuilder(args);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services
	.AddAsyncEndpoints()
	.AddAsyncEndpointsRedisStore(redisConnectionString)
	.AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default);

var app = builder.Build();

app.MapAsyncGetJobDetails();

app.MapAsyncPost("empty-body-success", "/empty-body/success");
app.MapAsyncPost("empty-body-failure", "/empty-body/failure");
app.MapAsyncPost<ExampleRequest>("with-body-success", "/with-body/success");
app.MapAsyncPost<ExampleRequest>("with-body-failure", "/with-body/failure");

await app.RunAsync();
