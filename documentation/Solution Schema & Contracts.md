# Solution Schema & Contracts
**DSG Omnichannel Engine — Generated from codebase**

---

## 1. HTTP Endpoints & Port Configuration

### API Host — `DsgOmnichannel.Api`

| Profile | URL |
|---------|-----|
| `http` | `http://localhost:5140` |
| `https` | `https://localhost:7156;http://localhost:5140` |

Both profiles launch to the Swagger UI (`/swagger`).

---

### `OrdersController` — `api/orders`

#### `POST api/orders`
Creates a new order, persists it to SQL Server via EF Core, and publishes `OrderPlacedEvent` through the MassTransit transactional outbox.

**Authorization:** Anonymous

**Request body** (`CreateOrderRequest`):
```json
{
  "storeId": "store-001",
  "customerName": "Jane Smith",
  "productId": "SKU-ABC-123",
  "quantity": 2,
  "totalAmount": 49.99
}
```

**Response:** `201 Created` — returns the persisted `Order` object. Location header: `/api/orders/{id}`.

---

#### `POST api/orders/{id:guid}/pickup`
Confirms that a store associate has picked up an allocated order. Publishes `OrderPickedUpEvent`.

**Authorization:** Anonymous

**Request body** (`ConfirmPickupRequest`):
```json
{
  "associateId": "associate-007"
}
```

**Response:**
- `200 OK` — `{ "orderId": "<guid>", "status": "PickedUp" }`
- `404 Not Found` — order does not exist
- `409 Conflict` — order is not in `Allocated` state

---

#### `POST api/orders/{id:guid}/cancel`
Requests cancellation of an allocated order. Publishes `OrderCancelledEvent` so the Worker restores inventory.

**Authorization:** Anonymous

**Request body:** None

**Response:**
- `200 OK` — `{ "orderId": "<guid>", "status": "CancellationRequested" }`
- `404 Not Found` — order does not exist
- `409 Conflict` — order is not in `Allocated` state

---

### `InventoryController` — `api/inventory`

#### `GET api/inventory`
Returns all store-inventory records ordered by `ProductId`.

**Authorization:** Anonymous

**Response:** `200 OK` — array of `InventoryItemResponse`:
```json
[
  { "id": "<guid>", "storeId": "store-001", "productId": "SKU-ABC-123", "quantity": 50 }
]
```

---

#### `POST api/inventory`
Creates or updates a store-inventory record. If a record for `(StoreId, ProductId)` already exists, its quantity is updated.

**Authorization:** Anonymous

**Request body** (`UpsertInventoryRequest`):
```json
{
  "storeId": "store-001",
  "productId": "SKU-ABC-123",
  "quantity": 100
}
```

**Response:**
- `201 Created` — `InventoryItemResponse` when a new record is created. Location: `/api/inventory/{id}`
- `200 OK` — `InventoryItemResponse` when an existing record is updated

---

#### `PATCH api/inventory/{id:guid}/quantity`
Updates only the quantity of an existing inventory record.

**Authorization:** Anonymous

**Request body** (`UpdateQuantityRequest`):
```json
{
  "quantity": 75
}
```

**Response:**
- `200 OK` — `InventoryItemResponse`
- `404 Not Found` — inventory item does not exist

---

#### `DELETE api/inventory/{id:guid}`
Deletes an inventory record and all orders referencing the same `(StoreId, ProductId)` pair.

**Authorization:** Anonymous

**Response:**
- `204 No Content`
- `404 Not Found` — inventory item does not exist

---

### `TestController` — `api/test`

#### `GET api/test/public`
Smoke endpoint, publicly accessible.

**Authorization:** `[AllowAnonymous]`

**Response:** `200 OK` — `"Public endpoint accessible"`

---

#### `GET api/test/secured`
Returns the authenticated user's JWT claims.

**Authorization:** `[Authorize(Policy = "RequireCustomerRole")]`

**Response:** `200 OK` — `{ "message": "Secured endpoint accessed", "claims": [...] }`

---

