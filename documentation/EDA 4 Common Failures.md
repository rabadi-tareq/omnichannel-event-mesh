### 1. Dual-Write Failures (Lost Events)
* **The Failure:** A service successfully persists state to the database but crashes before publishing the corresponding event to the broker (or vice versa), permanently breaking system synchronization.
* **The Guard:** The `POST /api/orders` endpoint leverages the Transactional Outbox Pattern via MassTransit and EF Core 10[cite: 1, 5]. This guarantees the `Order` entity and the `OrderPlacedEvent` are committed in a single, atomic local database transaction[cite: 1, 5].

### 2. Duplicate Processing Failures (Non-Idempotency)
* **The Failure:** The message broker redelivers an event due to a transient network drop or consumer fault, causing the background worker to execute the operation a second time (e.g., deducting inventory twice).
* **The Guard:** The system enforces consumer idempotency using the Inbox Pattern[cite: 1, 5]. This ensures that retried messages from RabbitMQ never result in duplicate side effects[cite: 1, 5].

### 3. Distributed Rollback Failures
* **The Failure:** A complex business flow succeeds in Step 1 (creating an order) but fails in Step 2 (inventory allocation). Because traditional ACID rollbacks cannot span distributed microservices, the system is left in a corrupted half-state.
* **The Guard:** Complex, multi-step order lifecycles are managed by MassTransit State Machine Sagas (`OrderStateMachine`)[cite: 1, 5]. The system executes compensating transactions, capturing events like `AllocationFailedEvent` to cleanly revert the order status in SQL Server when physical inventory is insufficient[cite: 1, 5].

### 4. Poison Messages & Worker Starvation
* **The Failure:** A malformed payload or inherent domain error (e.g., `ArgumentException`) continually fails to process. The message broker endlessly retries the message, monopolizing worker threads and preventing healthy messages from being processed.
* **The Guard:** The worker engine employs strict exception filtering and Dead-Letter Queues (DLQ)[cite: 1, 5]. Domain exceptions instantly bypass retries and are routed to isolation (e.g., `OrderPlacedEvent_error`) for manual review, freeing up worker capacity[cite: 1, 5].

### 5. Transient Dependency Failures
* **The Failure:** Downstream databases or network connections experience a temporary blip, causing event consumers to crash and drop messages unnecessarily.
* **The Guard:** The `DsgOmnichannel.Worker` relies on an exponential backoff retry policy (~1s, ~3s, ~5s) to gracefully absorb temporary downstream locks or timeouts without failing the entire event loop[cite: 1].