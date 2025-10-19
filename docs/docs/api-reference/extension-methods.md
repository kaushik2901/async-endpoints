---
sidebar_position: 1
title: Extension Methods
---

# Extension Methods

This page provides detailed reference documentation for all AsyncEndpoints extension methods, including their signatures, parameters, return types, and usage examples.

## AddAsyncEndpoints

### Signature
```csharp
public static IServiceCollection AddAsyncEndpoints(
    this IServiceCollection services, 
    Action<AsyncEndpointsConfigurations>? configureOptions = null)
```

### Parameters
- **services** (`IServiceCollection`): The `IServiceCollection` to add services to
- **configureOptions** (`Action<AsyncEndpointsConfigurations>`): Optional action to configure AsyncEndpoints options

### Returns
- **IServiceCollection**: The `IServiceCollection` for method chaining

### Description
Adds the core AsyncEndpoints services to the dependency injection container.

### Example
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    options.JobManagerConfiguration.DefaultMaxRetries = 3;
});
```

---

## AddAsyncEndpointsInMemoryStore

### Signature
```csharp
public static IServiceCollection AddAsyncEndpointsInMemoryStore(
    this IServiceCollection services)
```

### Parameters
- **services** (`IServiceCollection`): The `IServiceCollection` to add services to

### Returns
- **IServiceCollection**: The `IServiceCollection` for method chaining

### Description
Adds an in-memory job store implementation to the dependency injection container. Use this for development or single-instance deployments.

### Example
```csharp
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore();
```

---

## AddAsyncEndpointsRedisStore (Connection String)

### Signature
```csharp
public static IServiceCollection AddAsyncEndpointsRedisStore(
    this IServiceCollection services, 
    string connectionString)
```

### Parameters
- **services** (`IServiceCollection`): The `IServiceCollection` to add services to
- **connectionString** (`string`): The Redis connection string

### Returns
- **IServiceCollection**: The `IServiceCollection` for method chaining

### Description
Adds a Redis-based job store implementation using a connection string.

### Example
```csharp
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore("localhost:6379");
```

---

## AddAsyncEndpointsRedisStore (Connection Multiplexer)

### Signature
```csharp
public static IServiceCollection AddAsyncEndpointsRedisStore(
    this IServiceCollection services, 
    IConnectionMultiplexer connectionMultiplexer)
```

### Parameters
- **services** (`IServiceCollection`): The `IServiceCollection` to add services to
- **connectionMultiplexer** (`IConnectionMultiplexer`): The Redis connection multiplexer instance

### Returns
- **IServiceCollection**: The `IServiceCollection` for method chaining

### Description
Adds a Redis-based job store implementation using a pre-configured connection multiplexer.

### Example
```csharp
var connection = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(connection);
```

---

## AddAsyncEndpointsRedisStore (Configuration Action)

### Signature
```csharp
public static IServiceCollection AddAsyncEndpointsRedisStore(
    this IServiceCollection services, 
    Action<RedisConfiguration> setupAction)
```

### Parameters
- **services** (`IServiceCollection`): The `IServiceCollection` to add services to
- **setupAction** (`Action<RedisConfiguration>`): Action to configure the Redis connection

### Returns
- **IServiceCollection**: The `IServiceCollection` for method chaining

### Description
Adds a Redis-based job store implementation with configuration action.

### Example
```csharp
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(config =>
    {
        config.ConnectionString = "localhost:6379";
        config.Password = "your-password";
    });
```

---

## AddAsyncEndpointsWorker

### Signature
```csharp
public static IServiceCollection AddAsyncEndpointsWorker(
    this IServiceCollection services,
    Action<AsyncEndpointsRecoveryConfiguration>? recoveryConfiguration = null)
```

### Parameters
- **services** (`IServiceCollection`): The `IServiceCollection` to add services to
- **recoveryConfiguration** (`Action<AsyncEndpointsRecoveryConfiguration>`): Optional configuration for distributed job recovery

### Returns
- **IServiceCollection**: The `IServiceCollection` for method chaining

### Description
Adds the background worker services required to process async jobs.

### Example
```csharp
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore("localhost:6379")
    .AddAsyncEndpointsWorker(recoveryConfiguration =>
    {
        recoveryConfiguration.EnableDistributedJobRecovery = true;
        recoveryConfiguration.JobTimeoutMinutes = 30;
    });
```

---

## AddAsyncEndpointHandler (With Request and Response)

### Signature
```csharp
public static IServiceCollection AddAsyncEndpointHandler<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] 
    TAsyncEndpointRequestHandler, 
    TRequest, 
    TResponse>(
    this IServiceCollection services, 
    string jobName)
    where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TRequest, TResponse>
```

### Parameters
- **services** (`IServiceCollection`): The `IServiceCollection` to add services to
- **jobName** (`string`): The unique name of the job, used to identify the specific handler

### Type Parameters
- **TAsyncEndpointRequestHandler**: The type of the handler that implements `IAsyncEndpointRequestHandler<TRequest, TResponse>`
- **TRequest**: The type of the request object
- **TResponse**: The type of the response object

### Returns
- **IServiceCollection**: The `IServiceCollection` for method chaining

### Description
Registers an asynchronous endpoint handler for processing requests of type TRequest and returning responses of type TResponse.

### Example
```csharp
builder.Services.AddAsyncEndpointHandler<ProcessDataHandler, DataRequest, ProcessResult>("ProcessData");
```

---

## AddAsyncEndpointHandler (No-Body Request)

### Signature
```csharp
public static IServiceCollection AddAsyncEndpointHandler<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] 
    TAsyncEndpointRequestHandler, 
    TResponse>(
    this IServiceCollection services,
    string jobName)
    where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TResponse>
