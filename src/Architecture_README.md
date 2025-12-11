Recommended project layout (Clean Architecture)

- src/
  - Domain/           # Entities, ValueObjects, Domain interfaces
  - Application/      # Use cases, Services, DTOs, interfaces
  - Infrastructure/   # Implementations: Http clients, DB, external APIs
  - Presentation/     # APIs, UI, controllers (e.g., Orchestrator Api)
  - Workers/          # Optional: grouped worker/service projects

Notes:
- You already have projects aligned to these layers (VatIT.Domain, VatIT.Application, VatIT.Infrastructure, VatIT.Orchestrator.Api and worker projects). The placeholders above are safe to keep as a visual guide.
- Worker projects may live alongside the main solution as independent projects under `src/Workers/<WorkerName>` or alongside `src/` as top-level projects. Keeping them under `src/Workers` groups services in one place and is convenient for monorepos.
- Use `IOptions<T>`/configuration to avoid hard-coded values and make workers configurable per environment.
- When adding new projects, add them to the solution (`.sln`) so tooling (build/test) picks them up.

Quick next steps you can do:
- Move worker project directories into `src/Workers/<Name>` if you prefer grouped layout (git mv + update solution). This is optional and not required for correctness.
- Replace the `.gitkeep` files with real project folders/files as you expand each layer.
