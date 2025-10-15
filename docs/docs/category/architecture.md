---
sidebar_position: 4
---

# Architecture

## Overview

AsyncEndpoints follows a clean architectural pattern that separates concerns while providing a streamlined experience for handling asynchronous operations. The framework handles background job processing with state management, retry mechanisms, and status tracking.

## Core Components

### Job Store
The Job Store is responsible for persisting job state and coordinating between multiple instances:

- **In-Memory Store**: For development and single-instance deployments
- **Redis Store**: For production with distributed processing capabilities
- **Interface-based design**: Easy to extend with custom implementations

### Job Manager
The Job Manager coordinates all job state transitions and retry logic:

- Creates and manages job lifecycles
- Handles job state transitions (Queued → InProgress → Completed/Failed)
- Implements retry logic with exponential backoff
- Manages job claiming by workers

### Background Workers
Background services handle the processing of queued jobs:

- **Job Consumer**: Retrieves jobs from the store
- **Job Producer**: Adds jobs to the processing queue
- **Job Processor**: Executes the registered handlers
- **Distributed Recovery**: Automatic recovery of stuck jobs in multi-instance deployments

### Request Handlers
Your business logic implementation:

- Implement `IAsyncEndpointRequestHandler<TRequest, TResponse>` for typed requests
- Implement `IAsyncEndpointRequestHandler<TResponse>` for requests without body
- Access full HTTP context including headers, route parameters, and query parameters

## Job Lifecycle

Jobs progress through the following states:

1. **Queued**: Job is created and waiting for processing
2. **Scheduled**: Job is scheduled for delayed execution (during retries)
3. **InProgress**: Job is actively being processed by a worker
4. **Completed**: Job has successfully completed
5. **Failed**: Job has failed after all retry attempts
6. **Canceled**: Job was explicitly canceled

```
[Queued] → [Scheduled] → [InProgress] → [Completed]
              ↑                      ↓
              └──── [Failed] ←───────┘
```

## Request Flow

1. Client sends request to async endpoint
2. Framework immediately responds with 202 (Accepted) and job details
3. Job is stored in the job store with status "Queued"
4. Background worker claims the job for processing
5. Worker executes the registered handler
6. Job status is updated throughout the process
7. Client can check job status via status endpoint

## HTTP Context Preservation

AsyncEndpoints preserves important HTTP context information throughout the job lifecycle:

- **Headers**: All request headers are captured and available in handlers
- **Route Parameters**: Route parameters are preserved for complex routing scenarios
- **Query Parameters**: Query string parameters are maintained for processing
- **Job ID**: Unique job identifier for tracking and correlation

## Configuration Layers

The framework is organized into three main configuration areas:

- **Worker Configuration**: Concurrency, polling intervals, queue sizes
- **Job Manager Configuration**: Retry logic, timeouts, batch processing
- **Response Configuration**: Custom response factories and serialization

## Storage Options

### In-Memory Store
- **Use Case**: Development and single-instance deployments
- **Pros**: Simple setup, no external dependencies
- **Cons**: Data loss on application restart, no distributed support

### Redis Store
- **Use Case**: Production with multiple instances
- **Pros**: Persistence, distributed processing, job recovery
- **Cons**: Requires Redis infrastructure

## Distributed Processing

In multi-instance deployments:

- Multiple workers can safely process jobs concurrently
- Job claiming mechanism prevents duplicate processing
- Distributed recovery service monitors for stuck jobs
- Redis provides coordination between instances