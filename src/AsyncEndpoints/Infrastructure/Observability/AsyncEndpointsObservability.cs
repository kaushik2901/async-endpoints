using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Infrastructure.Observability;

/// <inheritdoc />
public class AsyncEndpointsObservability : IAsyncEndpointsObservability
{
    private readonly Counter<long>? _jobsCreated;
    private readonly Counter<long>? _jobsProcessed;
    private readonly Counter<long>? _jobsFailed;
    private readonly Counter<long>? _jobsRetries;
    private readonly Histogram<double>? _jobQueueDuration;
    private readonly Histogram<double>? _jobProcessingDuration;
    private readonly UpDownCounter<long>? _jobsCurrentCount;
    private readonly Histogram<double>? _handlerExecutionDuration;
    private readonly Counter<long>? _handlerErrors;
    private readonly Counter<long>? _storeOperations;
    private readonly Histogram<double>? _storeOperationDuration;
    private readonly Counter<long>? _storeErrors;
    private readonly Counter<long>? _backgroundProcessingRate;
    private readonly AsyncEndpointsObservabilityConfigurations _config;
    
    private static readonly ActivitySource _activitySource = new("AsyncEndpoints", "1.0.0");

    private static readonly string _jobNameTag = "job_name";
    private static readonly string _storeTypeTag = "store_type";
    private static readonly string _statusTag = "status";
    private static readonly string _errorTypeTag = "error_type";
    private static readonly string _operationTag = "operation";
    private static readonly string _workerIdTag = "worker_id";
    private static readonly string _handlerTypeTag = "handler_type";
    private static readonly string _jobIdTag = "job.id";
    private static readonly string _unitSeconds = "seconds";
    private static readonly string _activityJobName = "job.name";
    private static readonly string _activityStoreType = "store.type";
    private static readonly string _activityWorkerId = "worker.id";
    private static readonly string _activityHandlerType = "handler.type";

    public AsyncEndpointsObservability(IOptions<AsyncEndpointsConfigurations> configurations)
    {
        _config = configurations.Value.ObservabilityConfigurations;
        
        // Only create metrics if observability is enabled
        if (_config.EnableMetrics)
        {
            var meter = new Meter("AsyncEndpoints", "1.0.0");
            
            // Job metrics
            _jobsCreated = meter.CreateCounter<long>("asyncendpoints.jobs.created.total", 
                description: "Total number of jobs created");
            _jobsProcessed = meter.CreateCounter<long>("asyncendpoints.jobs.processed.total", 
                description: "Total number of jobs processed");
            _jobsFailed = meter.CreateCounter<long>("asyncendpoints.jobs.failed.total", 
                description: "Total number of job failures");
            _jobsRetries = meter.CreateCounter<long>("asyncendpoints.jobs.retries.total", 
                description: "Total number of job retries");
            _jobQueueDuration = meter.CreateHistogram<double>("asyncendpoints.jobs.queue.duration", 
                unit: _unitSeconds, description: "Time jobs spend in queue before processing");
            _jobProcessingDuration = meter.CreateHistogram<double>("asyncendpoints.jobs.processing.duration", 
                unit: _unitSeconds, description: "Time spent processing jobs");
            _jobsCurrentCount = meter.CreateUpDownCounter<long>("asyncendpoints.jobs.current.count", 
                description: "Current number of jobs in each state");
            
            // Handler metrics
            _handlerExecutionDuration = meter.CreateHistogram<double>("asyncendpoints.handlers.execution.duration", 
                unit: _unitSeconds, description: "Time spent executing handlers");
            _handlerErrors = meter.CreateCounter<long>("asyncendpoints.handlers.error.rate", 
                description: "Count of handler execution errors");
            
            // Store metrics
            _storeOperations = meter.CreateCounter<long>("asyncendpoints.store.operations.total", 
                description: "Total store operations by operation type");
            _storeOperationDuration = meter.CreateHistogram<double>("asyncendpoints.store.operation.duration", 
                unit: _unitSeconds, description: "Duration of store operations");
            _storeErrors = meter.CreateCounter<long>("asyncendpoints.store.errors.total", 
                description: "Count of store operation errors");
            
            // Background service metrics
            _backgroundProcessingRate = meter.CreateCounter<long>("asyncendpoints.background.processing.rate", 
                description: "Rate of job processing");
        }
        else
        {
            // Initialize as null when metrics are disabled
            _jobsCreated = null;
            _jobsProcessed = null;
            _jobsFailed = null;
            _jobsRetries = null;
            _jobQueueDuration = null;
            _jobProcessingDuration = null;
            _jobsCurrentCount = null;
            _handlerExecutionDuration = null;
            _handlerErrors = null;
            _storeOperations = null;
            _storeOperationDuration = null;
            _storeErrors = null;
            _backgroundProcessingRate = null;
        }
    }

