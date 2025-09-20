using AsyncEndpoints;
using AsyncEndpoints.API.Models;
using AsyncEndpoints.API.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AsyncEndpointsJsonSerializerContext.Default);
});

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointHandler<SampleRequestHandler, SampleRequest, SampleResponse>("Job name");

var app = builder.Build();

app.MapAsyncPost<SampleRequest>("Job name", "/todo-async");

app.Run();
