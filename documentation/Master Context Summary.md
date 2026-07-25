# DSG Omnichannel Engine — Master Context Summary

## 0. AI Assistant Directives (Meta-Instructions)
* **Persona**: Act as a senior .NET developer assisting me in building a distributed omnichannel order engine.
* **Execution Rules**: Guide me step-by-step through the Execution Roadmap using a **4-Stage Interactive Gated Flow**:
  1. *Stage 1: Concept Overview (With AI)* — Present architectural concepts and design patterns only. Pause and wait for explicit confirmation/understanding before providing any code or prompts.
  2. *Stage 2: Copilot Prompt (For Copilot)* — Provide a clean, laser-focused prompt designed specifically for GitHub Copilot to scaffold or update code. For any entity or schema changes, explicitly include instructions for Copilot to create and run the EF Core migration directly.
  3. *Stage 3: Interactive Step-by-Step Verification (With AI)* — Guide the verification process interactively, one single step at a time. Adjust dynamically based on terminal/database outputs, and confirm success before closing the milestone slice.
  4. *Stage 4: Narrative Summary & Next Bridge (With AI)* — Provide a narrative summary explaining what was tested, the outcome, why it occurred, how it matches expectations, and how the upcoming slice connects.
* **Context Window Optimization Rule**:
  * Keep all AI responses ultra-concise, lean, and direct. Avoid unsolicited architectural analysis, verbose breakdowns, or unnecessary fluff to minimize context window consumption.
* **SQL Query & Parameters Protocol**:
  * **Parameter Inquiries**: Never output SQL queries with placeholders. Explicitly ask for required values (like `OrderId`) first, then generate the finalized query ready to run.
  * **JSON Output Formatting**: Format all SQL verification queries using `FOR JSON PATH` so results can be easily pasted and verified as JSON text.
* **Workflow Integrity & Stage 3 Reset Rule**:
  * Any code modification, fix, or logic tweak made during a slice invalidates previous test runs.
  * When a change occurs, immediately restart the workflow at **Stage 3: Interactive Step-by-Step Verification** from Step 1.
  * Proceed to **Stage 4: Narrative Summary & Next Bridge** only after Stage 3 is fully executed and verified. Never bridge to the next slice until Stage 4 is concluded.
* **Context Maintenance & Startup Rule**:
  * Keep `Master Context Summary.md`, `Architecture & Design Concepts.md`, and `Solution Schema & Contracts.md` updated chronologically.[cite: 1]
  * **New Session Protocol**: At the start of any new chat session, immediately generate a Copilot prompt for the user to run locally to refresh `Solution Schema & Contracts.md` for seeding context.

## 1. Project Brief & Core Objective
* **Target Domain**: Dick's Sporting Goods — Omnichannel "Buy Online, Pick Up In-Store" (BOPIS) Order Processing Engine.
* **Core Architectural Goal**: Demonstrate senior-level mastery of distributed systems resilience, transactional integrity, end-to-end security context propagation, and real-time frontend updates without mock flags.
* **Key Patterns**: Transactional Outbox Pattern, Consumer Idempotency (Inbox Pattern), Domain Fallback Routing, Sagas (State Machines), Decoupled Event-Driven Audit Logging, Exponential Backoff Retries & Exception Filtering, Real-Time WebSockets via SignalR, JWT Security Context Propagation.

## 2. Tech Stack & Infrastructure
* **Backend**: C# / .NET 10, ASP.NET Core Web API, Background Worker Service, EF Core 10, SQL Server 2022 (Docker with named volume `dsg-sqldata`).
* **Security**: JWT Bearer Token Authentication, Policy-Based Authorization (`RequireCustomerRole`), RabbitMQ Header Context Propagation (`UserId`, `CorrelationId`).
* **Messaging & Resilience**: MassTransit + RabbitMQ (Outbox/Inbox, Exponential Backoff Retries, Dead-Letter Queue / Poison Messages, Exception Filtering, State Machines).
* **Frontend (Planned)**: Angular 19+ (Signals, `computed()`, `toSignal()`, Native Control Flow `@if`/`@for`, `@defer` views, Functional HTTP Interceptors for JWT auth).
* **Real-Time Stream (Planned)**: SignalR Hub pushing event payloads directly to Angular Signals.
* **Infrastructure Management**: Containers run via Docker Compose. Daily lifecycle managed using `docker compose stop` and `docker compose start` to retain SQL Server schema and outbox data in `dsg-sqldata`.[cite: 1]

