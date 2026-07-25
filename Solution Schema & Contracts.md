# Solution Schema & Contracts
**DSG Omnichannel Engine — POC Reference**

---

## 1. API Host Configuration

### Ports (`launchSettings.json`)

| Profile | URL |
|---------|-----|
| `http`  | `http://localhost:5140` |
| `https` | `https://localhost:7156` / `http://localhost:5140` |

Swagger UI is launched at `/swagger` in both profiles (Development only).

### Infrastructure (`appsettings.Development.json`)

| Setting | Value |
|---------|-------|
| SQL Server | `localhost,1433` |
| Database | `DsgOmnichannelDb` |
| RabbitMQ Host | `localhost` |
| RabbitMQ VirtualHost | `/` |
| RabbitMQ Username | `guest` |

---

## 2. HTTP Endpoints

### `OrdersController` — `/api/orders`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/orders` | ⚠️ None (no `[Authorize]`) | Creates an order, publishes `OrderPlacedEvent` via Transactional Outbox |

**Request Body — `CreateOrderRequest`**

```json
{
  "storeId": "string (required, max 50)",
  "customerName": "string (required, max 200)",
  "productId": "string (required, max 100)",
  "quantity": 1,
  "totalAmount": 9.99
}
```

| Property | Type | Constraints |
|----------|------|-------------|
| `StoreId` | `string` | Required, max length 50 |
| `CustomerName` | `string` | Required, max length 200 |
| `ProductId` | `string` | Required, max length 100 |
| `Quantity` | `int` | Range: 1 – `int.MaxValue` |
| `TotalAmount` | `decimal` | Range: 0.01 – `decimal.MaxValue` |

**Response:** `201 Created` — returns the created `Order` entity with `Location: /api/orders/{id}`

---

### `TestController` — `/api/test`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/test/public` | `[AllowAnonymous]` | Returns a plain string confirming access |
| `GET` | `/api/test/secured` | `[Authorize(Policy = "RequireCustomerRole")]` | Returns token claims for the authenticated user |
| `POST` | `/api/test/publish-order-event` | `[AllowAnonymous]` | Manually publishes an `OrderPlacedEvent` (test/dev only) |

**Request Body — `PublishOrderEventTestRequest`** (for `POST /api/test/publish-order-event`)

```json
{
  "messageId": "guid",
  "orderId": "guid",
  "storeId": "string",
  "customerName": "string (optional)",
  "productId": "string",
  "quantity": 1,
  "totalAmount": 100.00
}
```

| Property | Type | Notes |
|----------|------|-------|
| `MessageId` | `Guid` | Used to set `context.MessageId` for idempotency |
| `OrderId` | `Guid` | Correlation ID for the saga |
| `StoreId` | `string` | |
| `CustomerName` | `string?` | Optional — defaults to `"Test Customer"` |
| `ProductId` | `string` | |
| `Quantity` | `int` | |
| `TotalAmount` | `decimal` | Defaults to `100.00` if `<= 0` |

---

## 3. Domain Entities (`DsgOmnichannel.Domain`)

### `Order`
Namespace: `DsgOmnichannel.Domain.Entities`

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `Id` | `Guid` | `Guid.NewGuid()` | Primary key |
| `StoreId` | `string` | `""` | Max length 50, required |
| `CustomerName` | `string` | `""` | Max length 100, required |
| `ProductId` | `string` | `""` | Max length 50 |
| `Quantity` | `int` | `0` | |
| `TotalAmount` | `decimal` | `0` | Precision (18, 2) |
| `Status` | `string` | `""` | Set to `"Submitted"` on creation |
| `CreatedAt` | `DateTime` | `DateTime.UtcNow` | |

---

### `StoreInventory`
Namespace: `DsgOmnichannel.Domain.Entities`

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `Id` | `Guid` | `Guid.NewGuid()` | Primary key |
| `StoreId` | `string` | `""` | Max length 50, required |
| `ProductId` | `string` | `""` | Max length 50, required |
| `Quantity` | `int` | `0` | Available stock |

---

## 4. Event Contracts (`DsgOmnichannel.Contracts`)

All contracts are immutable C# `record` types in namespace `DsgOmnichannel.Contracts.Events`.

---

### `OrderPlacedEvent`
Published by: `OrdersController` (via Transactional Outbox) and `TestController`
Consumed by: `OrderPlacedEventConsumer` (Worker) / `OrderStateMachine` (Saga)

| Property | Type | Notes |
|----------|------|-------|
| `OrderId` | `Guid` | Saga correlation ID |
| `StoreId` | `string` | |
| `CustomerName` | `string` | |
| `ProductId` | `string` | |
| `Quantity` | `int` | |
| `TotalAmount` | `decimal` | |
| `CreatedAt` | `DateTime` | UTC timestamp |

---

### `StoreInventoryAllocatedEvent`
Direction: Worker → downstream consumers (allocation success path)

| Property | Type | Notes |
|----------|------|-------|
| `OrderId` | `Guid` | Saga correlation ID |
| `StoreId` | `string` | |
| `ProductId` | `string` | |
| `Quantity` | `int` | Quantity successfully allocated |
| `AllocatedAtUtc` | `DateTime` | UTC timestamp |

---

### `AllocationFailedEvent`
Direction: Worker → downstream consumers (allocation failure path)

| Property | Type | Notes |
|----------|------|-------|
| `OrderId` | `Guid` | Saga correlation ID |
| `StoreId` | `string` | |
| `ProductId` | `string` | |
| `Reason` | `string` | Human-readable failure reason |
| `FailedAtUtc` | `DateTime` | UTC timestamp |

---

## 5. Saga State Instance (`DsgOmnichannel.Infrastructure`)

### `OrderState`
Namespace: `DsgOmnichannel.Infrastructure.Persistence.Sagas`
Table: `dbo.OrderState`

| Property | Type | Notes |
|----------|------|-------|
| `CorrelationId` | `Guid` | Primary key — correlated from `OrderPlacedEvent.OrderId` |
| `CurrentState` | `string` | MassTransit-managed state name (e.g., `"Processing"`) |
| `OrderPlacedDate` | `DateTime` | Set from `OrderPlacedEvent.CreatedAt` |
| `StoreId` | `string` | Set from `OrderPlacedEvent.StoreId` |
