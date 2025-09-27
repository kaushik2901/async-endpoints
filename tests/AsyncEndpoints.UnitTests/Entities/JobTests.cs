using AsyncEndpoints.Entities;

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

        // Act
        var job = Job.Create(id, name, payload);

        // Assert
        Assert.Equal(id, job.Id);
        Assert.Equal(name, job.Name);
        Assert.Equal(payload, job.Payload);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal(AsyncEndpointsConstants.MaximumRetries, job.MaxRetries);
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

        // Act
        var job = Job.Create(id, name, payload, headers, routeParams, queryParams);

        // Assert
        Assert.Equal(id, job.Id);
        Assert.Equal(name, job.Name);
        Assert.Equal(payload, job.Payload);
        Assert.Equal(headers, job.Headers);
        Assert.Equal(routeParams, job.RouteParams);
        Assert.Equal(queryParams, job.QueryParams);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal(AsyncEndpointsConstants.MaximumRetries, job.MaxRetries);
    }

    [Fact]
    public void UpdateStatus_UpdatesStatusAndLastUpdatedAt()
    {
        // Arrange
        var job = new Job();
        var initialUpdatedAt = job.LastUpdatedAt;

        // Act
        job.UpdateStatus(JobStatus.InProgress);

        // Assert
        Assert.Equal(JobStatus.InProgress, job.Status);
        Assert.True(job.LastUpdatedAt > initialUpdatedAt);
        Assert.NotNull(job.StartedAt);
    }

    [Fact]
    public void UpdateStatus_WithCompletedStatus_SetsCompletedAt()
    {
        // Arrange
        var job = new Job();

        // Act
        job.UpdateStatus(JobStatus.Completed);

        // Assert
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void UpdateStatus_WithFailedStatus_SetsCompletedAt()
    {
        // Arrange
        var job = new Job();

        // Act
        job.UpdateStatus(JobStatus.Failed);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void UpdateStatus_WithCanceledStatus_SetsCompletedAt()
    {
        // Arrange
        var job = new Job();

        // Act
        job.UpdateStatus(JobStatus.Canceled);

        // Assert
        Assert.Equal(JobStatus.Canceled, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void SetResult_UpdatesStatusAndResult()
    {
        // Arrange
        var job = new Job();
        var result = "Success";

        // Act
        job.SetResult(result);

        // Assert
        Assert.Equal(result, job.Result);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void SetException_UpdatesStatusAndException()
    {
        // Arrange
        var job = new Job();
        var exception = "Error occurred";

        // Act
        job.SetException(exception);

        // Assert
        Assert.Equal(exception, job.Exception);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void IncrementRetryCount_IncrementsRetryCount()
    {
        // Arrange
        var job = new Job();
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
        var job = new Job();
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
        var job = new Job();

        // Act
        job.UpdateStatus(JobStatus.Canceled);

        // Assert
        Assert.True(job.IsCanceled);
    }

    [Fact]
    public void IsCanceled_ReturnsFalseWhenStatusIsNotCanceled()
    {
        // Arrange
        var job = new Job();

        // Act
        job.UpdateStatus(JobStatus.InProgress);

        // Assert
        Assert.False(job.IsCanceled);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Act
        var job = new Job();

        // Assert
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(string.Empty, job.Name);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(string.Empty, job.Payload);
        Assert.Null(job.Result);
        Assert.Null(job.Exception);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal(AsyncEndpointsConstants.MaximumRetries, job.MaxRetries);
        Assert.Null(job.RetryDelayUntil);
        Assert.Null(job.WorkerId);
        Assert.NotNull(job.CreatedAt);
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.NotNull(job.LastUpdatedAt);
        Assert.NotNull(job.Headers);
        Assert.NotNull(job.RouteParams);
        Assert.NotNull(job.QueryParams);
    }
}