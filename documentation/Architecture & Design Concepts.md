# Architecture & Design Concepts — DSG Omnichannel Engine

## Milestone 0: Infrastructure & Messaging Setup

### 1. Clean Architecture & Solution Decoupling
Decoupling the API host from worker processing, domain models, contracts, and infrastructure prevents circular dependencies and isolates core business logic from framework choices. Controllers depend on abstract contracts and interfaces rather than concrete infrastructure implementations.

### 2. Infrastructure Isolation & Database Persistence
Running SQL Server and RabbitMQ in Docker Compose allows local environment parity with cloud infrastructure. Utilizing Docker named volumes (e.g., `dsg-sqldata:/var/opt/mssql`) ensures database schemas, migrations, and transactional tables persist across container restarts, allowing daily execution via `docker compose stop` and `start`.

---

## Milestone 1: Domain Entities, Event Contracts & Security Baseline

### 3. Pure POCO Domain Entities (`DsgOmnichannel.Domain`)
Domain entities represent core business state (e.g., `Order`, `StoreInventory`) without dependencies on external frameworks like EF Core or ASP.NET Core. Keeping entities pure isolates domain rules from database storage mechanics.

### 4. Strongly-Typed Shared Contracts (`DsgOmnichannel.Contracts`)
Events represent immutable facts that occurred in the domain (e.g., `OrderPlacedEvent`, `StoreInventoryAllocatedEvent`). Placing these contract definitions in a shared assembly allows both publishing services (`Api`) and consuming services (`Worker`) to communicate without coupling their internal implementation details.

### 5. Policy-Based JWT Authorization & Pipeline Security (`DsgOmnichannel.Api`)
Registering `UseAuthentication` and `UseAuthorization` middleware configures the request pipeline to evaluate incoming JWT bearer tokens. Decorating routes with `[Authorize(Policy = "...")]` enforces identity checks prior to action execution, returning `401 Unauthorized` for missing/invalid tokens while exposing authenticated user claims (`UserId`) for downstream propagation.

### 6. Security Context Verification Endpoints (`TestController`)
Creating explicit public (`GET /api/test/public`) and protected (`GET /api/test/secured`) test endpoints verifies that authentication handlers and policy evaluation rules function correctly within the ASP.NET Core request pipeline before building full domain actions.

---

## Milestone 2: Transactional Outbox & Order Submission

### 7. MassTransit Outbox Configuration & Entity Mapping (`ApplicationDbContext`)
To guarantee consistency across database writes and message publishing without 2-Phase Commit (2PC) protocols, MassTransit integrates directly with EF Core. Calling `modelBuilder.AddTransactionalOutboxEntities()` maps internal tables (`OutboxMessage`, `OutboxState`, `InboxState`) into EF Core metadata, allowing outbox operations to participate directly in local SQL transactions.

### 8. Atomic Dual-Write Staging (`POST /api/orders`)
The Transactional Outbox pattern resolves dual-write vulnerabilities. When an order is created, the `Order` entity is saved and `IPublishEndpoint.Publish()` stages the `OrderPlacedEvent` into `OutboxMessage` inside the same database context. Executing `SaveChangesAsync()` commits both the domain change and the outbound message atomically.

### 9. Broker Outage Resilience & Automatic Outbox Draining (Chaos Test #1)
If the message broker (RabbitMQ) is offline during an order request, the HTTP request completes successfully with `201 Created` because the event is safely stored in local database storage. Once broker connectivity is restored, MassTransit's outbox background process polls `OutboxMessage`, dispatches the pending messages to RabbitMQ, and cleans up the outbox records.