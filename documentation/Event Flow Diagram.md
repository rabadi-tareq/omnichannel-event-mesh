# Event Flow Diagram
**DSG Omnichannel Engine — Generated from codebase**

---

## System Overview

The system contains four message types, three entry points (two controllers and one minimal-API endpoint), four consumers (three stateless, one saga), and two infrastructure dependencies (SQL Server via EF Core and RabbitMQ). All publishing from the API host is buffered through the EF Core Transactional Outbox. All consuming on the Worker host is guarded by the EF Core Inbox for idempotency, and all messages published from within consumers are staged via the consumer outbox middleware.

---

## Exchange Registry (RabbitMQ broker names)

| Message Type | Exchange Name (as shown in RabbitMQ UI) |
|---|---|
| `OrderPlacedEvent` | `DsgOmnichannel.Contracts.Events:OrderPlacedEvent` |
| `StoreInventoryAllocatedEvent` | `DsgOmnichannel.Contracts.Events:StoreInventoryAllocatedEvent` |
| `AllocationFailedEvent` | `DsgOmnichannel.Contracts.Events:AllocationFailedEvent` |
| `PingEvent` | `DsgOmnichannel.Domain.Events:PingEvent` |

---

## Flow A — Create Order via REST API

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Stage 1 — Entry Point: DsgOmnichannel.Api / OrdersController               │
│  POST /api/orders  (no authorization attribute — anonymous access)          │
│                                                                             │
│  Builds a new Order object in memory with Status set to "Submitted"         │
│  and CreatedAt set to the current UTC time.                                 │
│                                                                             │
│  ┌─ committed atomically ─────────────────────────────────────────────────┐ │
│  │  Insert a new record into dbo.Orders.                                  │ │
│  │  Insert a new record into dbo.OutboxMessage containing the             │ │
│  │  OrderPlacedEvent payload staged by MassTransit's EF Core Outbox.      │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                                                             │
│  Returns HTTP 201 Created with the new Order record body.                   │
└─────────────────────────────────────────────────────────────────────────────┘
						   │
		  Stage 2: The BusOutboxDeliveryService running inside
		  DsgOmnichannel.Api polls dbo.OutboxMessage for undelivered
		  rows, forwards the OrderPlacedEvent payload to RabbitMQ,
		  and marks the row as delivered.
						   │
						   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  Stage 3 — RabbitMQ Exchange                                                │
│  DsgOmnichannel.Contracts.Events:OrderPlacedEvent                           │
└─────────────────────────────────────────────────────────────────────────────┘
		  │                          │                          │
  Stage 4.1: Delivers         Stage 4.2: Delivers        Stage 4.3: Delivers
  message to queue            message to queue           message to queue
  order-placed-event          order-status-history       order-state
		  │                          │                          │
		  ▼                          ▼                          ▼
┌──────────────────┐    ┌────────────────────────┐    ┌──────────────────────┐
│  Stage 5.1       │    │  Stage 5.2              │    │  Stage 5.3           │
│  OrderPlaced     │    │  OrderStatusHistory     │    │  OrderState Saga     │
│  EventConsumer   │    │  Consumer               │    │  (stateful)          │
│  (stateless)     │    │  (stateless,            │    │                      │
│                  │    │   OrderPlaced branch)   │    │                      │
│  See flows A1,   │    │                         │    │                      │
│  A2, A3 below    │    │                         │    │                      │
└──────────────────┘    └────────────────────────┘    └──────────────────────┘
									│                          │
						 Stage 6.2: Inbox check.    Stage 6.3: Inbox check.
						 Reads dbo.InboxState        Reads dbo.InboxState
						 using the message           using the message
						 MessageId to detect         MessageId to detect
						 duplicates. If already      duplicates. If already
						 processed, skips            processed, skips
						 silently.                   silently.
									│                          │
									▼                          ▼
						 ┌──────────────────────┐    ┌─────────────────────────┐
						 │  Stage 7.2           │    │  Stage 7.3              │
						 │  OrderStatusHistory  │    │  OrderStateMachine      │
						 │  Consumer processes  │    │  processes OrderPlaced  │
						 │  the OrderPlaced     │    │  event in the Initial   │
						 │  event branch.       │    │  state.                 │
						 │                      │    │                         │
						 │  ┌─ committed ─────┐ │    │  Sets OrderPlacedDate   │
						 │  │  Insert a new   │ │    │  to the CreatedAt value │
						 │  │  record into    │ │    │  from the event and     │
						 │  │  dbo.Order      │ │    │  sets StoreId from the  │
						 │  │  StatusHistories│ │    │  event.                 │
						 │  │  with Status    │ │    │                         │
						 │  │  set to         │ │    │  ┌─ committed ────────┐ │
						 │  │  "Submitted"    │ │    │  │  Insert a new      │ │
						 │  │  and Reason set │ │    │  │  record into       │ │
						 │  │  to "Order      │ │    │  │  dbo.OrderState    │ │
						 │  │  received via   │ │    │  │  with Correlation  │ │
						 │  │  API".          │ │    │  │  Id set to         │ │
						 │  │  Update dbo.    │ │    │  │  OrderId, Current  │ │
						 │  │  InboxState to  │ │    │  │  State set to      │ │
						 │  │  mark message   │ │    │  │  "Processing",     │ │
						 │  │  as processed.  │ │    │  │  OrderPlacedDate,  │ │
						 │  └─────────────────┘ │    │  │  and StoreId.      │ │
						 └──────────────────────┘    │  │  Update dbo.Inbox  │ │
													 │  │  State to mark     │ │
													 │  │  message as        │ │
													 │  │  processed.        │ │
													 │  └────────────────────┘ │
													 └─────────────────────────┘
