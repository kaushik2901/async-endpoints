---
sidebar_position: 3
title: Integration Patterns
---

# Integration Patterns

This page covers common integration patterns for AsyncEndpoints, including EF Core integration, authentication/authorization, third-party API integration, event-driven patterns, and microservices integration.

## Overview

AsyncEndpoints is designed to integrate seamlessly with existing .NET applications and various external systems. This page demonstrates proven patterns for integrating AsyncEndpoints with different technologies and architectural styles.

## EF Core Integration

### Basic EF Core Integration Pattern

```csharp
public class EfCoreDataProcessingHandler(
    ILogger<EfCoreDataProcessingHandler> logger,
    AppDbContext dbContext) 
    : IAsyncEndpointRequestHandler<DataProcessingRequest, DataProcessingResult>
{
    public async Task<MethodResult<DataProcessingResult>> HandleAsync(AsyncContext<DataProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            // Use EF Core to fetch data for processing
            var entities = await dbContext.DataEntities
                .Where(e => e.Category == request.Category && e.CreatedAt >= request.StartDate)
                .ToListAsync(token);
            
            // Process the data
            var processedEntities = await ProcessEntitiesAsync(entities, token);
            
            // Save results back to database
            await dbContext.ProcessedData.AddRangeAsync(processedEntities, token);
            await dbContext.SaveChangesAsync(token);
            
            var result = new DataProcessingResult
            {
                ProcessedCount = processedEntities.Count,
                ProcessedAt = DateTime.UtcNow,
                Summary = $"Processed {processedEntities.Count} entities from category {request.Category}"
            };
            
            return MethodResult<DataProcessingResult>.Success(result);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database error during processing for category {Category}", request.Category);
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during EF Core processing for category {Category}", request.Category);
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
    }
    
    private async Task<List<ProcessedDataEntity>> ProcessEntitiesAsync(List<DataEntity> entities, CancellationToken token)
    {
        // Process the entities
        var results = new List<ProcessedDataEntity>();
        
        foreach (var entity in entities)
        {
            var processed = new ProcessedDataEntity
            {
                OriginalId = entity.Id,
                ProcessedData = entity.Data.ToUpper(), // Example processing
                ProcessedAt = DateTime.UtcNow,
                Status = "Processed"
            };
            
            results.Add(processed);
            
            // Check for cancellation periodically
            token.ThrowIfCancellationRequested();
        }
        
        return results;
    }
}
```

### Unit of Work Pattern with EF Core

```csharp
public interface IUnitOfWork : IDisposable
{
    AppDbContext DbContext { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    
    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }
    
    public AppDbContext DbContext => _context;
    
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
    
    public async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    
    public void Dispose()
    {
        _context?.Dispose();
    }
}

public class UnitOfWorkHandler(
    ILogger<UnitOfWorkHandler> logger,
    IUnitOfWork unitOfWork) 
    : IAsyncEndpointRequestHandler<DataProcessingRequest, DataProcessingResult>
{
    public async Task<MethodResult<DataProcessingResult>> HandleAsync(AsyncContext<DataProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            // Perform multiple operations within a single transaction
            var entities = await unitOfWork.DbContext.DataEntities
                .Where(e => e.Category == request.Category)
                .ToListAsync(token);
            
            // Process and create new entities
            var processedEntities = entities.Select(entity => new ProcessedDataEntity
            {
                OriginalId = entity.Id,
                ProcessedData = entity.Data.ToUpper(),
                ProcessedAt = DateTime.UtcNow,
                Status = "Processed"
            }).ToList();
            
            await unitOfWork.DbContext.ProcessedData.AddRangeAsync(processedEntities, token);
            
            // Update original entities
            foreach (var entity in entities)
            {
                entity.LastProcessedAt = DateTime.UtcNow;
                entity.ProcessStatus = "Processed";
            }
            
            // Save all changes in a single transaction
            await unitOfWork.SaveChangesAsync(token);
            
            var result = new DataProcessingResult
            {
                ProcessedCount = processedEntities.Count,
                ProcessedAt = DateTime.UtcNow,
                Summary = $"Processed {processedEntities.Count} entities in a single transaction"
            };
            
            return MethodResult<DataProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during unit of work processing");
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
    }
}
```

