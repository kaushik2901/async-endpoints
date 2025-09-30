using AsyncEndpoints;
using AsyncEndpoints.API;
using InMemoryExampleAPI.Models;
using InMemoryExampleAPI.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default)
    .AddAsyncEndpointHandler<SampleRequestHandler, SampleRequest, SampleResponse>("async-operation")
    .AddAsyncEndpointsWorker();

var app = builder.Build();

app.MapAsyncGetJobDetails();
app.MapAsyncPost<SampleRequest>("async-operation", "/async-operation");

app.Run();
