---
sidebar_position: 5
title: Deployment
---

# Deployment

This page covers deployment strategies for AsyncEndpoints applications, including production deployment patterns, containerization, Kubernetes deployment, configuration management, health checks, and scaling strategies.

## Overview

Deploying AsyncEndpoints applications requires careful consideration of storage backends, worker configurations, scaling patterns, and operational concerns. This guide covers best practices for production deployments.

## Production Deployment Strategies

### Single Instance Deployment

For simple applications or initial deployments:

```csharp
// Production configuration for single instance
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsyncEndpoints(options =>
{
    // Conservative settings for single instance
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    options.WorkerConfigurations.MaximumQueueSize = 500;
    options.WorkerConfigurations.JobTimeoutMinutes = 60;
    options.WorkerConfigurations.PollingIntervalMs = 2000;
    
    options.JobManagerConfiguration.DefaultMaxRetries = 3;
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0;
    options.JobManagerConfiguration.MaxConcurrentJobs = 20;
});

// Use Redis for persistence and reliability
builder.Services.AddAsyncEndpointsRedisStore(builder.Configuration.GetConnectionString("Redis"));
builder.Services.AddAsyncEndpointsWorker();

var app = builder.Build();

// Configure for production
app.UseExceptionHandler("/Error");
app.UseHsts();
app.UseHttpsRedirection();

app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");

app.Run();
```

### Multi-Instance Deployment

For high availability and scalability:

```csharp
// Configuration for multi-instance deployment
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsyncEndpoints(options =>
{
    // Distribute load across instances
    options.WorkerConfigurations.MaximumConcurrency = Math.Max(1, Environment.ProcessorCount / 2);
    options.WorkerConfigurations.MaximumQueueSize = 1000; // Shared across all instances
    options.WorkerConfigurations.JobTimeoutMinutes = 60;
    options.WorkerConfigurations.PollingIntervalMs = 3000; // Reduce frequency across instances
    
    // Job manager settings for distributed environment
    options.JobManagerConfiguration.DefaultMaxRetries = 5;
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0;
    options.JobManagerConfiguration.MaxConcurrentJobs = 50;
    options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(1);
});

// Use Redis for shared state
builder.Services.AddAsyncEndpointsRedisStore(builder.Configuration.GetConnectionString("Redis"));

// Enable distributed recovery
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    recoveryConfiguration.JobTimeoutMinutes = 60;
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // 5 minutes
    recoveryConfiguration.MaximumRetries = 3;
});

var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseHsts();
app.UseHttpsRedirection();

app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");

app.Run();
```

## Docker Containerization

### Basic Dockerfile

```dockerfile
# Use the official .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy everything else and build
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Use the official .NET runtime image to run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose the port the app runs on
EXPOSE 8080

# Create a non-root user for security
RUN addgroup --system appgroup && adduser --system appuser --ingroup appgroup
USER appuser

# Run the application
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### Production Dockerfile with Optimization

```dockerfile
# Use multi-stage build to reduce final image size
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Create runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install only necessary runtime dependencies
RUN apt-get update && apt-get install -y \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create non-root user
RUN groupadd -r appgroup && useradd -r -g appgroup appuser
USER appuser

# Set environment variables for production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=true
ENV COMPlus_ReadyToRun=1

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AsyncEndpointsApp.dll"]
```

### Docker Compose for Development

```yaml
version: '3.8'

services:
  app:
    build: .
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      - redis
    restart: unless-stopped
  
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    restart: unless-stopped
    command: redis-server --appendonly yes

  redis-commander:
    image: rediscommander/redis-commander:latest
    environment:
      - REDIS_HOSTS=local:redis:6379
    ports:
      - "8081:8081"
    depends_on:
      - redis

volumes:
  redis_data:
```

### Production Docker Compose

```yaml
version: '3.8'

services:
  app:
    image: asyncendpoints:latest
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '1.0'
          memory: 512M
        reservations:
          cpus: '0.5'
          memory: 256M
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Redis=redis-cluster:6379
      - Logging__LogLevel__Default=Information
    ports:
      - "80:8080"
    depends_on:
      - redis-cluster
    networks:
      - async-network

  redis-cluster:
    image: redis:7-alpine
    command: redis-server --appendonly yes
    volumes:
      - redis-data:/data
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.role == manager
    networks:
      - async-network

networks:
  async-network:
    driver: overlay

volumes:
  redis-data:
```

## Kubernetes Deployment

### Kubernetes Deployment YAML

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: asyncendpoints-app
  labels:
    app: asyncendpoints-app
spec:
  replicas: 3
  selector:
    matchLabels:
      app: asyncendpoints-app
  template:
    metadata:
      labels:
        app: asyncendpoints-app
    spec:
      containers:
      - name: app
        image: asyncendpoints:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: redis-secret
              key: connection-string
        - name: Logging__LogLevel__Default
          value: "Information"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
        startupProbe:
          httpGet:
            path: /health
            port: 8080
          failureThreshold: 30
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: asyncendpoints-service
spec:
  selector:
    app: asyncendpoints-app
  ports:
    - protocol: TCP
      port: 80
      targetPort: 8080
  type: LoadBalancer
```