### EF Core with Caching Integration

```csharp
public class CachedDataProcessingHandler(
    ILogger<CachedDataProcessingHandler> logger,
    AppDbContext dbContext,
    IMemoryCache cache) 
    : IAsyncEndpointRequestHandler<DataProcessingRequest, DataProcessingResult>
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(10);
    
    public async Task<MethodResult<DataProcessingResult>> HandleAsync(AsyncContext<DataProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            // Try to get data from cache first
            var cacheKey = $"data_{request.Category}";
            var entities = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
                
                return await dbContext.DataEntities
                    .Where(e => e.Category == request.Category)
                    .ToListAsync(token);
            });
            
            // Process the data
            var processedEntities = await ProcessEntitiesAsync(entities, token);
            
            // Save processed results
            await dbContext.ProcessedData.AddRangeAsync(processedEntities, token);
            await dbContext.SaveChangesAsync(token);
            
            // Clear cache since data has changed
            cache.Remove(cacheKey);
            
            var result = new DataProcessingResult
            {
                ProcessedCount = processedEntities.Count,
                ProcessedAt = DateTime.UtcNow,
                Summary = $"Processed {processedEntities.Count} entities with caching integration"
            };
            
            return MethodResult<DataProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during cached processing");
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
    }
    
    private async Task<List<ProcessedDataEntity>> ProcessEntitiesAsync(List<DataEntity> entities, CancellationToken token)
    {
        var results = new List<ProcessedDataEntity>();
        
        foreach (var entity in entities)
        {
            var processed = new ProcessedDataEntity
            {
                OriginalId = entity.Id,
                ProcessedData = entity.Data.ToUpper(),
                ProcessedAt = DateTime.UtcNow,
                Status = "Processed"
            };
            
            results.Add(processed);
            token.ThrowIfCancellationRequested();
        }
        
        return results;
    }
}
```

## Authentication/Authorization Integration

### Basic Authentication Integration

```csharp
public class AuthenticatedDataProcessingHandler(
    ILogger<AuthenticatedDataProcessingHandler> logger,
    AppDbContext dbContext,
    ICurrentUserService currentUserService) 
    : IAsyncEndpointRequestHandler<SecureDataProcessingRequest, DataProcessingResult>
{
    public async Task<MethodResult<DataProcessingResult>> HandleAsync(AsyncContext<SecureDataProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        // Extract user information from HTTP context
        var userId = context.Headers.GetValueOrDefault("X-User-Id", new List<string?>())?.FirstOrDefault();
        var roles = context.Headers.GetValueOrDefault("X-User-Roles", new List<string?>())?.FirstOrDefault()?.Split(',');
        
        try
        {
            // Verify user has permission for this operation
            var user = await dbContext.Users.FindAsync(new object[] { userId }, token);
            if (user == null)
            {
                return MethodResult<DataProcessingResult>.Failure(
                    AsyncEndpointError.FromCode("USER_NOT_FOUND", "User not found")
                );
            }
            
            // Check user permissions
            if (!HasPermission(user, request.OperationType))
            {
                return MethodResult<DataProcessingResult>.Failure(
                    AsyncEndpointError.FromCode("INSUFFICIENT_PERMISSIONS", "User lacks required permissions")
                );
            }
            
            // Perform the operation
            var processedEntities = await ProcessSecureDataAsync(request, user, token);
            
            // Save results
            await dbContext.ProcessedData.AddRangeAsync(processedEntities, token);
            await dbContext.SaveChangesAsync(token);
            
            // Log the operation
            await LogOperationAsync(user.Id, request.OperationType, processedEntities.Count, token);
            
            var result = new DataProcessingResult
            {
                ProcessedCount = processedEntities.Count,
                ProcessedAt = DateTime.UtcNow,
                Summary = $"Processed {processedEntities.Count} secure entities for user {user.Id}"
            };
            
            return MethodResult<DataProcessingResult>.Success(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized access attempt by user {UserId}", userId);
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during authenticated processing for user {UserId}", userId);
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
    }
    
    private static bool HasPermission(User user, string operationType)
    {
        // Implement permission logic based on user roles and operation type
        return operationType switch
        {
            "READ" => user.Roles.Contains("Reader") || user.Roles.Contains("Admin"),
            "WRITE" => user.Roles.Contains("Writer") || user.Roles.Contains("Admin"),
            "PROCESS" => user.Roles.Contains("Processor") || user.Roles.Contains("Admin"),
            _ => user.Roles.Contains("Admin")
        };
    }
    
    private async Task<List<ProcessedDataEntity>> ProcessSecureDataAsync(SecureDataProcessingRequest request, User user, CancellationToken token)
    {
        // Process data with user context
        var entities = await GetAccessibleEntitiesAsync(user, request, token);
        
        var results = new List<ProcessedDataEntity>();
        
        foreach (var entity in entities)
        {
            var processed = new ProcessedDataEntity
            {
                OriginalId = entity.Id,
                ProcessedData = entity.Data.ToUpper(),
                ProcessedAt = DateTime.UtcNow,
                Status = "Processed",
                ProcessedById = user.Id
            };
            
            results.Add(processed);
            token.ThrowIfCancellationRequested();
        }
        
        return results;
    }
    
    private async Task<List<DataEntity>> GetAccessibleEntitiesAsync(User user, SecureDataProcessingRequest request, CancellationToken token)
    {
        // Get entities based on user permissions
        // This might involve organization, department, or other access controls
        return await dbContext.DataEntities
            .Where(e => e.OrganizationId == user.OrganizationId && e.Category == request.Category)
            .ToListAsync(token);
    }
    
    private async Task LogOperationAsync(string userId, string operationType, int entityCount, CancellationToken token)
    {
        var operationLog = new OperationLog
        {
            UserId = userId,
            OperationType = operationType,
            EntityCount = entityCount,
            Timestamp = DateTime.UtcNow,
            Success = true
        };
        
        await dbContext.OperationLogs.AddAsync(operationLog, token);
    }
}
```

