namespace AsyncEndpoints.Entities;

public enum JobStatus
{
    Queued = 100,
    InProgress = 200,
    Completed = 300,
    Failed = 400,
    Canceled = 500,
}
