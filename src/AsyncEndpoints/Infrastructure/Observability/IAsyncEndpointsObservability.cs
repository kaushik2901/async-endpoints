using System;
using System.Diagnostics;
using AsyncEndpoints.JobProcessing;

namespace AsyncEndpoints.Infrastructure.Observability;

/// <summary>
/// Provides metric and tracing capabilities for AsyncEndpoints library
/// </summary>
public interface IAsyncEndpointsObservability
{
    // Job metrics
    void RecordJobCreated(string jobName, string storeType);
    void RecordJobProcessed(string jobName, string status, string storeType);
    void RecordJobFailed(string jobName, string errorType, string storeType);
    void RecordJobRetries(string jobName, string storeType);
    void RecordJobQueueDuration(string jobName, string storeType, double durationSeconds);
    void RecordJobProcessingDuration(string jobName, string status, double durationSeconds);
    void SetJobCurrentCount(string jobStatus, string storeType, long count);
    
    // Handler metrics  
    void RecordHandlerExecutionDuration(string jobName, string handlerType, double durationSeconds);
    void RecordHandlerError(string jobName, string errorType);
    
    // Store metrics
    void RecordStoreOperation(string operation, string storeType);
    void RecordStoreOperationDuration(string operation, string storeType, double durationSeconds);
    void RecordStoreError(string operation, string errorType, string storeType);
    
    // Background service metrics
    void RecordBackgroundProcessingRate(string workerId);
    
    // Duration tracking methods
    /// <summary>
    /// Records duration of job processing using a disposable timer
    /// </summary>
    /// <param name="jobName">Name of the job</param>
    /// <param name="status">Status of the job</param>
    /// <returns>IDisposable timer that records duration when disposed</returns>
    IDisposable TimeJobProcessingDuration(string jobName, string status);
    
    /// <summary>
    /// Records duration of a handler execution
    /// </summary>
    /// <param name="jobName">Name of the job</param>
    /// <param name="handlerType">Type of the handler</param>
    /// <returns>IDisposable timer that records duration when disposed</returns>
    IDisposable TimeHandlerExecution(string jobName, string handlerType);

    // Activity/tracing methods
    /// <summary>
    /// Starts a job submission activity if tracing is enabled
    /// </summary>
    /// <param name="jobName">Name of the job</param>
    /// <param name="storeType">The type of store</param>
    /// <param name="jobId">ID of the job</param>
    /// <returns>An Activity if tracing is enabled, otherwise null</returns>
    Activity? StartJobSubmitActivity(string jobName, string storeType, Guid jobId);
    
    /// <summary>
    /// Starts a job processing activity if tracing is enabled
    /// </summary>
    /// <param name="storeType">The type of store</param>
    /// <param name="job">The job being processed</param>
    /// <returns>An Activity if tracing is enabled, otherwise null</returns>
    Activity? StartJobProcessActivity(string storeType, Job job);
    
    /// <summary>
    /// Starts a handler execution activity if tracing is enabled
    /// </summary>
    /// <param name="jobName">Name of the job</param>
    /// <param name="jobId">ID of the job</param>
    /// <param name="handlerType">Type of the handler being executed</param>
    /// <returns>An Activity if tracing is enabled, otherwise null</returns>
    Activity? StartHandlerExecuteActivity(string jobName, Guid jobId, string handlerType);
    
    /// <summary>
    /// Starts a store operation activity if tracing is enabled
    /// </summary>
    /// <param name="operation">The store operation being performed</param>
    /// <param name="storeType">The type of store</param>
    /// <param name="jobId">Optional job ID associated with the operation</param>
    /// <returns>An Activity if tracing is enabled, otherwise null</returns>
    Activity? StartStoreOperationActivity(string operation, string storeType, Guid? jobId = null);
}