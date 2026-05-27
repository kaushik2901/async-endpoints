namespace AsyncEndpoints.Abstractions.Jobs;

public enum JobStatus { Queued = 100, Processing = 300, Completed = 500, Failed = 600, DeadLettered = 700 }
