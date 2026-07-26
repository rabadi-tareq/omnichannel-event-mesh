# DSG Omnichannel Engine — Master Context Summary

## 0. AI Assistant Directives (Meta-Instructions)
* **Persona**: Act as a senior .NET & distributed systems architect assisting in building an enterprise omnichannel order engine[cite: 6].
* **Output Format Rule**: Always generate updates to the `Master Context Summary.md` and `Architecture & Design Concepts.md` inside a markdown code block so the raw text can be easily copied and saved locally[cite: 6].
* **Collaboration & Execution Model**:
  * **Strategic Architect (Gemini Pro)**: High-level architecture, document alignment, design pattern strategy, prompt engineering, and root-cause troubleshooting[cite: 6].
  * **Local Context Executor (GitHub Copilot Pro+)**: Direct execution of local file modifications, EF Core migrations, extension class creation, and unit/integration test scaffolding inside Visual Studio[cite: 6].
* **Execution Rules**: Guide step-by-step using a **4-Stage Interactive Gated Flow**:
  1. *Stage 1: Concept Overview (With AI)* — Architectural concepts & design patterns only. Pause for confirmation before providing code/prompts[cite: 6].
  2. *Stage 2: Copilot Prompt (For Copilot)* — Clean, focused prompt for GitHub Copilot. For schema changes, direct Copilot to create and run EF Core migrations[cite: 6].
  3. *Stage 3: Interactive Verification (With AI)* — Step-by-step verification using API calls, database queries, or test runners. Adjust dynamically based on outputs[cite: 6].
  4. *Stage 4: Narrative Summary & Next Bridge (With AI)* — Storytelling summary of what was tested, the outcome, why it occurred, and how it bridges to the next slice[cite: 6].
* **Context Optimization**: Keep responses lean and direct. Avoid unsolicited walls of code or generic architectural fluff[cite: 6].
* **Database & Query Protocol**:
  * **Parameter Inquiries**: Ask for required runtime IDs before providing SQL scripts[cite: 6].
  * **JSON Formatting**: Format SQL verification outputs using `FOR JSON PATH`[cite: 6].

---

## 1. Project Brief & Core Architecture
* **Target Domain**: Dick's Sporting Goods — Omnichannel "Buy Online, Pick Up In-Store" (BOPIS) Order Engine[cite: 6].
* **Core Goal**: Enterprise-grade demonstration of distributed systems resilience, transactional integrity, security context propagation, and real-time reactive UI[cite: 6].
* **Architecture**: Clean Architecture (Domain, Contracts, Infrastructure, API, Worker, Web)[cite: 6].
* **Core Patterns**:
  * Transactional Outbox Pattern (MassTransit + EF Core 10)[cite: 6]
  * Consumer Idempotency / Inbox Pattern[cite: 6]
  * Saga State Machine Orchestration (`OrderStateMachine`)[cite: 6]
  * Decoupled Event-Driven Audit Logging (`dbo.OrderStatusHistory`)[cite: 6]
  * Real-Time Event Streaming (SignalR Hub `/hubs/order` -> Angular Signals)[cite: 6]

---

