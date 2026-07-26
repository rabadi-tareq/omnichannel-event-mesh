# Software Requirements Specification (SRS) & Architecture Blueprint
**Project:** DSG Omnichannel Engine (BOPIS)

## 1. Business Vision & Scope
The DSG Omnichannel Engine is a high-throughput "Buy Online, Pick Up In-Store" (BOPIS) order processing system designed for retail scale. It strictly decouples client-facing REST APIs from backend processing mechanisms using distributed messaging, local transactions, and state machine sagas.

## 2. Functional Requirements
* **FQ-01: Order Ingestion & Validation:** The system must accept order creation requests via a REST API and persist them reliably.
* **FQ-02: Inventory Reservation & Deduction:** The background processing engine must successfully allocate inventory for incoming orders or raise failure events if stock is insufficient.
* **FQ-03: Real-Time Status Tracking:** The system must asynchronously build chronological order timelines and push live state updates to a reactive, modern frontend using WebSockets.
* **FQ-04: Automated Failure Compensation:** Complex, multi-step business flows must revert order statuses when physical store inventory is insufficient using compensating transactions.

## 3. Non-Functional Requirements (NFRs)
* **Resiliency & Zero Data Loss:** The system must ensure zero data loss between HTTP request ingestion and background processing.
* **Eventual Consistency & Idempotency:** The system must prevent duplicate processing if events are redelivered, ensuring inventory is not deducted twice.
* **UI Reactivity:** The frontend must render sub-second real-time state changes without manual browser refreshing.

## 4. Comprehensive Multi-Component Architecture
The solution is evaluated and structured as a comprehensive multi-component solution built on .NET 10, utilizing Clean Architecture to ensure separation of concerns and dependency inversion.
* **Edge API Gateway (`DsgOmnichannel.Api`):** The client-facing edge layer handling JWT security, REST controllers, and real-time SignalR Hubs.
* **Background Processing Engine (`DsgOmnichannel.Worker`):** The background engine hosting MassTransit consumers, the Outbox/Inbox processors, and Saga State Machines.
* **Reactive Frontend (`DsgOmnichannel.Web`):** The Angular 22 frontend utilizing Zoneless change detection and Angular Signals.

## 5. Architectural Decision Records (ADRs)
* **ADR 001: Using the Transactional Outbox Pattern:** To prevent dual-write failures (e.g., saving to the database but failing to publish the message), the `POST /api/orders` endpoint atomically commits the `Order` entity and the `OrderPlacedEvent` into a local SQL Server transaction via MassTransit and EF Core 10.
* **ADR 002: Implementing Saga State Machines:** To manage complex, multi-step order lifecycles and distributed failures, MassTransit State Machines (`OrderStateMachine`) were chosen over point-to-point choreography, tracking instance state durably in SQL Server (`dbo.OrderStates`).
* **ADR 003: WebSockets via SignalR:** Bypassed standard HTTP polling by implementing ASP.NET Core SignalR (`/hubs/order`) to push live domain payloads directly to connected Angular clients, instantly mutating the DOM via Angular Signals.
* **ADR 004: Strict Exception Filtering:** Implemented strict exception filtering to immediately route domain exceptions (like `ArgumentException`) to a Dead-Letter Queue (DLQ), bypassing useless retries and freeing up worker threads for transient downstream failures.