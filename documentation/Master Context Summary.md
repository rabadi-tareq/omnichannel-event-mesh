# DSG Omnichannel Engine — Master Context Summary

## 0. AI Assistant Directives (Meta-Instructions)
* Persona: Act as a senior .NET developer assisting me in building a distributed omnichannel order engine.
* Execution Rules: Guide me step-by-step through the Execution Roadmap. You must wait for my explicit confirmation of success after each step before moving to the next one.
* Output Format: Do not generate raw C# code blocks directly. Use a two-part breakdown for implementation steps: 1) Concept Overview, 2) GitHub Copilot Prompt.
* Context Maintenance: You are strictly responsible for keeping track of new concepts and milestone progress. Whenever I ask for an update, you must regenerate this Master Context Summary and the Architecture & Design Concepts Store, ensuring new concepts are appended chronologically by milestone.

## 1. Project Brief & Core Objective
* Target Domain: Dick's Sporting Goods — Omnichannel "Buy Online, Pick Up In-Store" (BOPIS) Order Processing Engine.
* Core Architectural Goal: Demonstrate senior-level mastery of distributed systems resilience, transactional integrity, end-to-end security context propagation, and real-time frontend updates without mock flags.
* Key Patterns: Transactional Outbox Pattern, Consumer Idempotency (Inbox Pattern), Domain Fallback Routing, Real-Time WebSockets via SignalR, JWT Security Context Propagation.

## 2. Tech Stack & Infrastructure
* Backend: C# / .NET 10, ASP.NET Core Web API, Background Worker Service, EF Core 10, SQL Server 2022 (Docker with named volume `dsg-sqldata`).
* Security: JWT Bearer Token Authentication, Policy-Based Authorization (`RequireCustomerRole`), RabbitMQ Header Context Propagation (UserId, CorrelationId).
* Messaging & Resilience: MassTransit + RabbitMQ (Outbox/Inbox, Retries, Dead-Letter Queue / Poison Messages).
* Frontend (Planned): Angular 19+ (Signals, computed(), toSignal(), Native Control Flow @if/@for, @defer views, Functional HTTP Interceptors for JWT auth).
* Real-Time Stream (Planned): SignalR Hub pushing event payloads directly to Angular Signals.
* Infrastructure Management: Containers run via Docker Compose. Daily lifecycle managed using `docker compose stop` and `docker compose start` to retain SQL Server schema and outbox data in `dsg-sqldata`.

## 3. Engineering & Coding Preferences
* Scope Strategy: Thin Vertical Slice PoC. Implement minimal code bloat—focus strictly on what is required to prove distributed resilience and architecture.
* Terminal Commands: Standard `.NET CLI` syntax (`dotnet ef database update --project ... --startup-project ...`) and standard Docker commands (`docker compose start/stop`).
* Architectural Standard: Strict Clean Architecture (Separation of host, contracts, domain, and infrastructure).

## 4. Verified Project Structure
src/
├── DsgOmnichannel.Api/
│   ├── Controllers/
│   │   ├── OrdersController.cs
│   │   └── TestController.cs
│   ├── Program.cs
│   ├── appsettings.json / appsettings.Development.json
│   └── Properties/launchSettings.json
├── DsgOmnichannel.Worker/
│   ├── Consumers/
│   │   └── PingEventConsumer.cs
│   ├── Program.cs
│   ├── Worker.cs
│   └── appsettings.json / appsettings.Development.json
├── DsgOmnichannel.Contracts/
│   └── Events/
│       ├── PingEvent.cs
│       ├── OrderPlacedEvent.cs
│       ├── StoreInventoryAllocatedEvent.cs
│       └── AllocationFailedEvent.cs
├── DsgOmnichannel.Domain/
│   └── Entities/
│       ├── AuditLog.cs
│       ├── Order.cs
│       └── StoreInventory.cs
└── DsgOmnichannel.Infrastructure/
    ├── Persistence/
    │   └── ApplicationDbContext.cs
    └── Migrations/
        ├── <Timestamp>_InitialCreate.cs
        ├── <Timestamp>_AddMassTransitOutbox.cs
        ├── <Timestamp>_AddDomainEntities.cs
        └── ApplicationDbContextModelSnapshot.cs

## 5. Execution Roadmap & Milestone Status

### Milestone 0: Baseline Infrastructure & Messaging (COMPLETED & VERIFIED)
* [x] Multi-project .NET 10 solution created.
* [x] Docker Compose configured for SQL Server (with persistent volume `dsg-sqldata`) and RabbitMQ.
* [x] ApplicationDbContext created with isolated migrations in Infrastructure.
* [x] Standard AspNetCore.HealthChecks configured (/health reporting Healthy).
* [x] Decoupled messaging verified: Api publishes PingEvent, Worker consumes and logs.

### Milestone 1: Domain Models, Contracts & Security Setup (COMPLETED & VERIFIED)
* [x] Define Domain Entities (Order, StoreInventory) in DsgOmnichannel.Domain.
* [x] Define strongly-typed C# Event Contracts (OrderPlacedEvent, StoreInventoryAllocatedEvent, AllocationFailedEvent) in DsgOmnichannel.Contracts.
* [x] Configure JWT Bearer Token Authentication & Policy-Based Authorization in DsgOmnichannel.Api.
* [x] Configure EF Core mappings in ApplicationDbContext.
* [x] Verification: Applied AddDomainEntities migration to SQL Server; verified 401 Unauthorized on secured routes (`/api/test/secured`) vs 200 OK on public routes (`/api/test/public`).

### Milestone 2: Transactional Outbox & Order Endpoint (COMPLETED & VERIFIED)
* [x] Configure MassTransit EF Core Outbox in API and Worker with header context propagation.
* [x] Implement POST /api/orders REST endpoint with atomic local DB transaction (Order + Outbox write).
* [x] Verification: Performed Chaos Test #1 (Stopped RabbitMQ via `docker stop dsg-rabbitmq`, submitted order, verified 201 Created and staged event in `dbo.OutboxMessage`, restarted RabbitMQ, verified automatic outbox drain and cleanup).

### Milestone 3: Background Worker Processing & Consumer Idempotency (NEXT)
* [ ] Configure MassTransit Inbox in Worker.
* [ ] Build fulfillment logic in OrderPlacedEventConsumer (Stock deduction vs. AllocationFailedEvent fallback).
* [ ] Verification: Perform Chaos Test #2 (Duplicate Message) & Chaos Test #5 (Inventory Conflict).

### Milestone 4: Resilient Edge Cases & Dead-Letter Queue
* [ ] Configure MassTransit exponential retry policy and error queues.
* [ ] Verification: Perform Chaos Test #3 (Transient DB Lock) & Chaos Test #4 (Poison Payload DLQ).

### Milestone 5: Angular 19+ Dashboard, SignalR Integration & Interview Prep
* [ ] Scaffold Angular standalone app with Signals, modern Control Flow, and Functional HTTP Interceptor for JWT.
* [ ] Wire ASP.NET Core SignalR hub to dispatch events to frontend.
* [ ] Verification: Real-time split-screen dashboard displaying order status badge and event telemetry stream.
* [ ] Final Artifact: Generate INTERVIEW_TALKING_POINTS.md summary for senior technical interview prep.