### JWT Token Integration

```csharp
public class JwtProcessingHandler(
    ILogger<JwtProcessingHandler> logger,
    AppDbContext dbContext,
    ITokenValidator tokenValidator) 
    : IAsyncEndpointRequestHandler<JwtDataProcessingRequest, DataProcessingResult>
{
    public async Task<MethodResult<DataProcessingResult>> HandleAsync(AsyncContext<JwtDataProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            // Extract and validate JWT token
            var authHeader = context.Headers.GetValueOrDefault("Authorization", new List<string?>())?.FirstOrDefault();
            
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return MethodResult<DataProcessingResult>.Failure(
                    AsyncEndpointError.FromCode("INVALID_TOKEN", "Authorization header missing or invalid")
                );
            }
            
            var tokenValue = authHeader.Substring("Bearer ".Length);
            
            // Validate the token
            var validationResult = await tokenValidator.ValidateTokenAsync(tokenValue, token);
            if (!validationResult.IsValid)
            {
                return MethodResult<DataProcessingResult>.Failure(
                    AsyncEndpointError.FromCode("INVALID_TOKEN", validationResult.Error)
                );
            }
            
            // Extract user information from validated token
            var userId = validationResult.Claims.GetValueOrDefault("sub");
            var userRoles = validationResult.Claims.GetValueOrDefault("roles")?.Split(',');
            
            // Perform the operation with validated user context
            var processedEntities = await ProcessDataWithUserContextAsync(request, userId, userRoles, token);
            
            // Save results
            await dbContext.ProcessedData.AddRangeAsync(processedEntities, token);
            await dbContext.SaveChangesAsync(token);
            
            var result = new DataProcessingResult
            {
                ProcessedCount = processedEntities.Count,
                ProcessedAt = DateTime.UtcNow,
                Summary = $"Processed {processedEntities.Count} entities with JWT validation"
            };
            
            return MethodResult<DataProcessingResult>.Success(result);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning(ex, "JWT validation failed");
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during JWT-protected processing");
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
    }
    
    private async Task<List<ProcessedDataEntity>> ProcessDataWithUserContextAsync(JwtDataProcessingRequest request, string userId, string[] userRoles, CancellationToken token)
    {
        // Use user context for data access
        var entities = await dbContext.DataEntities
            .Where(e => e.OwnerId == userId || userRoles.Contains("Admin"))
            .ToListAsync(token);
        
        var results = new List<ProcessedDataEntity>();
        
        foreach (var entity in entities)
        {
            var processed = new ProcessedDataEntity
            {
                OriginalId = entity.Id,
                ProcessedData = entity.Data.ToUpper(),
                ProcessedAt = DateTime.UtcNow,
                Status = "Processed",
                ProcessedById = userId
            };
            
            results.Add(processed);
            token.ThrowIfCancellationRequested();
        }
        
        return results;
    }
}
```

