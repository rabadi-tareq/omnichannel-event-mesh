# DSG Omnichannel Engine — Master Context Summary

## 0. AI Assistant Directives (Meta-Instructions)
* **Persona**: Act as a senior .NET developer assisting me in building a distributed omnichannel order engine.[cite: 2]
* **Execution Rules**: Guide me step-by-step through the Execution Roadmap using a **3-Stage Interactive Gated Flow**:[cite: 2]
  1. *Stage 1: Concept Overview* — Present architectural concepts and design patterns only. Wait for explicit confirmation/understanding before providing the prompt.[cite: 2]
  2. *Stage 2: Copilot Prompt (On-Demand)* — Provide the targeted Copilot prompt upon request. Wait for execution confirmation.[cite: 2]
  3. *Stage 3: Copilot Verification Checklist (Post-Execution)* — Provide a plain-text verification checklist block to pass to Copilot for validation and test execution.[cite: 2]
* **Context Maintenance**: Strictly track new concepts, rules, and milestone progress.[cite: 2] Regenerate this Master Context Summary and the Architecture & Design Concepts Store upon request, appending new entries chronologically by milestone.[cite: 1, 2]

## 1. Project Brief & Core Objective
* **Target Domain**: Dick's Sporting Goods — Omnichannel "Buy Online, Pick Up In-Store" (BOPIS) Order Processing Engine.[cite: 2]
* **Core Architectural Goal**: Demonstrate senior-level mastery of distributed systems resilience, transactional integrity, end-to-end security context propagation, and real-time frontend updates without mock flags.[cite: 2]
* **Key Patterns**: Transactional Outbox Pattern, Consumer Idempotency (Inbox Pattern), Domain Fallback Routing, Real-Time WebSockets via SignalR, JWT Security Context Propagation.[cite: 2]

## 2. Tech Stack & Infrastructure
* **Backend**: C# / .NET 10, ASP.NET Core Web API, Background Worker Service, EF Core 10, SQL Server 2022 (Docker with named volume `dsg-sqldata`).[cite: 2]
* **Security**: JWT Bearer Token Authentication, Policy-Based Authorization (`RequireCustomerRole`), RabbitMQ Header Context Propagation (`UserId`, `CorrelationId`).[cite: 2]
* **Messaging & Resilience**: MassTransit + RabbitMQ (Outbox/Inbox, Retries, Dead-Letter Queue / Poison Messages).[cite: 2]
* **Frontend (Planned)**: Angular 19+ (Signals, `computed()`, `toSignal()`, Native Control Flow `@if`/`@for`, `@defer` views, Functional HTTP Interceptors for JWT auth).[cite: 2]
* **Real-Time Stream (Planned)**: SignalR Hub pushing event payloads directly to Angular Signals.[cite: 2]
* **Infrastructure Management**: Containers run via Docker Compose.[cite: 2] Daily lifecycle managed using `docker compose stop` and `docker compose start` to retain SQL Server schema and outbox data in `dsg-sqldata`.[cite: 1, 2]

## 3. Engineering & Workflow Preferences
* **3-Stage Gated Workflow**: Strict separation between Concept Overview, On-Demand Prompting, and Plain-Text Verification Checklists for Copilot validation.[cite: 2]
* **Repository Instructions**: Synchronized `.github/copilot-instructions.md` directing Copilot to use dedicated extension classes for clean `Program.cs`, reuse `ApplicationDbContext` across host projects (`Api` and `Worker`), and keep event contracts self-contained (`StoreId`).[cite: 2]
* **Scope Strategy**: Thin Vertical Slice PoC focusing strictly on distributed resilience and clean architecture.[cite: 1, 2]
* **Terminal Commands**: Standard `.NET CLI` syntax and standard Docker commands.[cite: 2]
* **Architectural Standard**: Strict Clean Architecture (Separation of host, contracts, domain, and infrastructure).[cite: 1, 2]

## 4. Verified Project Structure
* `DsgOmnichannel.Api/` (Controllers, Extensions, appsettings.json)[cite: 2]
* `DsgOmnichannel.Worker/` (Consumers, Extensions, Worker.cs)[cite: 2]
* `DsgOmnichannel.Contracts/` (Events: `PingEvent`, `OrderPlacedEvent`, `StoreInventoryAllocatedEvent`, `AllocationFailedEvent`)[cite: 2]
* `DsgOmnichannel.Domain/` (Entities: `AuditLog`, `Order`, `StoreInventory`)[cite: 2]
* `DsgOmnichannel.Infrastructure/` (Persistence: `ApplicationDbContext`, Migrations)[cite: 2]