```

---

## Flow A1 — OrderPlacedEventConsumer: Transient Fault Path (FAIL_RETRY test hook)

```
┌───────────────────────────────────────────────────────────────────────────┐
│  Stage 5.1 — OrderPlacedEventConsumer                                     │
│  Receives message from queue order-placed-event.                          │
│                                                                           │
│  Checks whether CustomerName equals "FAIL_RETRY".                        │
│  Throws a TimeoutException with message                                   │
│  "Simulated transient database timeout!".                                 │
└───────────────────────────────────────────────────────────────────────────┘
						  │
	 Stage 6.1: The UseMessageRetry exponential retry middleware
	 intercepts the TimeoutException because TimeoutException is
	 in the Handle list of the retry policy.
	 Retry attempt 1 fires after approximately 1 second.
	 Retry attempt 2 fires after approximately 3 seconds.
	 Retry attempt 3 fires after approximately 5 seconds.
	 Each attempt throws the same exception.
						  │
	 Stage 7.1: After all 3 retry attempts are exhausted,
	 MassTransit moves the message to the dead-letter queue
	 named order-placed-event_error.
	 No writes to any application table occur on this path.
```

---

## Flow A2 — OrderPlacedEventConsumer: Order Not Found Path

```
┌───────────────────────────────────────────────────────────────────────────┐
│  Stage 5.1 — OrderPlacedEventConsumer                                     │
│  Receives message from queue order-placed-event.                          │
│                                                                           │
│  Reads dbo.InboxState using the message MessageId.                       │
│  Message not yet processed: continues.                                    │
│  Reads dbo.Orders to find the record matching OrderId from the event.    │
│  No matching record is found.                                             │
└───────────────────────────────────────────────────────────────────────────┘
						  │
	 Stage 6.1: Logs a warning stating that no Order record was found
	 for the given OrderId. Returns without performing any database
	 write and without publishing any follow-up event.
	 The message is acknowledged and removed from the queue.
```

---

## Flow A3 — OrderPlacedEventConsumer: Inventory Allocation Paths

```
┌───────────────────────────────────────────────────────────────────────────┐
│  Stage 5.1 — OrderPlacedEventConsumer                                     │
│  Receives message from queue order-placed-event.                          │
│                                                                           │
│  Reads dbo.InboxState using the message MessageId.                       │
│  Message not yet processed: continues.                                    │
│  Reads the matching record from dbo.Orders using OrderId.                │
│  Reads the matching record from dbo.StoreInventories                     │
│  using StoreId and ProductId from the event.                             │
└───────────────────────────────────────────────────────────────────────────┘
						  │
		  ┌───────────────┴─────────────────────┐
		  │                                     │
  StoreInventory not found or           StoreInventory found and
  StoreInventory.Quantity is less       Quantity is sufficient to
  than the requested Quantity.          satisfy the requested Quantity.
		  │                                     │
		  ▼                                     ▼