## 3. Engineering & Workflow Preferences
* **Slice-Based Execution**: Strict separation of complex milestones into thin, verifiable slices.[cite: 1]
* **Narrative Stage 4 Summaries**: High-level storytelling explaining test case, outcome, reasoning, expectation match, and next-slice connection.
* **Automated Migrations in Stage 2**: Prompts for data structural changes must direct Copilot to create and run `dotnet ef` migrations as part of prompt execution.
* **Database Queries via Visual Studio**: Executed in Visual Studio targeting JSON outputs.
* **Repository Instructions**: Synchronized `.github/copilot-instructions.md` directing Copilot to use dedicated extension classes for clean `Program.cs`, reuse `ApplicationDbContext` across host projects (`Api` and `Worker`), and keep event contracts self-contained.
* **Terminal Commands**: Standard `.NET CLI` syntax and standard Docker commands.
* **Architectural Standard**: Strict Clean Architecture (Separation of host, contracts, domain, and infrastructure).[cite: 1] State entities (`OrderState`) belong in Infrastructure to avoid circular dependencies.

## 4. Verified Project Structure
* `DsgOmnichannel.Api/` (Controllers, Extensions, appsettings.json)
* `DsgOmnichannel.Worker/` (Consumers, StateMachines, Extensions, Worker.cs)
* `DsgOmnichannel.Contracts/` (Events: `PingEvent`, `OrderPlacedEvent`, `StoreInventoryAllocatedEvent`, `AllocationFailedEvent`)
* `DsgOmnichannel.Domain/` (Entities: `AuditLog`, `Order`, `OrderStatusHistory`, `StoreInventory`)
* `DsgOmnichannel.Infrastructure/` (Persistence: `ApplicationDbContext`, Migrations, Saga Entities)

## 5. Execution Roadmap & Milestone Status

### Milestone 0: Baseline Infrastructure & Messaging (COMPLETED)
* [x] Multi-project .NET 10 solution created.
* [x] Docker Compose configured for SQL Server and RabbitMQ.
* [x] `ApplicationDbContext` created with isolated migrations in Infrastructure.

### Milestone 1: Domain Models, Contracts & Security Setup (COMPLETED)
* [x] Define Domain Entities and C# Event Contracts.
* [x] Configure JWT Bearer Token Authentication & Policy-Based Authorization.

### Milestone 2: Transactional Outbox & Order Endpoint (COMPLETED)
* [x] Configure MassTransit EF Core Outbox in API and Worker.
* [x] Implement `POST /api/orders` REST endpoint with atomic local DB transaction.[cite: 1]

### Milestone 3: Background Worker Processing & Consumer Idempotency (COMPLETED)
* [x] Configure MassTransit EF Core Inbox pattern for message deduplication.[cite: 1]
* [x] Implement `OrderPlacedEventConsumer` fulfillment logic.

### Milestone 4: Resilient Edge Cases, Sagas & Dead-Letter Queue (COMPLETED)
* **Slice A: Saga Foundation & Persistence (COMPLETED & VERIFIED)**
  * [x] Create `OrderState` instance entity in Infrastructure and `OrderStateMachine` in Worker.
  * [x] Migration applied: `dbo.OrderState` exists in database.
  * [x] Verified: Order creation initializes saga state to `Processing` in SQL Server.
* **Slice B: Compensating Transactions & Audit Trail (COMPLETED & VERIFIED)**
  * [x] Handle `AllocationFailedEvent` to transition state to `Faulted` and update order status.
  * [x] Verified: Stock shortage order sets `dbo.Orders.Status` to `"AllocationFailed"` and `dbo.OrderState.CurrentState` to `"Faulted"` with exact failure details.
  * [x] **Slice B.1**: Implement decoupled, event-driven status tracking (`OrderStatusHistoryConsumer`) via EF Core migration `AddOrderStatusHistoryTable`.
  * [x] Verified: `dbo.OrderStatusHistory` asynchronously captures timeline entries (`Submitted` -> `AllocationFailed`) without direct controller coupling.
* **Slice C: Resilience, Retries & DLQ Exception Filtering (COMPLETED & VERIFIED)**
  * [x] Configure MassTransit exponential retry policy (`3 retries`: ~1s, ~3s, ~5s) in `DsgOmnichannel.Worker`.[cite: 1]
  * [x] Configure exception filtering (`TimeoutException` triggers retries; `ArgumentException` skips retries immediately).
  * [x] Verified: Simulated transient faults execute 3 retries before dead-letter routing (`R-RETRY` -> `R-FAULT`), and simulated payload domain faults trigger instant DLQ movement to `OrderPlacedEvent_error` on RabbitMQ.

### Milestone 5: Angular 19+ Dashboard, SignalR Integration & Interview Prep (PENDING)
* [ ] Scaffold Angular standalone app with Signals, modern Control Flow, and Functional HTTP Interceptors.
* [ ] Wire ASP.NET Core SignalR hub to dispatch events to frontend.
* [ ] Final Artifact: Generate `INTERVIEW_TALKING_POINTS.md` summary for senior technical interview prep.