using AsyncEndpoints.API;
using AsyncEndpoints.Extensions;
using InMemoryExampleAPI.Models;
using InMemoryExampleAPI.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default)
	.AddAsyncEndpointHandler<NoBodySuccessHandler, string>("empty-body-success")
	.AddAsyncEndpointHandler<NoBodyFailureHandler, string>("empty-body-failure")
	.AddAsyncEndpointHandler<WithBodySuccessHandler, ExampleRequest, ExampleResponse>("with-body-success")
	.AddAsyncEndpointHandler<WithBodyFailureHandler, ExampleRequest, ExampleResponse>("with-body-failure")
    .AddAsyncEndpointsWorker();

var app = builder.Build();

app.MapAsyncGetJobDetails();

app.MapAsyncPost("empty-body-success", "/empty-body/success");
app.MapAsyncPost("empty-body-failure", "/empty-body/failure");
app.MapAsyncPost<ExampleRequest>("with-body-success", "/with-body/success");
app.MapAsyncPost<ExampleRequest>("with-body-failure", "/with-body/failure");

app.Run();
