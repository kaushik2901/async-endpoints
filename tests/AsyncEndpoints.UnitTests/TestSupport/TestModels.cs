using AsyncEndpoints.Handlers;
using AsyncEndpoints.UnitTests.Utilities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests.TestSupport;

public class TestRequest
{
	public string? Value { get; set; }
}

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

public class TestNoBodyRequestHandler : IAsyncEndpointRequestHandler<string>
{
	public Task<MethodResult<string>> HandleAsync(AsyncContext context, CancellationToken token)
	{
		return Task.FromResult(MethodResult<string>.Success("Test response from no-body handler"));
	}
}