## 2. Tech Stack & Infrastructure
* **Backend**: C# / .NET 10, ASP.NET Core Web API, Background Worker Service, EF Core 10, SQL Server 2022 (Docker volume `dsg-sqldata`)[cite: 6].
* **Messaging**: MassTransit + RabbitMQ (Outbox, Inbox, DLQ, Exponential Backoff Retries)[cite: 6].
* **Frontend**: Angular 22 (Zoneless Change Detection, Angular Signals, `@microsoft/signalr`)[cite: 6].
* **Testing Stack (Phase 2)**: xUnit, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`), Testcontainers for SQL Server & RabbitMQ[cite: 6].

---

## 3. Project Lifecycle & Execution Roadmap

### Phase 1: Foundation & Baseline Features (COMPLETED)
* [x] **Milestone 0**: Multi-project .NET 10 solution, Docker Compose for SQL Server & RabbitMQ[cite: 6].
* [x] **Milestone 1**: Domain Entities, C# Contracts (`OrderPlacedEvent`, `AllocationFailedEvent`, etc.), JWT Security Setup[cite: 6].
* [x] **Milestone 2**: `POST /api/orders` REST endpoint with MassTransit EF Core Outbox[cite: 6].
* [x] **Milestone 3**: `OrderPlacedEventConsumer` background fulfillment & Inbox deduplication[cite: 6].
* [x] **Milestone 4**: State Machine Saga (`OrderState`), compensating transaction routing, and exponential retries/DLQ[cite: 6].
* [x] **Milestone 5**: Angular 22 dashboard scaffolding & SignalR `/hubs/order` real-time integration[cite: 6].

---

### Phase 2: Architectural Hardening, Integration Testing & UI Expansion (IN PROGRESS)

#### Slice 2.1 — Worker Outbox Protection for Follow-up Publishes
* **Goal**: Enable `UseEntityFrameworkOutbox<ApplicationDbContext>` inside `DsgOmnichannel.Worker`[cite: 6].
* **Problem**: Follow-up events (`StoreInventoryAllocatedEvent` / `AllocationFailedEvent`) inside `OrderPlacedEventConsumer` currently execute after `SaveChangesAsync` without outbox staging, risking lost events on worker crashes[cite: 6].
* **Target State**: Atomic commit of inventory updates and outgoing events within the worker consumer transaction[cite: 6].

#### Slice 2.2 — Decouple Saga State Machine from Direct Data Access
* **Goal**: Remove direct `ApplicationDbContext` resolution from `OrderStateMachine`[cite: 6].
* **Problem**: Resolving `ApplicationDbContext` inside `ThenAsync` creates uncommitted split transactions between `dbo.OrderState` and `dbo.Orders`[cite: 6].
* **Target State**: Saga emits an internal command (`MarkOrderAllocationFailedCommand`), handled by a dedicated stateless consumer with full inbox/outbox protection[cite: 6].

#### Slice 2.3 — SignalR Client/Server Deduplication Guard
* **Goal**: Prevent duplicate WebSocket updates on client browsers during message redeliveries[cite: 6].
* **Target State**: Include a unique `MessageId` or correlation sequence in `ReceiveOrderUpdate` SignalR payloads and implement client-side deduplication in Angular `SignalRService`[cite: 6].

#### Slice 2.4 — Automated Integration Testing Project (`DsgOmnichannel.IntegrationTests`)
* **Goal**: Scaffold a dedicated xUnit test suite using `WebApplicationFactory` and Testcontainers[cite: 6].
* **Target State**: End-to-end integration tests verifying HTTP `POST /api/orders` flow down to SQL Server persistence and RabbitMQ outbox dispatch without manually running local Docker containers[cite: 6].
* **Execution Plan**:
  * **Step 1: Scaffolding & Infrastructure Provisioning:** Create the `.NET 10` xUnit test project. Integrate `Testcontainers.MsSql` and `Testcontainers.RabbitMq`. Configure the `WebApplicationFactory` to override the default connection strings and point the API/Worker hosts to the ephemeral containers.
  * **Step 2: Dual-Write & Outbox Verification:** Implement tests that intentionally block broker connectivity during the `POST /api/orders` request to verify the transaction successfully commits to `dbo.OutboxMessage` locally without throwing a 500 error.
  * **Step 3: Multi-Component Saga Testing:** Evaluate the architecture as a comprehensive multi-component solution by executing an end-to-end distributed rollback. Submit an order for an out-of-stock item, trace the domain events across the application boundaries, and verify that `dbo.Orders.Status` and `dbo.OrderState.CurrentState` properly reflect the `AllocationFailed` and `Faulted` states respectively.
  * **Step 4: Resilience & Idempotency Guards:** Publish duplicate `OrderPlacedEvent` payloads via MassTransit test harnesses to assert the Inbox prevents double inventory deduction. Inject simulated `TimeoutException` faults to verify the exponential backoff policy behavior.

#### Slice 2.5 — Angular UI Feature Expansion
* **Goal**: Expand `DsgOmnichannel.Web` beyond the live stream dashboard[cite: 6].
* **Target State**: Add store inventory status management UI, order filter/search components, and a visual Saga status timeline per order[cite: 6].