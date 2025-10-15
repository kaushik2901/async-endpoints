---
title: AsyncEndpoints Examples
---

# AsyncEndpoints Examples

This page demonstrates various examples of using AsyncEndpoints in .NET applications.

## Basic Usage

Here's a simple example of defining an async endpoint:

```csharp
public class MyEndpoint : IAsyncEndpoint
{
    public async Task<string> ExecuteAsync()
    {
        // Your async logic here
        await Task.Delay(1000); // Simulate async work
        return "Hello from AsyncEndpoints!";
    }
}
```

## More Examples

Check back soon for more detailed examples of AsyncEndpoints usage in various scenarios.
