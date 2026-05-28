using AsyncEndpoints.Core.Legacy.Handlers;

namespace AsyncEndpoints.Core.UnitTests.Handlers;

public class HandlerRegistrationTests
{
	[Fact]
	public void Constructor_SetsPropertiesCorrectly()
	{
		var jobName = "TestJob";
		var requestType = typeof(string);
		var responseType = typeof(int);

		var registration = new HandlerRegistration(jobName, requestType, responseType);

		Assert.Equal(jobName, registration.JobName);
		Assert.Equal(requestType, registration.RequestType);
		Assert.Equal(responseType, registration.ResponseType);
	}

	[Fact]
	public void Properties_CanBeModified()
	{
		var initialJobName = "TestJob";
		var requestType = typeof(string);
		var responseType = typeof(int);
		var registration = new HandlerRegistration(initialJobName, requestType, responseType);

		var newJobName = "UpdatedJob";
		registration.JobName = newJobName;

		Assert.Equal(newJobName, registration.JobName);
		Assert.Equal(requestType, registration.RequestType);
		Assert.Equal(responseType, registration.ResponseType);
	}
}
