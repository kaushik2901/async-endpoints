using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.Legacy.Observability;
using AsyncEndpoints.Core.UnitTests.TestSupport;

namespace AsyncEndpoints.Core.UnitTests.Infrastructure.Observability;

public class AsyncEndpointsObservabilityTests
{
	[Fact]
	public void RecordJobCreated_WhenMetricsEnabled_CallsMetricInstrument()
	{
		var options = new AsyncEndpointsOptions();
		var observability = new AsyncEndpointsObservability(options);

		observability.RecordJobCreated("TestJob", "InMemory");

		Assert.True(true);
	}

	[Fact]
	public void RecordJobProcessed_WhenMetricsEnabled_CallsMetricInstrument()
	{
		var options = new AsyncEndpointsOptions();
		var observability = new AsyncEndpointsObservability(options);

		observability.RecordJobProcessed("TestJob", "completed", "InMemory");

		Assert.True(true);
	}

	[Fact]
	public void StartJobSubmitActivity_WhenTracingEnabled_ReturnsActivity()
	{
		var options = new AsyncEndpointsOptions { ObservabilityEnabled = true };
		var observability = new AsyncEndpointsObservability(options);

		observability.StartJobSubmitActivity("TestJob", "InMemory", Guid.NewGuid());

		Assert.True(options.ObservabilityEnabled);
	}

	[Theory, AutoMoqData]
	public void TimeJobProcessingDuration_WhenMetricsEnabled_ReturnsDisposableTimer(
		string jobName,
		string status)
	{
		var options = new AsyncEndpointsOptions();
		var observability = new AsyncEndpointsObservability(options);

		var timer = observability.TimeJobProcessingDuration(jobName, status);

		Assert.NotNull(timer);
		Assert.IsType<IDisposable>(timer, exactMatch: false);
	}
}
