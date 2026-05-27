# Folder Structure Refactor Implementation Plan

## Objective

Move the codebase from the current transitional state into the target structure defined in `technical-design-docs/002-folder-structure.md`, using iterative, low-risk changes.

Constraints for this phase:

- Do not rename classes, files, or namespaces yet.
- Prioritize build stability after each slice.
- Use compatibility shims where required to avoid breaking consumers.

---

## Current State Snapshot

- Target structure is documented in `technical-design-docs/002-folder-structure.md`.
- Solution currently includes split projects plus legacy aggregator:
  - `src/AsyncEndpoints/AsyncEndpoints.csproj`
  - `src/AsyncEndpoints.Abstractions/AsyncEndpoints.Abstractions.csproj`
  - `src/AsyncEndpoints.Core/AsyncEndpoints.Core.csproj`
  - `src/AsyncEndpoints.Worker/AsyncEndpoints.Worker.csproj`
  - `src/AsyncEndpoints.AspNetCore/AsyncEndpoints.AspNetCore.csproj`
  - `src/AsyncEndpoints.Provider.InMemory/AsyncEndpoints.Provider.InMemory.csproj`
  - `src/AsyncEndpoints.Redis/AsyncEndpoints.Redis.csproj`
- `AsyncEndpoints.AspNetCore` still contains cross-layer DI wiring to worker/provider concretes.
- Similar DI extension logic exists in legacy aggregator project, creating duplication.
- Redis provider still uses old project naming (`AsyncEndpoints.Redis`) and references legacy aggregator.
- Tests are mostly monolithic/provider-specific (`AsyncEndpoints.UnitTests`, `AsyncEndpoints.Redis.UnitTests`), while target test shape is `UnitTests + IntegrationTests`.
- Build currently fails due to transitional reference/service registration issues (expected in this phase).

---

## Target End-State (from 002)

Top-level layout:

- `src/AsyncEndpoints.Abstractions`
- `src/AsyncEndpoints.Core`
- `src/AsyncEndpoints.Worker`
- `src/AsyncEndpoints.AspNetCore`
- `src/AsyncEndpoints.Provider.SqlServer` (future)
- `src/AsyncEndpoints.Provider.Postgres` (future)
- `src/AsyncEndpoints.Provider.Redis`
- `src/AsyncEndpoints.Provider.InMemory`
- `tests/AsyncEndpoints.UnitTests`
- `tests/AsyncEndpoints.IntegrationTests`
- `samples/*`

Dependency graph:

- `AspNetCore -> Core -> Abstractions`
- `Worker -> Core -> Abstractions`
- `Providers -> Abstractions` (optionally `Core` only when justified)
- No circular dependencies

---

## Guiding Principles

1. Refactor by boundaries first, internals second.
2. Keep each PR vertical and independently buildable.
3. Preserve public APIs temporarily with `[Obsolete]` forwards where needed.
4. Prefer moving code over rewriting code.
5. Add tests around moved seams before behavior changes.

---

## Iterative Implementation Plan

## Phase 1: Stabilize Project Boundaries

### Goals

- Enforce desired project reference directions.
- Remove accidental layer leaks.

### Actions

- Define and document allowed references:
  - `Abstractions`: no runtime/web/DI dependencies.
  - `Core`: references `Abstractions` only.
  - `Worker`: references `Core`.
  - `AspNetCore`: references `Core`.
  - `Provider.*`: references `Abstractions` (and only `Core` when essential).
- Start removing provider/worker concrete registrations from AspNetCore project.
- Detach providers from legacy aggregator (`src/AsyncEndpoints`).

### Exit Criteria

- No forbidden project reference direction remains.
- Solution references reflect intended layering.

---

## Phase 2: Contract Surface Consolidation in Abstractions

### Goals

- Place all shared contracts in `AsyncEndpoints.Abstractions`.

### Actions

- Move interfaces and contract models from Core into Abstractions (without renaming).
- Remove runtime implementations from Abstractions (e.g., keep interfaces only, move concrete implementations to Core/Internal).
- Ensure providers and core consume contracts from Abstractions.

### Exit Criteria

- Abstractions contains interfaces/contracts only.
- No DI/logging/ASP.NET dependencies in Abstractions.

---

## Phase 3: Core Orchestration Consolidation

### Goals

- Make `AsyncEndpoints.Core` the orchestration center.

### Actions

- Move/align core logic into target folders:
  - `DependencyInjection/`
  - `Configuration/`
  - `Submission/`
  - `Execution/`
  - `Listener/`
  - `Partitioning/`
  - `Channels/`
  - `Serialization/`
  - `Internal/`
- Consolidate DI entrypoints currently duplicated across AspNetCore and legacy aggregator.

