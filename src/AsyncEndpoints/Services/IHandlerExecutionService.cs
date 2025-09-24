using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Services;

public interface IHandlerExecutionService
{
    Task<MethodResult<object>> ExecuteHandlerAsync(string jobName, object request, Job job, CancellationToken cancellationToken);
}