using AsyncEndpoints.Handlers;
using AsyncEndpoints.UnitTests.Utilities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests.TestSupport;

public class TestResponse
{
	public string? Value { get; set; }
}

public class TestAsyncEndpointRequestHandler : IAsyncEndpointRequestHandler<TestRequest, TestResponse>
{
	public Task<MethodResult<TestResponse>> HandleAsync(AsyncContext<TestRequest> context, CancellationToken token)
	{
		throw new NotImplementedException();
	}
}