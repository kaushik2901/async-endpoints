using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using Moq;

namespace AsyncEndpoints.UnitTests.Entities;

public class JobTests
{
    [Fact]
    public void Create_WithIdNameAndPayload_SetsPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "TestJob";
        var payload = "{\"data\":\"value\"}";
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

        // Act
        var job = Job.Create(id, name, payload, mockDateTimeProvider.Object);

        // Assert
        Assert.Equal(id, job.Id);
        Assert.Equal(name, job.Name);
        Assert.Equal(payload, job.Payload);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal(AsyncEndpointsConstants.MaximumRetries, job.MaxRetries);
        Assert.Equal(expectedTime, job.CreatedAt);
        Assert.Equal(expectedTime, job.LastUpdatedAt);
        Assert.NotNull(job.Headers);
        Assert.NotNull(job.RouteParams);
        Assert.NotNull(job.QueryParams);
        Assert.Empty(job.Headers);
        Assert.Empty(job.RouteParams);
        Assert.Empty(job.QueryParams);
    }

    [Fact]
    public void Create_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "TestJob";
        var payload = "{\"data\":\"value\"}";
        var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
        var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
        var queryParams = new List<KeyValuePair<string, List<string?>>> { new KeyValuePair<string, List<string?>>("query1", new List<string?> { "value1" }) };
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

        // Act
        var job = Job.Create(id, name, payload, headers, routeParams, queryParams, mockDateTimeProvider.Object);

        // Assert
        Assert.Equal(id, job.Id);
        Assert.Equal(name, job.Name);
        Assert.Equal(payload, job.Payload);
        Assert.Equal(headers, job.Headers);
        Assert.Equal(routeParams, job.RouteParams);
        Assert.Equal(queryParams, job.QueryParams);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal(expectedTime, job.CreatedAt);
        Assert.Equal(expectedTime, job.LastUpdatedAt);
        Assert.Equal(AsyncEndpointsConstants.MaximumRetries, job.MaxRetries);
    }

    [Fact]
    public void UpdateStatus_UpdatesStatusAndLastUpdatedAt()
    {
        // Arrange
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var newTime = DateTimeOffset.UtcNow.AddSeconds(1);
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(newTime);
        var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);

        // Act
        job.UpdateStatus(JobStatus.InProgress, mockDateTimeProvider.Object);

        // Assert
        Assert.Equal(JobStatus.InProgress, job.Status);
        Assert.Equal(newTime, job.LastUpdatedAt);
        Assert.Equal(newTime, job.StartedAt);
    }

    [Fact]
    public void UpdateStatus_WithCompletedStatus_SetsCompletedAt()
    {
        // Arrange
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
        var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);

        // Act
        job.UpdateStatus(JobStatus.Completed, mockDateTimeProvider.Object);

        // Assert
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(expectedTime, job.CompletedAt);
    }

    [Fact]
    public void UpdateStatus_WithFailedStatus_SetsCompletedAt()
    {
        // Arrange
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
        var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);

        // Act
        job.UpdateStatus(JobStatus.Failed, mockDateTimeProvider.Object);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(expectedTime, job.CompletedAt);
    }

    [Fact]
    public void UpdateStatus_WithCanceledStatus_SetsCompletedAt()
    {
        // Arrange
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
        var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);

        // Act
        job.UpdateStatus(JobStatus.Canceled, mockDateTimeProvider.Object);

        // Assert
        Assert.Equal(JobStatus.Canceled, job.Status);
        Assert.Equal(expectedTime, job.CompletedAt);
    }

    [Fact]
    public void SetResult_UpdatesStatusAndResult()
    {
        // Arrange
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
        var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);
        var result = "Success";

        // Act
        job.SetResult(result, mockDateTimeProvider.Object);

        // Assert
        Assert.Equal(result, job.Result);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(expectedTime, job.CompletedAt);
    }

    [Fact]
    public void SetException_UpdatesStatusAndException()
    {
        // Arrange
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
        var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);
        var exception = "Error occurred";

        // Act
        job.SetException(exception, mockDateTimeProvider.Object);

        // Assert
        Assert.Equal(exception, job.Exception);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(expectedTime, job.CompletedAt);
    }

    [Fact]
    public void IncrementRetryCount_IncrementsRetryCount()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var job = new Job(currentTime);
        var initialRetryCount = job.RetryCount;

        // Act
        job.IncrementRetryCount();

        // Assert
        Assert.Equal(initialRetryCount + 1, job.RetryCount);
    }

    [Fact]
    public void SetRetryTime_SetsRetryDelayUntil()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var job = new Job(currentTime);
        var retryTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        job.SetRetryTime(retryTime);

        // Assert
        Assert.Equal(retryTime, job.RetryDelayUntil);
    }

    [Fact]
    public void IsCanceled_ReturnsTrueWhenStatusIsCanceled()
    {
        // Arrange
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
        var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);

        // Act
        job.UpdateStatus(JobStatus.Canceled, mockDateTimeProvider.Object);

        // Assert
        Assert.True(job.IsCanceled);
    }

    [Fact]
    public void IsCanceled_ReturnsFalseWhenStatusIsNotCanceled()
    {
        // Arrange
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var expectedTime = DateTimeOffset.UtcNow;
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
        var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);

        // Act
        job.UpdateStatus(JobStatus.InProgress, mockDateTimeProvider.Object);

        // Assert
        Assert.False(job.IsCanceled);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;

        // Act
        var job = new Job(currentTime);

        // Assert
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(string.Empty, job.Name);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(string.Empty, job.Payload);
        Assert.Null(job.Result);
        Assert.Null(job.Exception);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal(AsyncEndpointsConstants.MaximumRetries, job.MaxRetries);
        Assert.Equal(currentTime, job.CreatedAt);
        Assert.Equal(currentTime, job.LastUpdatedAt);
        Assert.Null(job.RetryDelayUntil);
        Assert.Null(job.WorkerId);
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.NotNull(job.Headers);
        Assert.NotNull(job.RouteParams);
        Assert.NotNull(job.QueryParams);
    }
}