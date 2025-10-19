## Architecture & Extensibility

Q: Pluggable storage providers: Redis/EF are mentioned, but make storage abstractions clean so others (e.g., MongoDB, DynamoDB, SQL Server) can be plugged in.
A: I want to support only EF core and Redis, EF core supports all of these providers 

Q: Worker scaling strategy: Define how workers are distributed, scaled (horizontal scaling, multi-instance scenarios), and coordinated (e.g., distributed locks).
A: Yes, but later, for MVP we can consider single instance

Q: Background service lifecycle: Ensure graceful shutdown and job cancellation when the host stops.
A: Yes, we need this in MVP

## API Design

Q: Standardized metadata: Define a consistent response schema for 202 Accepted, including jobId, status URL, and optional expiration time.
A: Yes, This is the highest priority item.

Q: Status endpoint: Consider supporting pagination or filtering for jobs (e.g., ?status=completed).
A: Status endpoint will respond per request id only, no need of pagination, filtering etc., We are planning to build a dashboard, but not a priority in MVP. 

Q: Idempotency enforcement: Instead of just warning when no request ID is provided, enforce stricter guarantees to prevent duplicate work.
A: Nope, Idempotency will be enforced by header only, we can allow it by query string as well, but not other than that.

Q: Result expiration/cleanup: Define how long completed/failed job results are stored before being purged.
A: Not a priority.

## Reliability & Fault Tolerance

Q: Retry policies: Allow configurable retry count, exponential backoff, and dead-lettering for permanently failed jobs.
A: Yes, this is also need to design.

Q: Poison message handling: Detect and isolate jobs that consistently fail.
A: Very low or no priority.

Q: Partial failures: If a job involves multiple operations, decide whether partial results should be persisted.
A: Nope, job will have single step only, we are building async endpoints, not a job engine.

## Observability

Q: Job state tracking: Clearly define possible job states (queued, in-progress, completed, failed, canceled).
A: Yes, We need schema for Job

Q: Structured logging: Include jobId and requestId in all logs for traceability.
A: Yes, we need to take care of this throughout the development.

Q: Metrics: Expose metrics (queue length, processing time, retries, failures) for monitoring.
A: Yes, same priority as structured logging.

## Security & Compliance

Q: Authentication/Authorization: Ensure only authorized clients can enqueue/check jobs.
A: Nope, we are async endpoints library, We are just extending the original minimal APIs, so developers have flexibility to implement authentication of their choice.

Q: Data protection: Decide whether job payloads are encrypted in storage/transport.
A: Nope, will not be encrypted. These are running in backend, This is headache for backend security team.

Q: Request validation: Enforce schema validation before enqueueing.
A: We are giving allowing to execute middleware before queueing the job, so developer can implement request validator of their choice.

## Developer Experience

Q: Handler registration: Simplify handler discovery (AOT-safe reflection alternatives, source generators).
A: Current one is simplest one.

Q: Middleware support: Allow pre-processing before enqueueing (validation, transformation, auth).
A: We already considered in the initial design.

Q: Testing utilities: Provide mocks or test harness for async endpoints.
A: Low priority.

## Advanced Considerations

Q: Cancellation support: Allow clients to cancel jobs in progress.
A: Yes, medium priority.

Q: Streaming results: For long-running tasks, consider progressive updates rather than only final status.
A: Nope, no priority.

Q: Concurrency controls: Prevent the same resource from being processed concurrently if needed.
A: Yes, but later when we think about supporting scaling workers.

Q: Multi-tenancy: Namespace jobs by tenant to avoid collisions.
A: Nope, no priority.