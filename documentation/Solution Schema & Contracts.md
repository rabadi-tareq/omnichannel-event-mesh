# Solution Schema & Contracts
**DSG Omnichannel Engine — Generated from codebase**

---

## 1. HTTP Endpoints & Port Configuration

### API Host — `DsgOmnichannel.Api`

| Profile | URL |
|---------|-----|
| `http` | `http://localhost:5140` |
| `https` | `https://localhost:7156` and `http://localhost:5140` |

---

### `OrdersController` — `api/orders`

#### `POST /api/orders`
Creates a new order, persists it to the database, and publishes an `OrderPlacedEvent` via the EF Core Transactional Outbox. No authorization is required.

**Request body** (`CreateOrderRequest`):
```json
{
  "storeId": "STORE-001",
  "customerName": "Jane Smith",
  "productId": "SKU-9912",
  "quantity": 2,
  "totalAmount": 49.99
}
```

**Response:** `201 Created` — returns the full `Order` entity (including generated `Id` and `CreatedAt`) in the response body. Location header is set to `/api/orders/{id}`.

---

### `TestController` — `api/test`

#### `GET /api/test/public`
Returns a plain string confirming the public endpoint is accessible. No authorization required (`[AllowAnonymous]`).

**Response:** `200 OK` — returns the string `"Public endpoint accessible"`.

---

#### `GET /api/test/secured`
Returns the authenticated user's JWT claims. Requires the `RequireCustomerRole` authorization policy.

**Response:** `200 OK` — returns a JSON object containing a `Message` string and a `Claims` array of `{ type, value }` pairs.

---

#### `POST /api/test/publish-order-event`
Publishes an `OrderPlacedEvent` directly to RabbitMQ via the EF Core Outbox without creating an `Order` record in the database. Designed for integration testing and retry/idempotency validation. No authorization required (`[AllowAnonymous]`).

**Request body** (`PublishOrderEventTestRequest`):
```json
{
  "messageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderId": "a1b2c3d4-0000-0000-0000-000000000001",
  "storeId": "STORE-001",
  "customerName": "FAIL_RETRY",
  "productId": "SKU-9912",
  "quantity": 1,
  "totalAmount": 25.00
}
```

**Response:** `200 OK` — returns `{ "message": "Event published successfully", "messageId": "...", "orderId": "..." }`.

---

### Minimal API Endpoint — `/test-publish`

#### `POST /test-publish?text={text}`
Builds and publishes a `PingEvent` via the EF Core Outbox. No authorization required.

**Response:** `200 OK` — returns `{ "status": "Published", "message": { "id": "...", "message": "...", "timestamp": "..." } }`.

---

## 2. Infrastructure Configuration (Development)

### `DsgOmnichannel.Api` — `appsettings.Development.json`

| Key | Value |
|-----|-------|
| `ConnectionStrings:DefaultConnection` | `Server=localhost,1433;Database=DsgOmnichannelDb;User Id=sa;Password=DSGLoginPassword!;TrustServerCertificate=True;` |
| `RabbitMQ:Host` | `localhost` |
| `RabbitMQ:VirtualHost` | `/` |
| `RabbitMQ:Username` | `guest` |
| `RabbitMQ:Password` | `guest` |

### `DsgOmnichannel.Worker` — `appsettings.Development.json`

| Key | Value |
|-----|-------|
| `ConnectionStrings:DefaultConnection` | `Server=localhost,1433;Database=DsgOmnichannelDb;User Id=sa;Password=DSGLoginPassword!;TrustServerCertificate=True;` |

> RabbitMQ connection for the Worker is read at runtime from `configuration["RabbitMQ:Host"]`, `configuration["RabbitMQ:Username"]`, and `configuration["RabbitMQ:Password"]` with defaults of `localhost` / `guest` / `guest` if no appsettings key is present.

---

## 3. Request DTOs

### `CreateOrderRequest`
Defined in `src/DsgOmnichannel.Api/Controllers/OrdersController.cs`.

| Property | Type | Validation |
|----------|------|------------|
| `StoreId` | `string` | `[Required]`, `[StringLength(50)]` |
| `CustomerName` | `string` | `[Required]`, `[StringLength(200)]` |
| `ProductId` | `string` | `[Required]`, `[StringLength(100)]` |
| `Quantity` | `int` | `[Range(1, int.MaxValue)]` |
| `TotalAmount` | `decimal` | `[Range(typeof(decimal), "0.01", "79228162514264337593543950335")]` |

---

### `PublishOrderEventTestRequest`
Defined in `src/DsgOmnichannel.Api/Controllers/TestController.cs`.

| Property | Type | Validation |
|----------|------|------------|
| `MessageId` | `Guid` | — |
| `OrderId` | `Guid` | — |
| `StoreId` | `string` | — |
| `CustomerName` | `string?` | — |
| `ProductId` | `string` | — |
| `Quantity` | `int` | — |
| `TotalAmount` | `decimal` | — |

---

## 4. Domain Entities

