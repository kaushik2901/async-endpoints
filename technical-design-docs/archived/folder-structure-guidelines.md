# AsyncEndpoints Library Structure Guidelines

## Overview

This document outlines the recommended project structure for the AsyncEndpoints library, following professional patterns observed in successful .NET NuGet packages like ASP.NET Core, Entity Framework, MediatR, and other Microsoft.Extensions libraries.

## Professional .NET Library Structure Patterns

Based on analysis of successful .NET libraries, common patterns include:

### 1. Feature-Based Organization
- Group functionality by feature area rather than architectural layer
- Keep related types together (interfaces, implementations, models)
- Use clear, descriptive folder names that reflect the feature

### 2. Public API Focus
- Organize for library consumer experience
- Keep public APIs easily discoverable
- Separate internal implementation details appropriately

### 3. Minimal, Intuitive Structure
- Avoid over-architecting for a library context
- Use simple, clear organization that matches library purpose
- Follow naming conventions consistent with .NET ecosystem

### 4. Extension-Friendly Design
- Separate core functionality from extension points
- Support optional features through separate packages
- Organize extension methods logically

## Recommended Professional Structure

The following structure follows patterns used by successful .NET libraries:

```
AsyncEndpoints/
├── .editorconfig
├── .gitattributes
├── .gitignore
├── global.json
├── Directory.Build.props
├── AsyncEndpoints.sln
├── LICENSE
├── README.md
├── CONTRIBUTING.md
├── async-endpoints.png
├── docs/
│   ├── api/
│   ├── articles/
│   │   ├── getting-started.md
│   │   └── advanced-usage.md
│   └── contributing/
├── src/
│   ├── AsyncEndpoints/
│   │   ├── AsyncEndpoints.csproj
│   │   ├── JobProcessing/
│   │   │   ├── Job.cs
│   │   │   ├── JobStatus.cs
│   │   │   ├── IJobStore.cs
│   │   │   ├── InMemoryJobStore.cs
│   │   │   ├── JobManager.cs
│   │   │   └── IJobManager.cs
│   │   ├── Handlers/
│   │   │   ├── IAsyncEndpointRequestHandler.cs
│   │   │   ├── IAsyncEndpointRequestDelegate.cs
│   │   │   ├── AsyncEndpointRequestDelegate.cs
│   │   │   └── HandlerRegistration.cs
│   │   ├── Background/
│   │   │   ├── IJobProducerService.cs
│   │   │   ├── JobProducerService.cs
│   │   │   ├── IJobConsumerService.cs
│   │   │   ├── JobConsumerService.cs
│   │   │   ├── IJobProcessorService.cs
│   │   │   ├── JobProcessorService.cs
│   │   │   ├── IHandlerExecutionService.cs
│   │   │   ├── HandlerExecutionService.cs
│   │   │   └── AsyncEndpointsBackgroundService.cs
│   │   ├── Infrastructure/
│   │   │   ├── IDateTimeProvider.cs
│   │   │   ├── DateTimeProvider.cs
│   │   │   ├── ISerializer.cs
│   │   │   └── AsyncEndpointsJsonSerializationContext.cs
│   │   ├── Configuration/
│   │   │   ├── AsyncEndpointsConfigurations.cs
│   │   │   ├── AsyncEndpointsConstants.cs
│   │   │   └── ResponseConfigurations.cs
│   │   ├── Extensions/
│   │   │   ├── ServiceCollectionExtensions.cs
│   │   │   ├── RouteBuilderExtensions.cs
│   │   │   └── HttpContextExtensions.cs
│   │   ├── Utilities/
│   │   │   ├── MethodResult.cs
│   │   │   ├── AsyncEndpointError.cs
│   │   │   ├── ErrorClassifier.cs
│   │   │   ├── JobResponse.cs
│   │   │   ├── JobResponseMapper.cs
│   │   │   ├── ResponseDefaults.cs
│   │   │   ├── ExceptionInfo.cs
│   │   │   ├── InnerExceptionInfo.cs
│   │   │   ├── ExceptionSerializer.cs
│   │   │   └── AsyncContextBuilder.cs
│   │   └── Properties/
│   └── AsyncEndpoints.Redis/
│       ├── AsyncEndpoints.Redis.csproj
│       ├── RedisJobStore.cs
│       ├── RedisConfiguration.cs
│       └── Extensions/
│           └── RedisServiceCollectionExtensions.cs
├── tests/
│   ├── AsyncEndpoints.UnitTests/
│   │   ├── AsyncEndpoints.UnitTests.csproj
│   │   ├── JobProcessing/
│   │   │   ├── JobTests.cs
│   │   │   ├── InMemoryJobStoreTests.cs
│   │   │   └── JobManagerTests.cs
│   │   ├── Handlers/
│   │   │   ├── AsyncEndpointRequestDelegateTests.cs
│   │   │   └── HandlerRegistrationTests.cs
│   │   ├── Background/
│   │   │   ├── JobProducerServiceTests.cs
│   │   │   ├── JobConsumerServiceTests.cs
│   │   │   └── HandlerExecutionServiceTests.cs
│   │   ├── Infrastructure/
│   │   │   ├── DateTimeProviderTests.cs
│   │   │   └── SerializationTests.cs
│   │   ├── Configuration/
│   │   │   ├── AsyncEndpointsConfigurationsTests.cs
│   │   │   └── ResponseConfigurationsTests.cs
│   │   ├── Extensions/
│   │   │   ├── ServiceCollectionExtensionsTests.cs
│   │   │   ├── RouteBuilderExtensionsTests.cs
│   │   │   └── HttpContextExtensionsTests.cs
│   │   ├── Utilities/
│   │   │   ├── MethodResultTests.cs
│   │   │   ├── AsyncEndpointErrorTests.cs
│   │   │   └── ResponseDefaultsTests.cs
│   │   └── TestSupport/
│   └── AsyncEndpoints.Redis.UnitTests/
│       ├── AsyncEndpoints.Redis.UnitTests.csproj
│       ├── RedisJobStoreTests.cs
│       └── RedisJobStoreExceptionTests.cs
├── samples/
│   ├── BasicUsage/
│   ├── RedisExample/
│   └── AdvancedConfiguration/
├── tools/
└── build/
    ├── Build.ps1
    ├── Build.csproj
    └── Directory.Build.targets
```

