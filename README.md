# MoneyTransfer API

An ASP.NET Core Web API that simulates a money transfer. It validates the request, saves the transfer as `Pending`, calls an external bank API, logs the bank request and response, and updates the transfer to `Success` or `Failed`.

<img width="1901" height="917" alt="image" src="https://github.com/user-attachments/assets/6cd824f5-4706-4fb7-8167-801e4e09a33b" />


---

## Tech Stack

| Tool | Used for |
|---|---|
| .NET 10 / ASP.NET Core Web API | The API (Controllers) |
| Clean Architecture | Project structure |
| MediatR 12.5.0 | CQRS (Commands, Queries, Pipeline Behaviors) |
| SQL Server + EF Core | Database and migrations |
| FluentValidation | Request validation |
| IHttpClientFactory (typed client) | Calling the external bank |
| Swashbuckle | Swagger UI |

---

## Architecture

The solution follows **Clean Architecture**. It is split into 4 layers, and dependencies only point **inward**.

```text
            ┌──────────────┐
            │     API      │  Controllers, Program.cs, exception handling
            └──────┬───────┘
                   │
     ┌─────────────┼──────────────┐
     ▼                            ▼
┌──────────────┐          ┌────────────────┐
│ Application  │ ◄────────│ Infrastructure │  DbContext, Migrations, BankClient
└──────┬───────┘          └────────────────┘
       │  Commands, Queries, Handlers, Validators, Interfaces
       ▼
┌──────────────┐
│    Domain    │  Entities, Enums (no dependencies, no packages)
└──────────────┘
```

| Layer | Responsibility |
|---|---|
| **Domain** | Business entities (`Transfer`, `ExternalServiceLog`) and `TransferStatus`. Depends on nothing. |
| **Application** | Use cases (CQRS commands and queries), validation, and the interfaces it needs (`IAppDbContext`, `IBankClient`). |
| **Infrastructure** | Implements Application's interfaces: EF Core `AppDbContext`, migrations, and `BankClient` (HTTP). |
| **API** | Thin controllers that only send requests to MediatR, global exception handling, Swagger, DI setup. |

**Key rules:**

- **Controllers contain no business logic.** They receive the request, call `ISender.Send(...)`, and return the result.
- **Application never knows about SQL Server or HTTP details.** It works with interfaces; Infrastructure provides the implementations.
- **Each layer registers its own services** (`AddApplication()`, `AddInfrastructure()`), so `Program.cs` stays short.

For the reasoning behind each design choice, see [DECISIONS.md](DECISIONS.md).

### CQRS

| Type | Name | Endpoint |
|---|---|---|
| Command | `CreateTransferCommand` | `POST /api/transfers` |
| Query | `GetTransfersQuery` | `GET /api/transfers` |
| Query | `GetTransferByIdQuery` | `GET /api/transfers/{id}` |

A `ValidationBehavior` (MediatR pipeline behavior) runs the FluentValidation rules automatically **before every handler**. Invalid requests never reach the handler.

### Create Transfer flow

```text
POST /api/transfers
  1. Validate the request (ValidationBehavior + FluentValidation)
  2. Check Amount > 0
  3. Reject a duplicate Reference (409)
  4. Save the transfer as Pending
  5-7. Call the bank with HttpClient and parse the response
  8. Save the bank request/response in ExternalServiceLogs
  9. Update the transfer to Success or Failed
  10. Return the result (201 Created)
```

The log and the status update are saved **in the same `SaveChanges`**, so they are stored together or not at all.

---

## Project Structure

```text
MoneyTransfer/
├── Domain/                  Entities, Enums
├── Application/             Commands, Queries, Handlers, Validators, Interfaces, Behaviors
├── Infrastructure/          AppDbContext, Configurations, Migrations, BankClient
├── API/                     Controllers, Exception handling, Program.cs
├── Tools/MockBankAPI/       A fake external bank used for testing
├── MoneyTransfer.postman_collection.json
└── MoneyTransfer.slnx
```

---

## Database

### `Transfers`

`Id`, `TransactionId`, `Reference`, `AccountNumber`, `Amount`, `Currency`, `Status`, `BankReference`, `ResponseCode`, `ResponseMessage`, `CreatedAt`

- **Unique Index** on `Reference`.
- `Amount` is `decimal(18,3)` (LYD uses 3 decimal places).
- `Status` is stored as text (`Pending`, `Success`, `Failed`) for readability.

### `ExternalServiceLogs`

`Id`, `TransactionId`, `ServiceName`, `Request`, `Response`, `HttpStatusCode`, `DurationMs`, `CreatedAt`

- `Response` and `HttpStatusCode` are empty when the bank never answered (Timeout / Connection Error).

---

## External Bank Integration

- `IBankClient` is defined in **Application**, `BankClient` is implemented in **Infrastructure**.
- Registered as a **typed client** with `AddHttpClient<IBankClient, BankClient>()`, so it uses `IHttpClientFactory`. `new HttpClient()` is never used.
- The bank URL and timeout are read from `appsettings.json`.

### How each case is handled

| Bank result | Detected by | Transfer Status | ResponseCode |
|---|---|---|---|
| HTTP 200 + code `00` | Status code + body | `Success` | `00` |
| HTTP 200 + other code (Business Error) | Body code is not `00` | `Failed` | bank code (e.g. `51`) |
| HTTP 400 | Status code | `Failed` | bank code (e.g. `30`) |
| HTTP 500 | Status code | `Failed` | bank code (e.g. `96`) |
| Timeout | `TaskCanceledException` | `Failed` | `TIMEOUT` |
| Connection Error | `HttpRequestException` | `Failed` | `CONNECTION_ERROR` |
| HTTP 200 + `00` but no bank reference | Body has no reference | `Failed` | `INVALID_RESPONSE` |
| Any other unexpected error | Final catch in `BankClient` | `Failed` | `UNEXPECTED_ERROR` |

`BankClient` never throws for bank failures. It always returns a `BankTransferResult`, and the handler decides what to save.

---

## Error Handling

A global `IExceptionHandler` converts exceptions into standard **ProblemDetails** responses:

| Situation | Status |
|---|---|
| Transfer processed (bank Success **or** Failed) | `201 Created` |
| Validation error | `400 Bad Request` (with an `errors` object) |
| Transfer not found | `404 Not Found` |
| Duplicate Reference | `409 Conflict` |
| Unexpected error | `500` (generic message, full details only in the logs) |

A bank failure still returns **201**, because the request was valid and a transfer record was created. The result is in the `status` field.

---

## Logging

- **Database:** every bank call is stored in `ExternalServiceLogs` (request, response, status code, duration).
- **Application logs:** `ILogger` with structured messages. `Information` for success, `Warning` for expected failures (bank refused, timeout), `Error` for connection errors and unhandled exceptions. Account numbers are not written to application logs.

---

## How to Run

### Prerequisites

- .NET 10 SDK
- SQL Server (LocalDB, Express, or full)
- EF Core tool: `dotnet tool install --global dotnet-ef`

### 1. Configure

`API/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=MoneyTransferDb;Trusted_Connection=True;TrustServerCertificate=True"
},
"Bank": {
  "BaseUrl": "http://localhost:5100/",
  "TimeoutSeconds": 5
}
```

Change `Server=` to match your SQL Server (e.g. `.\\SQLEXPRESS`).

### 2. Create the database

```bash
dotnet ef database update --project Infrastructure --startup-project API
```

### 3. Start the Mock Bank (terminal 1)

```bash
dotnet run --project Tools/MockBankAPI
```

### 4. Start the API (terminal 2)

```bash
dotnet run --project API
```

Swagger: `http://localhost:<api-port>/swagger`

---

## Testing

You can test with **Swagger**, the **Postman collection** (`MoneyTransfer.postman_collection.json`, set the `baseUrl` variable), or the `.http` files.

### Mock Bank scenarios

The Mock Bank decides its answer based on the **amount**:

| Amount | Result |
|---|---|
| Any normal amount (e.g. `150.750`) | Success |
| `400` | Bank returns 400 |
| `500` | Bank returns 500 |
| `999` | Bank waits 10s → Timeout (our timeout is 5s) |
| More than `10000` | Business Error (`51` Insufficient funds) |
| Mock Bank stopped | Connection Error |

---

## Design Notes and Trade-offs

- **Timeout is saved as `Failed`**, as required by the task. In a real system, a timeout means the result is unknown (the bank may have processed it), so an extra status like `Unknown` with a later status check would be safer.
- **No automatic retries.** Retrying a money transfer could send the money twice.
- **Duplicate `Reference` is checked twice:** in code (clear 409 message) and by the Unique Index (protects against two requests at the same moment).
- **MediatR is pinned to 12.5.0**, the last version that does not require a license key.

## Future Improvements

- Pagination and filtering for `GET /api/transfers`
- An `Unknown` status and a background job to re-check timed-out transfers
- Masking the account number in stored request logs
- Unit and integration tests
