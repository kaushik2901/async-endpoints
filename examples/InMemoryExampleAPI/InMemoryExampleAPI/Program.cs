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

// PUT endpoints
app.MapAsyncPut("empty-body-success", "/empty-body/success-put");
app.MapAsyncPut("empty-body-failure", "/empty-body/failure-put");
app.MapAsyncPut<ExampleRequest>("with-body-success", "/with-body/success-put");
app.MapAsyncPut<ExampleRequest>("with-body-failure", "/with-body/failure-put");

// PATCH endpoints
app.MapAsyncPatch("empty-body-success", "/empty-body/success-patch");
app.MapAsyncPatch("empty-body-failure", "/empty-body/failure-patch");
app.MapAsyncPatch<ExampleRequest>("with-body-success", "/with-body/success-patch");
app.MapAsyncPatch<ExampleRequest>("with-body-failure", "/with-body/failure-patch");

// DELETE endpoints
app.MapAsyncDelete("empty-body-success", "/empty-body/success-delete");
app.MapAsyncDelete("empty-body-failure", "/empty-body/failure-delete");
app.MapAsyncDelete<ExampleRequest>("with-body-success", "/with-body/success-delete");
app.MapAsyncDelete<ExampleRequest>("with-body-failure", "/with-body/failure-delete");

app.Run();
