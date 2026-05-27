# Implementation Tasks — Architecture Realignment

This directory contains phase-wise task lists derived from `004-implementation-plan.md`. Each file is a self-contained checklist that can be checked off as work progresses.

## Phase Overview

| Phase | Description | Dependencies | Est. Effort |
|-------|-------------|--------------|-------------|
| [01](phase-01-abstractions-cleanup.md) | Abstractions Cleanup + Contract Foundation | None | Medium |
| [02](phase-02-core-decoupling.md) | Core ASP.NET Decoupling | Phase 01 | Large |
| [03](phase-03-config-consolidation.md) | Configuration Consolidation | Phase 01 | Medium |
| [04](phase-04-jobstore-migration.md) | IJobStore Implementation Migration | Phase 01, 03 | Large |
| [05](phase-05-core-orchestration.md) | Core Orchestration Services | Phase 01, 02, 03, 04 | Large |
| [06](phase-06-worker-redesign.md) | Worker Engine Redesign | Phase 01, 04, 05 | Large |
| [07](phase-07-aspnetcore-endpoints.md) | AspNetCore Endpoint Rewrite | Phase 01, 02, 05 | Medium |
| [08](phase-08-provider-renaming.md) | Provider Renaming and Cleanup | Phase 04 | Small |
| [09](phase-09-aggregator-compat.md) | Aggregator Transition + Backward Compat | Phase 02, 05, 06, 07, 08 | Small |
| [10](phase-10-examples-docs.md) | Examples, Docs, and Samples Update | Phase 05, 07, 08 | Medium |
| [11](phase-11-test-restructuring.md) | Test Restructuring | All preceding | Large |

## Legend

- `[ ]` — Not started
- `[~]` — In progress
- `[x]` — Completed

## How to Use

1. Start with Phase 01 and work sequentially through the phases
2. Each task has a **Validation** section — ensure all validation criteria are met before marking a task complete
3. Each phase has a **Phase Definition of Done** — all tasks must be complete and all validations passing
4. Run `dotnet build` after completing each phase to verify compilation
5. Run relevant unit tests per the testing strategy in each phase
