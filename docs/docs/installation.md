---
title: "Installation"
description: "Install AsyncEndpoints NuGet package in your .NET project. Step-by-step installation guide for async processing with background jobs and job tracking."
keywords: ["install asyncendpoints", "async endpoints nuget", ".NET async processing", "background job installation", "C# queue setup", "async endpoint setup"]
sidebar_position: 2
---

# Installation

This page provides detailed instructions for installing and setting up AsyncEndpoints in your .NET application.

## Prerequisites

Before installing AsyncEndpoints, ensure your development environment meets the following requirements:

- **.NET Runtime**: .NET 8.0, .NET 9.0, or .NET 10.0 (the library is built for all target frameworks)
- **Development Tools**: Visual Studio 2022, Visual Studio Code, or equivalent IDE
- **Package Manager**: NuGet package manager (ships with .NET)

The library supports both development and production scenarios with optional Redis integration for distributed deployments.

## Installing Core Package

The core AsyncEndpoints package provides all the essential functionality for creating asynchronous endpoints. Install it using one of these methods:

### Using .NET CLI

```bash
dotnet add package AsyncEndpoints
```

### Using Package Manager Console

```powershell
Install-Package AsyncEndpoints
```

### Using PackageReference

```xml
<PackageReference Include="AsyncEndpoints" Version="1.1.1-alpha" />
```

## Installing Redis Extension (Optional)

For production deployments with distributed processing and persistence, install the Redis extension:

### Using .NET CLI

```bash
dotnet add package AsyncEndpoints.Redis
```

### Using Package Manager Console

```powershell
Install-Package AsyncEndpoints.Redis
```

### Using PackageReference

```xml
<PackageReference Include="AsyncEndpoints.Redis" Version="1.1.1-alpha" />
```

## Verify Installation

After installation, you can verify the package is properly added by creating a minimal working example:

```csharp
using AsyncEndpoints;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

// Add core AsyncEndpoints services
builder.Services
    .AddAsyncEndpoints() // Core services
    .AddAsyncEndpointsInMemoryStore() // Development storage
    .AddAsyncEndpointsWorker(); // Background processing

var app = builder.Build();

// Define a simple async endpoint
app.MapAsyncPost<SimpleRequest>("SimpleJob", "/api/simple-job");
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}"); // Job status endpoint

// Simple request model
public class SimpleRequest
{
    public string Message { get; set; } = string.Empty;
}

app.Run();
```

## Package Contents

The core package includes:

- **Core Services**: Job management, background processing, and state management
- **HTTP Integration**: Extension methods for mapping async endpoints
- **Storage Interfaces**: Abstract storage layer with in-memory implementation
- **Configuration**: Flexible configuration system with defaults
- **Utilities**: Helper classes and error handling utilities
- **Serialization**: Built-in serialization support

The Redis extension package includes:

- **Redis Storage**: Production-ready Redis-based job storage
- **Distributed Recovery**: Automatic recovery for stuck jobs in multi-instance deployments
- **Connection Management**: Robust Redis connection handling with reconnection logic
- **Lua Scripts**: Optimized Lua scripts for atomic operations

## Common Installation Issues

### Target Framework Mismatch

If you encounter target framework errors, ensure your project targets .NET 8.0, .NET 9.0, or .NET 10.0:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>  <!-- Update to net8.0 or net9.0 as needed -->
  </PropertyGroup>
</Project>
```

### Missing Dependencies

AsyncEndpoints requires the following .NET components:

- `Microsoft.AspNetCore.App` framework reference
- .NET 8.0, 9.0, or 10.0 SDK

## Next Steps

After successful installation, continue to the [Quick Start](./quick-start.md) guide to learn how to implement your first async endpoint.