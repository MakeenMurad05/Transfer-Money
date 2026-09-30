# MoneyTransfer API – Project Plan

A simple money transfer API built with ASP.NET Core, step by step.
Every step is small, and every decision has a **why** and a **why not**.

---

## 1. What are we building?

An API that receives a transfer request, saves it, sends it to a bank, and returns the result.

```http
POST /api/transfers
GET  /api/transfers
GET  /api/transfers/{id}
```

Example request:

```json
{
  "accountNumber": "001234567890",
  "amount": 150.750,
  "currency": "LYD",
  "reference": "MW-001"
}
```

### The flow in simple words

```text
Client sends request
   -> Validate the data (Amount > 0, required fields...)
   -> Check the Reference is not used before
   -> Save the transfer as Pending
   -> Call the bank using HttpClient
   -> Save what we sent and what we received (log table)
   -> Update the transfer to Success or Failed
   -> Return the result to the client
```

---

## 2. Tech stack

| Tool | Used for |
|---|---|
| .NET 10 | The framework |
| ASP.NET Core Web API (Controllers) | The API |
| SQL Server + EF Core | Database |
| MediatR 12.5.0 | CQRS (Commands / Queries / Handlers) |
| FluentValidation | Validation rules |
| IHttpClientFactory | Calling the bank (never `new HttpClient()`) |
| Swashbuckle | Swagger UI |

---

## 3. Decisions we made (and why)

### .NET 10, not .NET 8
- **Why:** .NET 10 is LTS and the latest version, with long support.
- **Why not .NET 8:** its support ends in November 2026.

### `Amount` is `decimal(18,3)`
- **Why:** Libyan Dinar has 3 decimal places (150.750). Money must be exact.
- **Why not `double`:** it has rounding errors. Never use it for money.

### Save as `Pending` before calling the bank
- **Why:** if the server crashes in the middle, we still have a record that a transfer started.
- **Why not save only after the bank answers:** money could move and we would have no trace of it.

### Stop duplicate `Reference` in two places
- In code with `AnyAsync()` **and** with a `Unique Index` in the database.
- **Why both:** two requests with the same Reference at the same moment can both pass the code check. The database index is the final guard.

### `IAppDbContext` interface instead of Repositories
- **Why:** simpler, less code. EF Core already works like a repository.
- **Why not Repositories:** an extra layer we don't need for a project this size.
- **Trade-off:** Application references `Microsoft.EntityFrameworkCore` (core only), but **not** SQL Server.

### MediatR 12.5.0 (pinned)
- **Why:** from version 13, MediatR needs a license key. 12.5.0 is the last free version and has everything we need.

### Swagger needs to be added manually
- .NET 10 templates don't include Swagger UI, so we install `Swashbuckle.AspNetCore`.

### We build our own Mock Bank
- **Why:** no real bank API was given. With our own mock, we control every response and can test all cases.
- **Why a separate project:** the bank must be *external*, so our HttpClient makes a real network call.
- **Why not an online mock tool:** depends on internet and a third party, harder to control.

---

## 4. Architecture (Clean Architecture)

```text
MoneyTransfer/
├── src/
│   ├── MoneyTransfer.Domain          -> Entities, Enums (no packages)
│   ├── MoneyTransfer.Application     -> Commands, Queries, Handlers, Validators, Interfaces
│   ├── MoneyTransfer.Infrastructure  -> DbContext, Migrations, BankClient
│   └── MoneyTransfer.Api             -> Controllers, Program.cs, Exception handling
└── tools/
    └── MockBank.Api                  -> The fake external bank
```

### Who references who

```text
Api  ──►  Application  ──►  Domain
 │            ▲
 └──►  Infrastructure
```

- **Application → Domain:** handlers use the entities.
- **Infrastructure → Application:** Infrastructure *implements* the interfaces Application defines (`IBankClient`, `IAppDbContext`).
- **Api → Infrastructure:** only so `Program.cs` can register services in DI. Controllers never use Infrastructure directly.
- **Domain → nothing:** the heart of the app never depends on databases, HTTP, or frameworks.

### The golden rule
Dependencies point **inward**. The inner layers don't know the outer layers exist.