## Third-Party API Integration

### External API Integration Handler

```csharp
public class ExternalApiProcessingHandler(
    ILogger<ExternalApiProcessingHandler> logger,
    HttpClient httpClient,
    IRateLimiter rateLimiter) 
    : IAsyncEndpointRequestHandler<ExternalApiRequest, ExternalApiResponse>
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    
    public async Task<MethodResult<ExternalApiResponse>> HandleAsync(AsyncContext<ExternalApiRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            logger.LogInformation(
                "Starting external API integration for request {RequestId} to {ApiUrl}",
                request.RequestId, request.ApiUrl);
            
            // Apply rate limiting
            await rateLimiter.AcquirePermitAsync("external-api", token);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(_timeout);
            
            // Prepare the request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, request.ApiUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request.Data), 
                    Encoding.UTF8, 
                    "application/json")
            };
            
            // Add headers from original context if needed
            foreach (var header in context.Headers)
            {
                if (IsForwardableHeader(header.Key))
                {
                    httpRequest.Headers.Add(header.Key, string.Join(", ", header.Value.Where(v => v != null)));
                }
            }
            
            // Execute the external API call
            var response = await httpClient.SendAsync(httpRequest, cts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"External API call failed with status {response.StatusCode}: {errorContent}";
                
                logger.LogError(errorMessage);
                return MethodResult<ExternalApiResponse>.Failure(
                    AsyncEndpointError.FromCode("EXTERNAL_API_ERROR", errorMessage)
                );
            }
            
            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync();
            var externalResponse = JsonSerializer.Deserialize<ExternalApiResponse>(responseContent);
            
            logger.LogInformation(
                "External API integration completed successfully for request {RequestId}",
                request.RequestId);
            
            return MethodResult<ExternalApiResponse>.Success(externalResponse!);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogInformation("External API call was cancelled for request {RequestId}", request.RequestId);
            return MethodResult<ExternalApiResponse>.Failure(
                AsyncEndpointError.FromCode("OPERATION_CANCELLED", "Request was cancelled")
            );
        }
        catch (TaskCanceledException) when (cts.Token.IsCancellationRequested)
        {
            logger.LogWarning("External API call timed out for request {RequestId}", request.RequestId);
            return MethodResult<ExternalApiResponse>.Failure(
                AsyncEndpointError.FromCode("EXTERNAL_API_TIMEOUT", "External API call timed out")
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error during external API call for request {RequestId}", request.RequestId);
            return MethodResult<ExternalApiResponse>.Failure(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during external API integration for request {RequestId}", request.RequestId);
            return MethodResult<ExternalApiResponse>.Failure(ex);
        }
    }
    
    private static bool IsForwardableHeader(string headerName)
    {
        var nonForwardableHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "host", "content-length", "connection", "transfer-encoding", "upgrade"
        };
        
        return !nonForwardableHeaders.Contains(headerName);
    }
}
```

### Circuit Breaker with External API Integration