#### `POST api/test/publish-order-event`
Publishes an `OrderPlacedEvent` with a caller-specified `MessageId` for manual integration testing.

**Authorization:** `[AllowAnonymous]`

**Request body** (`PublishOrderEventTestRequest`):
```json
{
  "messageId": "<guid>",
  "orderId": "<guid>",
  "storeId": "store-001",
  "customerName": "Test Customer",
  "productId": "SKU-ABC-123",
  "quantity": 1,
  "totalAmount": 25.00
}
```

**Response:** `200 OK` — `{ "message": "Event published successfully", "messageId": "<guid>", "orderId": "<guid>" }`

---

### SignalR Hub

| Hub | Route |
|-----|-------|
| `OrderHub` | `/hubs/order` |

Clients subscribe to the `ReceiveOrderJourneyEvent` method to receive real-time order journey notifications.

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
| `SignalR:HubUrl` | `http://localhost:5140/hubs/order` |

---

## 3. Request DTOs

### `CreateOrderRequest`
Defined in `src/DsgOmnichannel.Api/Controllers/OrdersController.cs`.

| Property | Type | Validation |
|----------|------|------------|
| `StoreId` | `string` | `[Required]`, `[StringLength(50)]` |
| `CustomerName` | `string` | `[Required]`, `[StringLength(100)]` |
| `ProductId` | `string` | `[Required]`, `[StringLength(100)]` |
| `Quantity` | `int` | `[Range(1, int.MaxValue)]` |
| `TotalAmount` | `decimal` | `[Range(0.01, decimal.MaxValue)]` |

---

### `ConfirmPickupRequest`
Defined in `src/DsgOmnichannel.Api/Controllers/OrdersController.cs`.

| Property | Type | Validation |
|----------|------|------------|
| `AssociateId` | `string` | `[Required]`, `[StringLength(100)]` |

---

### `UpsertInventoryRequest`
Defined in `src/DsgOmnichannel.Api/Controllers/InventoryController.cs`.

| Property | Type | Validation |
|----------|------|------------|
| `StoreId` | `string` | — |
| `ProductId` | `string` | — |
| `Quantity` | `int` | — |

---

### `UpdateQuantityRequest`
Defined in `src/DsgOmnichannel.Api/Controllers/InventoryController.cs`.

| Property | Type | Validation |
|----------|------|------------|
| `Quantity` | `int` | — |

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
| `Id` | `Guid` | PK, default `Guid.NewGuid()` |
| `StoreId` | `string` | — |
| `CustomerName` | `string` | — |
| `ProductId` | `string` | — |
| `Quantity` | `int` | — |
| `TotalAmount` | `decimal` | — |
| `Status` | `string` | Lifecycle: `Submitted` → `Allocated` / `AllocationFailed` → `PickedUp` / `CancellationRequested` |
| `CreatedAt` | `DateTime` | Default `DateTime.UtcNow` |

---

### `StoreInventory`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.StoreInventories`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, default `Guid.NewGuid()` |
| `StoreId` | `string` | — |
| `ProductId` | `string` | — |
| `Quantity` | `int` | — |
| `RowVersion` | `byte[]` | Optimistic-concurrency token |

---

### `OrderStatusHistory`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.OrderStatusHistories`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK |
| `OrderId` | `Guid` | FK to `Orders` |
| `Status` | `string` | Snapshot of order status at time of event |
| `Reason` | `string?` | Optional description |
| `CreatedAtUtc` | `DateTime` | — |

---

### `AuditLog`
Namespace: `DsgOmnichannel.Domain.Entities` — Table: `dbo.AuditLogs`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, default `Guid.NewGuid()` |
| `EventType` | `string` | — |
| `Details` | `string` | — |
| `CreatedAtUtc` | `DateTime` | Default `DateTime.UtcNow` |

---

## 5. Event Contracts

### `OrderPlacedEvent`
Namespace: `DsgOmnichannel.Contracts.Events`

