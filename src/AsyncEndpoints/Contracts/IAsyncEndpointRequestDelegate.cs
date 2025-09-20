using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.Contracts;

public interface IAsyncEndpointRequestDelegate
{
    Task<IResult> HandleAsync<TRequest>(string jobName, HttpContext httpContext, TRequest request, Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null, CancellationToken cancellationToken = default);
}