```csharp
public class CircuitBreakerExternalApiHandler(
    ILogger<CircuitBreakerExternalApiHandler> logger,
    IHttpClientFactory httpClientFactory,
    ICircuitBreaker circuitBreaker) 
    : IAsyncEndpointRequestHandler<ExternalApiRequest, ExternalApiResponse>
{
    public async Task<MethodResult<ExternalApiResponse>> HandleAsync(AsyncContext<ExternalApiRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            logger.LogInformation(
                "Starting circuit breaker protected external API call for {RequestId}",
                request.RequestId);
            
            // Execute with circuit breaker protection
            var result = await circuitBreaker.ExecuteAsync(async () =>
            {
                using var httpClient = httpClientFactory.CreateClient();
                
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, request.ApiUrl)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(request.Data), 
                        Encoding.UTF8, 
                        "application/json")
                };
                
                var response = await httpClient.SendAsync(httpRequest, token);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new ExternalServiceException($"API call failed: {response.StatusCode}");
                }
                
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ExternalApiResponse>(content)!;
            });
            
            logger.LogInformation(
                "Circuit breaker protected API call succeeded for {RequestId}",
                request.RequestId);
            
            return MethodResult<ExternalApiResponse>.Success(result);
        }
        catch (ExternalServiceException ex)
        {
            logger.LogWarning(
                "External service error for {RequestId}: {Message}", 
                request.RequestId, ex.Message);
            
            return MethodResult<ExternalApiResponse>.Failure(ex);
        }
        catch (CircuitBreakerOpenException ex)
        {
            logger.LogWarning(
                "Circuit breaker is open, failing fast for {RequestId}", 
                request.RequestId);
            
            return MethodResult<ExternalApiResponse>.Failure(
                AsyncEndpointError.FromCode("CIRCUIT_BREAKER_OPEN", "Service temporarily unavailable", ex)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, 
                "Error during circuit breaker protected API call for {RequestId}", 
                request.RequestId);
            
            return MethodResult<ExternalApiResponse>.Failure(ex);
        }
    }
}
```

## Event-Driven Integration Patterns

### Event Publishing Handler

```csharp
public class EventPublishingHandler(
    ILogger<EventPublishingHandler> logger,
    IEventPublisher eventPublisher,
    AppDbContext dbContext) 
    : IAsyncEndpointRequestHandler<EventProcessingRequest, EventProcessingResult>
{
    public async Task<MethodResult<EventProcessingResult>> HandleAsync(AsyncContext<EventProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            logger.LogInformation("Starting event publishing for type {EventType}", request.EventType);
            
            // Process the request data
            var processedData = await ProcessRequestDataAsync(request, token);
            
            // Save to database
            var processedEntity = new ProcessedEntity
            {
                Id = Guid.NewGuid(),
                EventType = request.EventType,
                ProcessedData = JsonSerializer.Serialize(processedData),
                ProcessedAt = DateTime.UtcNow,
                Status = "Processed"
            };
            
            await dbContext.ProcessedEntities.AddAsync(processedEntity, token);
            await dbContext.SaveChangesAsync(token);
            
            // Publish events for other services to consume
            var eventsToPublish = await GenerateEventsAsync(processedEntity, token);
            
            foreach (var @event in eventsToPublish)
            {
                await eventPublisher.PublishAsync(@event, token);
            }
            
            logger.LogInformation(
                "Published {EventCount} events for processed entity {EntityId}", 
                eventsToPublish.Count, processedEntity.Id);
            
            var result = new EventProcessingResult
            {
                ProcessedId = processedEntity.Id,
                EventCount = eventsToPublish.Count,
                ProcessedAt = DateTime.UtcNow,
                Summary = $"Processed request and published {eventsToPublish.Count} events"
            };
            
            return MethodResult<EventProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during event publishing for type {EventType}", request.EventType);
            return MethodResult<EventProcessingResult>.Failure(ex);
        }
    }
    
    private async Task<object> ProcessRequestDataAsync(EventProcessingRequest request, CancellationToken token)
    {
        // Process the request data based on event type
        return request.EventType switch
        {
            "USER_CREATED" => await ProcessUserCreatedEventAsync(request, token),
            "ORDER_PROCESSED" => await ProcessOrderProcessedEventAsync(request, token),
            _ => await ProcessGenericEventAsync(request, token)
        };
    }
    
    private async Task<List<object>> GenerateEventsAsync(ProcessedEntity processedEntity, CancellationToken token)
    {
        var events = new List<object>();
        
        // Generate different types of events based on the processed entity
        if (processedEntity.EventType == "USER_CREATED")
        {
            events.Add(new UserProfileCreatedEvent 
            { 
                UserId = processedEntity.Id, 
                CreatedAt = processedEntity.ProcessedAt 
            });
            
            events.Add(new WelcomeEmailRequestedEvent 
            { 
                UserId = processedEntity.Id 
            });
        }
        
        // Add more event types as needed
        
        return events;
    }
    
    private async Task<object> ProcessUserCreatedEventAsync(EventProcessingRequest request, CancellationToken token)
    {
        // Process user creation event
        return new { UserId = Guid.NewGuid(), Email = request.Data?.ToString()?.Split('|')[0] };
    }
    
    private async Task<object> ProcessOrderProcessedEventAsync(EventProcessingRequest request, CancellationToken token)
    {
        // Process order processed event
        return new { OrderId = Guid.NewGuid(), Status = "Processed", Total = 100.0 };
    }
    
    private async Task<object> ProcessGenericEventAsync(EventProcessingRequest request, CancellationToken token)
    {
        // Process generic event
        return request.Data;
    }
}
```

