---
title: "Architecture"
description: "Complete AsyncEndpoints architecture overview. Learn about system components, data flow, job lifecycle, scalability patterns, and distributed processing design."
keywords: ["async endpoints architecture", ".NET async processing design", "background job architecture", "distributed processing architecture", "job queue design", "async system components"]
sidebar_position: 1
---

# Architecture

This page provides a comprehensive overview of the AsyncEndpoints architecture, including system components, data flow, and scalability considerations.

## System Architecture Overview

AsyncEndpoints implements a producer-consumer pattern that separates request handling from processing. The architecture consists of multiple interconnected components that work together to provide asynchronous job processing capabilities.

### Core Components

#### Job Store
The Job Store is the persistence layer that maintains job state and enables distributed processing:

- **Interface**: `IJobStore` defines the storage contract
- **Implementations**: In-memory store for development, Redis store for production
- **Responsibilities**: 
  - Create, read, update, and delete job records
  - Manage job state transitions
  - Handle job queuing and claiming mechanisms
  - Support job recovery operations

#### Job Manager
The Job Manager coordinates job state management and retry logic:

- **Interface**: `IJobManager` provides job lifecycle management
- **Responsibilities**:
  - Create new jobs from HTTP requests
  - Process job success and failure operations
  - Manage retry logic with exponential backoff
  - Handle job state transitions
  - Coordinate job claiming by workers

#### Background Services
The background services implement the producer-consumer pattern:

- **AsyncEndpointsBackgroundService**: Main hosted service managing the job processing workflow
- **Job Producer**: Creates jobs from pending HTTP requests
- **Job Consumer**: Processes jobs from the queue
- **Job Processor**: Executes job handler logic
- **Job Claiming Service**: Manages job assignment to workers

#### HTTP Integration Layer
The HTTP integration layer provides endpoint mapping and request processing:

- **RouteBuilder Extensions**: Map asynchronous endpoints using minimal APIs
- **Request Delegate**: Processes HTTP requests and submits jobs
- **Context Builder**: Preserves HTTP context information through job lifecycle

## Component Interaction

The system follows this interaction pattern:

```
HTTP Request → RouteBuilder → Request Delegate → Job Manager → Job Store
                ↓
    Job Producer ←→ Job Consumer ←→ Job Processor
         ↓              ↓              ↓
    Submit Job    Claim Job     Execute Handler
```

1. **HTTP Request Processing**: When a client makes a request to an async endpoint, the RouteBuilder extension processes it
2. **Job Creation**: The request delegate serializes the request and submits it to the Job Manager
3. **Storage**: The Job Manager persists the job to the Job Store
4. **Job Production**: The Job Producer discovers pending jobs in the store
5. **Job Consumption**: The Job Consumer claims jobs for processing
6. **Job Execution**: The Job Processor executes the appropriate handler
7. **Result Storage**: Results are stored back in the Job Store

## Job Lifecycle Flow

Jobs progress through a defined lifecycle that ensures proper state management:

1. **Queued**: Job created and waiting for processing
2. **Scheduled**: Job scheduled for delayed execution (with retry delays)
3. **InProgress**: Currently being processed by a worker
4. **Completed**: Successfully completed with result available
5. **Failed**: Failed after all retry attempts exhausted
6. **Canceled**: Explicitly canceled before completion

## Data Flow Diagram

```
Client Request → HTTP Context → Job Object → Storage → Processing → Result

HTTP Context:
├── Request Body (serialized to payload)
├── HTTP Headers (preserved for handler access)
├── Route Parameters (preserved for handler access)
└── Query Parameters (preserved for handler access)

Job Object:
├── ID (unique identifier)
├── Name (job handler identifier)
├── Payload (serialized request data)
├── Status (current job state)
├── Retry Information (count, max retries, delay until)
├── Timestamps (created, started, completed, last updated)
├── Result/Error (processing result or error details)
├── Worker ID (current processing worker)
└── HTTP Context (headers, route params, query params)
```

## Scalability Considerations

### Concurrency Management
- **Worker Concurrency**: Configurable maximum concurrency based on CPU count or custom settings
- **Queue Limits**: Configurable maximum queue size to prevent system overload
- **Batch Processing**: Configurable batch sizes for efficient processing

### Distributed Processing
- **Redis Storage**: Enables multiple application instances to share job queues
- **Job Claiming**: Atomic job assignment prevents duplicate processing
- **Distributed Recovery**: Automatic recovery of stuck jobs across instances

### Performance Characteristics
- **Throughput**: Determined by worker concurrency, processing time, and I/O characteristics
- **Latency**: Job queuing is immediate; processing begins based on queue position
- **Memory Usage**: Minimal for the core system; depends on queue sizes and storage implementation

## Architecture Patterns

### Producer-Consumer Pattern
The system implements a classic producer-consumer pattern where:
- Producers create jobs from HTTP requests
- Consumers process jobs from a queue
- Channels provide efficient communication between components

### Circuit Breaker Pattern
- Queue size limits prevent system overload
- Immediate rejection when queue is full
- Client feedback for rejected requests

### State Machine Pattern
- Jobs follow a defined state transition model
- Validation prevents invalid state transitions
- Clear lifecycle from creation to completion or failure

## Integration Points

### With ASP.NET Core
- Uses minimal APIs for endpoint mapping
- Integrates with dependency injection
- Leverages HTTP context for request information
- Works with existing middleware pipeline

### With Storage Systems
- Abstract storage interface enables multiple backends
- Consistent API across storage implementations
- Efficient data structures for job management

This architecture provides a robust foundation for handling long-running operations asynchronously while maintaining system responsiveness and enabling horizontal scaling.