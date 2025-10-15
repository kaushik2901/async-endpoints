---
sidebar_position: 1
---

# Introduction to AsyncEndpoints

**AsyncEndpoints** is a modern, lightweight framework for building asynchronous endpoints in .NET applications with clean architecture and minimal boilerplate.

## What is AsyncEndpoints?

AsyncEndpoints provides a structured approach to define and execute asynchronous operations in your .NET applications. Unlike traditional controllers, it offers a streamlined way to handle async operations with clear separation of concerns.

## Core Benefits

- **Async-First**: Designed specifically for asynchronous operations from the ground up
- **Clean Architecture**: Follows SOLID principles and modern .NET architectural patterns
- **Minimal Overhead**: Lightweight with no unnecessary dependencies
- **Testable Design**: Built with testability in mind for reliable code
- **Flexible Integration**: Easy to integrate with existing .NET applications

## Quick Example

Here's a simple example of what an AsyncEndpoint looks like:

```csharp
public class GetUserEndpoint : IAsyncEndpoint<int, User>
{
    public async Task<User> ExecuteAsync(int userId)
    {
        // Your async logic here
        var user = await userRepository.GetByIdAsync(userId);
        return user;
    }
}
```

## Next Steps

Continue to the next sections to learn how to install, configure, and use AsyncEndpoints in your projects.