### Event Subscription Handler

```csharp
public class EventSubscriptionHandler(
    ILogger<EventSubscriptionHandler> logger,
    AppDbContext dbContext) 
    : IAsyncEndpointRequestHandler<SubscriptionEvent, SubscriptionResult>
{
    public async Task<MethodResult<SubscriptionResult>> HandleAsync(AsyncContext<SubscriptionEvent> context, CancellationToken token)
    {
        var @event = context.Request;
        
        try
        {
            logger.LogInformation("Processing subscription event {EventId} of type {EventType}", @event.Id, @event.EventType);
            
            // Handle different event types
            var result = @event.EventType switch
            {
                "USER_CREATED" => await HandleUserCreatedEventAsync((UserCreatedEvent)@event.Data, token),
                "ORDER_COMPLETED" => await HandleOrderCompletedEventAsync((OrderCompletedEvent)@event.Data, token),
                "PAYMENT_PROCESSED" => await HandlePaymentProcessedEventAsync((PaymentProcessedEvent)@event.Data, token),
                _ => await HandleGenericEventAsync(@event, token)
            };
            
            var subscriptionResult = new SubscriptionResult
            {
                EventId = @event.Id,
                HandledAt = DateTime.UtcNow,
                Success = true,
                Summary = $"Successfully handled {@event.EventType} event"
            };
            
            logger.LogInformation("Successfully processed {@event.EventType} event {@event.Id}");
            
            return MethodResult<SubscriptionResult>.Success(subscriptionResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing event {@event.Id} of type {@event.EventType}", @event.Id, @event.EventType);
            return MethodResult<SubscriptionResult>.Failure(ex);
        }
    }
    
    private async Task<object> HandleUserCreatedEventAsync(UserCreatedEvent userEvent, CancellationToken token)
    {
        // Create user profile in the local database
        var userProfile = new UserProfile
        {
            Id = userEvent.UserId,
            Email = userEvent.Email,
            CreatedAt = DateTime.UtcNow,
            Status = "Active"
        };
        
        await dbContext.UserProfiles.AddAsync(userProfile, token);
        await dbContext.SaveChangesAsync(token);
        
        // Send welcome email event
        // This might trigger another async operation
        
        return userProfile;
    }
    
    private async Task<object> HandleOrderCompletedEventAsync(OrderCompletedEvent orderEvent, CancellationToken token)
    {
        // Update order status in local database
        var order = await dbContext.Orders.FindAsync(new object[] { orderEvent.OrderId }, token);
        if (order != null)
        {
            order.Status = "Completed";
            order.CompletedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(token);
        }
        
        return order;
    }
    
    private async Task<object> HandlePaymentProcessedEventAsync(PaymentProcessedEvent paymentEvent, CancellationToken token)
    {
        // Update payment status in local database
        var payment = await dbContext.Payments.FindAsync(new object[] { paymentEvent.PaymentId }, token);
        if (payment != null)
        {
            payment.Status = "Processed";
            payment.ProcessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(token);
        }
        
        return payment;
    }
    
    private async Task<object> HandleGenericEventAsync(SubscriptionEvent @event, CancellationToken token)
    {
        // Generic event handling
        var genericRecord = new EventLog
        {
            Id = @event.Id,
            EventType = @event.EventType,
            Payload = JsonSerializer.Serialize(@event.Data),
            ProcessedAt = DateTime.UtcNow
        };
        
        await dbContext.EventLogs.AddAsync(genericRecord, token);
        await dbContext.SaveChangesAsync(token);
        
        return genericRecord;
    }
}
```

## Microservices Integration

### Service-to-Service Communication