    public void RecordJobCreated(string jobName, string storeType)
    {
        if (_config.EnableMetrics && _jobsCreated != null)
        {
            _jobsCreated.Add(1, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordJobProcessed(string jobName, string status, string storeType)
    {
        if (_config.EnableMetrics && _jobsProcessed != null)
        {
            _jobsProcessed.Add(1, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_statusTag, status), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordJobFailed(string jobName, string errorType, string storeType)
    {
        if (_config.EnableMetrics && _jobsFailed != null)
        {
            _jobsFailed.Add(1, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_errorTypeTag, errorType), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordJobRetries(string jobName, string storeType)
    {
        if (_config.EnableMetrics && _jobsRetries != null)
        {
            _jobsRetries.Add(1, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordJobQueueDuration(string jobName, string storeType, double durationSeconds)
    {
        if (_config.EnableMetrics && _jobQueueDuration != null)
        {
            _jobQueueDuration.Record(durationSeconds, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordJobProcessingDuration(string jobName, string status, double durationSeconds)
    {
        if (_config.EnableMetrics && _jobProcessingDuration != null)
        {
            _jobProcessingDuration.Record(durationSeconds, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_statusTag, status));
        }
    }


    public void SetJobCurrentCount(string jobStatus, string storeType, long count)
    {
        if (_config.EnableMetrics && _jobsCurrentCount != null)
        {
            // For UpDownCounter, we'll record the count as a change from the previous state.
            // For simplicity in the context of this API, we'll add the count directly.
            _jobsCurrentCount.Add(count, new KeyValuePair<string, object?>(_statusTag, jobStatus), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordHandlerExecutionDuration(string jobName, string handlerType, double durationSeconds)
    {
        if (_config.EnableMetrics && _handlerExecutionDuration != null)
        {
            _handlerExecutionDuration.Record(durationSeconds, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_handlerTypeTag, handlerType));
        }
    }

    public void RecordHandlerError(string jobName, string errorType)
    {
        if (_config.EnableMetrics && _handlerErrors != null)
        {
            _handlerErrors.Add(1, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_errorTypeTag, errorType));
        }
    }

    public void RecordStoreOperation(string operation, string storeType)
    {
        if (_config.EnableMetrics && _storeOperations != null)
        {
            _storeOperations.Add(1, new KeyValuePair<string, object?>(_operationTag, operation), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordStoreOperationDuration(string operation, string storeType, double durationSeconds)
    {
        if (_config.EnableMetrics && _storeOperationDuration != null)
        {
            _storeOperationDuration.Record(durationSeconds, new KeyValuePair<string, object?>(_operationTag, operation), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordStoreError(string operation, string errorType, string storeType)
    {
        if (_config.EnableMetrics && _storeErrors != null)
        {
            _storeErrors.Add(1, new KeyValuePair<string, object?>(_operationTag, operation), new KeyValuePair<string, object?>(_errorTypeTag, errorType), new KeyValuePair<string, object?>(_storeTypeTag, storeType));
        }
    }

    public void RecordBackgroundProcessingRate(string workerId)
    {
        if (_config.EnableMetrics && _backgroundProcessingRate != null)
        {
            _backgroundProcessingRate.Add(1, new KeyValuePair<string, object?>(_workerIdTag, workerId));
        }
    }


    public IDisposable TimeJobProcessingDuration(string jobName, string status)
    {
        if (_config.EnableMetrics && _jobProcessingDuration != null)
        {
            return MetricTimer.Start(duration => _jobProcessingDuration.Record(duration, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_statusTag, status)));
        }
        
        return NullDisposable.Instance; // Return a no-op disposable when metrics are disabled
    }
    
    public IDisposable TimeHandlerExecution(string jobName, string handlerType)
    {
        if (_config.EnableMetrics && _handlerExecutionDuration != null)
        {
            return MetricTimer.Start(duration => _handlerExecutionDuration.Record(duration, new KeyValuePair<string, object?>(_jobNameTag, jobName), new KeyValuePair<string, object?>(_handlerTypeTag, handlerType)));
        }
        
        return NullDisposable.Instance; // Return a no-op disposable when metrics are disabled
    }

    public Activity? StartJobSubmitActivity(string jobName, string storeType, Guid jobId)
    {
        if (_config.EnableTracing)
        {
            var activity = _activitySource.StartActivity("Job.Submit", ActivityKind.Server);
            activity?.SetTag(_jobIdTag, jobId.ToString());
            activity?.SetTag(_activityJobName, jobName);
            activity?.SetTag(_activityStoreType, storeType);
            return activity;
        }
        return null; // Return null when tracing is disabled
    }

    public Activity? StartJobProcessActivity(string storeType, Job job)
    {
        if (_config.EnableTracing)
        {
            var activity = _activitySource.StartActivity("Job.Process", ActivityKind.Consumer);
            activity?.SetTag(_jobIdTag, job.Id.ToString());
            activity?.SetTag(_activityJobName, job.Name);
            activity?.SetTag("job.status", job.Status.ToString());
            activity?.SetTag(_activityWorkerId, job.WorkerId?.ToString());
            activity?.SetTag(_activityStoreType, storeType);
            return activity;
        }
        return null; // Return null when tracing is disabled
    }

    public Activity? StartHandlerExecuteActivity(string jobName, Guid jobId, string handlerType)
    {
        if (_config.EnableTracing)
        {
            var activity = _activitySource.StartActivity("Handler.Execute", ActivityKind.Internal);
            activity?.SetTag(_jobIdTag, jobId.ToString());
            activity?.SetTag(_activityJobName, jobName);
            activity?.SetTag(_activityHandlerType, handlerType);
            return activity;
        }
        return null; // Return null when tracing is disabled
    }

    public Activity? StartStoreOperationActivity(string operation, string storeType, Guid? jobId = null)
    {
        if (_config.EnableTracing)
        {
            var activity = _activitySource.StartActivity("Store.Operation", ActivityKind.Internal);
            activity?.SetTag("operation", operation);
            activity?.SetTag(_activityStoreType, storeType);
            if (jobId.HasValue)
            {
                activity?.SetTag(_jobIdTag, jobId.Value.ToString());
            }
            return activity;
        }
        return null; // Return null when tracing is disabled
    }
}
