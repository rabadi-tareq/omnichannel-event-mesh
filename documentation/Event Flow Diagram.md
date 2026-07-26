# Event Flow Diagram — DSG Omnichannel Engine

---

## Flow A — PingEvent (smoke-test publish)

```
+---------------------------------------------------------------------------------+
¦  Stage 1 — Entry Point: DsgOmnichannel.Api  POST /test-publish                  ¦
¦  The caller sends an HTTP POST request to /test-publish with a text query param. ¦
+---------------------------------------------------------------------------------+
                                   ¦
                    1. The endpoint builds a new PingEvent in memory
                       and calls IPublishEndpoint.Publish.
                       The Api uses the BusOutbox; the event is staged
                       into dbo.OutboxMessage within the same HTTP request.
                                   ¦
                                   ?
+---------------------------------------------------------------------------------+
¦  Stage 2 — BusOutboxDeliveryService (inside DsgOmnichannel.Api process)         ¦
¦  The BusOutboxDeliveryService polls dbo.OutboxMessage for undelivered rows,      ¦
¦  forwards each row to RabbitMQ, and marks the row as delivered.                  ¦
+---------------------------------------------------------------------------------+
                                   ¦
                    2. The service forwards the PingEvent payload to RabbitMQ.
                                   ¦
                                   ?
+---------------------------------------------------------------------------------+
¦  Stage 3 — RabbitMQ Exchange                                                    ¦
¦  Exchange name: DsgOmnichannel.Domain.Events:PingEvent                          ¦
+---------------------------------------------------------------------------------+
                                   ¦
                    3. RabbitMQ routes the message to queue: ping-event
                                   ¦
                                   ?
+---------------------------------------------------------------------------------+
¦  Stage 4 — PingEventConsumer  (DsgOmnichannel.Worker)                           ¦
¦  Queue: ping-event  |  Stateless  |  No idempotency guard                       ¦
¦                                                                                 ¦
¦  The consumer logs the received PingEvent message and ID at Information level.  ¦
¦  No database writes are performed and no follow-up messages are published.      ¦
+---------------------------------------------------------------------------------+
```

---

## Flow B — Order Placement and Fulfilment

