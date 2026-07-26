# DSG Omnichannel Engine — Master Context Summary

## 0. AI Assistant Directives (Meta-Instructions)
* **Persona**: Act as a senior .NET developer assisting me in building a distributed omnichannel order engine.
* **Collaboration & Execution Model**:
  * **Strategic Architect (Gemini)**: Provides high-level architecture, design patterns, exact prompt definitions, and handles root-cause troubleshooting when local fixes fail.
  * **Local Context Executor (GitHub Copilot Pro+)**: Primary engine for executing local file modifications, path/import realignment, and environment-specific fixes directly inside Visual Studio.
* **Execution Rules**: Guide me step-by-step through the Execution Roadmap using a **4-Stage Interactive Gated Flow**:
  1. *Stage 1: Concept Overview (With AI)* — Present architectural concepts and design patterns only. Pause and wait for explicit confirmation/understanding before providing any code or prompts.
  2. *Stage 2: Copilot Prompt (For Copilot)* — Provide a clean, laser-focused prompt designed specifically for GitHub Copilot to scaffold or update code. For entity/schema changes, instruct Copilot to create and run EF Core migrations directly.
  3. *Stage 3: Interactive Step-by-Step Verification (With AI)* — Guide verification interactively, one step at a time. Adjust dynamically based on terminal/database outputs, confirming success before closing the slice.
  4. *Stage 4: Narrative Summary & Next Bridge (With AI)* — Provide a narrative summary explaining what was tested, the outcome, why it occurred, how it matches expectations, and how the upcoming slice connects.
* **Context Window Optimization Rule**: Keep all AI responses ultra-concise, lean, and direct. Avoid unsolicited architectural analysis or unnecessary fluff.
* **SQL Query & Parameters Protocol**:
  * **Parameter Inquiries**: Never output SQL queries with placeholders. Explicitly ask for required values (like `OrderId`) first, then generate the finalized query.
  * **JSON Output Formatting**: Format all SQL verification queries using `FOR JSON PATH` for easy verification.
* **Workflow Integrity & Stage 3 Reset Rule**:
  * Any code modification or logic tweak made during a slice invalidates previous test runs. Restart immediately at **Stage 3** from Step 1.
* **Context Maintenance & Startup Rule**:
  * Keep `Master Context Summary.md`, `Architecture & Design Concepts.md`, and `Solution Schema & Contracts.md` updated chronologically.
  * **New Session Protocol**: At the start of a new chat session, generate a Copilot prompt to refresh `Solution Schema & Contracts.md` for seeding context.

## 1. Project Brief & Core Objective
* **Target Domain**: Dick's Sporting Goods — Omnichannel "Buy Online, Pick Up In-Store" (BOPIS) Order Processing Engine.
* **Core Architectural Goal**: Demonstrate senior-level mastery of distributed systems resilience, transactional integrity, end-to-end security context propagation, and real-time frontend updates without mock flags.
* **Key Patterns**: Transactional Outbox Pattern, Consumer Idempotency (Inbox Pattern), Domain Fallback Routing, Sagas (State Machines), Decoupled Event-Driven Audit Logging, Exponential Backoff Retries & Exception Filtering, Real-Time WebSockets via SignalR, JWT Security Context Propagation.

## 2. Tech Stack & Infrastructure
* **Backend**: C# / .NET 10, ASP.NET Core Web API, Background Worker Service, EF Core 10, SQL Server 2022 (Docker with named volume `dsg-sqldata`).
* **Frontend**: Angular 22 (Standalone components, Zoneless Change Detection via `provideExperimentalZonelessChangeDetection`, Angular Signals), `@microsoft/signalr` client library, hosted via Visual Studio JavaScript/TypeScript project (`.esproj`).
* **Security**: JWT Bearer Token Authentication, Policy-Based Authorization (`RequireCustomerRole`), RabbitMQ Header Context Propagation (`UserId`, `CorrelationId`).
* **Messaging & Resilience**: MassTransit + RabbitMQ (Outbox/Inbox, Exponential Backoff Retries, Dead-Letter Queue / Poison Messages, Exception Filtering, State Machines).
* **Real-Time Stream**: SignalR Hub (`/hubs/order`) pushing event payloads directly to Angular Signals on the frontend.
* **Infrastructure Management**: Containers run via Docker Compose (`docker compose stop`/`start`). Visual Studio configured for **Multiple Startup Projects (F5)** running `Api`, `Worker`, and `Web` simultaneously.