### Kubernetes ConfigMap for Configuration

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: asyncendpoints-config
data:
  appsettings.Production.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "ConnectionStrings": {
        "Redis": "redis-service:6379"
      },
      "AsyncEndpoints": {
        "WorkerConfigurations": {
          "MaximumConcurrency": 4,
          "MaximumQueueSize": 1000,
          "PollingIntervalMs": 2000
        },
        "JobManagerConfiguration": {
          "DefaultMaxRetries": 3,
          "RetryDelayBaseSeconds": 2.0
        }
      }
    }
```

### Kubernetes StatefulSet for Redis

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: redis
spec:
  serviceName: redis-service
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
      - name: redis
        image: redis:7-alpine
        ports:
        - containerPort: 6379
        command:
          - redis-server
          - --appendonly
          - "yes"
        volumeMounts:
        - name: redis-storage
          mountPath: /data
  volumeClaimTemplates:
  - metadata:
      name: redis-storage
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 1Gi
---
apiVersion: v1
kind: Service
metadata:
  name: redis-service
spec:
  ports:
  - port: 6379
    targetPort: 6379
  selector:
    app: redis
```

## Configuration Management

### Environment-Specific Configuration

```json
// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "AsyncEndpoints": "Information"
    }
  },
  "ConnectionStrings": {
    "Redis": "redis-cluster-endpoint:6379,password=your-redis-password"
  },
  "AsyncEndpoints": {
    "WorkerConfigurations": {
      "MaximumConcurrency": 8,
      "MaximumQueueSize": 2000,
      "PollingIntervalMs": 3000,
      "JobTimeoutMinutes": 120
    },
    "JobManagerConfiguration": {
      "DefaultMaxRetries": 5,
      "RetryDelayBaseSeconds": 2.0,
      "MaxConcurrentJobs": 100,
      "JobPollingIntervalMs": 1000,
      "MaxClaimBatchSize": 20
    }
  }
}
```

### Configuration from Environment Variables

```csharp
// Program.cs with environment variable configuration
var builder = WebApplication.CreateBuilder(args);

// Allow configuration from environment variables
builder.Configuration.AddEnvironmentVariables();

// Configure AsyncEndpoints with environment variables or appsettings
builder.Services.AddAsyncEndpoints(options =>
{
    // Use environment variables with defaults
    options.WorkerConfigurations.MaximumConcurrency = 
        int.Parse(builder.Configuration["ASYNC_WORKER_CONCURRENCY"] ?? Environment.ProcessorCount.ToString());
    
    options.WorkerConfigurations.MaximumQueueSize = 
        int.Parse(builder.Configuration["ASYNC_QUEUE_SIZE"] ?? "1000");
    
    options.WorkerConfigurations.PollingIntervalMs = 
        int.Parse(builder.Configuration["ASYNC_POLLING_INTERVAL"] ?? "2000");
    
    options.JobManagerConfiguration.DefaultMaxRetries = 
        int.Parse(builder.Configuration["ASYNC_MAX_RETRIES"] ?? "3");
});
```

### Secret Management

```yaml
# Kubernetes Secret for sensitive configuration
apiVersion: v1
kind: Secret
metadata:
  name: asyncendpoints-secrets
type: Opaque
data:
  redis-password: <base64-encoded-password>
  api-key: <base64-encoded-api-key>
```

## Health Checks and Monitoring

### Health Check Configuration

```csharp
// Health check configuration in Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<AsyncEndpointsHealthCheck>("asyncendpoints", timeout: TimeSpan.FromSeconds(5))
    .AddRedis(builder.Configuration.GetConnectionString("Redis"), name: "redis")
    .AddProcessAllocatedMemoryHealthCheck(maximumMegabytes: 500);

var app = builder.Build();

// Add health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Custom health check
public class AsyncEndpointsHealthCheck : IHealthCheck
{
    private readonly IJobStore _jobStore;
    private readonly ILogger<AsyncEndpointsHealthCheck> _logger;

    public AsyncEndpointsHealthCheck(IJobStore jobStore, ILogger<AsyncEndpointsHealthCheck> logger)
    {
        _jobStore = jobStore;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test job store connectivity
            await _jobStore.GetQueueDepthAsync(cancellationToken);
            
            return HealthCheckResult.Healthy("AsyncEndpoints job store is accessible");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AsyncEndpoints health check failed");
            return HealthCheckResult.Unhealthy("AsyncEndpoints job store is not accessible", ex);
        }
    }
}
```

### Application Insights Integration

```csharp
// Application Insights for monitoring
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<AsyncEndpointsConfigurations>(options =>
{
    // Configure for monitoring
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        // Add telemetry
        var telemetryClient = context.RequestServices.GetService<TelemetryClient>();
        telemetryClient?.TrackEvent("JobSubmitted", new Dictionary<string, string>
        {
            ["JobId"] = job.Id.ToString(),
            ["JobName"] = job.Name
        });

        return Results.Accepted($"/jobs/{job.Id}", job);
    };
});
```

