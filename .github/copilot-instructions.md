# DSG Omnichannel Engine — Copilot Execution Directives

## 1. Project Guidelines & Workflow
* **Clean Program.cs Rule**: Keep `Program.cs` clean and concise by extracting service registrations, middleware pipelines, and host configurations into dedicated extension classes (e.g., `ServiceCollectionExtensions.cs`).
* **Design Approval First**: Present and review high-level architectural ideas or implementation plans before generating large code changes or implementations.

## 2. System Architecture & Infrastructure Principles
* **Architecture Pattern**: Clean Architecture with strict separation between host layers (`Api`, `Worker`), shared domain contracts, core domain logic, and persistence infrastructure[cite: 1, 2].
* **Shared Database Context**: Always reuse `ApplicationDbContext` from `DsgOmnichannel.Infrastructure` across all host applications (`Api` and `Worker`)[cite: 2]. Never create separate DbContext definitions for worker processing[cite: 2].
* **Messaging & Resilience**: Use MassTransit over RabbitMQ[cite: 2]. Leverage the EF Core Transactional Outbox pattern on the publishing side (`Api`) and the EF Core Inbox pattern on the consuming side (`Worker`) for consumer idempotency (`.UseEntityFrameworkCoreModel()` & `.UseConsumerOutbox()`)[cite: 1, 2].
* **Self-Contained Event Contracts**: Events in `DsgOmnichannel.Contracts` represent immutable domain facts[cite: 1, 2]. Events MUST contain all data required for downstream execution (e.g., `OrderPlacedEvent` MUST include `StoreId`, `OrderId`, product details, and quantity directly)[cite: 2]. Consumers must not query back to the primary database host solely to look up basic event context[cite: 2].

## 3. Tech Stack & Environment Baseline
* **Framework**: .NET 10 / C# 13[cite: 2].
* **Persistence**: Entity Framework Core 10, SQL Server 2022 (Docker with named volume `dsg-sqldata`)[cite: 2].
* **Security**: JWT Bearer Token Authentication, Policy-Based Authorization, Header Context Propagation via MassTransit[cite: 2].
* **Frontend (Target)**: Angular 19+ (Signals, Functional HTTP Interceptors)[cite: 2].

## 4. Project File Organization & Namespaces
When generating code, adhere strictly to the existing solution layout:
* **`DsgOmnichannel.Contracts/Events/`**: Strongly-typed C# message contracts (`OrderPlacedEvent`, `StoreInventoryAllocatedEvent`, `AllocationFailedEvent`)[cite: 2].
* **`DsgOmnichannel.Domain/Entities/`**: Pure POCO domain models (`Order`, `StoreInventory`, `AuditLog`) isolated from EF Core annotations[cite: 1, 2].
* **`DsgOmnichannel.Infrastructure/Persistence/`**: `ApplicationDbContext` and EF Core entity type configurations[cite: 2].
* **`DsgOmnichannel.Worker/Consumers/`**: MassTransit consumer implementations (e.g., `OrderPlacedEventConsumer`)[cite: 2].
* **`DsgOmnichannel.Api/Controllers/`**: ASP.NET Core REST API controllers[cite: 2].

## 5. Execution Directives
* **No Speculative Decisions**: Assume shared infrastructure and models are centralized in `DsgOmnichannel.Infrastructure`[cite: 2].
* **Idempotency Standards**: Consumers using `ApplicationDbContext` must rely on MassTransit's native EF Core `InboxState` evaluation[cite: 1, 2]. Do not invent manual custom duplicate checking logic inside consumer bodies when MassTransit Inbox is enabled.

## 6. Developer Workflow & Response Guidelines
* **Step-by-Step Verification**: User validates each small step before proceeding to the next. Never assume multiple follow-up steps or create elaborate multi-step plans unless explicitly requested.
* **Ask Before Planning**: If the task seems to require multi-step coordination, ask the user for clarification first rather than creating a detailed plan.
* **Single Task Focus**: Address one discrete task at a time. When the user says "do X", respond with exactly X, not X + Y + Z.
* **POC Scope**: This is a thin vertical-slice demo for an omnichannel BOPIS (Buy Online Pick It Up In Store) POC. Do NOT:
  - Over-engineer for production environments
  - Create multi-platform support code
  - Generate configuration for multiple environments
  - Build elaborate test harnesses or automation frameworks
  - Assume enterprise-scale requirements
* **Response Type**: Generate simple, inline examples in the chat when asked (e.g., SQL queries, curl commands, HTTP payloads). Only create files when explicitly requested. Keep responses concise and focused.
* **Minimal Context Gathering**: Avoid excessive exploration of the codebase before responding. Read only what's necessary to answer the specific question.
* **Verify Configuration Before Generating Examples**: Always check `launchSettings.json` for actual port and protocol before generating HTTP requests or commands targeting the API. Never assume http://localhost:5000 - verify the actual configuration.
