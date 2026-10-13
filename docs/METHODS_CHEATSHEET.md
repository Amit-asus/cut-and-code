# Methods Cheat Sheet

Every important method used in this project, in simple English. Node/Express equivalents are given where they help.

---

## 1. Database reading (EF Core + LINQ)

EF Core turns these C# calls into SQL. Nothing hits the database until you call a method ending in `Async` like `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync`.

| Method | Simple meaning | Node / Mongoose equivalent | Where used |
|---|---|---|---|
| `Where(x => ...)` | Filter rows (SQL `WHERE`) | `find({ ... })` | Bookings list, availability |
| `FirstOrDefaultAsync(x => ...)` | First match, or `null` if none. If 2+ match, returns the first, no error | `findOne()` | Login (find user by email) |
| `FindAsync(id)` | Find by primary key (Id), or `null` | `findById()` | Salons, bookings |
| `AnyAsync(x => ...)` | "Does at least one row match?" → `true`/`false` | `exists()` | Duplicate email check, double-booking check |
| `CountAsync()` | How many rows match | `countDocuments()` | Paging (total count) |
| `ToListAsync()` | Run the query and give me all rows as a list | `await find()` | Everywhere |
| `Select(x => new {...})` | Pick only certain columns | `.select('a b')` | Availability (only start/end times) |
| `Include(x => x.Services)` | Also load related data (SQL `JOIN`) | `.populate()` | Salon with its services |
| `OrderByDescending(...)` / `ThenByDescending(...)` | Sort newest first; `ThenBy` = second sort key | `.sort({ a: -1, b: -1 })` | My bookings |
| `Skip(n)` / `Take(n)` | Skip n rows / take n rows → paging | `.skip()` / `.limit()` | My bookings (pages) |
| `Any(...)` (no Async) | Same as `AnyAsync` but on a list already in memory | `array.some()` | Marking slots as booked |
| `Contains(...)` | "Is this value in the list/text?" | `includes()` | |

**Common question — `First` vs `FirstOrDefault` vs `Single` vs `SingleOrDefault`:**
- `First` — first match; **crashes** if none.
- `FirstOrDefault` — first match; `null` if none.
- `Single` — exactly one; **crashes** if zero or more than one.
- `SingleOrDefault` — one or `null`; **crashes** if more than one.

---

## 2. Database writing (EF Core)

| Method | Simple meaning | Node equivalent |
|---|---|---|
| `db.Things.Add(x)` | Mark a new row to be inserted (not saved yet) | `new Model(x)` |
| `db.Things.Remove(x)` | Mark a row to be deleted (not saved yet) | |
| `SaveChangesAsync()` | **Actually** write all pending changes to the DB in one go | `await doc.save()` |
| `Migrate()` | On startup, create/update the tables to match the code | running migrations |

Updating is just: load the object, change a property, call `SaveChangesAsync()`. EF tracks what changed (called **change tracking**).

