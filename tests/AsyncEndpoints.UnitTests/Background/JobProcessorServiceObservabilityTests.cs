using AsyncEndpoints.Background;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Observability;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class JobProcessorServiceObservabilityTests
{
    /// <summary>
    /// Verifies that when a job processing completes successfully, 
    /// the observability interface records the job processing metric.
    /// This ensures proper tracking of successful job completions.
    /// </summary>
    [Theory, AutoMoqData]
    public async Task Process_JobSuccess_RecordsJobProcessedMetric(
        Job job,
        Mock<ILogger<JobProcessorService>> mockLogger,
        Mock<IJobManager> mockJobManager,
        Mock<IHandlerExecutionService> mockHandlerExecutionService,
        Mock<ISerializer> mockSerializer,
        Mock<IAsyncEndpointsObservability> mockMetrics)
    {
        // Arrange
        var result = MethodResult<string>.Success("test result");
        mockHandlerExecutionService.Setup(h => h.ExecuteHandlerAsync(
                job.Name, 
                It.IsAny<object>(), 
                job, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<object>.Success(result.Data));
        mockJobManager.Setup(j => j.ProcessJobSuccess(job.Id, result.Data, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult.Success);

        var processor = new JobProcessorService(
            mockLogger.Object,
            mockJobManager.Object,
            mockHandlerExecutionService.Object,
            mockSerializer.Object,
            mockMetrics.Object);

        // Act
        await processor.ProcessAsync(job, CancellationToken.None);

        // Assert
        mockJobManager.Verify(j => j.ProcessJobSuccess(job.Id, result.Data, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    /// <summary>
    /// Verifies that when a job processing fails, 
    /// the observability interface records the handler error metric.
    /// This ensures proper tracking of handler execution failures.
    /// </summary>
    [Theory, AutoMoqData]
    public async Task Process_JobFailure_RecordsHandlerErrorMetric(
        Job job,
        Mock<ILogger<JobProcessorService>> mockLogger,
        Mock<IJobManager> mockJobManager,
        Mock<IHandlerExecutionService> mockHandlerExecutionService,
        Mock<ISerializer> mockSerializer,
        Mock<IAsyncEndpointsObservability> mockMetrics,
        AsyncEndpointError error)
    {
        // Arrange
        mockHandlerExecutionService.Setup(h => h.ExecuteHandlerAsync(
                job.Name, 
                It.IsAny<object>(), 
                job, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<object>.Failure(error));
        mockJobManager.Setup(j => j.ProcessJobFailure(job.Id, error, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult.Success);

        var processor = new JobProcessorService(
            mockLogger.Object,
            mockJobManager.Object,
            mockHandlerExecutionService.Object,
            mockSerializer.Object,
            mockMetrics.Object);

        // Act
        await processor.ProcessAsync(job, CancellationToken.None);

        // Assert
        mockJobManager.Verify(j => j.ProcessJobFailure(job.Id, error, It.IsAny<CancellationToken>()), Times.Once);
    }
}