```
+---------------------------------------------------------------------------------+
¦  Stage 1 — Entry Point: DsgOmnichannel.Api  POST /api/orders                   ¦
¦  The caller sends an HTTP POST with StoreId, CustomerName, ProductId,           ¦
¦  Quantity, and TotalAmount.                                                     ¦
+---------------------------------------------------------------------------------+
                                   ¦
                    1. The controller builds a new Order object in memory
                       with Status set to Submitted and CreatedAt set to
                       the current UTC time.
                                   ¦
                                   ?
  +- committed atomically ------------------------------------------------------+
  ¦  Insert a new record into dbo.Orders with Status set to Submitted.          ¦
  ¦  Insert a new record into dbo.OutboxMessage containing the                  ¦
  ¦  OrderPlacedEvent payload (OrderId, StoreId, CustomerName, ProductId,       ¦
  ¦  Quantity, TotalAmount, CreatedAt).                                         ¦
  +----------------------------------------------------------------------------+
                                   ¦
                    1. The controller returns HTTP 201 Created
                       with the new order record.
                                   ¦
                                   ?
+---------------------------------------------------------------------------------+
¦  Stage 2 — BusOutboxDeliveryService (inside DsgOmnichannel.Api process)         ¦
¦  The BusOutboxDeliveryService polls dbo.OutboxMessage for undelivered rows,      ¦
¦  forwards each row to RabbitMQ, and marks the row as delivered.                  ¦
+---------------------------------------------------------------------------------+
                                   ¦
                    2. The service forwards the OrderPlacedEvent payload to RabbitMQ.
                                   ¦
                                   ?
+---------------------------------------------------------------------------------+
¦  Stage 3 — RabbitMQ Exchange                                                    ¦
¦  Exchange name: DsgOmnichannel.Contracts.Events:OrderPlacedEvent                ¦
+---------------------------------------------------------------------------------+
       ¦               ¦                       ¦                      ¦
  3.1 Routes to   3.2 Routes to          3.3 Routes to         3.4 Routes to
  queue:          queue:                 queue:                queue:
  order-placed    order-status-history   order-state           order-status-
  -event                                                       notification
       ¦               ¦                       ¦                      ¦
       ?               ?                       ?                      ?

+----------------+ +----------------------+ +----------------------+ +-------------------------+
¦ Stage 4.1      ¦ ¦ Stage 4.2            ¦ ¦ Stage 4.3            ¦ ¦ Stage 4.4               ¦
¦ OrderPlaced    ¦ ¦ OrderStatusHistory   ¦ ¦ OrderStateMachine    ¦ ¦ OrderStatusNotification ¦
¦ EventConsumer  ¦ ¦ Consumer (Worker)    ¦ ¦ Saga (Worker)        ¦ ¦ Consumer (Api)          ¦
¦ (Worker)       ¦ ¦ Queue:               ¦ ¦ Queue: order-state   ¦ ¦ Queue:                  ¦
¦ Queue:         ¦ ¦ order-status-history ¦ ¦ Stateful (EF Core    ¦ ¦ order-status-           ¦
¦ order-placed   ¦ ¦ Stateless            ¦ ¦ saga store)          ¦ ¦ notification            ¦
¦ -event         ¦ ¦ Idempotency:         ¦ ¦                      ¦ ¦ Stateless               ¦
¦ Stateless      ¦ ¦ EF Core InboxState   ¦ ¦                      ¦ ¦ No idempotency guard ?  ¦
¦ Idempotency:   ¦ +----------------------+ +----------------------+ +-------------------------+
¦ EF Core        ¦            ¦                         ¦                          ¦
¦ InboxState     ¦ +----------------------+ +----------------------------------+   ¦
+----------------+ ¦ committed atomically ¦ ¦ Set OrderPlacedDate to CreatedAt ¦   ¦
       ¦           ¦                      ¦ ¦ from the event.                  ¦   ¦
       ¦           ¦ Insert a new record  ¦ ¦ Set StoreId from the event.      ¦   ¦
       ¦           ¦ into dbo.OrderStatus ¦ ¦ Transition CurrentState to       ¦   ¦
       ¦           ¦ History with Status  ¦ ¦ Processing.                      ¦   ¦
       ¦           ¦ set to Submitted,    ¦ ¦ Persist the updated OrderState   ¦   ¦
       ¦           ¦ Reason set to        ¦ ¦ record to dbo.OrderState via     ¦   ¦
       ¦           ¦ "Order received via  ¦ ¦ EF Core.                         ¦   ¦
       ¦           ¦ API", and            ¦ +----------------------------------+   ¦
       ¦           ¦ CreatedAtUtc set to  ¦                                        ¦
       ¦           ¦ current UTC time.    ¦         Send SignalR message            ¦
       ¦           +----------------------+         "ReceiveOrderUpdate" to        ¦
       ¦                                            all connected clients with     ¦
       ¦                                            orderId, status set to         ¦
       ¦                                            Submitted, and timestamp.      ¦
       ¦
       ¦  Reads the matching row from dbo.Orders using OrderId.
       ¦
       +--- Path A: Order record is not found in dbo.Orders ------------------------------+
       ¦    The consumer logs a warning and returns without performing any writes           ¦
       ¦    or publishing any follow-up messages. Processing ends for this path.           ¦
       +----------------------------------------------------------------------------------+
       ¦
       ¦  Reads the matching row from dbo.StoreInventories using StoreId and ProductId.
       ¦
       +--- Path B: Inventory record is missing or Quantity is insufficient --------------+
       ¦                                                                                   ¦
       ¦    +- committed atomically --------------------------------------------------+   ¦
       ¦    ¦  Update the Order row in dbo.Orders, setting Status to                  ¦   ¦
       ¦    ¦  AllocationFailed.                                                      ¦   ¦
       ¦    +------------------------------------------------------------------------+   ¦
       ¦                                                                                   ¦
       ¦    4.1-B. ? The consumer publishes AllocationFailedEvent directly to RabbitMQ    ¦
       ¦           after SaveChangesAsync — this publish is not protected by an outbox.    ¦
       +----------------------------------------------------------------------------------+
       ¦
       +--- Path C: Inventory is sufficient ---------------------------------------------+
                                                                                          ¦
            +- committed atomically --------------------------------------------------+  ¦
            ¦  Update the StoreInventory row in dbo.StoreInventories, decrementing    ¦  ¦
            ¦  Quantity by the ordered amount.                                        ¦  ¦
            ¦  Update the Order row in dbo.Orders, setting Status to Allocated.       ¦  ¦
            +------------------------------------------------------------------------+  ¦
                                                                                          ¦
            4.1-C. ? The consumer publishes StoreInventoryAllocatedEvent directly to      ¦
                   RabbitMQ after SaveChangesAsync — this publish is not protected         ¦
                   by an outbox.                                                           ¦
            +-----------------------------------------------------------------------------+
```

