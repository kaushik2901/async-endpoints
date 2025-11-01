using System;
using System.Diagnostics;
using AsyncEndpoints.JobProcessing;

namespace AsyncEndpoints.Infrastructure.Observability;

/// <summary>
/// Provides metric and tracing capabilities for AsyncEndpoints library
/// </summary>
public interface IAsyncEndpointsObservability
{
	/// <summary>
	/// Records that a job was created
	/// </summary>
	/// <param name="jobName">Name of the job</param>
	/// <param name="storeType">Type of the job store</param>
	void RecordJobCreated(string jobName, string storeType);

	/// <summary>
	/// Records that a job was processed with the specified status
	/// </summary>
	/// <param name="jobName">Name of the job</param>
	/// <param name="status">Status of the job (e.g., claimed, completed)</param>
	/// <param name="storeType">Type of the job store</param>
	void RecordJobProcessed(string jobName, string status, string storeType);

	/// <summary>
	/// Records that a job failed with the specified error type
	/// </summary>
	/// <param name="jobName">Name of the job</param>
	/// <param name="errorType">Type of error that occurred</param>
	/// <param name="storeType">Type of the job store</param>
	void RecordJobFailed(string jobName, string errorType, string storeType);

	/// <summary>
	/// Records that a job was retried
	/// </summary>
	/// <param name="jobName">Name of the job</param>
	/// <param name="storeType">Type of the job store</param>
	void RecordJobRetries(string jobName, string storeType);

	/// <summary>
	/// Records the duration a job spent in the queue
	/// </summary>
	/// <param name="jobName">Name of the job</param>
	/// <param name="storeType">Type of the job store</param>
	/// <param name="durationSeconds">Duration in seconds</param>
	void RecordJobQueueDuration(string jobName, string storeType, double durationSeconds);

	/// <summary>
	/// Records the duration of job processing
	/// </summary>
	/// <param name="jobName">Name of the job</param>
	/// <param name="status">Status of the job</param>
	/// <param name="durationSeconds">Duration in seconds</param>
	void RecordJobProcessingDuration(string jobName, string status, double durationSeconds);

	/// <summary>
	/// Sets the current count of jobs in a specific status
	/// </summary>
	/// <param name="jobStatus">Status of the jobs (e.g., queued, in-progress)</param>
	/// <param name="storeType">Type of the job store</param>
	/// <param name="count">Current count of jobs</param>
	void SetJobCurrentCount(string jobStatus, string storeType, long count);

	// Handler metrics  
	/// <summary>
	/// Records the duration of handler execution
	/// </summary>
	/// <param name="jobName">Name of the job</param>
	/// <param name="handlerType">Type of the handler</param>
	/// <param name="durationSeconds">Duration in seconds</param>
	void RecordHandlerExecutionDuration(string jobName, string handlerType, double durationSeconds);

	/// <summary>
	/// Records that a handler encountered an error
	/// </summary>
	/// <param name="jobName">Name of the job</param>
	/// <param name="errorType">Type of error that occurred</param>
	void RecordHandlerError(string jobName, string errorType);

	// Store metrics
	/// <summary>
	/// Records a store operation
	/// </summary>
	/// <param name="operation">Name of the operation (e.g., CreateJob, GetJobById)</param>
	/// <param name="storeType">Type of the job store</param>
	void RecordStoreOperation(string operation, string storeType);

	/// <summary>
	/// Records the duration of a store operation
	/// </summary>
	/// <param name="operation">Name of the operation</param>
	/// <param name="storeType">Type of the job store</param>
	/// <param name="durationSeconds">Duration in seconds</param>
	void RecordStoreOperationDuration(string operation, string storeType, double durationSeconds);

	/// <summary>
	/// Records that a store operation encountered an error
	/// </summary>
	/// <param name="operation">Name of the operation</param>
	/// <param name="errorType">Type of error that occurred</param>
	/// <param name="storeType">Type of the job store</param>
	void RecordStoreError(string operation, string errorType, string storeType);

	// Background service metrics
	/// <summary>
	/// Records the processing rate of background services
	/// </summary>
	/// <param name="workerId">ID of the worker</param>
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
