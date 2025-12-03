# DotNetSample — Workspace Overview

This document summarizes the DotNetSample workspace so a new developer can quickly understand, run, and extend the project.

## Projects

- `Api/` — ASP.NET Core Web API (entrypoint for the service).
  - Controllers: `Api/Controllers/*.cs` (e.g. `ProductsController.cs`, `CustomersController.cs`, `OrdersController.cs`).
  - Configuration: `Program.cs`, `appsettings*.json`.
  - Local SQLite DB file: `Api/app.db` (seeded on startup by `Infrastructure.DbSeeder`).

- `Core/` — Domain models and shared interfaces.
  - `Core/DomainModels.cs` — entities used across the solution.
  - `Core/Interfaces.cs` — repository and service interfaces.

- `Infrastructure/` — EF Core implementation and repositories.
  - `Infrastructure/AppDbContext.cs` — EF Core DbContext, model configuration.
  - `Infrastructure/Repositories.cs` — repository implementations.
  - `Infrastructure/OrderProcessor.cs` — business logic for processing orders.
  - `Infrastructure/DbSeeder.cs` — seeds initial data into the DB.
  - Migrations: `Infrastructure/Migrations/` (EF Core migration files).

- `Worker/` — Background worker that processes pending orders (uses `IOrderProcessor`).
  - `Worker/OrderWorker.cs`, `Worker/Program.cs`.

- `Client/` — Small console client that calls the API endpoints for quick smoke tests.
  - `Client/Program.cs` uses `HttpClient` and `System.Net.Http.Json`.

- `Tests/` — xUnit tests covering domain logic and client behavior.
  - Tests use in-memory or fake handlers so most tests run without the API server.

## How to run locally

1. Build solution

   cd /path/to/DotNetSample
   dotnet build

2. Run the API

   dotnet run --project Api

   By default the API binds to the ports configured in `Api/Properties/launchSettings.json` (the client expects `http://localhost:5181`).

3. Run the worker (optional — processes pending orders)

   dotnet run --project Worker

4. Run the client (quick smoke test)

   dotnet run --project Client

5. Run tests

   dotnet test

## Database & EF Core

- The project uses SQLite for local development. The DB file used by the API is `Api/app.db`.
- EF Core migrations are stored in `Infrastructure/Migrations/`.

Typical migration workflow (from repo root):

- Add migration:

  dotnet ef migrations add <Name> --project Infrastructure --startup-project Api -o Migrations

- Apply migrations:

  dotnet ef database update --project Infrastructure --startup-project Api

Note: use the `--project` and `--startup-project` flags to target the correct assemblies.

## API endpoints (quick reference)

- Customers
  - GET /api/customers
  - GET /api/customers/{id}
  - POST /api/customers
  - PUT /api/customers/{id}
  - DELETE /api/customers/{id}

- Products
  - GET /api/products
  - GET /api/products/{id}
  - POST /api/products
  - PUT /api/products/{id}
  - DELETE /api/products/{id}

- Orders
  - GET /api/orders
  - GET /api/orders/{id}
  - GET /api/orders/paged?pageNumber=1&pageSize=10
  - GET /api/orders/customer/{customerId}
  - POST /api/orders  — body: { customerId: "<guid>", items: [{ productId:"<guid>", quantity:1 }] }
  - POST /api/orders/reprocess-pending  — mark pending orders for reprocessing

Example curl to create an order:

  curl -X POST "http://localhost:5181/api/orders" \
    -H "Content-Type: application/json" \
    -d '{"customerId":"<guid>","items":[{"productId":"<guid>","quantity":2}]}'

## Where to look for code

- Controllers: `Api/Controllers` — thin HTTP layer that maps DTOs to domain models.
- Domain models: `Core/DomainModels.cs`.
- Persistence: `Infrastructure/AppDbContext.cs`, `Infrastructure/Repositories.cs`.
- Business logic / order processing: `Infrastructure/OrderProcessor.cs` and `Worker/OrderWorker.cs`.
- Seeding: `Infrastructure/DbSeeder.cs`.
- Tests: `Tests/` — xUnit tests. Client tests use a fake HttpMessageHandler so the API server is not required.

## Testing notes

- Run `dotnet test` from the repo root to execute all tests.
- Tests are xUnit-based and intentionally small and focused.
- Client tests use a FakeHttpMessageHandler to simulate API responses for deterministic unit tests.

## Development notes & suggestions

- Keep controllers thin and delegate business rules to `IOrderProcessor` / services in `Infrastructure`.
- Use DTOs for HTTP boundaries; domain models belong to `Core` and persistence details to `Infrastructure`.
- For production-like behavior, run the API and worker together (Docker or separate terminals).
- Consider adding integration tests that run against an in-memory or ephemeral SQLite DB to validate the full stack.
- Add logging to the worker and order processing paths for easier debugging in long-running scenarios.

## Misc

- README.md contains general project information.
- docs/ contains project documentation; update or add topic-specific docs here.

---

If you want, I can:
- Generate a `docs/` page per area (API reference, architecture, runbook, developer quickstart).
- Add integration tests that use SQLite in-memory for the DbContext.
- Create a sample Postman collection or OpenAPI snippets for the endpoints.

Tell me which one you want next.
