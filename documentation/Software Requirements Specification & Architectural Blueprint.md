# DSG Omnichannel Engine — Software Requirements Specification & Architectural Blueprint

> Derived strictly from the current codebase (`DsgOmnichannel.slnx`, .NET 10 / C# 13, Angular 22).
> Scope: thin vertical-slice BOPIS proof of concept composed of four cooperating runtime components — **API**, **Worker**, **Web UI**, and **SQL Server**, joined by **RabbitMQ**.

---

## 1. Business Vision & Scope

### 1.1 Vision
The DSG Omnichannel Engine demonstrates a high-throughput **Buy Online, Pick Up In-Store (BOPIS)** order pipeline in which a digital order placed on the web channel is immediately reconciled against *physical store-level inventory*, asynchronously, without blocking the customer's checkout request.

### 1.2 Domain Evidence
The domain model is deliberately narrow and store-centric:

| Signal in code | Business meaning |
| --- | --- |
| `Order.StoreId` (`Order.cs`) | Every order is bound to a **pickup store**, not a warehouse. |
| `StoreInventory { StoreId, ProductId, Quantity }` | Inventory is tracked **per store, per product** — the defining BOPIS constraint. |
| `OrdersController.CreateOrder` sets `Status = "Submitted"` | Checkout returns instantly; fulfillment is deferred. |
| `OrderPlacedEventConsumer` decrements `storeInventory.Quantity` | Allocation (soft reservation) happens out-of-band in the Worker. |
| `OrderStatusNotificationConsumer` emits `"ReadyForPickup"` | Terminal happy-path business outcome is **pickup readiness**, not shipment. |
| `OrderStatusHistory` | Auditable, append-only customer-facing order timeline. |

### 1.3 In Scope
- Order capture over HTTP (`POST /api/orders`).
- Asynchronous, idempotent store inventory allocation.
- Compensating/terminal handling of allocation failure via a saga.
- Append-only order status history projection.
- Real-time push of order state to a browser dashboard.
- JWT-secured API surface with policy-based authorization.

### 1.4 Out of Scope (explicitly, per POC nature)
Payments, pricing, order cancellation/refund, multi-line-item carts (the `Order` aggregate holds a **single** `ProductId`/`Quantity`), inventory replenishment, store associate workflows, multi-environment/production hardening.

---

## 2. Functional Requirements

### FR-1 — Order Submission (`OrdersController`, `api/orders`)

**Endpoint:** `POST /api/orders`
**Auth:** No `[Authorize]` attribute — anonymous in current POC state.
**Handler signature:** `CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)`

**Request payload (`CreateOrderRequest`) and validation rules:**

| Property | Type | Data annotation constraint |
| --- | --- | --- |
| `StoreId` | `string` | `[Required]`, `[StringLength(50)]` |
| `CustomerName` | `string` | `[Required]`, `[StringLength(200)]` |
| `ProductId` | `string` | `[Required]`, `[StringLength(100)]` |
| `Quantity` | `int` | `[Range(1, int.MaxValue)]` |
| `TotalAmount` | `decimal` | `[Range(typeof(decimal), "0.01", "79228162514264337593543950335")]` |

> **Contract drift note (defect risk):** `CreateOrderRequest.CustomerName` allows 200 characters, but `ApplicationDbContext` maps `Order.CustomerName` to `HasMaxLength(100)`. A 101–200 character name passes model validation and fails at `SaveChangesAsync` with a `DbUpdateException`.

**Behavior:**
1. Materialize an `Order` with `Status = "Submitted"` and `CreatedAt = DateTime.UtcNow`; `Id` self-generates via `Guid.NewGuid()`.
2. `dbContext.Orders.Add(order)` — *not yet saved*.
3. `publishEndpoint.Publish(new OrderPlacedEvent(order.Id, StoreId, CustomerName, ProductId, Quantity, TotalAmount, CreatedAt))` — captured by the **bus outbox** into the same `DbContext` change tracker.
4. `await dbContext.SaveChangesAsync(cancellationToken)` — atomically commits the `Order` row **and** the `OutboxMessage` row.
5. Return `201 Created` with `Location: /api/orders/{order.Id}` and the `Order` entity as body.

Because `[ApiController]` is applied, validation failures short-circuit to an automatic `400 Bad Request` `ValidationProblemDetails` response.

### FR-2 — Diagnostics & Security Probe Surface (`TestController`, `api/Test`)

| Route | Method | Auth | Behavior |
| --- | --- | --- | --- |
| `/api/Test/public` | GET | `[AllowAnonymous]` | Returns `"Public endpoint accessible"`. |
| `/api/Test/secured` | GET | `[Authorize(Policy = "RequireCustomerRole")]` | Returns the caller's projected `Claim.Type`/`Claim.Value` set. Requires `ClaimTypes.Role == "Customer"`. |
| `/api/Test/publish-order-event` | POST | `[AllowAnonymous]` | Publishes an `OrderPlacedEvent` with a **caller-supplied `MessageId`** (`context.MessageId = request.MessageId`), then `SaveChangesAsync` to flush the outbox. |

`PublishOrderEventTestRequest { MessageId, OrderId, StoreId, CustomerName?, ProductId, Quantity, TotalAmount }`.
Defaults applied: `CustomerName ?? "Test Customer"`, `TotalAmount > 0 ? TotalAmount : 100.00m`.

> **FR-2 is the idempotency test harness.** Re-posting the same `MessageId` proves that the Worker's EF Core `InboxState` deduplication suppresses duplicate side effects.

An additional minimal-API test publish route is registered via `MapTestPublishEndpoint()`, and a health probe via `MapApiHealthEndpoint()`. Root `GET /` redirects to `/swagger`.

### FR-3 — Store Inventory Allocation (`OrderPlacedEventConsumer`, Worker)

Consumes `OrderPlacedEvent`. Logic:

1. Load `Order` by `message.OrderId`. If missing → log warning, **return successfully** (message is acknowledged, not retried).
2. Load `StoreInventory` matching `si.StoreId == message.StoreId && si.ProductId == message.ProductId`.
3. **Failure branch** — `storeInventory == null || storeInventory.Quantity < message.Quantity`:
   - Reason text is deterministic:
	 - missing record → `"Inventory record for product '{ProductId}' does not exist at store '{StoreId}'."`
	 - insufficient → `"Insufficient stock for product '{ProductId}' at store '{StoreId}'. Requested: {Quantity}, Available: {Available}."`
   - Set `order.Status = "AllocationFailed"`, `SaveChangesAsync`.
   - Publish `AllocationFailedEvent(OrderId, StoreId, ProductId, reason, DateTime.UtcNow)`.
4. **Success branch:**
   - `storeInventory.Quantity -= message.Quantity`; `order.Status = "Allocated"`; `SaveChangesAsync`.
   - Publish `StoreInventoryAllocatedEvent(OrderId, StoreId, ProductId, Quantity, DateTime.UtcNow)`.
5. `DbUpdateException` and all other exceptions are logged and **re-thrown** so the retry pipeline and `_error` queue engage.

### FR-4 — Order Status History Projection (`OrderStatusHistoryConsumer`, Worker)

A single consumer implementing `IConsumer<OrderPlacedEvent>`, `IConsumer<StoreInventoryAllocatedEvent>`, and `IConsumer<AllocationFailedEvent>`. Each handler appends one `OrderStatusHistory` row (`Id = NewId.NextGuid()`, i.e. sequential COMB GUID):

| Event | `Status` | `Reason` |
| --- | --- | --- |
| `OrderPlacedEvent` | `"Submitted"` | `"Order received via API"` |
| `StoreInventoryAllocatedEvent` | `"Allocated"` | `"Inventory successfully reserved"` |
| `AllocationFailedEvent` | `"AllocationFailed"` | `context.Message.Reason` (propagated verbatim) |

### FR-5 — Saga Orchestration (`OrderStateMachine`, Worker)
See §6.3.

### FR-6 — Real-Time Notification (`OrderStatusNotificationConsumer`, **API host**)
See §7.

### FR-7 — Connectivity Smoke Test (`PingEventConsumer` / `PingEvent`)
`PingEvent(Guid Id, string Message, DateTime Timestamp)` provides an infrastructure liveness path independent of the order domain.

> **Namespace anomaly:** `PingEvent.cs` physically resides in `DsgOmnichannel.Contracts/Events/` but declares `namespace DsgOmnichannel.Domain.Events`. MassTransit derives its RabbitMQ exchange name from the **namespace + type name**, so this contract binds to `DsgOmnichannel.Domain.Events:PingEvent` rather than the `Contracts` convention used by all other messages.

---

## 3. Non-Functional Requirements (NFRs)

### NFR-1 — Message Resiliency (Exponential Retry)
Defined in `MessagingExtensions.UseConsumerRetryPolicy()` and applied **globally to every receive endpoint** in the Worker via `x.AddConfigureEndpointsCallback(...)`:

```
r.Exponential(
	retryLimit:    3,
	minInterval:   TimeSpan.FromSeconds(1),
	maxInterval:   TimeSpan.FromSeconds(5),
	intervalDelta: TimeSpan.FromSeconds(2));
```
Approximate delay curve: **1s → 3s → 5s**.

**Exception filters (the resiliency contract):**

| Filter | Exception types | Rationale |
| --- | --- | --- |
| `r.Handle<T>()` | `TimeoutException`, `DbUpdateException`, `HttpRequestException` | Transient infrastructure faults — SQL deadlock/timeout, broker or network blips. Worth retrying. |
| `r.Ignore<T>()` | `ArgumentException` | Non-transient domain/validation error. Retrying is guaranteed to fail again; fail fast to the error queue. |

### NFR-2 — Poison Message Isolation (Dead Letter)
No explicit DLQ configuration exists — the requirement is satisfied by MassTransit's built-in behavior: a message exhausting all retries is moved to `[queue-name]_error`. This is documented deliberately in the `MessagingExtensions` XML comment as an accepted implicit dependency on framework defaults.

### NFR-3 — Exactly-Once Effective Processing (Idempotency)
Consumer-side idempotency is delegated entirely to MassTransit's EF Core **Inbox** (`InboxState` table), enabled by `x.AddEntityFrameworkOutbox<ApplicationDbContext>()` combined with `cfg.UseEntityFrameworkOutbox<ApplicationDbContext>(context)` on every endpoint. **No hand-rolled duplicate detection exists in any consumer body**, which is an explicit architectural directive.

### NFR-4 — Atomicity Between State and Messaging
The API registers `options.UseBusOutbox()`. Consequently `IPublishEndpoint.Publish` inside `OrdersController` does **not** hit RabbitMQ inline; it inserts an `OutboxMessage` row into the same EF Core change tracker as the `Order` entity. The single `SaveChangesAsync` therefore guarantees: *no order exists without its event, and no event exists without its order.* The `BusOutboxDeliveryService` hosted service subsequently relays the message to the broker.

### NFR-5 — Eventual Consistency
`Order.Status` is a *lagging* projection. The HTTP 201 response returns `"Submitted"` while the authoritative allocation outcome (`"Allocated"` / `"AllocationFailed"`) is written asynchronously by the Worker and the saga. Clients must not treat the POST response body as final state — they must subscribe to the SignalR channel (§7) or re-read the order.

### NFR-6 — Real-Time UI Latency & Rendering
- Push transport (SignalR WebSockets) rather than polling; `withAutomaticReconnect()` provides transparent recovery.
- The Angular client runs **zoneless** (`provideZonelessChangeDetection()`), so UI refresh is driven exclusively by Signal invalidation, eliminating Zone.js monkey-patching overhead across the whole change-detection tree.
- The real-time route is forced to `RenderMode.Client` in `app.routes.server.ts`, guaranteeing the WebSocket is never opened during SSR.

### NFR-7 — Security
- JWT Bearer authentication with **all** validation flags enabled: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`, and `ClockSkew = TimeSpan.Zero` (no grace window).
- Symmetric signing key (`SymmetricSecurityKey` over `JwtOptions.SigningKey`) — acceptable for POC, not for production key management.
- Policy `"RequireCustomerRole"` requires `ClaimTypes.Role == "Customer"`.
- `app.UseHttpsRedirection()` precedes `UseAuthentication()` / `UseAuthorization()`.

### NFR-8 — Observability
- Structured logging with a consistent `>>> [ConsumerName]` prefix carrying `OrderId`, `StoreId`, `ProductId`, `Quantity`, and remaining stock.
- `MapApiHealthEndpoint()` exposes an aggregated health probe covering SQL Server and RabbitMQ dependencies.
- Swagger/SwaggerUI enabled only when `app.Environment.IsDevelopment()`.

### NFR-9 — Maintainability
`Program.cs` in both hosts is ≤ 18 lines; every concern is isolated in an extension class (`ApiServiceCollectionExtensions`, `ApiApplicationBuilderExtensions`, `MassTransitServiceCollectionExtensions`, `WorkerServiceCollectionExtensions`, `MessagingExtensions`, `ApiEndpointRouteBuilderExtensions`). All extension classes are `internal static` and guard arguments with `ArgumentNullException.ThrowIfNull`.

---

## 4. Comprehensive Multi-Component Architecture

### 4.1 Runtime Topology

```
					┌──────────────────────────────┐
					│  DsgOmnichannel.Web (ng 22)  │
					│  SSR host + zoneless client  │
					└───────┬──────────────┬───────┘
			 /api  (proxy)  │              │  /hubs/order (proxy, ws:true)
							▼              ▼
					┌──────────────────────────────────────────┐
					│  DsgOmnichannel.Api  (https://:7156)     │
					│  OrdersController · TestController       │
					│  OrderHub (SignalR)                      │
					│  OrderStatusNotificationConsumer         │
					│  MassTransit + EF Core BUS OUTBOX        │
					└───────┬───────────────────────┬──────────┘
				 write      │                       │  publish / consume
							▼                       ▼
			  ┌───────────────────────┐    ┌──────────────────────┐
			  │  SQL Server 2022      │    │      RabbitMQ        │
			  │  DsgOmnichannelDb     │◄──►│  topic exchanges +   │
			  │  domain + outbox +    │    │  queues + _error DLQ │
			  │  inbox + saga tables  │    └──────────┬───────────┘
			  └───────────▲───────────┘               │
						  │ shared ApplicationDbContext
						  │                           ▼
			  ┌───────────┴──────────────────────────────────────┐
			  │  DsgOmnichannel.Worker (Host, BackgroundService) │
			  │  OrderPlacedEventConsumer                        │
			  │  OrderStatusHistoryConsumer                      │
			  │  PingEventConsumer                               │
			  │  OrderStateMachine saga (EF Core repository)     │
			  │  Global exponential retry + EF Core INBOX        │
			  └──────────────────────────────────────────────────┘
```

### 4.2 Component Responsibilities

#### `DsgOmnichannel.Api` — Edge / Command & Notification Host
Composition root: `AddApiCore()` (MVC controllers, Swagger, **SignalR**), `AddApiConfiguration()` (options binding), `AddApiInfrastructure()` (`ApplicationDbContext` on SQL Server + health checks), `AddApiMessaging()` (`AddApiMassTransit`), `AddApiSecurity()` (JWT + policies).
Responsibilities:
- Accept and validate commands (`OrdersController`, `TestController`).
- Own the **transactional outbox producer** side (`UseBusOutbox()`).
- Host the SignalR `OrderHub` at `/hubs/order` and the fan-out consumer `OrderStatusNotificationConsumer`.
- Terminate authentication/authorization.

> Note the deliberate asymmetry: the API is *both* a producer and a consumer, but its only consumer (`OrderStatusNotificationConsumer`) performs **no database writes** — it exists purely to translate domain events into UI signals. This keeps the SignalR hub context in-process with the connected browsers.

#### `DsgOmnichannel.Worker` — Fulfillment / Orchestration Host
A `Microsoft.Extensions.Hosting` generic host. `Worker : BackgroundService` is currently an inert 1-second heartbeat loop; **all real work is MassTransit-driven**, not loop-driven. Responsibilities:
- Business rule execution against store inventory (`OrderPlacedEventConsumer`).
- History projection (`OrderStatusHistoryConsumer`).
- Long-running saga correlation and persistence (`OrderStateMachine` + `OrderState`).
- Owns the **inbox/idempotency** and **retry/DLQ** policies.

#### `DsgOmnichannel.Infrastructure` — Shared Persistence Kernel
Single `ApplicationDbContext` shared by **both** hosts (no per-host context). Hosts the EF Core model configuration, MassTransit transactional outbox entities (`modelBuilder.AddTransactionalOutboxEntities()`), the saga instance `OrderState`, and the complete migration history (7 migrations, `InitialCreate` → `AddOrderStatusHistoryTable`).

#### `DsgOmnichannel.Domain` — Pure Model
POCOs only: `Order`, `StoreInventory`, `OrderStatusHistory`, `AuditLog`. Zero EF Core attributes, zero MassTransit references — all mapping is externalized to `OnModelCreating`.

#### `DsgOmnichannel.Contracts` — Message Contracts
Immutable `record` types representing domain facts: `OrderPlacedEvent`, `StoreInventoryAllocatedEvent`, `AllocationFailedEvent`, `PingEvent`. This is the **only** assembly shared across the process boundary for messaging, and it is deliberately dependency-free.

**Self-contained event principle in practice:** `OrderPlacedEvent` carries `StoreId`, `ProductId`, `Quantity`, `CustomerName`, and `TotalAmount` so `OrderPlacedEventConsumer` can evaluate allocation without calling back to the API. (It does still read the `Order` row — but to *mutate* status, not to look up event context.)

#### `DsgOmnichannel.Web` — Angular 22 Presentation
An `.esproj`-hosted Angular SSR application.
- `app.config.ts`: `provideZonelessChangeDetection()`, `provideRouter(routes)`, `provideHttpClient(withFetch())`.
- `main.server.ts`: bootstrap accepts and forwards `BootstrapContext` (required to avoid `NG0401: Missing Platform`).
- `app.routes.server.ts`: catch-all `RenderMode.Client`.
- `proxy.conf.json`: `/api` and `/hubs` → `https://localhost:7156`, with `"ws": true` on `/hubs` for WebSocket upgrade and `"secure": false` for the dev certificate.
- `signalr.service.ts` + `order-dashboard.ts` provide the live view.

### 4.3 Clean Architecture Dependency Rule
```
Web ──http/ws──► Api ──► Infrastructure ──► Domain
				  │            ▲
				  └──► Contracts │
Worker ──► Infrastructure ──► Domain
   └───► Contracts
```
`Domain` depends on nothing. `Contracts` depends on nothing. Dependencies point inward only; hosts never reference each other.

---

## 5. Data & Schema Dictionary

Database: **`DsgOmnichannelDb`** on `localhost,1433` (SQL Server 2022 container, named volume `dsg-sqldata`).

### 5.1 `Orders`
| Column | CLR type | Mapping / constraint |
| --- | --- | --- |
| `Id` | `Guid` | **PK** (`HasKey`), client-generated `Guid.NewGuid()` |
| `StoreId` | `string` | `nvarchar(50)`, `NOT NULL` |
| `CustomerName` | `string` | `nvarchar(100)`, `NOT NULL` |
| `ProductId` | `string` | `nvarchar(50)` |
| `Quantity` | `int` | |
| `TotalAmount` | `decimal` | `decimal(18,2)` (`HasPrecision(18,2)`) |
| `Status` | `string` | Free-text state token: `Submitted` → `Allocated` \| `AllocationFailed` |
| `CreatedAt` | `DateTime` | UTC |

### 5.2 `StoreInventories`
| Column | CLR type | Mapping / constraint |
| --- | --- | --- |
| `Id` | `Guid` | **PK**, `Guid.NewGuid()` |
| `StoreId` | `string` | `nvarchar(50)`, `NOT NULL` |
| `ProductId` | `string` | `nvarchar(50)`, `NOT NULL` |
| `Quantity` | `int` | On-hand count; decremented by the allocation consumer |

> **Constraint gap:** no unique index on `(StoreId, ProductId)` and no concurrency token (`rowversion`). Lookups use `FirstOrDefaultAsync`, so duplicate rows would be silently ignored and concurrent allocations could over-allocate (lost update). Recorded as a known POC limitation.

### 5.3 `OrderStatusHistory` (explicitly `ToTable("OrderStatusHistory")`, singular)
| Column | CLR type | Mapping / constraint |
| --- | --- | --- |
| `Id` | `Guid` | **PK**, `NewId.NextGuid()` (sequential COMB — index-friendly) |
| `OrderId` | `Guid` | Logical FK to `Orders.Id` — **no FK constraint declared** |
| `Status` | `string` | `nvarchar(64)`, `NOT NULL` |
| `Reason` | `string?` | `nvarchar(500)`, nullable |
| `CreatedAtUtc` | `DateTime` | UTC |

Append-only; never updated or deleted.

### 5.4 `dbo.OrderState` (saga instance, `ToTable("OrderState", "dbo")`)
| Column | CLR type | Mapping / constraint |
| --- | --- | --- |
| `CorrelationId` | `Guid` | **PK**, `ValueGeneratedNever()` — equals `OrderId` |
| `CurrentState` | `string` | `nvarchar(64)`, `NOT NULL` — persisted via `InstanceState(x => x.CurrentState)` |
| `OrderPlacedDate` | `DateTime` | Set from `OrderPlacedEvent.CreatedAt` |
| `StoreId` | `string` | `nvarchar(50)` |
| `FailureReason` | `string?` | Populated on `AllocationFailed` |
| `FaultedAt` | `DateTime?` | From `AllocationFailedEvent.FailedAtUtc` |

Migrations: `AddOrderStateSaga` created the table; `AddOrderStateFaultedFields` added `FailureReason`/`FaultedAt`.

### 5.5 `AuditLogs`
| Column | CLR type | Mapping |
| --- | --- | --- |
| `Id` | `Guid` | **PK** |
| `EventType` | `string` | `nvarchar(100)`, `NOT NULL` |
| `Details` | `string` | `nvarchar(1000)` |
| `CreatedAtUtc` | `DateTime` | UTC |

Mapped and migrated; currently no writer in the codebase (reserved capacity).

### 5.6 MassTransit Framework Tables
Created by `modelBuilder.AddTransactionalOutboxEntities()` (migration `AddMassTransitOutbox`):

| Table | Role |
| --- | --- |
| `InboxState` | Consumer-side deduplication keyed by `MessageId` + `ConsumerId`. Backs NFR-3. |
| `OutboxMessage` | Serialized pending outbound messages. |
| `OutboxState` | Delivery cursor / lock for the outbox relay. |

### 5.7 Migration History
`InitialCreate` → `AddMassTransitOutbox` → `AddDomainEntities` → `RefactorDomainSchema` → `AddStoreIdToOrder` → `AddOrderStateSaga` → `AddOrderStateFaultedFields` → `AddOrderStatusHistoryTable`.

---

## 6. Messaging & Saga Topologies

### 6.1 Broker Configuration
Both hosts connect to RabbitMQ vhost `/`:

| Host | Configuration source | Values |
| --- | --- | --- |
| API | `IOptions<RabbitMqOptions>` bound from the `RabbitMQ` section | `Host=localhost`, `VirtualHost=/`, `Username=guest`, `Password=guest` |
| Worker | Direct `configuration["RabbitMQ:Host" \| ":Username" \| ":Password"]` with `?? "localhost"/"guest"/"guest"` fallbacks; vhost hard-coded `"/"` | same |

Both call `cfg.ConfigureEndpoints(context)`, so MassTransit auto-generates topology using kebab-cased consumer names.

### 6.2 Exchange / Queue Map

| Message type (exchange, fanout by contract namespace) | Publisher | Consumers → queue |
| --- | --- | --- |
| `DsgOmnichannel.Contracts.Events:OrderPlacedEvent` | API (`OrdersController`, `TestController`) via **outbox** | Worker → `order-placed-event` · Worker saga → `order-state` · Worker → `order-status-history` · API → `order-status-notification` |
| `DsgOmnichannel.Contracts.Events:StoreInventoryAllocatedEvent` | Worker (`OrderPlacedEventConsumer`) | Worker → `order-status-history` · API → `order-status-notification` |
| `DsgOmnichannel.Contracts.Events:AllocationFailedEvent` | Worker (`OrderPlacedEventConsumer`) | Worker saga → `order-state` · Worker → `order-status-history` · API → `order-status-notification` |
| `DsgOmnichannel.Domain.Events:PingEvent` | API test surface | Worker → `ping-event` |

Every Worker queue is shadowed by an auto-created `<queue>_error` dead-letter queue (NFR-2).

Because `OrderPlacedEventConsumer` and `OrderStatusHistoryConsumer` are distinct consumer types, MassTransit creates **separate queues** bound to the same exchange — giving independent retry state and independent failure isolation per concern.

### 6.3 `OrderStateMachine` — State Transitions

**Correlation:** both events correlate on the message's `OrderId`:
```csharp
Event(() => OrderPlaced,      x => x.CorrelateById(c => c.Message.OrderId));
Event(() => AllocationFailed, x => x.CorrelateById(c => c.Message.OrderId));
```
`OrderId` therefore *is* the saga `CorrelationId` — no separate correlation identifier is introduced.

**Transition table:**

| From | Trigger | Actions | To |
| --- | --- | --- | --- |
| `Initial` | `OrderPlacedEvent` | `Saga.OrderPlacedDate = Message.CreatedAt`; `Saga.StoreId = Message.StoreId` | **`Processing`** |
| `Processing` | `AllocationFailedEvent` | `Saga.FailureReason = Message.Reason`; `Saga.FaultedAt = Message.FailedAtUtc`; resolve `IServiceProvider` from the consume payload, `CreateScope()`, resolve `ApplicationDbContext`, `Orders.FindAsync(OrderId)`, set `Status = "AllocationFailed"`, `SaveChangesAsync()` | **`Faulted`** |

```
 Initial ──OrderPlacedEvent──► Processing ──AllocationFailedEvent──► Faulted
								   │
								   └── (no transition on StoreInventoryAllocatedEvent —
										saga has no Completed/Final state)
```

**Observations:**
- There is **no `Final` state and no `Finalize()`** — successful sagas remain parked in `Processing`, so `dbo.OrderState` grows monotonically. Successful allocation (`StoreInventoryAllocatedEvent`) is *not* wired into the machine.
- The saga's compensating write to `Orders.Status` is **redundant** with the write already performed by `OrderPlacedEventConsumer` — it is idempotent (same value) but represents duplicated authority over `Order.Status`.
- The saga uses an explicitly created DI scope rather than the ambient consume scope, avoiding `DbContext` reuse across the saga repository transaction.

**Persistence:** `AddSagaStateMachine<OrderStateMachine, OrderState>().EntityFrameworkRepository(r => { r.ExistingDbContext<ApplicationDbContext>(); r.UseSqlServer(); })` — `UseSqlServer()` selects pessimistic row-locking (`UPDLOCK, ROWLOCK`) for saga concurrency.

### 6.4 Outbox / Inbox Pattern Placement

| Concern | API | Worker |
| --- | --- | --- |
| `AddEntityFrameworkOutbox<ApplicationDbContext>` | ✔ | ✔ |
| `options.UseBusOutbox()` | ✔ (defers publish to `SaveChangesAsync`) | ✖ |
| `cfg.UseEntityFrameworkOutbox<ApplicationDbContext>(context)` per endpoint | ✖ | ✔ (via `AddConfigureEndpointsCallback`) |
| Effective role | **Producer-side transactional outbox** | **Consumer-side inbox (idempotency) + outbox for events it publishes** |

Flow guarantee chain:
```
HTTP POST ─► [Order row + OutboxMessage row]  ── single SaveChangesAsync (atomic)
		  ─► BusOutboxDeliveryService ─► RabbitMQ
		  ─► Worker endpoint ─► InboxState check (dedupe)
							 ─► consumer body + outbound events ─► same transaction
```

---

## 7. Real-Time UI Flow

### 7.1 Server Side — `OrderStatusNotificationConsumer` → `OrderHub`
`OrderHub : Hub` is intentionally minimal (only an `OnConnectedAsync` override delegating to base) and is mapped in `ApiEndpointRouteBuilderExtensions`:
```csharp
endpoints.MapHub<OrderHub>("/hubs/order");
```
The consumer injects `IHubContext<OrderHub>` (never the hub itself — hub instances are transient per invocation) and broadcasts to `Clients.All` on method **`ReceiveOrderUpdate`**:

| Consumed event | Broadcast `status` value |
| --- | --- |
| `OrderPlacedEvent` | `"Submitted"` |
| `StoreInventoryAllocatedEvent` | `"ReadyForPickup"` |
| `AllocationFailedEvent` | `"AllocationFailed"` |

Payload shape (anonymous object, camelCase by default JSON policy):
```json
{ "orderId": "<guid>", "status": "ReadyForPickup", "timestamp": "<utc>" }
```

> **Semantic translation:** the wire status `"ReadyForPickup"` deliberately differs from the persisted `Order.Status` value `"Allocated"`. The consumer is an anti-corruption layer mapping *internal fulfillment state* to *customer-facing BOPIS language*.
>
> **Delivery semantics:** `Clients.All` is an unfiltered broadcast with no group/user scoping — acceptable for a POC dashboard, unsuitable for multi-tenant/customer isolation.

### 7.2 Client Side — `SignalRService` (zoneless signal mutation)
```ts
public latestUpdate   = signal<OrderStatusUpdate | null>(null);
public connectionState = signal<string>('Disconnected');
```
`startConnection()` behavior:
1. **SSR guard:** `if (!isPlatformBrowser(inject(PLATFORM_ID))) return;` — no WebSocket during server render.
2. Build the connection with `.withUrl('/hubs/order')` (a *relative* URL, resolved through the Angular dev proxy `"ws": true` rule) and `.withAutomaticReconnect()`.
3. `connectionState.set('Connecting')` → `'Connected'` on success, `'Error'` on failure.
4. `registerOrderStateListener()` subscribes:
   ```ts
   this.hubConnection.on('ReceiveOrderUpdate',
	   (update: OrderStatusUpdate) => this.latestUpdate.set(update));
   ```

`OrderStatusUpdate { orderId: string; status: string; timestamp: string }` mirrors the server payload exactly.

### 7.3 Change Detection Without Zone.js
`OrderDashboardComponent` (`standalone: true`, imports `CommonModule` + `DatePipe`) injects `SignalRService` and calls `startConnection()` in `ngOnInit`, keeping a local `orderEvents = signal<OrderStatusUpdate[]>([])` log.

The critical mechanism: a SignalR callback executes **outside any Angular zone** — under `provideZoneChangeDetection` this would require a manual `NgZone.run()` or `ChangeDetectorRef.markForCheck()`. Because the app uses `provideZonelessChangeDetection()`, calling `signal.set()` marks the consuming template dirty and schedules change detection directly through the reactive graph. The result is a fully push-based, zone-free real-time pipeline:

```
Worker publishes StoreInventoryAllocatedEvent
   → RabbitMQ
   → API OrderStatusNotificationConsumer
   → IHubContext.Clients.All.SendAsync("ReceiveOrderUpdate", …)
   → WebSocket (via /hubs proxy)
   → hubConnection.on(...) callback
   → latestUpdate.set(update)
   → signal graph invalidation
   → targeted template re-render (no Zone.js)
```

### 7.4 End-to-End Sequence

```
Browser        API                SQL             RabbitMQ        Worker
   │  POST /api/orders
   ├──────────►│
   │           ├─ Orders.Add + Publish→Outbox
   │           ├─ SaveChangesAsync ─────►│ (atomic: Order + OutboxMessage)
   │◄──201─────┤
   │           ├─ BusOutboxDeliveryService ──────►│
   │           │                                  ├──OrderPlacedEvent──►│ InboxState dedupe
   │           │◄────OrderPlacedEvent─────────────┤                     ├─ saga: Initial→Processing
   │◄─"Submitted" (ws)                            │                     ├─ history: "Submitted"
   │           │                                  │                     ├─ allocate inventory
   │           │                                  │◄─StoreInventoryAllocated─┤
   │           │◄────────────────────────────────┤                     ├─ history: "Allocated"
   │◄─"ReadyForPickup" (ws)                       │                     │
```
Failure path replaces the last two steps with `AllocationFailedEvent` → saga `Processing → Faulted`, history `"AllocationFailed"`, and a `"AllocationFailed"` UI push.

---

## 8. Architectural Decision Records (ADRs)

### ADR-001 — Transactional Outbox for Atomic Commit of State + Event
**Context.** `OrdersController` must persist an `Order` *and* publish `OrderPlacedEvent`. A direct broker publish creates a dual-write: a crash between `SaveChangesAsync` and `Publish` (or vice versa) yields an order with no event, or a phantom event with no order.
**Decision.** Enable `AddEntityFrameworkOutbox<ApplicationDbContext>(o => { o.UseSqlServer(); o.UseBusOutbox(); })` in the API and publish *before* `SaveChangesAsync`.
**Consequences.** ✔ Exactly-once publish semantics tied to the database transaction. ✔ No distributed transaction / 2PC. ✖ Added delivery latency (relay poll interval). ✖ Three additional framework tables. ✖ Ordering of `Publish` before `SaveChangesAsync` in controller code is now load-bearing and non-obvious.

### ADR-002 — EF Core Inbox for Consumer Idempotency Instead of Hand-Rolled Deduplication
**Context.** At-least-once broker delivery means `OrderPlacedEventConsumer` can run twice, which would double-decrement `StoreInventory.Quantity`.
**Decision.** Apply `cfg.UseEntityFrameworkOutbox<ApplicationDbContext>(context)` to every Worker endpoint via `AddConfigureEndpointsCallback` and rely exclusively on MassTransit `InboxState`. Explicitly forbid manual duplicate checks in consumer bodies.
**Consequences.** ✔ Idempotency is uniform, cross-cutting, and cannot be forgotten per-consumer. ✔ Consumer code stays purely business logic. ✖ Requires the consumer and its `DbContext` to share the same transaction scope. ✖ Correctness depends on framework behavior rather than visible domain code — mitigated by the `TestController.publish-order-event` `MessageId` override used to prove it.

### ADR-003 — Saga State Machine for Long-Running Correlation and Compensation
**Context.** Order fulfillment spans multiple messages and hosts; failure requires a durable, queryable record of *why* and *when*.
**Decision.** Introduce `OrderStateMachine : MassTransitStateMachine<OrderState>` persisted through `EntityFrameworkRepository` with `UseSqlServer()` pessimistic locking, correlated on `OrderId`.
**Consequences.** ✔ Explicit, inspectable lifecycle (`Initial → Processing → Faulted`) with `FailureReason`/`FaultedAt` durably captured. ✔ Row-level pessimistic locking prevents concurrent saga corruption. ✖ Adds `dbo.OrderState` with monotonic growth (no `Final` state). ✖ Overlapping authority with `OrderPlacedEventConsumer` over `Order.Status`. ✖ Locking reduces concurrency under high write contention on the same order.

### ADR-004 — SignalR Push in Preference to HTTP Polling
**Context.** Because the system is eventually consistent (ADR-001), the browser cannot learn the final order outcome from the POST response. Polling `GET /api/orders/{id}` would multiply read load and add O(interval) latency.
**Decision.** Host `OrderHub` in the API and drive it from `OrderStatusNotificationConsumer` via `IHubContext<OrderHub>`.
**Consequences.** ✔ Sub-second, event-driven UI updates. ✔ Zero polling load on SQL Server. ✔ The consumer doubles as an anti-corruption layer (`Allocated` → `ReadyForPickup`). ✖ Requires sticky/backplane consideration if the API scales out (none configured). ✖ `Clients.All` broadcasts every order to every browser — no per-customer scoping.

### ADR-005 — Place the Notification Consumer in the API Host, Not the Worker
**Context.** `IHubContext` can only reach clients connected to the *same* process (absent a backplane).
**Decision.** Register `OrderStatusNotificationConsumer` inside `AddApiMassTransit`, giving the API a consumer role in addition to its producer role.
**Consequences.** ✔ Hub broadcasts reach connected browsers without Redis/Azure SignalR. ✔ The consumer touches no database, preserving the API's write-only posture. ✖ Blurs the "API = write side, Worker = processing side" split. ✖ Multi-instance API scale-out requires a SignalR backplane.

### ADR-006 — Single Shared `ApplicationDbContext` Across Hosts
**Context.** API and Worker both need domain tables plus MassTransit outbox/inbox/saga tables.
**Decision.** Define one `ApplicationDbContext` in `DsgOmnichannel.Infrastructure`, referenced by both hosts; never fork a worker-specific context.
**Consequences.** ✔ Single migration history; the outbox/inbox/saga tables live alongside domain data enabling true local transactions. ✔ No schema drift. ✖ Deployment coupling — a migration affects both hosts. ✖ Contradicts database-per-service microservice orthodoxy (accepted: this is a modular monolith split by workload, not by bounded context).

### ADR-007 — Self-Contained Event Contracts
**Context.** A consumer that must call back to the producer to enrich an event reintroduces synchronous coupling and a failure mode the async design was meant to remove.
**Decision.** `OrderPlacedEvent` carries `StoreId`, `CustomerName`, `ProductId`, `Quantity`, `TotalAmount`, and `CreatedAt`; `AllocationFailedEvent` carries a fully-formed human-readable `Reason`.
**Consequences.** ✔ Consumers are autonomous; the Worker can run while the API is down. ✔ Events are immutable historical facts, replayable for audit. ✖ Larger payloads and denormalized duplication. ✖ Contract evolution requires additive-only versioning.

### ADR-008 — Selective Exponential Retry with Explicit Non-Transient Exclusion
**Context.** Blanket retry wastes broker and database capacity on deterministic failures; no retry surrenders availability during transient blips.
**Decision.** `r.Exponential(3, 1s, 5s, 2s)` handling `TimeoutException`, `DbUpdateException`, `HttpRequestException`, and ignoring `ArgumentException`; exhausted messages fall through to MassTransit's default `_error` queue.
**Consequences.** ✔ Transient SQL/broker faults self-heal within ~9 seconds. ✔ Validation errors fail fast and are quarantined immediately. ✔ Policy is applied once, globally, via `AddConfigureEndpointsCallback`. ✖ Retried consumers must be idempotent — satisfied by ADR-002. ✖ DLQ behavior is implicit framework default rather than explicit configuration.

### ADR-009 — Zoneless Angular with Signals
**Context.** Real-time SignalR callbacks originate outside Angular's zone; Zone.js also imposes global monkey-patching overhead.
**Decision.** `provideZonelessChangeDetection()` plus `signal()`-based state in `SignalRService`; drop Zone.js entirely.
**Consequences.** ✔ SignalR callbacks update the UI with a plain `signal.set()` — no `NgZone.run()`, no `markForCheck()`. ✔ Fine-grained, targeted re-render. ✔ Zone.js is unavailable in the SSR route-extraction worker, so this is also a hard SSR prerequisite. ✖ Any library relying on implicit zone-triggered change detection is incompatible.

### ADR-010 — SSR Shell with Client-Rendered Real-Time Routes
**Context.** WebSockets, `HubConnectionBuilder`, and `PLATFORM_ID`-sensitive APIs cannot execute during server rendering.
**Decision.** Ship `@angular/ssr` with a catch-all `RenderMode.Client` server route, `provideHttpClient(withFetch())` for Node compatibility, `BootstrapContext` forwarding in `main.server.ts`, and an `isPlatformBrowser` guard in `SignalRService.startConnection()`.
**Consequences.** ✔ SSR infrastructure available for future static routes. ✔ `NG0401: Missing Platform` avoided. ✔ No WebSocket attempted server-side. ✖ The dashboard forfeits SSR benefits (first paint is client-rendered).

### ADR-011 — Thin Composition Root via Extension Classes
**Context.** Startup logic (messaging, security, persistence, health, endpoints) accretes rapidly and obscures `Program.cs`.
**Decision.** Keep both `Program.cs` files under 20 lines; delegate to `internal static` extension classes with `ArgumentNullException.ThrowIfNull` guards.
**Consequences.** ✔ Each cross-cutting concern is independently readable and testable. ✔ Consistent shape across API and Worker. ✖ Registration order becomes implicit inside extension bodies rather than visible in `Program.cs`.

### ADR-012 — `BackgroundService` Retained but Message-Driven Workload
**Context.** The Worker template ships with a polling `ExecuteAsync` loop, but all real work is broker-triggered.
**Decision.** Keep `Worker : BackgroundService` registered via `AddHostedService<Worker>()` as an inert heartbeat, delegating all processing to MassTransit's own hosted bus service.
**Consequences.** ✔ Hosting model stays idiomatic and leaves a hook for future scheduled work (e.g. outbox sweeps, stale-saga reaping). ✖ The loop currently does nothing but consume a timer, which can mislead readers into thinking the Worker is poll-based.

---

## Appendix A — Known Gaps & Recommended Follow-Ups

| # | Gap | Evidence | Recommendation |
| --- | --- | --- | --- |
| 1 | `CustomerName` length mismatch (200 vs 100) | `CreateOrderRequest` vs `OnModelCreating` | Align `[StringLength(100)]`. |
| 2 | Saga never completes | No `Final`/`Finalize()` in `OrderStateMachine` | Add `Completed` state on `StoreInventoryAllocatedEvent` and `.Finalize()`. |
| 3 | Inventory over-allocation risk | No unique index on `(StoreId, ProductId)`, no concurrency token | Add unique index + `rowversion` optimistic concurrency. |
| 4 | `PingEvent` namespace mismatch | Declares `DsgOmnichannel.Domain.Events` in the `Contracts` project | Correct to `DsgOmnichannel.Contracts.Events` (breaking topology change). |
| 5 | Unrestricted SignalR broadcast | `Clients.All` in `OrderStatusNotificationConsumer` | Scope to per-order or per-customer groups. |
| 6 | `Order.Status` written by two authorities | `OrderPlacedEventConsumer` and `OrderStateMachine` | Make the saga the sole writer of terminal status. |
| 7 | `AuditLogs` mapped but unused | No writer in the solution | Wire an audit consumer or remove. |
| 8 | Duplicated broker config style | API uses `IOptions<RabbitMqOptions>`; Worker reads raw keys | Share `RabbitMqOptions` binding. |
| 9 | Credentials in `appsettings.Development.json` | `sa` password and `guest/guest` in source control | Move to user-secrets. |
