using AsyncEndpoints;
using AsyncEndpoints.API;
using InMemoryExampleAPI.Models;
using InMemoryExampleAPI.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddAsyncEndpoints(options => options.MaximumRetries = 5)
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default)
    .AddAsyncEndpointHandler<SampleRequestHandler, SampleRequest, SampleResponse>("async-operation")
    .AddAsyncEndpointsWorker();

var app = builder.Build();

app.MapAsyncPost<SampleRequest>("async-operation", "/async-operation");

app.Run();
