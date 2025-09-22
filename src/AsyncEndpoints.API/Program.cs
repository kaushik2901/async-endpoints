using AsyncEndpoints;
using AsyncEndpoints.API;
using AsyncEndpoints.API.Models;
using AsyncEndpoints.API.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddAsyncEndpoints(options => options.MaximumRetries = 5)
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default)
    .AddAsyncEndpointHandler<SampleRequestHandler, SampleRequest, SampleResponse>("Job name")
    .AddAsyncEndpointsWorker();

var app = builder.Build();

app.MapAsyncPost<SampleRequest>("Job name", "/todo-async");

app.Run();