**Migration file methods** (auto-generated, you don't write them): `CreateTable`, `DropTable`, `AddColumn`, `DropColumn`, `CreateIndex`, `HasKey`, `HasIndex(...).IsUnique()`, `HasOne(...).WithMany(...).HasForeignKey(...).OnDelete(Cascade)` — they describe table structure: primary key, unique email, "a Service belongs to a Salon, deleting a Salon deletes its Services".

---

## 3. Endpoints (Minimal APIs)

| Method | Simple meaning | Express equivalent |
|---|---|---|
| `app.MapGet("/path", handler)` | Handle GET requests | `app.get()` |
| `app.MapPost` / `MapPut` / `MapDelete` | Handle POST / PUT / DELETE | `app.post()` etc. |
| `.RequireAuthorization()` | Must be logged in (valid JWT) | auth middleware on the route |
| `.RequireAuthorization(p => p.RequireRole("Admin"))` | Must be logged in **and** have that role | role-check middleware |
| `.DisableAntiforgery()` | Turn off the anti-CSRF check for file uploads (we use JWT, not cookies) | |
| `MapAuthEndpoints()`, `MapSalonEndpoints()`, `MapBookingEndpoints()` | Our own methods that register a group of routes | `app.use(router)` |

**Responses** (`Results.*`) — like `res.status(...).json(...)`:

| Method | Status code |
|---|---|
| `Results.Ok(data)` | 200 |
| `Results.Created(url, data)` | 201 |
| `Results.NoContent()` | 204 (success, nothing to return) |
| `Results.BadRequest(msg)` | 400 (bad input) |
| `Results.Unauthorized()` | 401 (not logged in / wrong password) |
| `Results.Forbid()` | 403 (logged in, but not allowed) |
| `Results.NotFound()` | 404 |
| `Results.Conflict(msg)` | 409 (duplicate email, slot already booked) |
| `Results.Text(json, "application/json")` | 200 with raw text — used for Redis cache hits |

**401 vs 403:** 401 = "who are you?", 403 = "I know who you are, but you can't do this".

---

## 4. Auth (passwords + JWT)

| Method | Simple meaning | Node equivalent |
|---|---|---|
| `HashPassword(user, password)` | Turn a password into a safe one-way hash before saving | `bcrypt.hash()` |
| `VerifyHashedPassword(...)` / our `Verify(...)` | Check a typed password against the saved hash | `bcrypt.compare()` |
| `GenerateToken(user)` (ours) | Build and sign a JWT with id, email, role | `jwt.sign()` |
| `WriteToken(token)` | Turn the token object into the `xxx.yyy.zzz` string | part of `jwt.sign()` |
| `AddAuthentication().AddJwtBearer(...)` | "Read `Authorization: Bearer ...` and check the token" | `express-jwt` / `jwt.verify()` middleware |
| `AddJwtAuth()` (ours) | Our helper that does the above in one line | |
| `UseAuthentication()` / `UseAuthorization()` | Turn on the auth middleware in the pipeline (order matters: authentication first) | `app.use(authMiddleware)` |
| `user.FindFirstValue("sub")` | Read a value (claim) from the logged-in user's token | `req.user.sub` |
| `Encoding.UTF8.GetBytes(key)` | Turn the signing-key text into bytes for signing | `Buffer.from(key)` |

---

## 5. Dependency Injection (DI) — "registering services"

In .NET you don't `require()` and create objects yourself; you **register** them once in `Program.cs`, and the framework hands them to any endpoint/class that asks for them in its parameters.

| Method | Simple meaning |
|---|---|
| `AddSingleton<T>()` | **One** shared instance for the whole app lifetime (e.g. `TokenService`, `PasswordHasherService`, `BookingEventPublisher`) |
| `AddScoped<T>()` | **One new instance per request** (e.g. `EmailSender`, DbContext) |
| `AddHttpClient<T>()` | Register a class that gets a ready-made `HttpClient` (like an axios instance) |
| `AddHostedService<T>()` | Run this class in the background for the app's whole life (our Service Bus consumer) |
| `CreateScope()` + `GetRequiredService<T>()` | Manually get a service — used in background workers and startup code, where there's no request |

**Common question:** *Why does the consumer use `CreateScope()`?* It's a singleton living forever, but the DbContext/email services are "per request". So for each message it creates a small scope, like a fake request.

---

## 6. Aspire AppHost (`apphost.cs`)

| Method | Simple meaning |
|---|---|
| `CreateBuilder()` / `Build()` / `Run()` | Start describing the app / finish / launch everything |
| `AddPostgres("postgres")` | Run a Postgres container |
| `AddDatabase("salondb")` | Create a database inside that Postgres |
| `AddRedis("cache")` | Run a Redis container |
| `AddAzureStorage(...).RunAsEmulator()` | Run Azurite (fake Azure Storage) locally |
| `AddBlobContainer("salon-images")` | A "bucket" for images |
| `AddAzureServiceBus(...)` / `AddServiceBusQueue("booking-created")` | Message broker + a queue |
| `AddProject(...)` | Run one of our .NET services |
| `WithReference(x)` | Give this service the connection info for `x` (injects connection strings/URLs automatically) |
| `WaitFor(x)` | Don't start this service until `x` is healthy |
| `WithEnvironment("KEY", value)` | Set an environment variable (like `.env`) |
| `WithLifetime(Persistent)` | Keep the container running between restarts (data survives) |
| `WithDataVolume(...)` | Save data to a Docker volume so it survives |
| `WithPgAdmin()` / `WithRedisCommander()` | Add admin UIs to browse Postgres / Redis |
| `WithBlobPort(10000)` | Fix the blob port so saved image URLs never break |

**Inside each service** (client side):
`AddNpgsqlDbContext<T>("salondb")`, `AddRedisClient("cache")`, `AddAzureBlobContainerClient("salon-images")`, `AddAzureServiceBusClient("servicebus")` — "connect me to the resource with this name". The name must match the one in `apphost.cs`.

`AddServiceDefaults()` / `MapDefaultEndpoints()` — shared setup for every service: health checks (`/health`, `/alive`), logging/tracing (OpenTelemetry), retries, service discovery.

---

## 7. Redis (StackExchange.Redis)

| Method | Simple meaning | Node (`ioredis`) |
|---|---|---|
| `redis.GetDatabase()` | Get a handle to run commands | the client |
| `StringGetAsync(key)` | Read a value | `redis.get()` |
| `StringSetAsync(key, value, TimeSpan.FromDays(1))` | Save a value that expires in 1 day | `redis.set(k, v, 'EX', 86400)` |
| `KeyDeleteAsync(key)` | Delete a key (used to clear the cache after a booking changes) | `redis.del()` |
| `cached.HasValue` | "Was anything found?" | `value !== null` |

---

## 8. Blob storage (images)

| Method | Simple meaning |
|---|---|
| `CreateIfNotExistsAsync()` | Create the container if it's not there |
| `SetAccessPolicyAsync(PublicAccessType.Blob)` | Let anyone view the images by URL (but not list them) |
| `GetBlobClient(name)` | Get a handle to one file in storage |
| `UploadAsync(stream, overwrite: true)` | Upload the file |
| `file.OpenReadStream()` | Read the uploaded file from the request (like `multer`'s `req.file`) |
| `blobClient.Uri` | The public URL of the uploaded image |

---

## 9. Service Bus (messaging between services)

| Method | Simple meaning |
|---|---|
| `CreateSender("booking-created")` | Get a "sender" for that queue |
| `SendMessageAsync(message)` | Drop a message in the queue (Booking.Api does this after a booking) |
| `CreateProcessor("booking-created")` | Create a listener for the queue |
| `StartProcessingAsync()` / `StopProcessingAsync()` | Start / stop listening |
| `args.Message.Body.ToObjectFromJson<T>()` | Turn the message body back into a C# object (`JSON.parse`) |
| `CompleteMessageAsync(message)` | "Done, remove it from the queue". If we crash before this, the message stays and is retried |
| `Task.Delay(Timeout.Infinite, token)` | "Wait forever until the app shuts down" — keeps the background worker alive |

---

## 10. HTTP calls between services

| Method | Simple meaning | Node equivalent |
|---|---|---|
| `httpClient.GetAsync(url)` | Send a GET request | `fetch(url)` / `axios.get` |
| `SendAsync(request)` | Send a custom request (headers, POST body) | `fetch(url, options)` |
| `ReadFromJsonAsync<T>()` | Read the JSON response into a C# object | `await res.json()` |
| `ReadAsStringAsync()` | Read the response as plain text | `await res.text()` |
| `AddServiceDiscovery()` | Lets `https://identity-api` resolve to the real address (no hardcoded ports) | |
| `AddStandardResilienceHandler()` | Automatic retries + timeouts + circuit breaker | `axios-retry` |

---

## 11. JSON

| Method | Simple meaning | JS equivalent |
|---|---|---|
| `JsonSerializer.Serialize(obj)` | Object → JSON text | `JSON.stringify` |
| `JsonSerializer.Deserialize<T>(text)` | JSON text → object | `JSON.parse` |
| `JsonSerializerDefaults.Web` | Use camelCase names (`startTime`, not `StartTime`) | |
| `JsonStringEnumConverter` | Send enums as text (`"Admin"`) instead of numbers (`2`) | |
| `[JsonIgnore]` | Don't include this property in JSON (stops Salon ↔ Service loop) | |

---

## 12. Dates and time zones

| Method | Simple meaning |
|---|---|
| `DateTime.UtcNow` | Current time in UTC (always store UTC) |
| `AddHours(n)` / `AddDays(n)` | Add time |
| `TimeSpan.FromDays(1)` / `FromSeconds(n)` | A length of time (used for TTL/timeouts) |
| `TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")` | Get the IST time zone |
| `TimeZoneInfo.ConvertTimeToUtc(...)` | IST → UTC (availability endpoint) |
| `TimeZoneInfo.ConvertTimeFromUtc(...)` | UTC → IST (email shows local time) |
| `DateTime.SpecifyKind(..., Unspecified)` | "This time has no zone yet" — needed before converting |
| `DateOnly.FromDateTime(...)` / `date.ToDateTime(...)` | Convert between date-only and date+time |

---

## 13. Small helpers

| Method | Simple meaning |
|---|---|
| `Guid.NewGuid()` | Create a new unique id (like `uuid()`) |
| `Guid.Parse(text)` | Text → Guid |
| `string.IsNullOrWhiteSpace(s)` | "Is it empty or just spaces?" |
| `Math.Max` / `Math.Clamp(x, 1, 50)` | Keep a number within limits (page size 1–50) |
| `Math.Ceiling` | Round up (total pages) |
| `Enumerable.Range(9, 12)` | Numbers 9, 10 … 20 (12 numbers, **not** 9 to 12) |
| `WebUtility.HtmlEncode(text)` | Make text safe to put in HTML (stops injected HTML/script) |

---

## 14. Logging (Serilog)

| Method | Simple meaning |
|---|---|
| `logger.LogInformation("Booking {Id} created", id)` | Normal info log. `{Id}` is a **named field**, not string concatenation — searchable in the dashboard |
| `LogWarning` / `LogError` | Something odd / something failed |
| `UseSerilog()` | Use Serilog as the logger |
| `WriteTo.Console()` / `WriteTo.File(...)` | Where logs go |

---

## 15. Middleware / pipeline (`Program.cs`)

| Method | Simple meaning | Express |
|---|---|---|
| `WebApplication.CreateBuilder(args)` | Start building the app | `const app = express()` |
| `builder.Build()` | Finish setup | |
| `app.Run()` | Start the server | `app.listen()` |
| `UseHttpsRedirection()` | Redirect http → https | |
| `AddCors` / `UseCors("WebClient")` | Allow the React app (other port) to call the API | `app.use(cors())` |
| `AddReverseProxy().LoadFromConfig(...)` / `MapReverseProxy()` | Gateway: forward requests to the right service (YARP) | `http-proxy-middleware` |
| `AddOpenApi()` / `MapOpenApi()` | API description (Swagger-like) | `swagger-jsdoc` |
| `app.Environment.IsDevelopment()` | "Am I running locally?" | `NODE_ENV === 'development'` |

---

## 16. React (frontend)

| Hook / method | Simple meaning |
|---|---|
| `useState` | A value that re-renders the page when it changes |
| `useEffect` | Run code after render (e.g. fetch data when the page opens or a value changes) |
| `useContext` / `useAuth` / `useTheme` / `useToast` / `useAvatar` | Read shared app-wide data (logged-in user, theme, notifications) |
| `useCallback` | Remember a function so it isn't recreated every render |
| `useRef` | Hold a reference to a DOM element (the `<dialog>`) |
| `useNavigate` / `navigate('/x')` | Go to another page in code |
| `useParams` | Read `:id` from the URL |
| `useLocation` | Read current URL info (used to return to the page you came from after login) |
| `fetch(url, options)` | Call the Gateway |
| `localStorage.getItem/setItem/removeItem` | Save the token/theme in the browser |
| `Promise.all([...])` | Run several requests at once and wait for all |
| `.map` / `.filter` / `.find` | Loop to render lists / keep matching items / get first match |
| `toLocaleString` / `toLocaleTimeString` / `toISOString` | Format dates for display / send dates to the API in UTC |
| `dialog.showModal()` | Open the native popup (confirm modal) |

---

## Most likely "tricky" questions — quick answers

1. **`async`/`await`?** Same as Node — don't block while waiting for DB/network.
2. **Why does nothing hit the DB until `ToListAsync`?** EF builds the query first (like a query builder), then runs it once.
3. **`Singleton` vs `Scoped`?** Singleton = one forever. Scoped = new per request. DbContext must be scoped (not thread-safe).
4. **`Add` then `SaveChangesAsync`?** `Add` only marks; `SaveChangesAsync` writes to the DB.
5. **`FindAsync` vs `FirstOrDefaultAsync`?** `FindAsync` only by Id (and checks memory first). `FirstOrDefaultAsync` by any condition.
6. **`Include`?** Loads related data (Salon + its Services). Without it, `Services` is empty.
7. **401 vs 403?** Not logged in vs not allowed.
8. **Why hash passwords?** If the DB leaks, attackers can't read passwords. Hashing is one-way.
9. **`WithReference` vs `WaitFor`?** `WithReference` gives connection info; `WaitFor` delays start until the dependency is ready.
10. **Why `CompleteMessageAsync` at the end?** So a failed message is retried instead of lost.
