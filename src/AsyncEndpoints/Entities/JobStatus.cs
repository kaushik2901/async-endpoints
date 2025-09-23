namespace AsyncEndpoints.Entities;

public enum JobStatus
{
    Queued = 100,
    Scheduled = 200,
    InProgress = 300,
    Completed = 400,
    Failed = 500,
    Canceled = 600,
}
