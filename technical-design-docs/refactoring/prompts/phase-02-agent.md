# Agent Prompt: Phase 02 — Core ASP.NET Decoupling

## Role
You are implementing Phase 02 of the AsyncEndpoints architecture realignment. You remove all ASP.NET dependencies from the Core project by moving HTTP-specific code to AspNetCore.

## Key Architectural Context
The target dependency graph:
```
AspNetCore ──► Core ──► Abstractions (zero deps)
```
- `Core` must NOT have `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- `Core` must have zero `using Microsoft.AspNetCore.*` statements
- `AspNetCore` project keeps the FrameworkReference — it needs ASP.NET
- All HTTP-specific types (`HttpContext`, `IResult`, `ProblemDetails`, ASP.NET `JsonOptions`) must live in `AspNetCore`

## Current State Before Phase
- `Abstractions` is clean (Phase 01 done)
- `Core.csproj` has `FrameworkReference Include="Microsoft.AspNetCore.App"` (violation)
- Files in Core that use ASP.NET types:
  - `HttpContextExtensions.cs` — uses `HttpContext`
  - `Infrastructure/Serialization/IJsonBodyParserService.cs` — uses `HttpContext`
  - `Infrastructure/Serialization/JsonBodyParserService.cs` — uses `HttpContext`
  - `Configuration/AsyncEndpointsResponseConfigurations.cs` — references `IResult` and `HttpContext`
  - `Utilities/JobResponse.cs` — HTTP response DTO
  - `Utilities/JobResponseMapper.cs` — mapping logic
  - `Utilities/JobResultResponse.cs` — implements `IResult`
  - `Utilities/ResponseDefaults.cs` — references `IResult`
  - `Infrastructure/Serialization/Serializer.cs` — uses `IOptions<JsonOptions>` from ASP.NET
  - `Infrastructure/AsyncEndpointsJsonSerializationContext.cs` — references `ProblemDetails`

## Phase Goal
Move all HTTP-specific files from Core → AspNetCore. Refactor Serializer to use `System.Text.Json` directly. Remove FrameworkReference from Core.

## Task List

### 2.1 Remove FrameworkReference from `Core.csproj`
- Edit the csproj, remove `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- Build will fail — that's expected until files are moved

### 2.2 Move `HttpContextExtensions.cs`
- Move from `Core/HttpContextExtensions.cs` → `AspNetCore/Extensions/HttpContextExtensions.cs`
- Update namespace to `AsyncEndpoints.AspNetCore.Extensions`

### 2.3 Move `IJsonBodyParserService` + `JsonBodyParserService`
- Move from `Core/Infrastructure/Serialization/` → `AspNetCore/Serialization/`

### 2.4 Move `AsyncEndpointsResponseConfigurations.cs`
- Move from `Core/Configuration/` → `AspNetCore/Configuration/`

### 2.5 Move `JobResponse.cs` + `JobResponseMapper.cs`
- Move from `Core/Utilities/` → `AspNetCore/Models/`

### 2.6 Move `JobResultResponse.cs` + `ResponseDefaults.cs`
- Move from `Core/Utilities/` → `AspNetCore/Endpoints/`

### 2.7 Refactor `Serializer.cs`
- Replace `IOptions<JsonOptions>` (from `Microsoft.AspNetCore.Http.Json`) with `System.Text.Json.JsonSerializerOptions`
- Accept options via constructor parameter directly

### 2.8 Refactor `AsyncEndpointsJsonSerializationContext.cs`
- Remove `ProblemDetails` from `JsonSerializable` list
- Add `JobRecord` and `JobDescriptor` to the serializable types

### 2.9 Update `AspNetCore.csproj`
- Ensure it has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- Ensure all moved files are included (or use default wildcard include)

### 2.10 Clean up
- Search Core/ for any remaining `using Microsoft.AspNetCore.*` — fix if found
- `dotnet build src/AsyncEndpoints.Core/` must succeed

### 2.11 Write unit tests in `AspNetCore.UnitTests/` for moved types

## Validation
- `Core.csproj` has no FrameworkReference
- `dotnet build src/AsyncEndpoints.Core/` succeeds
- `dotnet build src/AsyncEndpoints.AspNetCore/` succeeds
- No `using Microsoft.AspNetCore.*` in `Core/`
- `dotnet test tests/AsyncEndpoints.AspNetCore.UnitTests/` passes

## Do NOT
- Modify Abstractions (Phase 01 is done)
- Change any business logic — pure file moves + namespace updates + one refactor (Serializer)
- Delete or rewrite AspNetCore endpoints yet (Phase 07)
