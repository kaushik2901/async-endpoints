using AsyncEndpoints.Handlers;

namespace AsyncEndpoints.UnitTests.Handlers;

public class HandlerRegistrationTests
{
	[Fact]
	public void Constructor_SetsPropertiesCorrectly()
	{
		// Arrange
		var jobName = "TestJob";
		var requestType = typeof(string);
		var responseType = typeof(int);

		// Act
		var registration = new HandlerRegistration(jobName, requestType, responseType);

		// Assert
		Assert.Equal(jobName, registration.JobName);
		Assert.Equal(requestType, registration.RequestType);
		Assert.Equal(responseType, registration.ResponseType);
	}

	[Fact]
	public void Properties_CanBeModified()
	{
		// Arrange
		var initialJobName = "TestJob";
		var requestType = typeof(string);
		var responseType = typeof(int);
		var registration = new HandlerRegistration(initialJobName, requestType, responseType);

		// Act
		var newJobName = "UpdatedJob";
		registration.JobName = newJobName;

		// Assert
		Assert.Equal(newJobName, registration.JobName);
		Assert.Equal(requestType, registration.RequestType);
		Assert.Equal(responseType, registration.ResponseType);
	}
}