## 5. Execution Roadmap & Milestone Status

### Milestone 0: Baseline Infrastructure & Messaging (COMPLETED & VERIFIED)
* [x] Multi-project .NET 10 solution created.[cite: 2]
* [x] Docker Compose configured for SQL Server (with persistent volume `dsg-sqldata`) and RabbitMQ.[cite: 2]
* [x] `ApplicationDbContext` created with isolated migrations in Infrastructure.[cite: 2]
* [x] Standard `AspNetCore.HealthChecks` configured (`/health` reporting Healthy).[cite: 2]
* [x] Decoupled messaging verified: `Api` publishes `PingEvent`, `Worker` consumes and logs.[cite: 2]

### Milestone 1: Domain Models, Contracts & Security Setup (COMPLETED & VERIFIED)
* [x] Define Domain Entities (`Order`, `StoreInventory`) in `DsgOmnichannel.Domain`.[cite: 2]
* [x] Define strongly-typed C# Event Contracts (`OrderPlacedEvent`, `StoreInventoryAllocatedEvent`, `AllocationFailedEvent`) in `DsgOmnichannel.Contracts`.[cite: 2]
* [x] Configure JWT Bearer Token Authentication & Policy-Based Authorization in `DsgOmnichannel.Api`.[cite: 2]
* [x] Configure EF Core mappings in `ApplicationDbContext`.[cite: 2]
* [x] Verification: Applied `AddDomainEntities` migration to SQL Server; verified `401 Unauthorized` on secured routes vs `200 OK` on public routes.[cite: 2]

### Milestone 2: Transactional Outbox & Order Endpoint (COMPLETED & VERIFIED)
* [x] Configure MassTransit EF Core Outbox in API and Worker with header context propagation.[cite: 2]
* [x] Implement `POST /api/orders` REST endpoint with atomic local DB transaction (Order + Outbox write).[cite: 2]
* [x] Verification: Performed Chaos Test #1 (Broker Outage Resilience).[cite: 1, 2]

### Milestone 3: Background Worker Processing & Consumer Idempotency (COMPLETED & VERIFIED)
* [x] Configure MassTransit EF Core Inbox pattern in `DsgOmnichannel.Worker` for message deduplication via `dbo.InboxState`.[cite: 1, 2]
* [x] Update `OrderPlacedEvent` contract to include `StoreId` as a self-contained domain fact.[cite: 2]
* [x] Implement `OrderPlacedEventConsumer` fulfillment logic (Stock deduction vs. `AllocationFailedEvent` fallback).[cite: 2]
* [x] Clean `Program.cs` service registrations via extension methods.[cite: 2]
* [x] Verification: Performed Chaos Test #2 (Duplicate Message Idempotency - Verified Inbox intercepted duplicate).
* [x] Verification: Performed Chaos Test #5 (Inventory Conflict Fallback - Verified consumer gracefully logged allocation failure and prevented negative stock).

### Milestone 4: Resilient Edge Cases, Sagas & Dead-Letter Queue (IN PROGRESS — TESTING NEXT)
* [ ] Implement MassTransit State Machines (Sagas) to manage multi-step distributed workflows.
* [ ] Introduce Compensating Transactions (publishing `InventoryAllocationFailedEvent` so upstream Order Service reacts).
* [ ] Configure MassTransit exponential retry policy and error queues.[cite: 2]
* [ ] Verification: Perform Chaos Test #3 (Transient DB Lock) & Chaos Test #4 (Poison Payload DLQ).[cite: 2]

### Milestone 5: Angular 19+ Dashboard, SignalR Integration & Interview Prep
* [ ] Scaffold Angular standalone app with Signals, modern Control Flow, and Functional HTTP Interceptors.[cite: 2]
* [ ] Wire ASP.NET Core SignalR hub to dispatch events to frontend.[cite: 2]
* [ ] Verification: Real-time split-screen dashboard displaying order status badge and event telemetry stream.[cite: 2]
* [ ] Final Artifact: Generate `INTERVIEW_TALKING_POINTS.md` summary for senior technical interview prep.[cite: 2]