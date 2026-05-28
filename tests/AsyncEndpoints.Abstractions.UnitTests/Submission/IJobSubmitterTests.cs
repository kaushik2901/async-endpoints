using AsyncEndpoints.Abstractions.Submission;

namespace AsyncEndpoints.Abstractions.UnitTests.Submission;

public class IJobSubmitterTests
{
	[Fact]
	public void Interface_IsPublic()
	{
		Assert.True(typeof(IJobSubmitter).IsPublic);
	}

	[Fact]
	public void SubmitAsync_ReturnsGuid()
	{
		var methods = typeof(IJobSubmitter).GetMethods().Where(m => m.Name == "SubmitAsync").ToList();
		Assert.NotEmpty(methods);
		foreach (var method in methods)
		{
			var returnType = method.ReturnType;
			Assert.True(returnType == typeof(Task<Guid>) || returnType.GetGenericTypeDefinition() == typeof(Task<>));
		}
	}

	[Fact]
	public void SubmitAsync_HasJobNameOverload()
	{
		var method = typeof(IJobSubmitter).GetMethods()
			.FirstOrDefault(m => m.Name == "SubmitAsync" && m.GetParameters().Length >= 2 && m.GetParameters()[0].Name == "jobName");
		Assert.NotNull(method);
	}

	[Fact]
	public void SubmitRawAsync_ReturnsGuid()
	{
		var method = typeof(IJobSubmitter).GetMethods()
			.FirstOrDefault(m => m.Name == "SubmitRawAsync");
		Assert.NotNull(method);
		var returnType = method!.ReturnType;
		Assert.True(returnType == typeof(Task<Guid>) || returnType.GetGenericTypeDefinition() == typeof(Task<>));
	}
}