┌─────────────────────────────┐    ┌──────────────────────────────────────┐
│  Stage 6.1a — Failure Path  │    │  Stage 6.1b — Success Path           │
│                             │    │                                      │
│  ┌─ committed atomically ─┐ │    │  ┌─ committed atomically ──────────┐ │
│  │  Update dbo.Orders:   │ │    │  │  Update dbo.Orders: set Status  │ │
│  │  set Status to        │ │    │  │  to "Allocated" for the         │ │
│  │  "AllocationFailed"   │ │    │  │  matching OrderId.              │ │
│  │  for the matching     │ │    │  │  Update dbo.StoreInventories:   │ │
│  │  OrderId.             │ │    │  │  decrement Quantity by the      │ │
│  │  Update dbo.Inbox     │ │    │  │  requested amount.              │ │
│  │  State to mark this   │ │    │  │  Update dbo.InboxState to mark  │ │
│  │  message as processed.│ │    │  │  this message as processed.     │ │
│  │  Stage the Allocation │ │    │  │  Stage the StoreInventory       │ │
│  │  FailedEvent payload  │ │    │  │  AllocatedEvent payload to      │ │
│  │  to dbo.OutboxMessage.│ │    │  │  dbo.OutboxMessage.             │ │
│  └───────────────────────┘ │    │  └─────────────────────────────────┘ │
└─────────────────────────────┘    └──────────────────────────────────────┘
		  │                                     │
  Stage 7.1a: The consumer outbox       Stage 7.1b: The consumer outbox
  middleware forwards the staged        middleware forwards the staged
  AllocationFailedEvent to RabbitMQ     StoreInventoryAllocatedEvent to
  after the consumer transaction        RabbitMQ after the consumer
  commits.                              transaction commits.
		  │                                     │
		  ▼                                     ▼
┌────────────────────────────┐    ┌─────────────────────────────────────┐
│  Stage 8.1a — Exchange     │    │  Stage 8.1b — Exchange              │
│  DsgOmnichannel.Contracts  │    │  DsgOmnichannel.Contracts           │
│  .Events:                  │    │  .Events:                           │
│  AllocationFailedEvent     │    │  StoreInventoryAllocatedEvent       │
└────────────────────────────┘    └─────────────────────────────────────┘
	   │             │                          │                  │
 Stage 9.1a.1  Stage 9.1a.2            Stage 9.1b.1         Stage 9.1b.2
 Delivers to   Delivers to             Delivers to          Delivers to
 queue         queue                   queue                queue
 order-status  order-state             order-status         order-state
 -history                              -history             (⚠ no handler
															registered for
															this event)
	   │             │                          │                  │
	   ▼             ▼                          ▼                  ▼
┌────────────┐ ┌──────────────────────┐ ┌─────────────────────────────────┐
│Stage 10.1  │ │ Stage 10.1a.2        │ │ Stage 10.1b.1                   │
│a.1         │ │ OrderStateMachine    │ │ OrderStatusHistoryConsumer       │
│OrderStatus │ │ Saga                 │ │ (StoreInventoryAllocated branch) │
│History     │ │ (stateful,           │ │                                 │
│Consumer    │ │ AllocationFailed     │ │ ┌─ committed atomically ───────┐ │
│(Allocation │ │ event in Processing  │ │ │  Insert a new record into   │ │
│Failed      │ │ state)               │ │ │  dbo.OrderStatusHistories   │ │
│branch)     │ │                      │ │ │  with Status set to         │ │
│            │ │ Reads dbo.InboxState.│ │ │  "Allocated" and Reason     │ │
│┌─committed─┐│ │ If already done,    │ │ │  set to "Inventory          │ │
││Insert a   ││ │ skips silently.     │ │ │  successfully reserved".    │ │
││new record ││ │                     │ │ │  Update dbo.InboxState to   │ │
││into dbo.  ││ │ Sets FailureReason  │ │ │  mark message as processed. │ │
││OrderStatus││ │ and FaultedAt from  │ │ └─────────────────────────────┘ │
││Histories  ││ │ the event.          │ └─────────────────────────────────┘
││with Status││ │ Resolves a scoped   │
││set to     ││ │ ApplicationDbContext│
││"Allocation││ │ from the service    │        Stage 10.1b.2
││Failed"    ││ │ provider.           │        StoreInventoryAllocatedEvent
││and Reason ││ │ Reads dbo.Orders by │        arrives at order-state queue
││set to the ││ │ OrderId.            │        but OrderStateMachine has no
││event      ││ │                     │        handler registered for this
││Reason     ││ │ ┌─ committed ─────┐ │        event type in any state.
││field.     ││ │ │  Update dbo.    │ │        MassTransit moves the message
││Update dbo.││ │ │  OrderState:    │ │        to the dead-letter queue
││InboxState ││ │ │  set Current   │ │        named order-state_error.
││to mark    ││ │ │  State to      │ │
││message as ││ │ │  "Faulted",    │ │
││processed. ││ │ │  set Failure   │ │
│└───────────┘│ │ │  Reason and   │ │
└────────────┘ │ │  FaultedAt.    │ │
			   │ │  Update dbo.   │ │
			   │ │  Orders: set   │ │
			   │ │  Status to     │ │
			   │ │  "Allocation   │ │
			   │ │  Failed".      │ │
			   │ │  Update dbo.   │ │
			   │ │  InboxState to │ │
			   │ │  mark message  │ │
			   │ │  as processed. │ │
			   │ └────────────────┘ │
			   └──────────────────────┘