---

### Path C continuation — StoreInventoryAllocatedEvent fan-out

```
+---------------------------------------------------------------------------------+
¦  Stage 5 — RabbitMQ Exchange                                                    ¦
¦  Exchange name: DsgOmnichannel.Contracts.Events:StoreInventoryAllocatedEvent    ¦
+---------------------------------------------------------------------------------+
                           ¦                          ¦
              5.1 Routes to queue:       5.2 Routes to queue:
              order-status-history       order-status-notification
                           ¦                          ¦
                           ?                          ?

+----------------------------------+  +------------------------------------------+
¦ Stage 6.1                        ¦  ¦ Stage 6.2                                ¦
¦ OrderStatusHistoryConsumer       ¦  ¦ OrderStatusNotificationConsumer (Api)    ¦
¦ (Worker)                         ¦  ¦ Queue: order-status-notification         ¦
¦ Queue: order-status-history      ¦  ¦ Stateless  |  No idempotency guard ?    ¦
¦ Stateless                        ¦  +------------------------------------------+
¦ Idempotency: EF Core InboxState  ¦                       ¦
+----------------------------------+     Send SignalR message "ReceiveOrderUpdate"
                 ¦                       to all connected clients with orderId,
  +------------------------------+       status set to ReadyForPickup, and timestamp.
  ¦ committed atomically         ¦
  ¦                              ¦
  ¦ Insert a new record into     ¦
  ¦ dbo.OrderStatusHistory with  ¦
  ¦ Status set to Allocated,     ¦
  ¦ Reason set to "Inventory     ¦
  ¦ successfully reserved", and  ¦
  ¦ CreatedAtUtc set to the      ¦
  ¦ current UTC time.            ¦
  +------------------------------+
```

---

### Path B continuation — AllocationFailedEvent fan-out

