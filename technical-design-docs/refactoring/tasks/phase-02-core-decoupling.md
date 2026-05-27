# Phase 02: Core ASP.NET Decoupling

**Goal**: Remove all ASP.NET dependencies from `Core`. Move HTTP-specific code to `AspNetCore`.

**Prerequisites**: Phase 01 (Abstractions must be clean first)

---

## Tasks

### 2.1 Remove FrameworkReference from Core.csproj

- [ ] Open `src/AsyncEndpoints.Core/AsyncEndpoints.Core.csproj`
- [ ] Remove `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- [ ] Attempt `dotnet build` — note all compilation errors

**Validation**: Build fails with clear errors pointing to ASP.NET types that still need to be moved.

---

### 2.2 Move HttpContextExtensions.cs

- [ ] Move `src/AsyncEndpoints.Core/HttpContextExtensions.cs`
- [ ] → `src/AsyncEndpoints.AspNetCore/Extensions/HttpContextExtensions.cs`
- [ ] Update namespace to `AsyncEndpoints.AspNetCore.Extensions`
- [ ] Fix any `using` statements

**Validation**: File compiles in `AspNetCore` project.

---

### 2.3 Move IJsonBodyParserService and JsonBodyParserService

- [ ] Move `src/AsyncEndpoints.Core/Infrastructure/Serialization/IJsonBodyParserService.cs`
- [ ] → `src/AsyncEndpoints.AspNetCore/Serialization/IJsonBodyParserService.cs`
- [ ] Move `src/AsyncEndpoints.Core/Infrastructure/Serialization/JsonBodyParserService.cs`
- [ ] → `src/AsyncEndpoints.AspNetCore/Serialization/JsonBodyParserService.cs`
- [ ] Update namespaces

**Validation**: Both files compile in `AspNetCore` project.

---

### 2.4 Move AsyncEndpointsResponseConfigurations

- [ ] Move `src/AsyncEndpoints.Core/Configuration/AsyncEndpointsResponseConfigurations.cs`
- [ ] → `src/AsyncEndpoints.AspNetCore/Configuration/AsyncEndpointsResponseConfigurations.cs`
- [ ] Update namespace

**Validation**: File compiles in `AspNetCore` project.

---

### 2.5 Move JobResponse, JobResponseMapper

- [ ] Move `src/AsyncEndpoints.Core/Utilities/JobResponse.cs`
- [ ] → `src/AsyncEndpoints.AspNetCore/Models/JobResponse.cs`
- [ ] Move `src/AsyncEndpoints.Core/Utilities/JobResponseMapper.cs`
- [ ] → `src/AsyncEndpoints.AspNetCore/Models/JobResponseMapper.cs`
- [ ] Update namespaces

**Validation**: Both files compile in `AspNetCore` project.

---

### 2.6 Move JobResultResponse, ResponseDefaults

- [ ] Move `src/AsyncEndpoints.Core/Utilities/JobResultResponse.cs`
- [ ] → `src/AsyncEndpoints.AspNetCore/Endpoints/JobResultResponse.cs`
- [ ] Move `src/AsyncEndpoints.Core/Utilities/ResponseDefaults.cs`
- [ ] → `src/AsyncEndpoints.AspNetCore/Endpoints/ResponseDefaults.cs`
- [ ] Update namespaces

**Validation**: Both files compile in `AspNetCore` project.

---

### 2.7 Refactor Serializer.cs to remove ASP.NET dependency

- [ ] Open `src/AsyncEndpoints.Core/Infrastructure/Serialization/Serializer.cs`
- [ ] Replace `IOptions<JsonOptions>` (from `Microsoft.AspNetCore.Http.Json`) with direct `System.Text.Json.JsonSerializerOptions`
- [ ] Configure defaults via constructor parameter or `AsyncEndpointsOptions` (to be built in Phase 03)
- [ ] Remove any `using Microsoft.AspNetCore.Http.Json` statement

**Validation**: `Serializer.cs` compiles with no ASP.NET types.

---

### 2.8 Refactor AsyncEndpointsJsonSerializationContext

- [ ] Open `src/AsyncEndpoints.Core/Infrastructure/AsyncEndpointsJsonSerializationContext.cs`
- [ ] Remove `ProblemDetails` from the `JsonSerializable` attribute list
- [ ] Add `JobRecord` and `JobDescriptor` to the serializable types
- [ ] If `ProblemDetails` is still needed, create a separate context in `AspNetCore`

**Validation**: Context compiles. AOT-compatible source generation includes the new job types.

---

### 2.9 Update AspNetCore.csproj

- [ ] Open `src/AsyncEndpoints.AspNetCore/AsyncEndpoints.AspNetCore.csproj`
- [ ] Ensure `<FrameworkReference Include="Microsoft.AspNetCore.App" />` is present (it needs ASP.NET)
- [ ] Add any moved files to the project (they should be in the right folder already)

**Validation**: `dotnet build src/AsyncEndpoints.AspNetCore/` succeeds.

---

### 2.10 Clean up remaining ASP.NET references in Core

- [ ] Search entire `Core/` directory for any remaining `Microsoft.AspNetCore.*` using statements
- [ ] Fix or move any remaining files
- [ ] Final `dotnet build src/AsyncEndpoints.Core/` — must succeed with zero warnings about ASP.NET types

**Validation**: `dotnet build` on Core succeeds with no `FrameworkReference` and no ASP.NET types in use.

---

### 2.11 Write unit tests for moved types

- [ ] Create tests in `tests/AsyncEndpoints.AspNetCore.UnitTests/` for:
  - [ ] `HttpContextExtensions` (if testable)
  - [ ] `JobResponseMapper` mapping logic
  - [ ] `JsonBodyParserService` parsing logic (mock HttpContext)
- [ ] Verify no test references Core's ASP.NET logic

**Validation**: `dotnet test tests/AsyncEndpoints.AspNetCore.UnitTests/` passes.

---

## Phase 02 Definition of Done

- [ ] `Core.csproj` has **no** `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- [ ] `Core/` contains zero `using Microsoft.AspNetCore.*` statements
- [ ] All HTTP-specific files are moved to `AspNetCore/`
- [ ] `AspNetCore.csproj` has the `FrameworkReference` and compiles
- [ ] `Serializer.cs` uses `System.Text.Json` directly (no ASP.NET JSON options)
- [ ] `dotnet build` succeeds for both `Core` and `AspNetCore` projects
- [ ] Unit tests for `AspNetCore` types pass

**Next phase**: [Phase 03: Configuration Consolidation](phase-03-config-consolidation.md)
