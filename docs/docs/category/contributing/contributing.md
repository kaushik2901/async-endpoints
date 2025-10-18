---
sidebar_position: 1
title: Contributing
---

# Contributing to AsyncEndpoints

We welcome contributions from the community and are grateful for your efforts to make this project better. This guide provides information on how to contribute to the AsyncEndpoints project.

## Code of Conduct

By participating in this project, you agree to abide by our Code of Conduct. Please be respectful and considerate in all interactions. We pledge to create a harassment-free experience for everyone, regardless of experience level, gender, gender identity and expression, sexual orientation, disability, personal appearance, body size, race, ethnicity, age, religion, or nationality.

## How Can I Contribute?

### Reporting Bugs

- Use the issue tracker to report bugs
- Check if the issue has already been reported before creating a new one
- Provide as much detail as possible, including:
  - Steps to reproduce the issue
  - Expected vs. actual behavior
  - .NET version and operating system
  - Any relevant stack traces or error messages
  - Sample code that reproduces the issue

### Suggesting Features

- Use the issue tracker to suggest new features or enhancements
- Explain the use case and potential implementation approaches
- Consider the project's goals and scope
- Provide examples of how the feature would be used
- Consider backward compatibility implications

### Pull Requests

1. Fork the repository
2. Create a new branch for your feature or bug fix
3. Add tests if applicable (bug fixes and new features should have tests)
4. Ensure all tests pass
5. Update documentation if your changes affect public APIs
6. Submit a pull request with a clear description of your changes
7. Make sure your code follows the project's coding standards

## Development Setup

### Prerequisites

- .NET 8.0 or .NET 9.0 SDK
- Git
- An IDE or text editor of your choice (Visual Studio, Visual Studio Code, etc.)

### Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/kaushik2901/async-endpoints.git
   cd async-endpoints
   ```

2. Navigate to the project directory:
   ```bash
   cd AsyncEndpoints
   ```

3. Restore the dependencies:
   ```bash
   dotnet restore
   ```

4. Build the project:
   ```bash
   dotnet build
   ```

5. Run the tests:
   ```bash
   dotnet test
   ```

### Running the Documentation Site

1. Navigate to the docs directory:
   ```bash
   cd docs
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Start the development server:
   ```bash
   npm start
   ```

## Project Structure

The AsyncEndpoints repository is organized as follows:

```
AsyncEndpoints/
├── AsyncEndpoints.sln                    # Main solution file
├── src/
│   ├── AsyncEndpoints/                   # Core library
│   │   ├── Background/                   # Background service implementations
│   │   ├── Configuration/                # Configuration classes
│   │   ├── Extensions/                   # Extension methods
│   │   ├── Handlers/                     # Request handler interfaces
│   │   ├── Infrastructure/               # Core infrastructure components
│   │   ├── JobProcessing/                # Job processing logic
│   │   └── Utilities/                    # Utility classes
│   └── AsyncEndpoints.Redis/             # Redis integration library
├── tests/
│   ├── AsyncEndpoints.UnitTests/         # Unit tests for core library
│   └── AsyncEndpoints.Redis.UnitTests/   # Unit tests for Redis library
├── examples/                             # Example applications
├── docs/                                 # Documentation site
└── technical-design-docs/                # Technical design documents
```

### Core Library (AsyncEndpoints)

The main library contains:
- Background processing services for async job execution
- Configuration system for workers and job management
- Extension methods for mapping async endpoints
- Job processing and state management
- HTTP context preservation utilities

### Redis Integration (AsyncEndpoints.Redis)

The Redis library provides:
- Redis-based job storage implementation
- Distributed job recovery mechanisms
- Lua script optimizations for atomic operations

## Code Style

### C# Coding Conventions

- Follow .NET C# coding conventions
- Use meaningful variable and method names
- Write clear, concise comments for complex logic
- Include XML documentation for public APIs
- Maintain consistency with existing code style

### Example of Good Code Style

```csharp
/// <summary>
/// Processes the data asynchronously using the provided context.
/// </summary>
/// <param name="context">The async context containing request and HTTP context information.</param>
/// <param name="token">Cancellation token to cancel the operation.</param>
/// <returns>A method result containing the processed data or error information.</returns>
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var request = context.Request;
    
    try
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Data))
        {
            return MethodResult<ProcessResult>.Failure(
                AsyncEndpointError.FromCode("INVALID_REQUEST", "Data field is required")
            );
        }
        
        // Process the request
        var result = await ProcessRequestInternalAsync(request, token);
        
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing request: {RequestData}", request.Data);
        return MethodResult<ProcessResult>.Failure(ex);
    }
}
```

## Testing

### Unit Tests

- Add unit tests for new features and bug fixes
- Use descriptive test method names that explain the expected behavior
- Follow the AAA (Arrange, Act, Assert) pattern
- Test both success and failure scenarios

### Test Structure

```csharp
[Fact]
public async Task HandleAsync_WhenRequestIsValid_ReturnsSuccessResult()
{
    // Arrange
    var handler = new ProcessDataHandler(mockLogger.Object);
    var request = new DataRequest { Data = "test data" };
    var context = CreateValidContext(request);
    
    // Act
    var result = await handler.HandleAsync(context, CancellationToken.None);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Data);
}
```

### Integration Tests

- Test the integration between components
- Use mocking to isolate the component under test
- Ensure tests are deterministic and don't depend on external services

## Documentation

### API Documentation

- Include XML documentation for all public APIs
- Document parameters, return values, and exceptions
- Provide usage examples where helpful

### User Documentation

- Update the documentation site if your changes affect public APIs
- Provide clear examples of how to use new features
- Follow the documentation structure outlined in the technical design docs

## Pull Request Process

### Before Submitting

1. Ensure all tests pass
2. Add new tests for your changes
3. Update documentation as needed
4. Verify that the build passes locally
5. Follow the project's coding standards

### Submitting a Pull Request

1. Push your branch to your fork
2. Open a pull request to the main repository
3. Provide a clear description of your changes
4. Reference any related issues
5. Wait for review and address feedback

### Review Process

- Pull requests require at least one approval
- Code reviewers will check for:
  - Correctness of the implementation
  - Adherence to coding standards
  - Adequate test coverage
  - Proper documentation
  - Performance implications
  - Security considerations

## Security

### Reporting Security Issues

If you discover a security vulnerability, please report it responsibly by contacting the maintainers directly instead of opening a public issue. Security issues should be handled discreetly to prevent potential exploitation.

### Security Best Practices

- Validate all input data
- Handle sensitive information appropriately
- Use secure coding practices
- Consider potential attack vectors in your code

## Community

### Getting Help

- Check the documentation first
- Search existing issues for similar problems
- Ask questions in the issue tracker
- Be patient and respectful when seeking help

### Communication

- Be respectful and constructive in all interactions
- Provide helpful feedback during code reviews
- Welcome newcomers to the project
- Share knowledge and experience with others

## Maintainer Guidelines

### Issue Triage

Maintainers should:
- Review new issues promptly
- Label issues appropriately
- Provide initial guidance to contributors
- Close issues that are not actionable

### Pull Request Review

Maintainers should:
- Review pull requests in a timely manner
- Provide constructive feedback
- Ensure code quality standards are met
- Test changes when necessary

Thank you for your interest in contributing to AsyncEndpoints! Your contributions help make the project better for everyone.