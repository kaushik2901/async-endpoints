using System;
using System.Collections.Generic;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Xunit;

namespace AsyncEndpoints.UnitTests;

public class JobResponseMapperTests
{
    [Fact]
    public void ToResponse_ConvertsJobToJobResponseCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        var startedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var completedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var lastUpdatedAt = DateTimeOffset.UtcNow;
        
        var job = new Job
        {
            Id = jobId,
            Name = "TestJob",
            Status = JobStatus.InProgress,
            RetryCount = 2,
            MaxRetries = 5,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            LastUpdatedAt = lastUpdatedAt,
            Result = "Success",
            Exception = null
        };

        // Act
        var response = JobResponseMapper.ToResponse(job);

        // Assert
        Assert.Equal(jobId, response.Id);
        Assert.Equal("TestJob", response.Name);
        Assert.Equal("InProgress", response.Status);
        Assert.Equal(2, response.RetryCount);
        Assert.Equal(5, response.MaxRetries);
        Assert.Equal(createdAt, response.CreatedAt);
        Assert.Equal(startedAt, response.StartedAt);
        Assert.Equal(completedAt, response.CompletedAt);
        Assert.Equal(lastUpdatedAt, response.LastUpdatedAt);
        Assert.Equal("Success", response.Result);
        Assert.Null(response.Exception);
    }

    [Fact]
    public void ToResponse_WithFailedJob_ConvertsCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job
        {
            Id = jobId,
            Name = "FailedJob",
            Status = JobStatus.Failed,
            RetryCount = 3,
            MaxRetries = 3,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            StartedAt = DateTimeOffset.UtcNow.AddHours(-2),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-60),
            LastUpdatedAt = DateTimeOffset.UtcNow,
            Result = null,
            Exception = "Something went wrong"
        };

        // Act
        var response = JobResponseMapper.ToResponse(job);

        // Assert
        Assert.Equal(jobId, response.Id);
        Assert.Equal("FailedJob", response.Name);
        Assert.Equal("Failed", response.Status);
        Assert.Equal(3, response.RetryCount);
        Assert.Equal(3, response.MaxRetries);
        Assert.Equal("Something went wrong", response.Exception);
        Assert.Null(response.Result);
    }

    [Fact]
    public void ToResponse_WithNullValues_HandlesCorrectly()
    {
        // Arrange
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "TestJob",
            Status = JobStatus.Queued,
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = null,
            CompletedAt = null,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            Result = null,
            Exception = null
        };

        // Act
        var response = JobResponseMapper.ToResponse(job);

        // Assert
        Assert.Equal("Queued", response.Status);
        Assert.Equal(0, response.RetryCount);
        Assert.Equal(3, response.MaxRetries);
        Assert.Null(response.StartedAt);
        Assert.Null(response.CompletedAt);
        Assert.Null(response.Result);
        Assert.Null(response.Exception);
    }
}