```csharp
public class MicroserviceIntegrationHandler(
    ILogger<MicroserviceIntegrationHandler> logger,
    IGrpcServiceClient grpcClient,
    IJsonRpcClient jsonRpcClient) 
    : IAsyncEndpointRequestHandler<MicroserviceRequest, MicroserviceResponse>
{
    public async Task<MethodResult<MicroserviceResponse>> HandleAsync(AsyncContext<MicroserviceRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            logger.LogInformation(
                "Starting microservice integration for service {ServiceName}, operation {Operation}",
                request.ServiceName, request.Operation);
            
            MicroserviceResponse response;
            
            // Route to appropriate communication protocol
            if (request.Protocol == "grpc")
            {
                response = await CallGrpcServiceAsync(request, token);
            }
            else if (request.Protocol == "jsonrpc")
            {
                response = await CallJsonRpcServiceAsync(request, token);
            }
            else
            {
                return MethodResult<MicroserviceResponse>.Failure(
                    AsyncEndpointError.FromCode("UNSUPPORTED_PROTOCOL", $"Protocol {request.Protocol} not supported")
                );
            }
            
            logger.LogInformation(
                "Microservice call completed successfully for {ServiceName}",
                request.ServiceName);
            
            return MethodResult<MicroserviceResponse>.Success(response);
        }
        catch (RpcException ex)
        {
            logger.LogError(
                ex, 
                "gRPC error calling service {ServiceName}: {Message}", 
                request.ServiceName, ex.Message);
            
            return MethodResult<MicroserviceResponse>.Failure(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, 
                "Error during microservice integration for {ServiceName}", 
                request.ServiceName);
            
            return MethodResult<MicroserviceResponse>.Failure(ex);
        }
    }
    
    private async Task<MicroserviceResponse> CallGrpcServiceAsync(MicroserviceRequest request, CancellationToken token)
    {
        var grpcRequest = new GrpcServiceRequest
        {
            Operation = request.Operation,
            Parameters = { request.Parameters.Select(kvp => new Parameter { Key = kvp.Key, Value = kvp.Value }) }
        };
        
        var grpcResponse = await grpcClient.CallServiceAsync(grpcRequest, token);
        
        return new MicroserviceResponse
        {
            Success = grpcResponse.Success,
            Data = grpcResponse.Result,
            ServiceName = request.ServiceName,
            Operation = request.Operation
        };
    }
    
    private async Task<MicroserviceResponse> CallJsonRpcServiceAsync(MicroserviceRequest request, CancellationToken token)
    {
        var jsonRpcRequest = new JsonRpcRequest
        {
            Method = request.Operation,
            Parameters = request.Parameters
        };
        
        var jsonRpcResponse = await jsonRpcClient.CallAsync(jsonRpcRequest, token);
        
        return new MicroserviceResponse
        {
            Success = jsonRpcResponse.Success,
            Data = jsonRpcResponse.Result,
            ServiceName = request.ServiceName,
            Operation = request.Operation
        };
    }
}
```

### API Gateway Integration Pattern

```csharp
public class GatewayIntegrationHandler(
    ILogger<GatewayIntegrationHandler> logger,
    IGatewayClient gatewayClient) 
    : IAsyncEndpointRequestHandler<GatewayRequest, GatewayResponse>
{
    public async Task<MethodResult<GatewayResponse>> HandleAsync(AsyncContext<GatewayRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            logger.LogInformation(
                "Starting gateway integration for route {Route} and method {Method}",
                request.Route, request.Method);
            
            // Prepare headers from the original request
            var headers = new Dictionary<string, string>();
            foreach (var header in context.Headers.Where(h => !IsSystemHeader(h.Key)))
            {
                headers.Add(header.Key, string.Join(",", header.Value.Where(v => v != null)));
            }
            
            // Execute through gateway
            var gatewayResponse = await gatewayClient.SendAsync(
                new GatewayCallRequest
                {
                    Method = request.Method,
                    Route = request.Route,
                    Headers = headers,
                    Body = request.Body,
                    Timeout = TimeSpan.FromSeconds(60)
                },
                token);
            
            var response = new GatewayResponse
            {
                StatusCode = gatewayResponse.StatusCode,
                Body = gatewayResponse.Body,
                Headers = gatewayResponse.Headers,
                Success = gatewayResponse.IsSuccess
            };
            
            logger.LogInformation(
                "Gateway integration completed with status {StatusCode} for route {Route}",
                gatewayResponse.StatusCode, request.Route);
            
            return MethodResult<GatewayResponse>.Success(response);
        }
        catch (GatewayException ex)
        {
            logger.LogError(
                ex, 
                "Gateway error for route {Route}: {Message}", 
                request.Route, ex.Message);
            
            return MethodResult<GatewayResponse>.Failure(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, 
                "Error during gateway integration for route {Route}", 
                request.Route);
            
            return MethodResult<GatewayResponse>.Failure(ex);
        }
    }
    
    private static bool IsSystemHeader(string headerName)
    {
        var systemHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "host", "content-length", "connection", "transfer-encoding"
        };
        
        return systemHeaders.Contains(headerName);
    }
}
```

