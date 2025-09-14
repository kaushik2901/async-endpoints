using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints;

internal static class RouteBuilderExtensionsHelpers
{
    internal static async Task<IResult> HandleAsyncEndpoint<TRequest>(
        HttpContext httpContext,
        TRequest request,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        if (handler != null)
        {
            var handlerResponseTask = handler(httpContext);
            if (handlerResponseTask != null)
            {
                var handlerResponse = await handlerResponseTask;
                if (handlerResponse != null) return handlerResponse;
            }
        }

        var id = Guid.NewGuid(); // TODO: Read from request header/query
        var payLoad = JsonSerializer.Serialize(request);
        var job = Job.Create(id, payLoad);

        // TODO: Complete the implementation.

        return Results.Accepted($"/jobs/{job.Id}");
    }
}