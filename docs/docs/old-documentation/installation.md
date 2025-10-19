---
sidebar_position: 2
---

# Installation

## Prerequisites

AsyncEndpoints requires:
- .NET 8.0 or .NET 9.0
- Visual Studio 2022 or Visual Studio Code with .NET support
- ASP.NET Core runtime

## Core Package

Install the core AsyncEndpoints package:

```bash
dotnet add package AsyncEndpoints
```

## Redis Extension

If you want to use Redis as your job store for production deployments with distributed processing, install the Redis extension:

```bash
dotnet add package AsyncEndpoints.Redis
```

## Verify Installation

After installation, you can verify by creating a minimal setup in your `Program.cs`:

```csharp
using AsyncEndpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsWorker();

var app = builder.Build();

await app.RunAsync();
```

## Package Contents

The AsyncEndpoints package includes:

- Core async endpoint infrastructure
- Background job processing services
- In-memory job storage (for development)
- HTTP endpoint mapping extensions
- Configuration options
- Job status tracking
- Retry mechanisms with backoff
- Error handling utilities

The AsyncEndpoints.Redis package adds:

- Redis-based distributed storage
- Multi-instance job coordination
- Distributed job recovery features
- Production-ready persistence