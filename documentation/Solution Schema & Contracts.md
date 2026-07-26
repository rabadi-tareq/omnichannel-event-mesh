# Solution Schema & Contracts
**DSG Omnichannel Engine — Generated from codebase**

---

## 1. HTTP Endpoints & Port Configuration

### API Host — `DsgOmnichannel.Api`

| Profile | URL |
|---------|-----|
| `http` | `http://localhost:5140` |
| `https` | `https://localhost:7156` / `http://localhost:5140` |

---

### `OrdersController` — `api/orders`

#### `POST /api/orders`
Creates a new order, persists it to the database, and publishes an `OrderPlacedEvent` to the MassTransit outbox.

**Authorization:** Anonymous

**Request body** (`CreateOrderRequest`):
```json
{
  "storeId": "STORE-001",
  "customerName": "Jane Smith",
  "productId": "PROD-ABC",
  "quantity": 2,
  "totalAmount": 49.99
}
```

**Response:** `201 Created` — returns the created `Order` entity. Location header set to `/api/orders/{id}`.

---

### `TestController` — `api/test`

#### `GET /api/test/public`
Smoke endpoint with no authentication required.

**Authorization:** `[AllowAnonymous]`

**Response:** `200 OK` — `"Public endpoint accessible"`

---

#### `GET /api/test/secured`
Returns the caller's JWT claims. Requires the `RequireCustomerRole` policy.

**Authorization:** `[Authorize(Policy = "RequireCustomerRole")]`

**Response:** `200 OK`
```json
{
  "message": "Secured endpoint accessed",
  "claims": [{ "type": "...", "value": "..." }]
}
```

---

#### `POST /api/test/publish-order-event`
Manually publishes an `OrderPlacedEvent` to the bus. Used for integration testing.

**Authorization:** `[AllowAnonymous]`

**Request body** (`PublishOrderEventTestRequest`):
```json
{
  "messageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "storeId": "STORE-001",
  "customerName": "Test Customer",
  "productId": "PROD-ABC",
  "quantity": 1,
  "totalAmount": 29.99
}
```

**Response:** `200 OK`
```json
{
  "message": "Event published successfully",
  "messageId": "3fa85f64-...",
  "orderId": "3fa85f64-..."
}
```

---

### Minimal API Endpoints

#### `POST /test-publish?text={message}`
Publishes a `PingEvent` to the bus. Message text passed as a query parameter.

**Authorization:** Anonymous

**Response:** `200 OK`
```json
{
  "status": "Published",
  "message": { "id": "...", "message": "hello", "timestamp": "..." }
}
```

---

#### `GET /`
Redirects to `/swagger`.

**Response:** `302 Redirect`

---

### SignalR Hub — `DsgOmnichannel.Api`

#### Hub route: `/hubs/order`
Real-time order status hub. Angular clients connect via `@microsoft/signalr` and subscribe to the `ReceiveOrderUpdate` server method.

**Client method:** `ReceiveOrderUpdate` — expected payload shape:
```json
{
  "orderId": "3fa85f64-...",
  "status": "Allocated",
  "timestamp": "2026-07-25T22:50:00Z"
}
```

---

## 2. Infrastructure Configuration (Development)

### API (`DsgOmnichannel.Api`)

| Key | Value |
|-----|-------|
| `ConnectionStrings:DefaultConnection` | `Server=localhost,1433;Database=DsgOmnichannelDb;User Id=sa;Password=DSGLoginPassword!;TrustServerCertificate=True;` |
| `RabbitMQ:Host` | `localhost` |
| `RabbitMQ:VirtualHost` | `/` |
| `RabbitMQ:Username` | `guest` |
| `RabbitMQ:Password` | `guest` |

### Worker (`DsgOmnichannel.Worker`)

| Key | Value |
|-----|-------|
| `ConnectionStrings:DefaultConnection` | `Server=localhost,1433;Database=DsgOmnichannelDb;User Id=sa;Password=DSGLoginPassword!;TrustServerCertificate=True;` |

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
| `TotalAmount` | `decimal` | `[Range(0.01, decimal.MaxValue)]` |

---

### `PublishOrderEventTestRequest`
Defined in `src/DsgOmnichannel.Api/Controllers/TestController.cs`.

| Property | Type | Validation |
|----------|------|------------|
| `MessageId` | `Guid` | — |
| `OrderId` | `Guid` | — |
| `StoreId` | `string` | — |
| `CustomerName` | `string?` | Optional; defaults to `"Test Customer"` |
| `ProductId` | `string` | — |
| `Quantity` | `int` | — |
| `TotalAmount` | `decimal` | — |