## Integration Setup and Configuration

### Service Registration for Integrations

```csharp
// In Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add caching
builder.Services.AddMemoryCache();

// Add HTTP clients with resilience
builder.Services.AddHttpClient("external-api", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "AsyncEndpoints/1.0");
})
.AddResiliencePipeline("external-api", (builder, context) =>
{
    builder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        })
        .AddTimeout(TimeSpan.FromSeconds(30));
});

// Add JWT validation service
builder.Services.AddScoped<ITokenValidator, JwtTokenValidator>();

// Add rate limiting
builder.Services.AddSingleton<IRateLimiter>(provider => 
    new TokenBucketRateLimiter(
        new TokenBucketRateLimiterOptions
        {
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            QueueLimit = 5
        }));

// Add circuit breaker
builder.Services.AddSingleton<ICircuitBreaker, CircuitBreakerService>();

// Add event publisher
builder.Services.AddSingleton<IEventPublisher, EventPublisherService>();

// Add gRPC client
builder.Services.AddSingleton<IGrpcServiceClient, GrpcServiceClient>();

// Register handlers
builder.Services.AddAsyncEndpointHandler<EfCoreDataProcessingHandler, DataProcessingRequest, DataProcessingResult>("EfCoreProcess");
builder.Services.AddAsyncEndpointHandler<AuthenticatedDataProcessingHandler, SecureDataProcessingRequest, DataProcessingResult>("SecureProcess");
builder.Services.AddAsyncEndpointHandler<ExternalApiProcessingHandler, ExternalApiRequest, ExternalApiResponse>("ExternalApiCall");
builder.Services.AddAsyncEndpointHandler<EventPublishingHandler, EventProcessingRequest, EventProcessingResult>("EventPublish");
builder.Services.AddAsyncEndpointHandler<MicroserviceIntegrationHandler, MicroserviceRequest, MicroserviceResponse>("MicroserviceIntegration");

// Configure AsyncEndpoints
builder.Services.AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(builder.Configuration.GetConnectionString("Redis"))
    .AddAsyncEndpointsWorker();
```

### Endpoint Mappings for Integration Patterns

```csharp
// EF Core integration endpoints
app.MapAsyncPost<DataProcessingRequest>("EfCoreProcess", "/api/efcore/process");

// Authentication integration endpoints
app.MapAsyncPost<SecureDataProcessingRequest>("SecureProcess", "/api/secure/process")
    .RequireAuthorization();

// External API integration endpoints
app.MapAsyncPost<ExternalApiRequest>("ExternalApiCall", "/api/external/call");

// Event-driven integration endpoints
app.MapAsyncPost<EventProcessingRequest>("EventPublish", "/api/events/publish");
app.MapAsyncPost<SubscriptionEvent, SubscriptionResult>("EventSubscription", "/api/events/subscribe");

// Microservice integration endpoints
app.MapAsyncPost<MicroserviceRequest>("MicroserviceIntegration", "/api/microservice/integrate");

// Job status and monitoring
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");

// Additional integration-specific endpoints
app.MapGet("/api/health/integrations", async (IEventPublisher eventPublisher) =>
{
    var health = new IntegrationHealth
    {
        ExternalApiAvailable = await IsExternalApiAvailable(),
        EventPublisherHealthy = await eventPublisher.IsHealthyAsync()
    };
    
    return health;
});
```

These integration patterns demonstrate how to effectively combine AsyncEndpoints with various external systems and architectural styles, including data persistence, authentication, external APIs, event-driven systems, and microservices.