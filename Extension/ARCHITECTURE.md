# Extension architecture

`Extension` is intentionally one general-purpose assembly. Its advantage is a single package and a consistent extension-method surface; architectural boundaries are therefore expressed as capability folders and namespaces rather than separate projects.

## Capability map

| Module | Responsibility | May depend on |
| --- | --- | --- |
| `Core` | BCL-only primitives: conversion, collections, buffers, concurrency, comparison, enums, reflection, notification, shared models | — |
| `Automation` | Industrial automation vocabulary, separated into `Device` and `Secs` | — |
| `IO` | Files, CSV, compression, retention | — |
| `Platform` | Native interop, process, memory and diagnostics helpers | — |
| `Patterns` | Reusable creation, messaging, coordination and pipeline patterns | `Core` |
| `UI` | WPF controls and themes | `Core` |
| `Security` | Cryptography, licensing and protection | — |
| `Imaging` | Image models plus HALCON/OpenCV adapters | `IO` |
| `Integration` | Third-party adapters for Prism, Mapster, MiniExcel and cloning | `Imaging`; nothing may depend on `Integration` |

Dependencies are intentionally sparse: only four cross-module edges currently exist (`Patterns → Core`, `UI → Core`, `Imaging → IO`, and `Integration → Imaging`). `Integration` is the outer adapter layer. Top-level module cycles are forbidden. Same-module references are allowed.

## Namespace and placement rules

1. The physical directory is the namespace: `Extension/<A>/<B>/X.cs` uses `Extension.A.B`.
2. Domain-neutral data stays with the capability that owns it. For example, image models are in `Imaging.Models`; Mapster configuration is in `Integration.Mapster`.
3. Vendor/framework code belongs in the matching adapter module, not in `Core`.
4. Retention cleanup exposes one public operation backed by one implementation; compatibility aliases are not permitted.
5. New top-level folders are not permitted without updating this document and the architecture contract tests.
6. `Cognex` remains a separate assembly because its vendor runtime boundary is materially different; this is a technical exception, not a template for fragmenting the common collection.

## Enforcement

`Extension.Tests/ArchitectureContractTests.cs` locks the top-level module set, the explicit internal dependency graph, and graph acyclicity. `Extension.Tests/TierGuardTests.cs` separately controls which folders may reference WPF and third-party/vendor namespaces, and verifies directory-to-namespace alignment.

Run:

```powershell
dotnet test Extension.sln --no-restore
```

The older classification, tiering, and migration documents describe the analysis that led here. This file is the authoritative current-state architecture contract.