```

---

## Flow B — Test: Publish OrderPlacedEvent Without Creating an Order

```
┌────────────────────────────────────────────────────────────────────────────┐
│  Stage 1 — Entry Point: DsgOmnichannel.Api / TestController                │
│  POST /api/test/publish-order-event  ([AllowAnonymous])                    │
│                                                                            │
│  Builds an OrderPlacedEvent in memory using the supplied OrderId,          │
│  StoreId, CustomerName, ProductId, Quantity, and TotalAmount.              │
│  Uses the caller-supplied MessageId to support deduplication testing.      │
│                                                                            │
│  ┌─ committed atomically ─────────────────────────────────────────────────┐│
│  │  Insert a new record into dbo.OutboxMessage containing the            ││
│  │  OrderPlacedEvent payload. No dbo.Orders record is written.           ││
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                            │
│  Returns HTTP 200 OK with a confirmation body containing MessageId         │
│  and OrderId.                                                              │
└────────────────────────────────────────────────────────────────────────────┘
						  │
	 Stage 2: The BusOutboxDeliveryService running inside DsgOmnichannel.Api
	 polls dbo.OutboxMessage, forwards the OrderPlacedEvent to RabbitMQ,
	 and marks the row as delivered.
						  │
						  ▼
	 Stage 3 onward is identical to Flow A from Stage 3.
	 Because no dbo.Orders record is written, OrderPlacedEventConsumer
	 will follow Flow A2 (Order Not Found) unless a matching Order record
	 already exists in the database.
```

---

## Flow C — Test: Publish PingEvent via Minimal API

```
┌────────────────────────────────────────────────────────────────────────────┐
│  Stage 1 — Entry Point: DsgOmnichannel.Api / TestPublishEndpoint           │
│  POST /test-publish?text={text}  (anonymous)                               │
│                                                                            │
│  Builds a PingEvent in memory with a new Guid as Id, the supplied          │
│  text as Message, and the current UTC timestamp.                           │
│                                                                            │
│  ┌─ committed atomically ─────────────────────────────────────────────────┐│
│  │  Insert a new record into dbo.OutboxMessage containing the            ││
│  │  PingEvent payload.                                                   ││
│  └────────────────────────────────────────────────────────────────────────┘│
│                                                                            │
│  Returns HTTP 200 OK with status "Published" and the PingEvent object.     │
└────────────────────────────────────────────────────────────────────────────┘
						  │
	 Stage 2: The BusOutboxDeliveryService running inside DsgOmnichannel.Api
	 polls dbo.OutboxMessage, forwards the PingEvent to RabbitMQ, and marks
	 the row as delivered.
						  │
						  ▼
┌────────────────────────────────────────────────────────────────────────────┐
│  Stage 3 — RabbitMQ Exchange                                               │
│  DsgOmnichannel.Domain.Events:PingEvent                                    │
└────────────────────────────────────────────────────────────────────────────┘
						  │
	 Stage 4: Delivers message to queue ping-event.
						  │
						  ▼
┌────────────────────────────────────────────────────────────────────────────┐
│  Stage 5 — PingEventConsumer (stateless)                                   │
│                                                                            │
│  Logs an informational message containing the PingEvent Message field      │
│  and Id field. Performs no database write and publishes no follow-up       │
│  event. Returns Task.CompletedTask immediately.                            │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Design Notes

- **StoreInventoryAllocatedEvent has no saga handler.** The `OrderStateMachine` registers handlers only for `OrderPlacedEvent` and `AllocationFailedEvent`. Any `StoreInventoryAllocatedEvent` delivered to the `order-state` queue will be moved to `order-state_error` by MassTransit because no handler is registered for that event type in any saga state. This is a design gap in the saga — the success path has no terminal state.
- **FAIL_RETRY test hook is live in production consumer code.** Any message whose `CustomerName` equals `"FAIL_RETRY"` will throw a `TimeoutException` in `OrderPlacedEventConsumer`, exhaust all 3 retry attempts, and land in the `order-placed-event_error` dead-letter queue. This hook should be removed before deploying to a shared or production environment.
- **TestController does not write an Order record.** Using `POST /api/test/publish-order-event` without a pre-existing matching `dbo.Orders` row means `OrderPlacedEventConsumer` will silently return on the "Order Not Found" path.
