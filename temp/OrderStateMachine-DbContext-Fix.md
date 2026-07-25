# OrderStateMachine — `PayloadNotFoundException` Analysis

## Exception
**Type:** `MassTransit.PayloadNotFoundException`  
**Message:** `The payload was not found: DsgOmnichannel.Infrastructure.Persistence.ApplicationDbContext`  
**Location:** `OrderStateMachine.cs`, line 37

## Root Cause

`context.GetPayload<T>()` retrieves objects explicitly added to MassTransit's **pipe payload bag** — `ApplicationDbContext` is never placed there. It lives in the **DI container** as a scoped service.

```csharp
// ❌ Incorrect — ApplicationDbContext is not a MassTransit payload
var dbContext = context.GetPayload<ApplicationDbContext>();
```

---

## Fix Options

### Option 1 — Resolve from `IServiceProvider` payload
```csharp
var dbContext = context.GetRequiredPayload<IServiceProvider>()
					   .GetRequiredService<ApplicationDbContext>();
```
| ✅ Pros | ❌ Cons |
|---|---|
| Works in any MassTransit context | Slightly verbose |
| No assumptions about saga repository type | |

---

### Option 2 — Inject `ApplicationDbContext` via constructor
```csharp
public OrderStateMachine(ApplicationDbContext dbContext)
{
	// use dbContext in ThenAsync
}
```
| ✅ Pros | ❌ Cons |
|---|---|
| Clean and testable | `OrderStateMachine` is registered as a **singleton** by MassTransit — injecting a **scoped** `DbContext` into a singleton causes a **captive dependency** bug |

---

### Option 3 — Use a custom `IActivity<,>` class
Move the DB logic into a dedicated MassTransit activity class, which is resolved per-message and can safely receive `ApplicationDbContext` via constructor injection.

| ✅ Pros | ❌ Cons |
|---|---|
| Correct scoping, clean separation of concerns | More boilerplate |

---

## Recommendation

**Option 1** is the best immediate fix. It correctly resolves a scoped `DbContext` per-message without restructuring the saga, and it is the standard pattern for accessing DI services inside MassTransit saga `ThenAsync` blocks.

**Option 3** is the ideal long-term architecture if the saga grows in complexity.

**Option 2 should be avoided** — it introduces a captive dependency anti-pattern (scoped service injected into a singleton).