## Key Professional Patterns Applied

### 1. Feature-Based Grouping
- `JobProcessing/` - All job-related functionality
- `Handlers/` - All handler-related functionality  
- `Background/` - All background processing functionality
- `Infrastructure/` - Common infrastructure concerns
- `Configuration/` - All configuration-related types
- `Extensions/` - All ASP.NET Core extension methods
- `Utilities/` - All utility functionality

### 2. Interface-Implementation Proximity
- Interfaces and their implementations are grouped together by feature area
- This makes the API surface clear and discoverable
- Follows the pattern used in Microsoft libraries

### 3. Clear Public vs. Internal Separation
- Public APIs are organized for consumer discoverability
- Implementation details are grouped logically
- Internal utilities are separated appropriately

### 4. Extension Package Organization
- `AsyncEndpoints.Redis` package follows the same organizational principles
- Extension methods in dedicated `Extensions/` folder
- Maintains consistency with main package structure

### 5. Professional Build and CI Structure
- Uses `Directory.Build.props` for shared build configuration
- Clear separation of samples, tests, and source code
- Professional documentation structure

## Comparison with Current Structure

The current structure is already reasonably organized, but this professional structure:

1. Groups interfaces with their implementations for better discoverability
2. Uses feature-based names that match the library's purpose
3. Follows naming conventions consistent with .NET ecosystem
4. Provides clear separation between different concerns
5. Organizes for library consumer experience

## Benefits of Professional Structure

1. **Industry Standard**: Follows patterns used by successful .NET libraries
2. **Discoverability**: Related functionality is grouped intuitively
3. **Maintainability**: Changes within a feature area are localized
4. **Familiarity**: .NET developers will find the structure familiar
5. **Scalability**: Clear organization supports future growth

This structure aligns with professional .NET library patterns while maintaining the functional cohesion needed for the AsyncEndpoints library.

## Implementation Status

The recommended structure has been successfully implemented in the AsyncEndpoints project. All source files and test files have been reorganized according to the professional structure guidelines. The implementation includes:

- All main source code files moved to feature-based directories (JobProcessing, Handlers, Background, Infrastructure, Configuration, Extensions, Utilities)
- Redis extension project reorganized consistently with the main project structure
- All test files reorganized to match the source structure
- Namespace updates applied to all moved files
- All tests pass successfully after restructuring
- Solution builds without errors

The restructuring maintains full backward compatibility and doesn't affect the public API of the library - only the internal organization has changed.