### `Order`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.Orders`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | Primary key, default `Guid.NewGuid()` |
| `StoreId` | `string` | Max length 50, required |
| `CustomerName` | `string` | Max length 100, required |
| `ProductId` | `string` | Max length 50 |
| `Quantity` | `int` | — |
| `TotalAmount` | `decimal` | Precision (18, 2) |
| `Status` | `string` | Runtime values: `"Submitted"`, `"Allocated"`, `"AllocationFailed"` |
| `CreatedAt` | `DateTime` | Default `DateTime.UtcNow` |

---

### `StoreInventory`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.StoreInventories`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | Primary key, default `Guid.NewGuid()` |
| `StoreId` | `string` | Max length 50, required |
| `ProductId` | `string` | Max length 50, required |
| `Quantity` | `int` | Decremented on successful allocation |

---

### `OrderStatusHistory`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.OrderStatusHistories`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | Primary key |
| `OrderId` | `Guid` | Foreign reference to dbo.Orders |
| `Status` | `string` | Values: `"Submitted"`, `"Allocated"`, `"AllocationFailed"` |
| `Reason` | `string?` | Nullable — describes the status transition reason |
| `CreatedAtUtc` | `DateTime` | Timestamp of the status transition |

---

### `AuditLog`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.AuditLogs`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | Primary key, default `Guid.NewGuid()` |
| `EventType` | `string` | Max length 100, required |
| `Details` | `string` | Max length 1000 |
| `CreatedAtUtc` | `DateTime` | Default `DateTime.UtcNow` |

> `AuditLog` is defined and mapped in the schema but is not written to by any current consumer or controller.

---

## 5. Event Contracts

### `OrderPlacedEvent`
Published by: `OrdersController` (`POST /api/orders`) and `TestController` (`POST /api/test/publish-order-event`).
Consumed by: `OrderPlacedEventConsumer`, `OrderStatusHistoryConsumer`, `OrderStateMachine` saga.

| Property | Type |
|----------|------|
| `OrderId` | `Guid` |
| `StoreId` | `string` |
| `CustomerName` | `string` |
| `ProductId` | `string` |
| `Quantity` | `int` |
| `TotalAmount` | `decimal` |
| `CreatedAt` | `DateTime` |

---

### `StoreInventoryAllocatedEvent`
Published by: `OrderPlacedEventConsumer` (success path, via consumer outbox).
Consumed by: `OrderStatusHistoryConsumer` (on queue `order-status-history`).

> ⚠ This event is also delivered to the `order-state` queue, but `OrderStateMachine` has no registered handler for it. Messages for this event on that queue will be moved to `order-state_error`.

| Property | Type |
|----------|------|
| `OrderId` | `Guid` |
| `StoreId` | `string` |
| `ProductId` | `string` |
| `Quantity` | `int` |
| `AllocatedAtUtc` | `DateTime` |

---

### `AllocationFailedEvent`
Published by: `OrderPlacedEventConsumer` (failure path, via consumer outbox).
Consumed by: `OrderStatusHistoryConsumer` (on queue `order-status-history`) and `OrderStateMachine` saga (on queue `order-state`).

| Property | Type |
|----------|------|
| `OrderId` | `Guid` |
| `StoreId` | `string` |
| `ProductId` | `string` |
| `Reason` | `string` |
| `FailedAtUtc` | `DateTime` |

---

### `PingEvent`
Published by: `TestPublishEndpoint` (`POST /test-publish`).
Consumed by: `PingEventConsumer` (on queue `ping-event`).

| Property | Type |
|----------|------|
| `Id` | `Guid` |
| `Message` | `string` |
| `Timestamp` | `DateTime` |

---

## 6. Saga Entities

### `OrderState`
Namespace: `DsgOmnichannel.Infrastructure.Persistence.Sagas` — Table: `dbo.OrderState`
Implements: `MassTransit.SagaStateMachineInstance`

| Property | Type | Notes |
|----------|------|-------|
| `CorrelationId` | `Guid` | Primary key, correlated to `OrderId` from events, `ValueGeneratedNever` |
| `CurrentState` | `string` | State discriminator, max length 64, required |
| `OrderPlacedDate` | `DateTime` | Set from `CreatedAt` of the `OrderPlacedEvent` |
| `StoreId` | `string` | Set from `StoreId` of the `OrderPlacedEvent` |
| `FailureReason` | `string?` | Set from `Reason` of `AllocationFailedEvent`; null on success path |
| `FaultedAt` | `DateTime?` | Set from `FailedAtUtc` of `AllocationFailedEvent`; null on success path |

**State machine:** `OrderStateMachine` (in `DsgOmnichannel.Worker`)

**Defined states:** `Initial`, `Processing`, `Faulted`, `Final`

**Defined events and correlations:**

| Event Property | Event Type | Correlation Expression |
|---|---|---|
| `OrderPlaced` | `OrderPlacedEvent` | `context.Message.OrderId` |
| `AllocationFailed` | `AllocationFailedEvent` | `context.Message.OrderId` |

**State transitions:**
- `Initial` + `OrderPlaced` → sets `OrderPlacedDate` and `StoreId` → transitions to `Processing`
- `Processing` + `AllocationFailed` → sets `FailureReason` and `FaultedAt`, updates `dbo.Orders.Status` to `"AllocationFailed"` → transitions to `Faulted`

> No `StoreInventoryAllocatedEvent` handler is defined. The saga has no terminal success state — `Processing` is never closed on the happy path.