```

### Parameters
- **services** (`IServiceCollection`): The `IServiceCollection` to add services to
- **jobName** (`string`): A unique name for the async job, used for identifying the handler

### Type Parameters
- **TAsyncEndpointRequestHandler**: The type of the handler that implements `IAsyncEndpointRequestHandler<TResponse>`
- **TResponse**: The type of the response object

### Returns
- **IServiceCollection**: The `IServiceCollection` for method chaining

### Description
Adds an asynchronous endpoint handler for requests without body to the service collection.

### Example
```csharp
builder.Services.AddAsyncEndpointHandler<GenerateReportHandler, ReportResult>("GenerateReport");
```

---

## MapAsyncPost (With Request Body)

### Signature
```csharp
public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
    this IEndpointRouteBuilder endpoints,
    string jobName,
    string pattern,
    Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

### Parameters
- **endpoints** (`IEndpointRouteBuilder`): The `IEndpointRouteBuilder` to add the route to
- **jobName** (`string`): A unique name for the async job, used for identifying the handler
- **pattern** (`string`): The URL pattern for the endpoint
- **handler** (`Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>`): Optional custom handler function

### Type Parameters
- **TRequest**: The type of the request object

### Returns
- **IEndpointConventionBuilder**: That can be used to further configure the endpoint

### Description
Maps an asynchronous POST endpoint that processes requests in the background with a request body.

### Example
```csharp
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");
```

---

## MapAsyncPost (No-Body Request)

### Signature
```csharp
public static IEndpointConventionBuilder MapAsyncPost(
    this IEndpointRouteBuilder endpoints,
    string jobName,
    string pattern,
    Func<HttpContext, NoBodyRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

### Parameters
- **endpoints** (`IEndpointRouteBuilder`): The `IEndpointRouteBuilder` to add the route to
- **jobName** (`string`): A unique name for the async job, used for identifying the handler
- **pattern** (`string`): The URL pattern for the endpoint
- **handler** (`Func<HttpContext, NoBodyRequest, CancellationToken, Task<IResult?>?>`): Optional custom handler function

### Returns
- **IEndpointConventionBuilder**: That can be used to further configure the endpoint

### Description
Maps an asynchronous POST endpoint that processes requests without body in the background.

### Example
```csharp
app.MapAsyncPost("GenerateReport", "/api/generate-report");
```

---

## MapAsyncPut

### Signature
```csharp
public static IEndpointConventionBuilder MapAsyncPut<TRequest>(
    this IEndpointRouteBuilder endpoints,
    string jobName,
    string pattern,
    Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

### Parameters
- **endpoints** (`IEndpointRouteBuilder`): The `IEndpointRouteBuilder` to add the route to
- **jobName** (`string`): A unique name for the async job, used for identifying the handler
- **pattern** (`string`): The URL pattern for the endpoint
- **handler** (`Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>`): Optional custom handler function

### Type Parameters
- **TRequest**: The type of the request object

### Returns
- **IEndpointConventionBuilder**: That can be used to further configure the endpoint

### Description
Maps an asynchronous PUT endpoint that processes requests in the background.

### Example
```csharp
app.MapAsyncPut<UpdateRequest>("UpdateData", "/api/update-data");
```

---

## MapAsyncPatch

### Signature
```csharp
public static IEndpointConventionBuilder MapAsyncPatch<TRequest>(
    this IEndpointRouteBuilder endpoints,
    string jobName,
    string pattern,
    Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

### Parameters
- **endpoints** (`IEndpointRouteBuilder`): The `IEndpointRouteBuilder` to add the route to
- **jobName** (`string`): A unique name for the async job, used for identifying the handler
- **pattern** (`string`): The URL pattern for the endpoint
- **handler** (`Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>`): Optional custom handler function

### Type Parameters
- **TRequest**: The type of the request object

### Returns
- **IEndpointConventionBuilder**: That can be used to further configure the endpoint

### Description
Maps an asynchronous PATCH endpoint that processes requests in the background.

### Example
```csharp
app.MapAsyncPatch<PartialUpdateRequest>("UpdatePartial", "/api/update-partial");
```

---

## MapAsyncDelete

### Signature
```csharp
public static IEndpointConventionBuilder MapAsyncDelete<TRequest>(
    this IEndpointRouteBuilder endpoints,
    string jobName,
    string pattern,
    Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

### Parameters
- **endpoints** (`IEndpointRouteBuilder`): The `IEndpointRouteBuilder` to add the route to
- **jobName** (`string`): A unique name for the async job, used for identifying the handler
- **pattern** (`string`): The URL pattern for the endpoint
- **handler** (`Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>`): Optional custom handler function

### Type Parameters
- **TRequest**: The type of the request object

### Returns
- **IEndpointConventionBuilder**: That can be used to further configure the endpoint

### Description
Maps an asynchronous DELETE endpoint that processes requests in the background.

### Example
```csharp
app.MapAsyncDelete<DeleteRequest>("DeleteData", "/api/delete-data");
```

---

## MapAsyncGetJobDetails

### Signature
```csharp
public static IEndpointConventionBuilder MapAsyncGetJobDetails(
    this IEndpointRouteBuilder endpoints,
    string pattern = \"/jobs/{jobId:guid}\")
```

### Parameters
- **endpoints** (`IEndpointRouteBuilder`): The `IEndpointRouteBuilder` to add the route to
- **pattern** (`string`): The URL pattern for the endpoint. Should contain a &#123;jobId&#125; parameter

### Returns
- **IEndpointConventionBuilder**: That can be used to further configure the endpoint

### Description
Maps an asynchronous GET endpoint that fetches job responses by job ID.

### Example
```csharp
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");
```