---

## 4. Domain Entities

### `Order`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.Orders`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, default `Guid.NewGuid()` |
| `StoreId` | `string` | Store identifier |
| `CustomerName` | `string` | — |
| `ProductId` | `string` | — |
| `Quantity` | `int` | — |
| `TotalAmount` | `decimal` | — |
| `Status` | `string` | e.g. `"Submitted"`, `"AllocationFailed"` |
| `CreatedAt` | `DateTime` | UTC, default `DateTime.UtcNow` |

---

### `StoreInventory`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.StoreInventories`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, default `Guid.NewGuid()` |
| `StoreId` | `string` | Store identifier |
| `ProductId` | `string` | — |
| `Quantity` | `int` | Available stock |

---

### `AuditLog`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.AuditLogs`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, default `Guid.NewGuid()` |
| `EventType` | `string` | — |
| `Details` | `string` | — |
| `CreatedAtUtc` | `DateTime` | UTC |

---

### `OrderStatusHistory`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.OrderStatusHistories`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK |
| `OrderId` | `Guid` | FK to `Orders` |
| `Status` | `string` | e.g. `"Submitted"`, `"Allocated"`, `"AllocationFailed"` |
| `Reason` | `string?` | Optional description |
| `CreatedAtUtc` | `DateTime` | UTC |

---

## 5. Event Contracts

### `OrderPlacedEvent`
Namespace: `DsgOmnichannel.Contracts.Events`
Published by: `OrdersController` (via MassTransit Outbox) and `TestController`.
Consumed by: `OrderPlacedEventConsumer`, `OrderStatusHistoryConsumer`, `OrderStateMachine`.

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
Namespace: `DsgOmnichannel.Contracts.Events`
Published by: `OrderPlacedEventConsumer` (on successful inventory allocation).
Consumed by: `OrderStatusHistoryConsumer`.

| Property | Type |
|----------|------|
| `OrderId` | `Guid` |
| `StoreId` | `string` |
| `ProductId` | `string` |
| `Quantity` | `int` |
| `AllocatedAtUtc` | `DateTime` |

---

### `AllocationFailedEvent`
Namespace: `DsgOmnichannel.Contracts.Events`
Published by: `OrderPlacedEventConsumer` (on insufficient inventory).
Consumed by: `OrderStatusHistoryConsumer`, `OrderStateMachine`.

| Property | Type |
|----------|------|
| `OrderId` | `Guid` |
| `StoreId` | `string` |
| `ProductId` | `string` |
| `Reason` | `string` |
| `FailedAtUtc` | `DateTime` |

---

### `PingEvent`
Namespace: `DsgOmnichannel.Domain.Events`
Published by: `TestPublishEndpoint` (`POST /test-publish`).
Consumed by: `PingEventConsumer`.

| Property | Type |
|----------|------|
| `Id` | `Guid` |
| `Message` | `string` |
| `Timestamp` | `DateTime` |

---

## 6. Saga Entities

### `OrderState`
Namespace: `DsgOmnichannel.Infrastructure.Persistence.Sagas` — Table: `dbo.OrderStates`
Implements: `MassTransit.SagaStateMachineInstance`

| Property | Type | Notes |
|----------|------|-------|
| `CorrelationId` | `Guid` | PK — correlates by `OrderId` |
| `CurrentState` | `string` | State discriminator |
| `OrderPlacedDate` | `DateTime` | Set from `OrderPlacedEvent.CreatedAt` |
| `StoreId` | `string` | Set from `OrderPlacedEvent.StoreId` |
| `FailureReason` | `string?` | Set from `AllocationFailedEvent.Reason` |
| `FaultedAt` | `DateTime?` | Set from `AllocationFailedEvent.FailedAtUtc` |

**State machine:** `OrderStateMachine` (in `DsgOmnichannel.Worker`)

**Defined states:** `Processing`, `Faulted`

**Defined events:**

| Event Property | Event Type | Correlates By |
|----------------|------------|---------------|
| `OrderPlaced` | `OrderPlacedEvent` | `context.Message.OrderId` |
| `AllocationFailed` | `AllocationFailedEvent` | `context.Message.OrderId` |

**Transitions:**

| Trigger | From | To | Side Effect |
|---------|------|----|-------------|
| `OrderPlaced` | `Initial` | `Processing` | Sets `OrderPlacedDate`, `StoreId` |
| `AllocationFailed` | `Processing` | `Faulted` | Sets `FailureReason`, `FaultedAt`; updates `Order.Status = "AllocationFailed"` in DB |
