using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Utilities;

public static class AsyncContextBuilder
{
    public static AsyncContext<TRequest> Build<TRequest>(TRequest request, Job job)
    {
        var context = new AsyncContext<TRequest>(
            request,
            job.Headers,
            job.RouteParams,
            job.QueryParams
        );

        return context;
    }
}