## 3. Engineering & Workflow Preferences
* **Slice-Based Execution**: Strict separation of complex milestones into thin, verifiable slices.
* **Copilot-First Code Modifications**: Leverage GitHub Copilot Pro+ inside Visual Studio for local code edits, workspace type fixes, and path resolutions.
* **Narrative Stage 4 Summaries**: High-level storytelling explaining test case, outcome, reasoning, expectation match, and next-slice connection.
* **Automated Migrations in Stage 2**: Prompts for data structural changes direct Copilot to execute `dotnet ef` migrations as part of prompt execution.
* **Database Queries via Visual Studio**: Executed in Visual Studio targeting JSON outputs.
* **Repository Instructions**: Synchronized `.github/copilot-instructions.md` directing Copilot to use dedicated extension classes for `Program.cs`, reuse `ApplicationDbContext` across host projects, and keep event contracts self-contained.
* **Architectural Standard**: Strict Clean Architecture (Separation of host, contracts, domain, and infrastructure). State entities (`OrderState`) belong in Infrastructure.

## 4. Verified Project Structure
* `DsgOmnichannel.Api/` (Controllers, SignalR Hubs, Extensions, appsettings.json)
* `DsgOmnichannel.Worker/` (Consumers, StateMachines, Extensions, Worker.cs)
* `DsgOmnichannel.Contracts/` (Events: `PingEvent`, `OrderPlacedEvent`, `StoreInventoryAllocatedEvent`, `AllocationFailedEvent`)
* `DsgOmnichannel.Domain/` (Entities: `AuditLog`, `Order`, `OrderStatusHistory`, `StoreInventory`)
* `DsgOmnichannel.Infrastructure/` (Persistence: `ApplicationDbContext`, Migrations, Saga Entities)
* `DsgOmnichannel.Web/` (Angular 22 SPA: `services/signalr.service.ts`, `components/order-dashboard/`, `proxy.conf.json`)

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
* [x] Implement `POST /api/orders` REST endpoint with atomic local DB transaction.

### Milestone 3: Background Worker Processing & Consumer Idempotency (COMPLETED)
* [x] Configure MassTransit EF Core Inbox pattern for message deduplication.
* [x] Implement `OrderPlacedEventConsumer` fulfillment logic.

### Milestone 4: Resilient Edge Cases, Sagas & Dead-Letter Queue (COMPLETED)
* **Slice A: Saga Foundation & Persistence (COMPLETED & VERIFIED)**
  * [x] Create `OrderState` instance entity in Infrastructure and `OrderStateMachine` in Worker.
  * [x] Migration applied: `dbo.OrderState` exists in database.
  * [x] Verified: Order creation initializes saga state to `Processing` in SQL Server.
* **Slice B: Compensating Transactions & Audit Trail (COMPLETED & VERIFIED)**
  * [x] Handle `AllocationFailedEvent` to transition state to `Faulted` and update order status.
  * [x] Verified: Stock shortage order sets `dbo.Orders.Status` to `"AllocationFailed"` and `dbo.OrderState.CurrentState` to `"Faulted"`.
  * [x] **Slice B.1**: Implement decoupled, event-driven status tracking (`OrderStatusHistoryConsumer`) via EF Core migration `AddOrderStatusHistoryTable`.
  * [x] Verified: `dbo.OrderStatusHistory` asynchronously captures timeline entries (`Submitted` -> `AllocationFailed`).
* **Slice C: Resilience, Retries & DLQ Exception Filtering (COMPLETED & VERIFIED)**
  * [x] Configure MassTransit exponential retry policy in `DsgOmnichannel.Worker`.
  * [x] Configure exception filtering (`TimeoutException` retries; `ArgumentException` skips retries).
  * [x] Verified: Simulated transient faults execute 3 retries before dead-letter routing, and domain faults trigger instant DLQ movement on RabbitMQ.

### Milestone 5: Angular 22 Dashboard, SignalR Integration & Real-Time Stream (COMPLETED)
* **Slice 1: Angular 22 Client Scaffolding & SignalR Service Setup (COMPLETED & VERIFIED)**
  * [x] Scaffold Angular 22 Zoneless app inside `DsgOmnichannel.Web` (`.esproj`).
  * [x] Install `@microsoft/signalr` client library.
  * [x] Implement `SignalRService` using Angular Signals to stream real-time updates.
  * [x] Create `OrderDashboardComponent` with status badges and live event display cards.
  * [x] Configure Visual Studio Multiple Startup Projects (`Api`, `Worker`, `Web`).
* **Slice 2: End-to-End Real-Time Event Streaming Verification (COMPLETED & VERIFIED)**
  * [x] Verify SignalR WebSocket handshake (`Connected` status) across Visual Studio F5 launch.
  * [x] Trigger order placement via API and verify instant dashboard render without page refresh (verified `Allocated` and `AllocationFailed` state propagation).
* **Slice 3: Interview Talking Points & Final Architecture Wrap-up (COMPLETED)**
  * [x] Generate `INTERVIEW_TALKING_POINTS.md` summary for senior technical interview prep.

---
**PROJECT STATUS: COMPLETE**