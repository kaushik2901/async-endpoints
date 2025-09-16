using AsyncEndpoints.Job;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEndpoints.RouteBuilder;

public static class RouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string name,
        string pattern,
        Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapPost(pattern, async (HttpContext httpContext, TRequest request, CancellationToken token) =>
        {
            if (handler != null)
            {
                var handlerResponseTask = handler(httpContext, request, token);
                if (handlerResponseTask != null)
                {
                    var handlerResponse = await handlerResponseTask;
                    if (handlerResponse != null) return handlerResponse;
                }
            }

            var id = JobIdHelper.GetJobId(httpContext);
            var jobStore = httpContext.RequestServices.GetRequiredService<IJobStore>();

            var existingJob = await jobStore.Get(id, token);
            if (existingJob != null)
            {
                return Results.Accepted(existingJob.Id.ToString(), existingJob);
            }

            var payload = JsonSerializer.Serialize(request);
            var job = Job.Job.Create(id, name, payload);

            await jobStore.Add(job, token);

            return Results.Accepted(job.Id.ToString(), job);
        });
    }
}
