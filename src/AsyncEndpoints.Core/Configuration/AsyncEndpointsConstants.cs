namespace AsyncEndpoints.Configuration;

public static class AsyncEndpointsConstants
{
	public const string AsyncEndpointTag = "AsyncEndpoint";
	public const string JobIdHeaderName = "X-Async-Request-Id";
	public const int MaximumRetries = 3;

	// Configuration Default Values
	public const int DefaultPollingIntervalMs = 1000;
	public const int DefaultJobTimeoutMinutes = 30;
	public const int DefaultBatchSize = 5;
	public const int DefaultMaximumQueueSize = 50;

	// Background Service Constants
	public const int BackgroundServiceShutdownTimeoutSeconds = 30;
	public const int BackgroundServiceWaitDelayMs = 100;

	// Job Producer Service Constants
	public const int JobProducerMaxDelayMs = 30000;
	public const int JobProducerErrorDelaySeconds = 5;
	public const int JobProducerChannelWriteTimeoutSeconds = 5;

	// Exception Serializer Constants
	public const int ExceptionSeparatorLength = 50;

	// Job Result Serialization Constants
	public const string JobResultPlaceholder = "{{JOB_RESULT_PLACEHOLDER}}";
}
