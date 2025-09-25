namespace AsyncEndpoints;

internal static class AsyncEndpointsConstants
{
    internal const string AsyncEndpointTag = "AsyncEndpoint";
    internal const string JobIdHeaderName = "Async-Job-Id";
    internal const int MaximumRetries = 3;

    // Configuration Default Values
    internal const int DefaultPollingIntervalMs = 1000;
    internal const int DefaultJobTimeoutMinutes = 30;
    internal const int DefaultBatchSize = 5;
    internal const int DefaultMaximumQueueSize = 50;

    // Background Service Constants
    internal const int BackgroundServiceShutdownTimeoutSeconds = 30;
    internal const int BackgroundServiceWaitDelayMs = 100;

    // Job Producer Service Constants
    internal const int JobProducerMaxDelayMs = 30000;
    internal const int JobProducerErrorDelaySeconds = 5;
    internal const int JobProducerChannelWriteTimeoutSeconds = 5;

    // In-Memory Store Constants
    internal const int RetryDelayBaseSeconds = 5;

    // Exception Serializer Constants
    internal const int ExceptionSeparatorLength = 50;
}