### Controllers are thin
A controller only receives the request, sends it to MediatR, and returns the result. **No business logic.**

---

## 5. Packages per project

| Project | Packages |
|---|---|
| Domain | none |
| Application | MediatR (12.5.0), FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.EntityFrameworkCore |
| Infrastructure | Microsoft.EntityFrameworkCore.SqlServer, Microsoft.Extensions.Http, Microsoft.Extensions.Options.ConfigurationExtensions |
| Api | Swashbuckle.AspNetCore, Microsoft.EntityFrameworkCore.Design |
| MockBank.Api | none |

Global tool: `dotnet tool install --global dotnet-ef`

> ⚠️ All EF Core packages must be the **same major version** (all 10.x).

---

## 6. Database

### `Transfers` table

| Column | Type | Notes |
|---|---|---|
| Id | int | Primary key, used in `GET /api/transfers/{id}` |
| TransactionId | Guid | Created by us, sent to the bank |
| Reference | string | **Unique Index** |
| AccountNumber | string | |
| Amount | decimal(18,3) | |
| Currency | string | e.g. LYD |
| Status | enum | Pending / Success / Failed |
| BankReference | string? | Filled on success |
| ResponseCode | string? | From the bank |
| ResponseMessage | string? | From the bank |
| CreatedAt | DateTime | UTC |

### `ExternalServiceLogs` table

| Column | Type | Notes |
|---|---|---|
| Id | int | Primary key |
| TransactionId | Guid | Links the log to the transfer |
| ServiceName | string | e.g. "BankTransfer" |
| Request | string | What we sent (JSON) |
| Response | string? | Null on Timeout / Connection Error |
| HttpStatusCode | int? | Null on Timeout / Connection Error |
| DurationMs | long | How long the call took |
| CreatedAt | DateTime | UTC |

---

## 7. The Mock Bank rules

The fake bank decides its answer based on the **amount**:

| Amount | Bank returns | Case we test |
|---|---|---|
| normal (e.g. 150.750) | 200 + code `"00"` | Success |
| `400` | 400 Bad Request | 400 |
| `500` | 500 Server Error | 500 |
| `999` | waits longer than our timeout | Timeout |
| more than `10000` | 200 + code `"51"` Insufficient funds | Business Error |
| bank not running | no answer | Connection Error |

> **Business Error** = HTTP says OK (200), but the bank itself says "no". Very common in real banking.

### How each case updates the transfer

| Case | Transfer Status |
|---|---|
| 200 + `"00"` | Success |
| 200 + other code | Failed (business error) |
| 400 | Failed |
| 500 | Failed |
| Timeout | Failed |
| Connection Error | Failed |

> Real world note: on a Timeout, the money *might* have moved. Real systems often use an extra status like `Unknown` and check later. The task asks for Success/Failed only, so we use Failed, but it's good to mention this in the README.

---

## 8. Roadmap

| # | Step | Status |
|---|---|---|
| 1 | Create the solution, 5 projects, and references | ✅ Done |
| 2 | Domain: `TransferStatus`, `Transfer`, `ExternalServiceLog` | ✅ Done |
| 2.5 | Install packages | ✅ Done |
| 3 | Infrastructure: DbContext, Unique Index, first Migration | ⏳ Next |
| 4 | Application: Create Transfer Command + Validator + Validation pipeline | |
| 5 | Mock Bank API | |
| 6 | `IBankClient` + `BankClient` with IHttpClientFactory, settings from `appsettings.json` | |
| 7 | Create Transfer Handler (the full flow) | |
| 8 | Queries: Get all transfers, Get transfer by id | |
| 9 | Global exception handling + Logging | |
| 10 | Swagger, Postman Collection, README | |

---

## 9. What we deliver

- [ ] Source Code
- [ ] EF Core Migrations
- [ ] README
- [ ] Postman Collection
- [ ] Simple explanation of the Architecture

## 10. What they will test

- [ ] Clean Architecture
- [ ] CQRS
- [ ] SQL Server
- [ ] EF Core
- [ ] LINQ
- [ ] HttpClient
- [ ] Dependency Injection
- [ ] Validation
- [ ] Exception Handling
- [ ] Async/Await
- [ ] Logging
- [ ] Code Quality