```
+---------------------------------------------------------------------------------+
¦  Stage 5 — RabbitMQ Exchange                                                    ¦
¦  Exchange name: DsgOmnichannel.Contracts.Events:AllocationFailedEvent           ¦
+---------------------------------------------------------------------------------+
       ¦                          ¦                          ¦
  5.1 Routes to queue:    5.2 Routes to queue:     5.3 Routes to queue:
  order-status-history    order-state              order-status-notification
       ¦                          ¦                          ¦
       ?                          ?                          ?

+-------------------------+  +-------------------------------------------+  +-------------------------+
¦ Stage 6.1               ¦  ¦ Stage 6.2                                 ¦  ¦ Stage 6.3               ¦
¦ OrderStatusHistory      ¦  ¦ OrderStateMachine Saga (Worker)           ¦  ¦ OrderStatusNotification ¦
¦ Consumer (Worker)       ¦  ¦ Queue: order-state                        ¦  ¦ Consumer (Api)          ¦
¦ Queue:                  ¦  ¦ Stateful (EF Core saga store)             ¦  ¦ Queue:                  ¦
¦ order-status-history    ¦  ¦                                           ¦  ¦ order-status-           ¦
¦ Stateless               ¦  ¦ ? The saga resolves ApplicationDbContext  ¦  ¦ notification            ¦
¦ Idempotency: EF Core    ¦  ¦ directly from IServiceProvider inside the ¦  ¦ Stateless               ¦
¦ InboxState              ¦  ¦ ThenAsync handler to update dbo.Orders.   ¦  ¦ No idempotency guard ?  ¦
+-------------------------+  +-------------------------------------------+  +-------------------------+
           ¦                                      ¦                                       ¦
+----------------------+   +----------------------------------------------+  Send SignalR message
¦ committed atomically ¦   ¦ The saga reads the existing OrderState row   ¦  "ReceiveOrderUpdate"
¦                      ¦   ¦ from dbo.OrderState using CorrelationId.     ¦  to all connected clients
¦ Insert a new record  ¦   ¦ Set FailureReason to the reason string       ¦  with orderId, status set
¦ into dbo.OrderStatus ¦   ¦ from the event.                              ¦  to AllocationFailed,
¦ History with Status  ¦   ¦ Set FaultedAt to FailedAtUtc from the event. ¦  and timestamp.
¦ set to               ¦   ¦                                              ¦
¦ AllocationFailed,    ¦   ¦ +- committed atomically -------------------+ ¦
¦ Reason set to the    ¦   ¦ ¦ Update the Order row in dbo.Orders,      ¦ ¦
¦ reason from the      ¦   ¦ ¦ setting Status to AllocationFailed.      ¦ ¦
¦ event, and           ¦   ¦ ¦ Update the OrderState row in             ¦ ¦
¦ CreatedAtUtc set to  ¦   ¦ ¦ dbo.OrderState, setting CurrentState to  ¦ ¦
¦ current UTC time.    ¦   ¦ ¦ Faulted, FailureReason, and FaultedAt.  ¦ ¦
+----------------------+   ¦ +-----------------------------------------+ ¦
                           ¦ Transition CurrentState to Faulted.          ¦
                           ¦ No follow-up messages are published.         ¦
                           +----------------------------------------------+
```

---

## Summary — All Exchanges, Queues, and Consumers

| Exchange (RabbitMQ UI name)                                          | Queue                       | Consumer                        | Host   |
|----------------------------------------------------------------------|-----------------------------|---------------------------------|--------|
| `DsgOmnichannel.Domain.Events:PingEvent`                             | `ping-event`                | PingEventConsumer               | Worker |
| `DsgOmnichannel.Contracts.Events:OrderPlacedEvent`                   | `order-placed-event`        | OrderPlacedEventConsumer        | Worker |
| `DsgOmnichannel.Contracts.Events:OrderPlacedEvent`                   | `order-status-history`      | OrderStatusHistoryConsumer      | Worker |
| `DsgOmnichannel.Contracts.Events:OrderPlacedEvent`                   | `order-state`               | OrderStateMachine (OrderState)  | Worker |
| `DsgOmnichannel.Contracts.Events:OrderPlacedEvent`                   | `order-status-notification` | OrderStatusNotificationConsumer | Api    |
| `DsgOmnichannel.Contracts.Events:StoreInventoryAllocatedEvent`       | `order-status-history`      | OrderStatusHistoryConsumer      | Worker |
| `DsgOmnichannel.Contracts.Events:StoreInventoryAllocatedEvent`       | `order-status-notification` | OrderStatusNotificationConsumer | Api    |
| `DsgOmnichannel.Contracts.Events:AllocationFailedEvent`              | `order-status-history`      | OrderStatusHistoryConsumer      | Worker |
| `DsgOmnichannel.Contracts.Events:AllocationFailedEvent`              | `order-state`               | OrderStateMachine (OrderState)  | Worker |
| `DsgOmnichannel.Contracts.Events:AllocationFailedEvent`              | `order-status-notification` | OrderStatusNotificationConsumer | Api    |

