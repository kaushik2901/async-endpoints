---
sidebar_position: 16
---

# Contributing

## Overview

We welcome contributions from the community! This guide explains how to contribute to the AsyncEndpoints project, including reporting bugs, suggesting features, and submitting code changes.

## Code of Conduct

By participating in this project, you agree to abide by our Code of Conduct. Please be respectful and considerate in all interactions.

## How Can I Contribute?

### Reporting Bugs

- Use the issue tracker to report bugs
- Check if the issue has already been reported before creating a new one
- Provide as much detail as possible, including:
  - Steps to reproduce the issue
  - Expected vs. actual behavior
  - .NET version and operating system
  - Any relevant stack traces or error messages

### Suggesting Features

- Use the issue tracker to suggest new features or enhancements
- Explain the use case and potential implementation approaches
- Consider the project's goals and scope
- Provide examples of how the feature would be used

### Pull Requests

1. Fork the repository
2. Create a new branch for your feature or bug fix
3. Add tests if applicable
4. Ensure all tests pass
5. Submit a pull request with a clear description of your changes

## Development Setup

### Prerequisites

- .NET 8.0 or .NET 9.0 SDK
- Visual Studio 2022 or Visual Studio Code with .NET support
- Git

### Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/kaushik2901/async-endpoints.git
   ```

2. Navigate to the project directory:
   ```bash
   cd AsyncEndpoints
   ```

3. Open in Visual Studio or VS Code:
   ```bash
   # Using Visual Studio
   start AsyncEndpoints.sln
   
   # Or using VS Code
   code .
   ```

4. Build the project:
   ```bash
   dotnet build
   ```

5. Run the tests:
   ```bash
   dotnet test
   ```

### Running Examples

The project includes example applications to test functionality:

1. Navigate to the example directories:
   ```bash
   # In-Memory example
   cd examples/InMemoryExampleAPI/InMemoryExampleAPI
   
   # Redis example
   cd examples/RedisExampleAPI/RedisExampleAPI
   ```

2. Run the example:
   ```bash
   dotnet run
   ```

## Project Structure

```
AsyncEndpoints/
├── src/
│   ├── AsyncEndpoints/          # Core library
│   │   ├── Background/          # Background service implementations
│   │   ├── Configuration/       # Configuration classes
│   │   ├── Extensions/          # Extension methods
│   │   ├── Handlers/            # Handler interfaces and context
│   │   ├── Infrastructure/      # Core infrastructure
│   │   ├── JobProcessing/       # Job processing logic
│   │   └── Utilities/           # Utility classes
│   └── AsyncEndpoints.Redis/    # Redis extension
│       ├── Configuration/
│       ├── Extensions/
│       ├── Services/
│       └── Storage/
├── examples/
│   ├── InMemoryExampleAPI/      # Example using in-memory store
│   └── RedisExampleAPI/         # Example using Redis store
├── tests/
│   └── AsyncEndpoints.UnitTests/ # Unit tests
├── docs/                        # Documentation
└── technical- design-docs/      # Technical design documents
```

## Code Style

### C# Coding Standards

- Follow .NET C# coding conventions
- Use meaningful variable and method names
- Write clear, concise comments for complex logic
- Include XML documentation for public APIs
- Maintain consistency with existing code style

### Naming Conventions

- Use PascalCase for public members and types
- Use camelCase for local variables and private members
- Use meaningful and descriptive names
- Follow common .NET naming patterns

### Example

```csharp
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;

/// <summary>
/// Handles data processing requests asynchronously.
/// </summary>
public class DataProcessingHandler : IAsyncEndpointRequestHandler<DataRequest, DataResponse>
{
    private readonly ILogger<DataProcessingHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataProcessingHandler"/> class.
    /// </summary>
    public DataProcessingHandler(ILogger<DataProcessingHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the asynchronous request and returns a result.
    /// </summary>
    /// <param name="context">The context containing the request object and associated HTTP context information.</param>
    /// <param name="token">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MethodResult{TResponse}"/> containing the result of the operation.</returns>
    public async Task<MethodResult<DataResponse>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        try
        {
            _logger.LogInformation("Processing data request");
            
            var result = await ProcessData(context.Request, token);
            
            return MethodResult<DataResponse>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data request");
            return MethodResult<DataResponse>.Failure(ex);
        }
    }
}
```

## Testing

### Unit Tests

- Add unit tests for new features
- Ensure all tests pass before submitting a pull request
- Write tests that cover edge cases and error conditions
- Use appropriate test frameworks (xUnit, Moq, etc.)

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with code coverage (if supported)
dotnet test --collect:"XPlat Code Coverage"
```

### Test Structure

Follow the given-when-then pattern:

```csharp
[Fact]
public async Task HandleAsync_WithValidRequest_ReturnsSuccess()
{
    // Arrange
    var handler = new MyHandler();
    var request = new MyRequest { Data = "test" };
    var context = new AsyncContext<MyRequest>(request, new Dictionary<string, List<string?>>(), 
        new Dictionary<string, object?>(), new List<KeyValuePair<string, List<string?>>>());

    // Act
    var result = await handler.HandleAsync(context, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Data);
}
```

## Documentation

### API Documentation

All public APIs should have XML documentation:

```csharp
/// <summary>
/// Maps an asynchronous POST endpoint that processes requests in the background.
/// </summary>
/// <typeparam name="TRequest">The type of the request object.</typeparam>
/// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
/// <param name="jobName">A unique name for the async job, used for identifying the handler.</param>
/// <param name="pattern">The URL pattern for the endpoint.</param>
/// <param name="handler">Optional custom handler function to process the request.</param>
/// <returns>An <see cref="IEndpointConventionBuilder"/> that can be used to further configure the endpoint.</returns>
public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
    this IEndpointRouteBuilder endpoints,
    string jobName,
    string pattern,
    Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
{
    // Implementation
}
```

### User Documentation

Update user documentation in the `docs/` directory when adding new features or changing existing functionality.

## Submitting Changes

### Before Submitting

1. Ensure your changes follow the project's code style
2. Add or update tests as appropriate
3. Update documentation if needed
4. Run all tests and ensure they pass
5. Build the project and ensure there are no errors

### Creating a Pull Request

1. Push your changes to your fork
2. Submit a pull request with a clear description:
   - What changes are being made
   - Why these changes are needed
   - How the changes address the issue
3. Reference any relevant issues

### Pull Request Review Process

- PRs will be reviewed by maintainers
- Changes may be requested before merging
- Once approved, maintainers will merge the PR
- Your contribution will be included in the next release

## Types of Contributions

### Bug Fixes

- Identify bugs and submit fixes
- Include tests to prevent regression
- Update documentation if needed

### New Features

- Suggest and implement new features
- Ensure features align with project goals
- Add comprehensive documentation and tests

### Documentation

- Improve existing documentation
- Add examples and tutorials
- Fix typos and grammatical errors

### Performance Improvements

- Identify performance bottlenecks
- Implement optimizations
- Provide benchmarks showing improvements

## Questions?

If you have any questions about contributing, feel free to open an issue or contact the maintainers through the GitHub Discussions tab.

We appreciate your interest in contributing to AsyncEndpoints! Your contributions help make this project better for everyone.