## Scaling Strategies

### Horizontal Pod Autoscaling

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: asyncendpoints-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: asyncendpoints-app
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

### Queue-Based Scaling

```csharp
// Custom metrics for queue-based scaling
public class QueueMetricsService
{
    private readonly IJobStore _jobStore;
    private readonly ILogger<QueueMetricsService> _logger;
    private readonly Meter _meter;
    private readonly Gauge<int> _queueDepthGauge;

    public QueueMetricsService(IJobStore jobStore, ILogger<QueueMetricsService> logger)
    {
        _jobStore = jobStore;
        _logger = logger;
        _meter = new Meter("AsyncEndpoints.QueueMetrics");
        _queueDepthGauge = _meter.CreateGauge<int>("async_endpoint.queue.depth", description: "Current queue depth");
    }

    public async Task UpdateQueueMetrics()
    {
        try
        {
            var queueDepth = await GetQueueDepth();
            _queueDepthGauge.Record(queueDepth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating queue metrics");
        }
    }

    private async Task<int> GetQueueDepth()
    {
        // Implementation to get current queue depth
        return 0; // Placeholder
    }
}
```

## Zero-Downtime Deployment

### Blue-Green Deployment Strategy

```yaml
# Blue deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: asyncendpoints-blue
spec:
  replicas: 3
  selector:
    matchLabels:
      app: asyncendpoints-blue
  template:
    metadata:
      labels:
        app: asyncendpoints-blue
    spec:
      containers:
      - name: app
        image: asyncendpoints:v1.0.0
---
# Green deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: asyncendpoints-green
spec:
  replicas: 3
  selector:
    matchLabels:
      app: asyncendpoints-green
  template:
    metadata:
      labels:
        app: asyncendpoints-green
    spec:
      containers:
      - name: app
        image: asyncendpoints:v1.1.0
---
# Service routing to blue
apiVersion: v1
kind: Service
metadata:
  name: asyncendpoints-service
spec:
  selector:
    app: asyncendpoints-blue  # Switch to "asyncendpoints-green" for rollout
  ports:
  - port: 80
    targetPort: 8080
```

### Rolling Updates Configuration

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: asyncendpoints-app
spec:
  replicas: 6
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 1    # Only 1 pod can be unavailable during update
      maxSurge: 1         # Only 1 pod can be created above desired count
  selector:
    matchLabels:
      app: asyncendpoints-app
  template:
    metadata:
      labels:
        app: asyncendpoints-app
    spec:
      containers:
      - name: app
        image: asyncendpoints:latest
        # Graceful shutdown configuration
        lifecycle:
          preStop:
            exec:
              command: ["/bin/sh", "-c", "sleep 30"]
```

## Operational Considerations

### Graceful Shutdown

```csharp
// Configure graceful shutdown
var builder = WebApplication.CreateBuilder(args);

// Set longer shutdown timeout for async operations
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

// Add graceful shutdown handling
var app = builder.Build();

// Configure graceful shutdown
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Application is shutting down gracefully...");
    // Any cleanup code here
});

app.Run();
```

### Backup and Recovery

```bash
# Redis backup script
#!/bin/bash
# backup-redis.sh
REDIS_HOST="redis-service"
BACKUP_DIR="/backup"
DATE=$(date +%Y%m%d_%H%M%S)

# Create backup
kubectl exec -it redis-0 -- redis-cli BGSAVE
sleep 10  # Wait for BGSAVE to complete

# Copy RDB file to backup location
kubectl cp redis-redis-0:/data/dump.rdb ${BACKUP_DIR}/redis_backup_${DATE}.rdb
```

### Disaster Recovery

```csharp
// Recovery mechanism for critical failures
public class DisasterRecoveryService
{
    private readonly IJobStore _jobStore;
    private readonly IJobManager _jobManager;
    private readonly ILogger<DisasterRecoveryService> _logger;

    public async Task RecoverStuckJobsAsync()
    {
        try
        {
            // Identify jobs that have been stuck for too long
            var stuckJobs = await _jobStore.GetJobsStuckFor(TimeSpan.FromHours(2));
            
            foreach (var job in stuckJobs)
            {
                _logger.LogWarning("Recovering stuck job {JobId}", job.Id);
                
                // Reset job for retry
                job.UpdateStatus(JobStatus.Queued, DateTimeProvider.Instance);
                job.WorkerId = null;
                
                await _jobStore.UpdateJob(job, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disaster recovery");
        }
    }
}
```

## Deployment Best Practices

1. **Use External Storage**: Always use Redis or another external storage for production
2. **Configure Limits**: Set appropriate resource limits in containers
3. **Health Checks**: Implement comprehensive health checks
4. **Monitoring**: Set up monitoring and alerting
5. **Backup Strategy**: Implement regular backups
6. **Graceful Handling**: Configure proper shutdown handling
7. **Environment Separation**: Use different configurations for each environment
8. **Security**: Implement proper security measures including secrets management

Deployment of AsyncEndpoints applications should prioritize reliability, scalability, and operational efficiency while maintaining the asynchronous processing capabilities that make the library valuable.