---

## Design Gaps

### 1. Follow-up publishes in OrderPlacedEventConsumer are not transactionally protected

**Where:** `DsgOmnichannel.Worker.Consumers.OrderPlacedEventConsumer.Consume` — the calls to
`context.Publish(new StoreInventoryAllocatedEvent(...))` and `context.Publish(new AllocationFailedEvent(...))`
that occur after `SaveChangesAsync`.

**Risk:** If the Worker process crashes or loses its broker connection between the database commit and
the broker write, the inventory row is already updated (or the order is already marked AllocationFailed)
but the downstream event is never delivered. All consumers that depend on those events — including
OrderStatusHistoryConsumer, OrderStateMachine, and OrderStatusNotificationConsumer — will never be invoked
for that order, leaving it permanently stuck with no status history and no saga state.

**Recommendation:** Enable the EF Core outbox on the Worker's receive endpoint using
`UseEntityFrameworkOutbox<ApplicationDbContext>(context)` inside the
`AddConfigureEndpointsCallback` that is already present in `WorkerServiceCollectionExtensions`.
With the outbox active, `context.Publish` inside a consumer stages the message into
`dbo.OutboxMessage` in the same `SaveChangesAsync` call, and the BusOutboxDeliveryService
forwards it to RabbitMQ after the commit has durably succeeded.

---

### 2. OrderStateMachine directly manipulates application data inside the state machine body

**Where:** `DsgOmnichannel.Worker.Sagas.OrderStateMachine` — the `ThenAsync` handler for
`AllocationFailed` resolves `ApplicationDbContext` from `IServiceProvider` and calls
`dbContext.Orders.FindAsync` followed by `dbContext.SaveChangesAsync` directly inside the
state machine transition.

**Risk:** The saga's own EF Core persistence and the manually opened `ApplicationDbContext` scope
operate in separate transactions. If the saga state is persisted but the manual `SaveChangesAsync`
fails (or vice versa), the `dbo.Orders` status and the `dbo.OrderState` CurrentState will
diverge and be permanently inconsistent. Additionally, this pattern couples orchestration logic
to infrastructure concerns inside the state machine, making both harder to test and extend.

**Recommendation:** Remove the direct database access from the saga body. Instead, publish a
dedicated internal command (for example, `MarkOrderAllocationFailedCommand`) from within the
saga transition, and handle the `dbo.Orders` update in a separate stateless consumer that
benefits from the inbox idempotency guard and the outbox for any further follow-up events.

---

### 3. OrderStatusNotificationConsumer has no idempotency guard against duplicate SignalR pushes

**Where:** `DsgOmnichannel.Api.Consumers.OrderStatusNotificationConsumer` — all three
`Consume` implementations call `_hubContext.Clients.All.SendAsync` with no inbox check
and no deduplication state.

**Risk:** MassTransit will redeliver a message if the consumer faults or if the broker connection
is interrupted after delivery but before acknowledgement. Each redelivery will cause a duplicate
`ReceiveOrderUpdate` SignalR push to every connected client. For a UI that renders a live order
timeline, this produces phantom repeated status entries that are confusing and incorrect.

**Recommendation:** Because SignalR pushes are not persisted, a full EF Core inbox guard is
heavier than necessary. The lightest mitigation is to include a `messageId` field in the
SignalR payload (derived from `context.MessageId`) and deduplicate on the client side by
ignoring any `ReceiveOrderUpdate` whose `messageId` has already been applied to the local
order timeline. Alternatively, enable the MassTransit inbox on this consumer so the broker-level
deduplication prevents the second `Consume` call from reaching the `SendAsync` line at all.
