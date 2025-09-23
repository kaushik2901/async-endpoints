namespace AsyncEndpoints.Entities;

public enum ErrorType
{
    Transient = 100,   // Network timeouts, temporary service unavailability
    Permanent = 200,   // Invalid arguments, business logic errors
    Retriable = 300    // Unknown errors that might succeed on retry
}