# DSG Omnichannel Engine — Architecture & Design Concepts

## 1. System Vision & Overview
The DSG Omnichannel Engine is a highly resilient, event-driven "Buy Online, Pick Up In-Store" (BOPIS) order processing system[cite: 1]. Designed for high-throughput retail scale, it strictly decouples client-facing REST APIs from backend processing mechanisms using distributed messaging, local transactions, and state machine sagas[cite: 1]. The system pushes live state updates to a reactive, modern frontend using real-time WebSockets[cite: 1].

## 2. Clean Architecture & Solution Segregation
The solution is structured around the principles of Clean Architecture to ensure separation of concerns and dependency inversion[cite: 1].
* **Domain (`DsgOmnichannel.Domain`)**: The absolute core. Contains pure entities (`Order`, `StoreInventory`, `AuditLog`, `OrderStatusHistory`) and lacks any framework dependencies (no EF Core, no MassTransit)[cite: 1, 2].
* **Contracts (`DsgOmnichannel.Contracts`)**: Self-contained integration events (e.g., `OrderPlacedEvent`, `AllocationFailedEvent`) shared across boundaries[cite: 1, 2].
* **Infrastructure (`DsgOmnichannel.Infrastructure`)**: Contains database implementations (`ApplicationDbContext`), Entity Framework Core 10 migrations, and specific Saga persistence entities (`OrderState`)[cite: 1, 2]. 
* **API Host (`DsgOmnichannel.Api`)**: The client-facing edge layer[cite: 1]. Handles JWT security, REST controllers (`OrdersController`), and real-time SignalR Hubs (`OrderHub`)[cite: 1, 2].
* **Worker Host (`DsgOmnichannel.Worker`)**: The background processing engine[cite: 1]. Hosts MassTransit consumers, the Outbox/Inbox processors, and the Saga State Machines[cite: 1].
* **Web UI (`DsgOmnichannel.Web`)**: The Angular 22 frontend utilizing Zoneless change detection and Angular Signals[cite: 1].

## 3. Distributed Messaging & Transactional Integrity
* **Transactional Outbox Pattern**: Implemented via MassTransit and EF Core 10[cite: 1]. When a client submits an order via `POST /api/orders`, the `Order` entity and the `OrderPlacedEvent` message are committed to SQL Server in a single, atomic local database transaction[cite: 2]. This guarantees zero data loss (no dual-write failures) before the message is published to RabbitMQ[cite: 1].
* **Inbox Pattern (Consumer Idempotency)**: Prevents duplicate processing[cite: 1]. If RabbitMQ redelivers an event due to a transient network drop, the MassTransit EF Core Inbox ensures the `OrderPlacedEventConsumer` does not deduct store inventory twice[cite: 1, 2].
* **Decoupled Event-Driven Auditing**: Features like order history tracking are handled asynchronously[cite: 1]. The `OrderStatusHistoryConsumer` listens for domain events and writes to `dbo.OrderStatusHistories` without coupling to or slowing down the primary API request pipeline[cite: 1, 2].

## 4. Saga Orchestration & Distributed Transactions
* **State Machine Sagas**: Complex, multi-step business flows are orchestrated using MassTransit State Machines (`OrderStateMachine`)[cite: 1, 2]. The Saga instance state (`Processing`, `Faulted`) is durably persisted in SQL Server (`dbo.OrderStates`)[cite: 1, 2].
* **Compensating Transactions**: In distributed systems, traditional rollback is impossible. If the `OrderPlacedEventConsumer` cannot find sufficient stock, it publishes an `AllocationFailedEvent`[cite: 2]. The Saga intercepts this event, logs the failure reason, transitions into a `Faulted` state, and executes a compensating transaction that updates the main `Order` entity's status to `AllocationFailed`[cite: 1, 2].

## 5. Resilience & Fault Tolerance
* **Exponential Backoff Retries**: The `DsgOmnichannel.Worker` is configured to gracefully handle transient downstream failures (e.g., database locks or timeout exceptions) by applying a 3-tier exponential backoff policy (~1s, ~3s, ~5s)[cite: 1].
* **Exception Filtering**: Domain exceptions (e.g., `ArgumentException` for an invalid payload) are filtered to bypass retries entirely, preventing worker threads from being tied up on inherently unresolvable errors[cite: 1].
* **Dead-Letter Queues (DLQ)**: Poison messages that exhaust their retry limits or hit filtered exceptions are safely routed to DLQ exchanges in RabbitMQ (e.g., `OrderPlacedEvent_error`) for manual replay or analysis[cite: 1].

## 6. Real-Time Telemetry & UI Stream
* **WebSockets via SignalR**: The ASP.NET Core SignalR implementation (`/hubs/order`) provides a persistent, full-duplex WebSocket connection to client browsers[cite: 1, 2].
* **Reactive Frontend**: The Angular 22 dashboard is built around modern Web primitives, strictly avoiding `zone.js` for performance[cite: 1]. Backend consumers (like the status history tracker) inject `IHubContext` and broadcast live domain payloads (`ReceiveOrderUpdate`) directly to connected SignalR clients, which instantly map the data into Angular Signals to mutate the DOM without a page refresh[cite: 1, 2].

## 7. Security Context Propagation
* **Edge Security**: The `DsgOmnichannel.Api` is secured using JWT Bearer Authentication and Policy-Based Authorization (`RequireCustomerRole`)[cite: 1].
* **Context Flow**: Identifiers and request tracking metrics (e.g., `UserId`, `CorrelationId`) are natively passed downstream via MassTransit/RabbitMQ headers, preserving the user's security context even inside decoupled background worker threads[cite: 1].