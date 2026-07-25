---
name: solution-schema-and-contracts
description: Autonomously inspects the DSG Omnichannel Engine solution and writes an up-to-date "Solution Schema & Contracts.md" to the documentation folder. Covers HTTP endpoints, port settings, request DTOs, domain entities, event contracts, and saga state.
---

# Solution Schema & Contracts

Use this skill whenever the user asks to generate, refresh, or update `documentation\Solution Schema & Contracts.md`.

## Goal

Read the current source files, extract the exact definitions listed below, and overwrite `documentation\Solution Schema & Contracts.md` with an accurate, up-to-date markdown document.

---

## What to Extract

Work through each source area in order. Use `get_projects_in_solution`, `get_files_in_project`, and `get_file` to read the actual code — never guess or use values from memory.

### 1. Port & URL Configuration
- File: `src\DsgOmnichannel.Api\Properties\launchSettings.json`
- Extract every named profile and its `applicationUrl` value.

### 2. App Settings (Development)
- Files: `src\DsgOmnichannel.Api\appsettings.Development.json` and `src\DsgOmnichannel.Worker\appsettings.Development.json`
- Extract `ConnectionStrings:DefaultConnection`, and all keys under `RabbitMQ`.

### 3. HTTP Endpoints
- Files: all `*.cs` files under `src\DsgOmnichannel.Api\Controllers\` and `src\DsgOmnichannel.Api\Endpoints\`
- For every controller or minimal-API file, list:
  - HTTP method + route pattern
  - Authorization requirement (anonymous or named policy)
  - Request body type (if any)
  - Response shape description

### 4. Request DTOs
- Same controller files as above.
- For each DTO class, list every property: name, type, and any `[Required]`, `[StringLength]`, or `[Range]` validation attributes.

### 5. Domain Entities
- Files: all `*.cs` files under `src\DsgOmnichannel.Domain\Entities\`
- For each entity class, list every property: name and type.
- Note the corresponding SQL table name if derivable from EF configuration; otherwise derive it from EF Core convention (pluralized class name, `dbo` schema).

### 6. Event Contracts
- Files: all `*.cs` files under `src\DsgOmnichannel.Contracts\Events\`
- For each `record`, list every positional or init property: name and type.
- Note which component publishes each event and which consumers or state machines subscribe to it.

### 7. Saga State Entities
- Locate all classes that implement `SagaStateMachineInstance` (search `src\DsgOmnichannel.Infrastructure\Persistence\Sagas\`).
- For each saga state class, list every property: name and type, and note its role (PK, state discriminator, mapped event field, etc.).
- Locate the corresponding `MassTransitStateMachine<T>` class (under `src\DsgOmnichannel.Worker\Sagas\`) and document:
  - All defined `State` properties and their names.
  - All defined `Event<T>` properties, the event type, and the correlation expression.

---

## Output Format

Write the output as a structured markdown file using the exact template below. Replace every `{…}` placeholder with real values read from the code. Remove any section that has no content.

```markdown
# Solution Schema & Contracts
**DSG Omnichannel Engine — Generated from codebase**

---

## 1. HTTP Endpoints & Port Configuration

### API Host — `DsgOmnichannel.Api`

| Profile | URL |
|---------|-----|
| `{profile}` | `{applicationUrl}` |

---

### `{ControllerName}` — `{routePrefix}`

#### `{METHOD} {route}`
{Description of what the endpoint does.}

**Request body** (`{DtoTypeName}`):
```json
{ … example … }
```

**Response:** {status code and shape}

---

## 2. Infrastructure Configuration (Development)

| Key | Value |
|-----|-------|
| `ConnectionStrings:DefaultConnection` | `{value}` |
| `RabbitMQ:{Key}` | `{value}` |

---

## 3. Request DTOs

### `{DtoName}`
Defined in `{relative/path/to/file.cs}`.

| Property | Type | Validation |
|----------|------|------------|
| `{Name}` | `{Type}` | {constraints or —} |

---

## 4. Domain Entities

### `{EntityName}`
Namespace: `{namespace}` — Table: `dbo.{TableName}`

| Property | Type | Notes |
|----------|------|-------|
| `{Name}` | `{Type}` | {notes or —} |

---

## 5. Event Contracts

### `{EventName}`
Published by: {publisher}. Consumed by: {consumers / state machines}.

| Property | Type |
|----------|------|
| `{Name}` | `{Type}` |

---

## 6. Saga Entities

### `{SagaStateClassName}`
Namespace: `{namespace}` — Table: `dbo.{TableName}`
Implements: `MassTransit.SagaStateMachineInstance`

| Property | Type | Notes |
|----------|------|-------|
| `{Name}` | `{Type}` | {role} |

**State machine:** `{StateMachineClassName}` (in `{project}`)
**Defined states:** {comma-separated state names}
**Defined events:** {EventProperty} → `{EventType}` (correlates by `{expression}`)
```

---

## Execution Rules

1. **Always read from source** — do not rely on previously generated output or memory. Read every relevant file fresh.
2. **Overwrite, do not append** — use `create_file` if the file does not exist, or `replace_string_in_file` / recreate if it does.
3. **No placeholders in output** — every field in the written document must be a real value extracted from the code.
4. **Preserve heading order** — sections must appear in the order defined in the template.
5. **One JSON example per endpoint** — include a realistic but minimal JSON body example for every endpoint that accepts a request body.
6. **Confirm on completion** — after writing the file, tell the user the file path and briefly list which sections were updated.