Published by: `OrdersController` (via MassTransit transactional outbox); `TestController` (direct publish for testing).  
Consumed by: `OrderPlacedEventConsumer` (Worker — allocates inventory), `OrderStatusHistoryConsumer` (Worker — appends `Submitted` history), `OrderStatusNotificationConsumer` (API — pushes SignalR notification), `OrderStateMachine` (Worker saga — transitions to `Processing`).

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

Published by: `OrderPlacedEventConsumer` (Worker — on successful inventory allocation).  
Consumed by: `OrderStatusHistoryConsumer` (Worker — appends `Allocated` history), `OrderStatusNotificationConsumer` (API — pushes SignalR notification).

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

Published by: `OrderPlacedEventConsumer` (Worker — when inventory is insufficient or missing).  
Consumed by: `OrderStatusHistoryConsumer` (Worker — appends `AllocationFailed` history), `OrderStatusNotificationConsumer` (API — pushes SignalR notification), `OrderStateMachine` (Worker saga — records failure reason and finalizes).

| Property | Type |
|----------|------|
| `OrderId` | `Guid` |
| `StoreId` | `string` |
| `ProductId` | `string` |
| `Reason` | `string` |
| `FailedAtUtc` | `DateTime` |

---

### `OrderPickedUpEvent`
Namespace: `DsgOmnichannel.Contracts.Events`

Published by: `OrdersController` (`POST api/orders/{id}/pickup`).  
Consumed by: `OrderPickedUpConsumer` (Worker — sets `Order.Status = "PickedUp"`), `OrderStatusHistoryConsumer` (Worker — appends `PickedUp` history), `OrderStatusNotificationConsumer` (API — pushes SignalR notification), `OrderStateMachine` (Worker saga — finalizes saga).

| Property | Type |
|----------|------|
| `OrderId` | `Guid` |
| `StoreId` | `string` |
| `AssociateId` | `string` |
| `PickedUpAtUtc` | `DateTime` |

---

### `OrderCancelledEvent`
Namespace: `DsgOmnichannel.Contracts.Events`

Published by: `OrdersController` (`POST api/orders/{id}/cancel`).  
Consumed by: `OrderCancelledEventConsumer` (Worker — restores inventory to stock), `OrderStatusNotificationConsumer` (API — pushes SignalR notification).

| Property | Type |
|----------|------|
| `OrderId` | `Guid` |
| `StoreId` | `string` |
| `ProductId` | `string` |
| `Quantity` | `int` |
| `CancelledAtUtc` | `DateTime` |

---

## 6. Saga Entities

### `OrderState`
Namespace: `DsgOmnichannel.Infrastructure.Persistence.Sagas` — Table: `dbo.OrderStates`  
Implements: `MassTransit.SagaStateMachineInstance`

| Property | Type | Notes |
|----------|------|-------|
| `CorrelationId` | `Guid` | PK — correlates to `OrderId` |
| `CurrentState` | `string` | State discriminator (e.g., `"Processing"`, `"Final"`) |
| `OrderPlacedDate` | `DateTime` | Set from `OrderPlacedEvent.CreatedAt` when saga is created |
| `StoreId` | `string` | Set from `OrderPlacedEvent.StoreId` when saga is created |
| `FailureReason` | `string?` | Populated when `AllocationFailedEvent` is received |
| `FaultedAt` | `DateTime?` | Populated when `AllocationFailedEvent` is received |

**State machine:** `OrderStateMachine` (in `DsgOmnichannel.Worker`)  
**Defined states:** `Initial`, `Processing`, `Final`

**Defined events:**

| Event Property | Event Type | Correlates By |
|----------------|------------|---------------|
| `OrderPlaced` | `OrderPlacedEvent` | `context.Message.OrderId` |
| `AllocationFailed` | `AllocationFailedEvent` | `context.Message.OrderId` |
| `OrderPickedUp` | `OrderPickedUpEvent` | `context.Message.OrderId` |

**Transitions:**
- `Initial` + `OrderPlaced` → `Processing` (records `OrderPlacedDate` and `StoreId`)
- `Processing` + `AllocationFailed` → `Final` (records `FailureReason` and `FaultedAt`; saga finalized)
- `Processing` + `OrderPickedUp` → `Final` (saga finalized)