### Exit Criteria

- Core has orchestration + configuration only.
- No endpoint mapping or provider-specific implementations in Core.

---

## Phase 4: Worker Isolation

### Goals

- Keep worker-only runtime pipeline in `AsyncEndpoints.Worker`.

### Actions

- Move worker classes to aligned folders:
  - `Hosting/`
  - `Execution/`
  - `Heartbeat/`
  - `Concurrency/`
- Keep current type names/namespaces unchanged for now.
- Ensure worker service registration is scoped to worker package.

### Exit Criteria

- Worker contains only background execution concerns.
- Worker no longer introduces endpoint/provider concerns.

---

## Phase 5: AspNetCore Narrowing

### Goals

- Keep AspNetCore package focused on endpoint integration only.

### Actions

- Move/align ASP.NET files into:
  - `Endpoints/`
  - `Extensions/`
  - `Models/`
- Remove worker/provider registration methods from AspNetCore extension surface.
- Keep temporary compatibility wrappers where necessary.

### Exit Criteria

- AspNetCore compiles without direct worker/provider concrete dependencies.
- Public API is endpoint-centric.

---

## Phase 6: Provider Normalization

### Goals

- Align provider naming and package patterns.

### Actions

- Migrate `src/AsyncEndpoints.Redis` to `src/AsyncEndpoints.Provider.Redis`.
- Standardize provider internals to:
  - `Storage/`
  - `DependencyInjection/`
- Ensure provider project references follow boundary rules.

### Exit Criteria

- Providers follow `AsyncEndpoints.Provider.*` pattern.
- Providers implement contracts from Abstractions cleanly.

---

## Phase 7: Legacy Aggregator Transition

### Goals

- Control compatibility impact while moving consumers to new package layout.

### Actions

- Keep `src/AsyncEndpoints` temporarily as compatibility/meta package.
- Forward old extension APIs to new package entrypoints.
- Mark legacy APIs `[Obsolete]` with migration guidance.
- Plan deprecation/removal timeline.

### Exit Criteria

- No new business logic added to legacy aggregator.
- Compatibility behavior is explicit and documented.

---

## Phase 8: Test Topology Migration

### Goals

- Align tests to target structure and ownership.

### Actions

- Split and migrate tests:
  - Keep project-aligned unit tests under `tests/AsyncEndpoints.UnitTests`.
  - Move provider-backed scenarios to `tests/AsyncEndpoints.IntegrationTests`.
- Rehome `AsyncEndpoints.Redis.UnitTests` as needed into unit/integration lanes.
- Remove or activate placeholder test projects/folders consistently.

### Exit Criteria

- Every source project has clear unit-test ownership.
- Integration tests run independently per provider.

---

## Phase 9: Samples, Docs, and Solution Hygiene

### Goals

- Ensure repo shape and docs reflect actual architecture.

### Actions

- Align `examples/` to `samples/` (or document intentional divergence).
- Update README package names and registration examples.
- Update solution nesting and project identities.

### Exit Criteria

- New contributor can follow docs and run samples without legacy context.

---

## Proposed PR Sequence

1. Boundary enforcement + build baseline recovery.
2. Contract extraction to Abstractions.
3. Core consolidation + DI de-duplication.
4. Worker isolation and folder alignment.
5. AspNetCore narrowing (endpoint-only).
6. Redis provider rename/migration to `Provider.Redis`.
7. Legacy aggregator compatibility layer + obsoletions.
8. Test restructuring into unit/integration topology.
9. Samples/docs/solution cleanup.

---

## Validation Gates Per Phase

- Build gate: `dotnet build AsyncEndpoints.sln -c Debug`
- Test gate: `dotnet test AsyncEndpoints.sln -c Debug --no-build`
- Reference gate: verify project references respect target graph.
- API gate: verify compatibility wrappers and obsoletions where applicable.

---

## Key Risks and Mitigations

### Risk: DI duplication and ambiguous service registration

- Mitigation: centralize registration ownership (Core/Worker/Provider specific entrypoints).

### Risk: Consumer breakage from package/project identity changes

- Mitigation: keep legacy aggregator as temporary compatibility facade.

### Risk: Cross-project contract drift

- Mitigation: contract-first extraction to Abstractions before deeper moves.

### Risk: Refactor regressions hidden by sparse project-specific tests

- Mitigation: migrate tests incrementally with each phase and enforce phase gates.

---

## Definition of Done (Overall)

- Folder and project structure matches `002-folder-structure.md` intent.
- Dependency graph is clean and acyclic.
- Legacy aggregator is either compatibility-only or formally retired.
- Unit/integration test topology is aligned and green.
- Docs and samples reflect new structure accurately.
