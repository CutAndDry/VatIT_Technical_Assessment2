Last updated: 2025-12-12

Changelog:
- 2025-12-12: Notes updated to reference benchmark-mode and service-level resilience changes.

```markdown
Recommended project layout (Clean Architecture)

  - Domain/           # Entities, ValueObjects, Domain interfaces
  - Application/      # Use cases, Services, DTOs, interfaces
  - Infrastructure/   # Implementations: Http clients, DB, external APIs
  - Presentation/     # APIs, UI, controllers (e.g., Orchestrator Api)
  - Workers/          # Optional: grouped worker/service projects

Notes:

Quick next steps you can do:
