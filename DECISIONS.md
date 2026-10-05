# Design Decisions

This file explains **why** the project is built the way it is. For each decision: the options we considered, what we chose, and why the others lost.

## How decisions were made

For every choice, the same 4 questions:

1. **What must be true?** (the task requirements)
2. **What can go wrong?** (crashes, slow bank, two requests at the same time)
3. **What does it cost?** (more code, more packages, harder to read)
4. **Pick the simplest option that passes 1 and 2.**

---

## 1. Platform

### .NET 10 instead of .NET 8
- **Chosen:** .NET 10 (LTS, latest).
- **Why not .NET 8:** its support ends in November 2026.

### MediatR pinned to 12.5.0
- **Why:** from version 13, MediatR requires a license key. 12.5.0 is the last free version and has everything we need.

### Controllers instead of Minimal APIs (main API)
- **Why:** the task explicitly talks about Controllers. The Mock Bank uses Minimal API because it is only a test tool.

### Our own Mock Bank
- **Options:** online mock service / an endpoint inside our own API / a separate project.
- **Chosen:** a separate project (`Tools/MockBankAPI`).
- **Why:** no real bank API was provided. A separate project runs on its own port, so `HttpClient` makes a real network call. We control every scenario. An online mock depends on the internet and a third party.

---

## 2. Architecture

### Project references
- `Application → Domain`: handlers use the entities.
- `Infrastructure → Application`: Infrastructure **implements** the interfaces Application defines.
- `API → Application`: controllers send Commands and Queries, and return Application's DTOs.
- `API → Infrastructure`: only so `Program.cs` can call `AddInfrastructure(...)`. Controllers never use Infrastructure classes directly.
- `Domain → nothing`: the core never breaks when a framework changes. Domain has **zero** NuGet packages.

### `IAppDbContext` instead of Repositories
- **Options:** a repository per entity / an interface over the DbContext.
- **Chosen:** `IAppDbContext` in Application, implemented by `AppDbContext` in Infrastructure.
- **Why:** EF Core already works as a repository and unit of work. Repositories would be an extra layer with no benefit at this size.
- **Trade-off:** Application references `Microsoft.EntityFrameworkCore` (core only) for `DbSet`, `ToListAsync`, `AnyAsync`. The SQL Server provider stays in Infrastructure only.

### `BankClient` lives in Infrastructure
- **Options:** Application / API / a separate Integrations project / Infrastructure.
- **Chosen:** Infrastructure, behind `IBankClient` (defined in Application).
- **Why:** Infrastructure is the code that talks to the outside world (database, HTTP). In Application, business logic would depend on `HttpClient`, URLs and the bank's JSON format. In API, handlers could not even see it (no reference). A separate project is useful with many integrations, overkill for one.
- **Benefit:** the bank can be replaced, or a fake `IBankClient` used in tests, without touching the handler. This is **Dependency Inversion**.

### Each layer registers its own services
- `AddApplication()` and `AddInfrastructure()` instead of everything in `Program.cs`.
- **Why:** each layer knows its own details. `Program.cs` stays short and does not change when a layer adds something.

### Feature folders
- `Transfers/Commands/CreateTransfer/...` instead of `Commands/`, `Validators/`, `Handlers/`.
- **Why:** everything about one use case is in one place.

### Naming rule
- **Namespace = project name + folder path. File name = type name.** For example `Application/Transfers/Commands/CreateTransfer/` → `Application.Transfers.Commands.CreateTransfer`.
- **Options for the root:** rename everything to `MoneyTransfer.*` / keep the short project names (`Application.`, `Infrastructure.`, `API.`).
- **Chosen:** the short root. The problem was inconsistency, not the prefix. Renaming the projects would be a big, risky change for no real gain.

### One constructor style
- Classic constructors everywhere, not a mix of classic and primary constructors.
- **Why:** mixing styles makes similar classes look different for no reason. Most classes already used classic constructors.

---

## 3. Domain

### Status as an enum with explicit numbers
- **Why an enum:** only 3 valid values, no typos possible.
- **Why explicit numbers?** A safety habit: if the storage is ever switched to numbers, adding a new status won't shift existing values. Today `Status` is stored as text, so the numbers don't affect the database.
- **The real rule for text storage:** never **rename** a status once data is saved (e.g. `Success` → `Succeeded`), because existing rows would no longer match.

### Two IDs: `Id` and `TransactionId`
- `Id` (int): database primary key, small and fast, used in `GET /api/transfers/{id}`.
- `TransactionId` (Guid): created by us **before** saving and sent to the bank, so both sides refer to the same operation.

### `private set` + `MarkSuccess` / `MarkFailed`
- **Options:** public setters / state changes only through methods.
- **Why methods:** no code can put a transfer in a wrong state (e.g. `Success` without a bank reference). The constructor always starts in `Pending`.
- `EnsurePending()`: a finished transfer can never change again.
- `MarkSuccess` rejects a null or blank `bankReference` (`ArgumentException.ThrowIfNullOrWhiteSpace`). It is the last line of defense; `BankClient` already handles this case properly (see section 7), so the guard only catches programming mistakes.

### No guard clauses in the `Transfer` constructor (for now)
- **Options:** validate in the constructor too / rely on the validator.
- **Chosen:** rely on the validator.
- **Why:** there is only one way to create a transfer today (`CreateTransferCommand`), and FluentValidation checks every rule before the entity is created. Constructor guards would be added as soon as a second entry point appears (an import job, another command).

### `DateTime.UtcNow` instead of `DateTime.Now`
- **Why:** servers can run in different time zones. UTC is the same everywhere.

### Generic `ExternalServiceLog`
- **Why:** the table has a `ServiceName` column, so it can log any external service, not only the bank. A log is written once and never changed, so it has no methods.

### Nullable rule
- **Options:** nothing nullable (use `""` or `0`) / everything nullable / nullable only where "no value" is a real state.
- **Chosen:** the third one.
  - `BankReference`, `ResponseCode`, `ResponseMessage`: empty while `Pending`.
  - `Response`, `HttpStatusCode`: empty on Timeout / Connection Error (the bank never answered). `0` would be a lie.
  - `DurationMs`: never null, we always measure.
- EF Core uses `?` to decide `NULL` / `NOT NULL` columns, so this rule is also enforced by SQL Server.
- Nullable annotations are a compiler promise, not a runtime guarantee at the edges (JSON from the bank), so `BankClient` still uses `?.` and `??`.

---

## 4. Database

### `decimal(18,3)` for `Amount`
- **Why:** LYD has 3 decimal places (`150.750`). `double` has rounding errors and is never used for money.

### `HasMaxLength(50)` on `Reference`
- **Why:** required for the Unique Index. SQL Server cannot index `nvarchar(max)`.

### `Status` stored as text
- **Why:** `"Failed"` is readable in SQL. `2` means nothing.

### Fluent configuration classes instead of attributes
- **Why:** attributes like `[Index]` and `[Precision]` come from EF Core and would force Domain to install it.

### `IAppDbContext` resolves to the same `AppDbContext`
- `AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>())`.
- **Why not `AddScoped<IAppDbContext, AppDbContext>()`:** it could create a second, separate context in the same request.

### Migrations in Infrastructure, API as startup project
- **Why:** migrations sit next to the DbContext. The API has the connection string and the `.Design` package. Migrations are code in Git, so anyone can rebuild the same database.

### Connection string only in `appsettings.json`
- **Options:** in both `appsettings.json` and `appsettings.Development.json` / only in the Development file / only in `appsettings.json` / User Secrets.
- **Chosen:** only in `appsettings.json`, with `TrustServerCertificate=True` for local development.
- **Why:** the Development file overrides the base file, so a copy there would silently ignore a reviewer's edit in `appsettings.json`. One place, matching the README. User Secrets are for passwords; this string has none (`Trusted_Connection`).
- `TrustServerCertificate=True` is needed because the SQL client encrypts by default and a local SQL Server uses a self-signed certificate.

### All `DateTime` values are read back as UTC
- **Problem:** `datetime2` stores no time zone, so EF reads dates back as `Unspecified`. The same `createdAt` had a `Z` in the POST response but not in GET.
- **Options:** switch to `DateTimeOffset` / fix only the DTO mapping / one global value converter.
- **Chosen:** `UtcDateTimeConverter`, applied to every `DateTime` in `ConfigureConventions`. Reading marks the value as UTC (`SpecifyKind`); saving leaves it unchanged.
- **Why:** every date is saved with `DateTime.UtcNow`, so "it is UTC" is true. One small class covers all dates with no schema change. `DateTimeOffset` would be more complete but changes entities, DTOs and columns. Fixing only the mapping would leave other readers wrong.
- **Why not convert to UTC when saving:** `ToUniversalTime()` treats an `Unspecified` date as local time and shifts it.

---

## 5. Create Transfer flow

### Save `Pending` before calling the bank
- **Why:** required by the task, and if the server crashes mid-way, a record of the transfer still exists.

### How many `SaveChanges`?
- **Options:** one (at the end) / two / three (Pending, log, status).
- **Chosen:** two. Save `Pending`, call the bank, then save the log **and** the status update together.
- **Why:** one save breaks the "Pending first" rule. Three saves can leave a log saying "success" while the transfer is still `Pending`. Each `SaveChanges` runs in its own automatic transaction, so the log and status are saved together or not at all.

### No database transaction around the bank call
- **Why:** it would hold database locks during a slow HTTP call (up to 5s), and a rollback would erase the `Pending` record we want to keep.

### Updating the row: change tracking
- EF tracks the entity after the first save. `MarkSuccess` changes properties, EF detects it and generates an `UPDATE` with only the changed columns. No `Update()` call needed.
- **Why not `ExecuteUpdateAsync`:** it skips the Domain methods and their rules.

### Duplicate `Reference`
- **Options:** only `AnyAsync` / only the Unique Index / both.
- **Chosen:** both. `AnyAsync` gives a clear, fast answer. The index catches two requests arriving at the same moment.
- **Race case:** on `DbUpdateException`, check `AnyAsync` again. Duplicate → `ConflictException`, otherwise rethrow.
- **Why not check SQL error numbers (2601 / 2627):** that would put SQL Server details inside Application.

### Duplicate = exception, bank failure = result
- Duplicate: the request is **rejected**, nothing is created (like a validation error) → exception → `409`.
- Bank failure: the transfer **exists** and is saved as `Failed`, a valid outcome → result object.

### If the final save fails
- **Problem:** the bank has already answered (maybe "approved"), but saving the log and status fails. Without care, the bank's answer would be lost.
- **Options:** catch and still return 201 / retry the save / log everything at `Critical` and rethrow.
- **Chosen:** log the full bank result (`IsSuccess`, code, `BankReference`, HTTP status, response body) at `Critical`, then rethrow so the client gets an honest 500.
- **Why:** returning 201 would lie (the database still says `Pending`). A retry helps only for short database hiccups, not for bad data. `Critical` means a human must act: money may have moved.
- The request body is **not** logged here, because it contains the account number.

### `CancellationToken.None` after contacting the bank
- **Why:** once we talk to the bank, we finish the job even if the client disconnects. Otherwise the money could move while our record stays `Pending`.

### `201 Created` for every processed transfer
- **Options:** 200 for Success and 4xx/5xx for Failed / 201 for both.
- **Chosen:** 201 for both, the body contains `status`.
- **Why:** the request was valid and a transfer record was created. Whether the bank accepted it is a business result, not a broken request. Real errors still return 400 / 404 / 409.

---

## 6. Validation

### Pipeline behavior instead of calling validators manually
- **Options:** inside each handler / inside the controller / `FluentValidation.AspNetCore` / a MediatR pipeline behavior.
- **Chosen:** `ValidationBehavior`.
- **Why:** written once, runs before every handler automatically. Handlers stay clean, controllers stay logic-free. `FluentValidation.AspNetCore` auto-validation is deprecated and does not cover MediatR requests.
- It receives `IEnumerable<IValidator<TRequest>>`: a request can have zero validators (queries) or several. All errors are collected and returned at once.

### What the validator checks (and what it does not)
- Max lengths match the database columns: clear message instead of an SQL error.
- `PrecisionScale(18, 3, true)`: `150.7505` is rejected instead of silently rounded by SQL Server.
- **`Currency` must match `^[A-Z]{3}$`** (like `LYD`). Rejected options: a fixed list of currencies (the task doesn't say which ones the bank supports) and auto-uppercasing `"lyd"` (silently changing client input is risky with money).
- **`AccountNumber` must match `^[0-9]+$`**, not `\d`: in .NET, `\d` also matches Arabic-Indic digits (`٠١٢`), which a bank system would not expect. No exact length is enforced because the task does not define one.
- **`Cascade(CascadeMode.Stop)`** on these rules: an empty value gets one clear message instead of two.
- **Duplicate `Reference` is not in the validator:** the validator checks the **shape** of the data. Checking the database is a business rule and belongs in the handler.

### Missing fields
- Non-nullable `string` properties are treated as required by `[ApiController]`, so a missing field gets an automatic 400.
- `Amount` stays `decimal` (not `decimal?`): a missing amount becomes `0`, which `GreaterThan(0)` already rejects.

---

## 7. Bank integration

### Typed client with `IHttpClientFactory`
- `AddHttpClient<IBankClient, BankClient>()`.
- **Why not `new HttpClient()`:** a new client per request exhausts sockets under load, one kept forever misses DNS changes. The factory handles both.
- **Why a typed client:** URL and timeout are configured in one place, `BankClient` receives a ready `HttpClient`.

### Configuration in `appsettings.json`
- `BankOptions` (`BaseUrl`, `TimeoutSeconds`) bound from the `Bank` section.
- **Why:** required by the task. It can change per environment without recompiling.
- `BaseUrl` ends with `/` and the path has no leading `/`, otherwise parts of the URL can be dropped when combining.

### `BankClient` returns a result, it does not throw
- **Why:** a bank refusing, crashing or being slow is an **expected** outcome. In every case the handler must save a log and mark the transfer. A result object keeps that to one `if`.
- `BankClient` does not save the log itself. Its only job is HTTP. The handler saves log and status together.
- **A final `catch (Exception)`** keeps this promise for anything unexpected. It is placed last (after the specific catches), logs at `Error` with the full exception, and returns `Failed` with `UNEXPECTED_ERROR`. Catching everything is usually a bad sign, but here it is at the boundary with an external system, where an outcome must always be recorded, and nothing is hidden.
- **Options considered:** let it bubble up (the transfer stays `Pending` with no log) / catch it in the handler (the "never throws" rule would be split across two classes) / catch it in `BankClient`.

### Detecting each case

| Case | Detected by | ResponseCode |
|---|---|---|
| Success | HTTP 2xx **and** code `00` **and** a `BankReference` | bank code (`00`) |
| Business Error | HTTP 2xx but code is not `00` | bank code (e.g. `51`) |
| Approved without reference | HTTP 2xx + `00` but no `BankReference` | `INVALID_RESPONSE` |
| 400 / 500 | Status code | bank code |
| Timeout | `TaskCanceledException` when our own token was **not** cancelled | `TIMEOUT` |
| Connection Error | `HttpRequestException` | `CONNECTION_ERROR` |
| Anything else | Final `catch (Exception)` | `UNEXPECTED_ERROR` |

- `TIMEOUT`, `CONNECTION_ERROR`, `INVALID_RESPONSE` and `UNEXPECTED_ERROR` are our own codes, used when the bank gave no usable answer. They never clash with the bank's numeric codes, and all fit the `nvarchar(20)` column.

### No `Success` without a `BankReference`
- **Why:** the bank reference is the proof of the transfer. A "success" we cannot trace is not a success.
- **Defense in depth:** `BankClient` turns "approved without reference" into `Failed` / `INVALID_RESPONSE` and logs it at `Error`, and `MarkSuccess` refuses a blank reference as a last guard.
- **Trade-off:** the bank said "approved", so the money may have moved. `Failed` + an `Error` log is the safer of the two allowed statuses; a real system would use `Unknown` (same as timeout).

### Other details
- **Request serialized manually** (not `PostAsJsonAsync`): we need the exact text for the log table.
- **`Stopwatch`** for `DurationMs`: made for measuring time, more precise than subtracting `DateTime`s.
- **Safe JSON parsing:** a crashed server may return HTML or an empty body. Our code must not crash because the bank did.
- The bank's response model is **private** inside `BankClient`. If the bank's format changes, only this file changes.
- **No automatic retries:** not required, and retrying a money transfer could send the money twice.

---

## 8. Queries

### Mapping to `TransferDto`
- **Options:** load entities then map in memory / a `Select` written in each query / one shared `Expression`.
- **Chosen:** one shared `Expression` (`ToDtoExpression`). EF turns it into SQL that selects only the needed columns. `ToDto()` uses the same expression compiled, so there is one source of truth.
- **Why not AutoMapper / Mapster:** one entity, few fields. A library costs more than it gives, and AutoMapper is now commercial too.

### `AsNoTracking()`
- **Why:** queries only read. Tracking would waste memory and time.
- **Why not globally:** the Create handler needs tracking to update the status.

### Getting one row
- `FirstOrDefaultAsync` (`TOP 1`).
- **Why not `FindAsync`:** cannot be combined with `Select`. **Why not `SingleOrDefaultAsync`:** asks for `TOP 2` to check for duplicates, pointless on a primary key.

### Other choices
- **Order by `Id` descending:** newest first, and `Id` already has an index. `CreatedAt` does not.
- **No pagination:** not required by the task. Listed as a future improvement.
- **`IReadOnlyList<TransferDto>`:** the caller cannot add or remove items.
- **Not found → `NotFoundException`:** same style as `ConflictException`, all error mapping in one place. Returning `null` with an `if` in the controller would also be valid.
- **Route `{id:int}`:** a non-number id never reaches the code.

---

## 9. API layer

- **`ISender` instead of `IMediator`:** the controller only sends requests. Give code the smallest access it needs.
- **`ActionResult<T>`:** allows any status code, and Swagger still knows the body type.
- **The command is the request body:** same 4 fields. A separate request class would be a copy with no benefit.
- **`CreatedAtAction(nameof(GetById), ...)`:** the `Location` URL is built from the real route, no hand-written strings.
- **Swagger (Swashbuckle):** the task names Swagger. The template's built-in OpenAPI was removed so there is only one generator. Enabled only in Development.
- **`[ProducesResponseType]`:** Swagger shows every possible status and body, not only the success one.
- **No `UseHttpsRedirection`:** the API runs on HTTP locally, and a redirect would break the Postman and `.http` requests. In real deployments HTTPS is usually handled by the hosting proxy (IIS, Nginx, a load balancer).

---

## 10. Errors and logging

### Global exception handling
- **Options:** `try/catch` in every action / exception filter / custom middleware / `IExceptionHandler`.
- **Chosen:** `IExceptionHandler` (built into .NET 8+).
- **Why:** one place for all errors. `try/catch` repeats code and puts logic in controllers. Filters miss errors outside MVC. Custom middleware is the older manual way.

### ProblemDetails
- **Why:** a real standard (RFC 9457), and the same format `[ApiController]` already uses. Clients learn one error shape.

### Status codes are decided in the API layer
- **Why:** HTTP is an API concern. Application exceptions do not know status codes, so the same Application could be used by a non-HTTP app.

### Security
- A `500` returns a generic message. The real exception (which can include SQL or file paths) only goes to the logs.

### Logging
- **Options:** `Console.WriteLine` / `ILogger` / Serilog.
- **Chosen:** `ILogger`. `Console.WriteLine` has no levels. Serilog is an extra package the task does not ask for.
- **Message templates** (`"Transfer {Reference}"`) instead of string interpolation: values stay as searchable fields.
- **Levels:** `Information` for success, `Warning` for expected failures (bank refused, timeout, 400/404/409), `Error` for unexpected problems (connection error, unexpected bank error, approved without reference, 500), `Critical` when the bank's answer could not be saved.
- The full account number is **not** written to application logs. The `ExternalServiceLogs` table keeps the request body because the task requires it.

---

## 11. Testing setup

### How the Mock Bank chooses a scenario
- **Options:** by amount / reference prefix / account number / HTTP header / random / config switch.
- **Chosen:** by amount.
- **Why:** it uses a field our API already sends unchanged. Headers are not forwarded to the bank. Random results cannot be repeated. A config switch needs a restart for every case.
- The magic amounts exist **only** in the Mock Bank. The main API treats every amount > 0 the same.

### Mock Bank details
- Same JSON shape for every answer (including 400 and 500), so `BankClient` parses one format.
- Banking-style codes: `00` approved, `30` format error, `51` insufficient funds, `96` system error.
- The timeout scenario waits 10s, our client timeout is 5s.
- Fixed port (`5100`) so the URL in `appsettings.json` always matches.
- A single `http` launch profile and no browser launch: the mock only speaks HTTP, so there is no wrong profile to pick, and its root URL has no page to show.

### Postman collection
- References use `MW-{{$timestamp}}`, so the collection can be run many times without duplicate errors. Only the "Duplicate Reference" request uses a fixed value.
- Each request describes its expected result.
- **One collection only** (the JSON file in the repo root). Postman's own workspace folders are not tracked, so there is a single source of truth.

---

## 12. Known trade-offs

- **Timeout is saved as `Failed`**, as the task requires. In reality the result is unknown (the bank may have processed it). A production system would use an `Unknown` status and check later.
- **No pagination** on `GET /api/transfers`.
- **The stored request log contains the full account number**, because the task asks to store the request. Masking it would be a good improvement.
- **No automated tests yet.** The architecture makes them easy (e.g. a fake `IBankClient`).
- **"Approved without a bank reference" is saved as `Failed`**, with an `Error` log for a human to check. Same reasoning as timeout: an `Unknown` status would be more accurate.
- **Bank values are not truncated** to the column sizes. A very long message from the bank makes the final save fail; the transfer stays `Pending` and the full bank result is logged at `Critical`. Truncating would prevent it.
- **References are compared case-insensitively** by SQL Server, so `mw-001` and `MW-001` count as the same reference.
