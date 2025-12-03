# Architecture

System design and architectural decisions for DotNetSample.

## Overview

DotNetSample follows **Clean Architecture** principles (also known as Onion Architecture or Hexagonal Architecture) with clear separation of concerns across multiple projects.

**Core Principle:** Dependencies point inward. Outer layers depend on inner layers, never the reverse.

---

## Project Structure

### Dependency Flow

```
┌─────────────────────────────────────┐
│          Api (HTTP Layer)           │
│  ┌────────────────────────────┐     │
│  │   Worker (Background)      │     │
│  └─────────────┬──────────────┘     │
└────────────────┼────────────────────┘
                 │
                 ▼
┌────────────────────────────────────┐
│   Infrastructure (Data & Logic)    │
└────────────────┬───────────────────┘
                 │
                 ▼
┌────────────────────────────────────┐
│    Core (Domain Models & Ports)    │
└────────────────────────────────────┘
```

**External Dependencies:**

```
Client (HTTP) ──→ Api
Tests ──→ Infrastructure ──→ Core
```

### Dependency Rules

1. **Core** has no external dependencies (pure domain)
2. **Infrastructure** depends only on Core
3. **Api** depends on Infrastructure and Core
4. **Worker** depends on Infrastructure and Core
5. **Client** is independent (HTTP-only dependency on Api)
6. **Tests** can depend on any project for testing purposes

---

## Core Project

**Location:** `Core/`

**Responsibility:** Domain models and abstractions

### Key Files

- `DomainModels.cs` — Entities (Customer, Product, Order, OrderItem, OrderStatus enum)
- `Interfaces.cs` — Repository and service contracts (IRepository<T>, IOrderProcessor)

### Characteristics

- **No external dependencies** — Only depends on .NET base class libraries
- **Pure C# / .NET 8** — No references to EF Core, ASP.NET, or third-party libraries
- **Contains business entities** — Domain models that represent core business concepts
- **Defines contracts** — Interfaces that outer layers must implement

### Example Entity

```csharp
public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    public OrderStatus Status { get; set; }
}
```

### Example Interface

```csharp
public interface IRepository<T> where T : class
{
    Task<T> GetAsync(Guid id);
    Task<IEnumerable<T>> ListAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}
```

**Why this matters:** Core can be tested in isolation and reused across different infrastructure implementations.

---

## Infrastructure Project

**Location:** `Infrastructure/`

**Responsibility:** Data access, persistence, and business logic implementation

### Key Files

- `AppDbContext.cs` — Entity Framework Core DbContext configuration
- `Repositories.cs` — Generic repository implementation (EfRepository<T>)
- `OrderProcessor.cs` — Business logic for processing orders
- `DbSeeder.cs` — Seed data for development
- `Migrations/` — EF Core schema migrations

### Technologies

- **Entity Framework Core 8** — ORM for database access
- **SQLite** — Development database (configured via connection string)

### Design Patterns

**Repository Pattern:**
```
IRepository<T> (Core) ──implemented by──> EfRepository<T> (Infrastructure)
```

Generic repository provides consistent CRUD operations:

```csharp
public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _db;
    private readonly DbSet<T> _set;

    public async Task AddAsync(T entity)
    {
        await _set.AddAsync(entity);
        await _db.SaveChangesAsync();
    }
    // ... other CRUD methods
}
```

**Unit of Work:**
- DbContext serves as the Unit of Work
- Each repository operation commits changes immediately
- OrderProcessor uses DbContext directly for multi-entity transactions

### Order Processing Logic

File: `Infrastructure/OrderProcessor.cs`

```csharp
public async Task ProcessPendingOrdersAsync()
{
    // 1. Load pending orders with items
    var pending = await _db.Orders
        .Include(o => o.Items)
        .Where(o => o.Status == OrderStatus.Pending)
        .ToListAsync();

    foreach (var order in pending)
    {
        order.Status = OrderStatus.Processing;

        // 2. Process each item
        foreach (var item in order.Items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);

            // 3. Check stock and deduct if available
            if (product.Stock >= item.Quantity)
            {
                product.Stock -= item.Quantity;
                item.UnitPrice = product.Price;
            }
            else
            {
                order.Status = OrderStatus.Cancelled;
                break;
            }
        }

        // 4. Complete or cancel
        if (order.Status != OrderStatus.Cancelled)
        {
            order.Status = OrderStatus.Completed;
        }

        await _db.SaveChangesAsync();
    }
}
```

---

## Api Project

**Location:** `Api/`

**Responsibility:** HTTP layer, dependency injection, middleware configuration

### Key Files

- `Program.cs` — Application startup, DI registration, middleware pipeline
- `Controllers/CustomersController.cs` — Customer CRUD endpoints
- `Controllers/ProductsController.cs` — Product CRUD endpoints
- `Controllers/OrdersController.cs` — Order endpoints (CRUD + specialized queries)
- `Properties/launchSettings.json` — Launch profiles (http: localhost:5181)

### Characteristics

**Controllers are thin** — Delegate to repositories/services:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IRepository<Order> _orderRepo;

    public OrdersController(IRepository<Order> orderRepo)
    {
        _orderRepo = orderRepo;
    }

    [HttpGet]
    public async Task<IEnumerable<Order>> Get() => await _orderRepo.ListAsync();
}
```

**Dependency Injection** (Program.cs):

```csharp
// DbContext - SQLite file
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));

// Repositories and services
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IOrderProcessor, OrderProcessor>();

// Background worker
builder.Services.AddHostedService<OrderWorker>();
```

**Middleware Pipeline:**

1. **Routing** — Map requests to controllers
2. **Authorization** — (Placeholder for future auth)
3. **Controller mapping** — Execute controller actions

**Swagger/OpenAPI:**
- Enabled in Development mode
- Accessible at `http://localhost:5181/swagger`

**Database Initialization:**
- Migrations applied automatically at startup
- Seed data populated if database is empty

---

## Worker Project

**Location:** `Worker/`

**Responsibility:** Background order processing

### Key Files

- `OrderWorker.cs` — IHostedService that processes pending orders every 10 seconds

### Behavior

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            // Create a scope to resolve scoped services
            using var scope = _services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IOrderProcessor>();
            await processor.ProcessPendingOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing orders");
        }

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
    }
}
```

**Key Design Decisions:**

- Runs every **10 seconds** (configurable)
- Creates a **new scope** per iteration to avoid stale DbContext issues
- **Logs errors** without crashing the service
- Processes all `Pending` orders in each iteration

### Integration

**Registered as Hosted Service in Api/Program.cs:**

```csharp
builder.Services.AddHostedService<OrderWorker>();
```

This means the worker runs **inside the Api process**, not as a separate application.

**Benefits:**
- Simplified local development (one process to run)
- Shared DI container and configuration
- Easy to debug together

**Production Considerations:**
- Can run separately as standalone service (`dotnet run --project Worker`)
- For scale, consider moving to Azure Functions, AWS Lambda, or dedicated worker pods

---

## Client Project

**Location:** `Client/`

**Responsibility:** Console-based API smoke testing

### Key Files

- `Program.cs` — HttpClient-based API calls

### Usage

Executes quick smoke tests:
1. GET /api/products
2. GET /api/customers
3. POST /api/orders (creates an order with first product and customer)

**Example:**

```csharp
var client = new HttpClient { BaseAddress = new Uri("http://localhost:5181") };

var products = await client.GetFromJsonAsync<List<Product>>("api/products");
var customers = await client.GetFromJsonAsync<List<Customer>>("api/customers");

var order = new Order
{
    CustomerId = customers[0].Id,
    Items = new List<OrderItem>
    {
        new OrderItem { ProductId = products[0].Id, Quantity = 2 }
    }
};

var response = await client.PostAsJsonAsync("api/orders", order);
```

---

## Tests Project

**Location:** `Tests/`

**Responsibility:** Unit and integration testing

### Key Files

- `DomainTests.cs` — Domain model tests
- `ClientProgramTests.cs` — Client HTTP tests using FakeHttpMessageHandler

### Testing Strategy

**In-memory/fake handlers for client tests:**
- No live API server required
- FakeHttpMessageHandler simulates API responses
- Fast, deterministic tests

**Example:**

```csharp
[Fact]
public async Task CreateOrder_ReturnsCreated()
{
    var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.Created, "{}");
    var client = new HttpClient(fakeHandler);

    var response = await client.PostAsJsonAsync("api/orders", new Order { ... });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

**Integration tests** can use in-memory EF Core databases for full-stack testing.

---

## Data Flow Example: Creating an Order

End-to-end flow for order creation and processing:

### 1. Client Creates Order

**HTTP Request:**
```
POST /api/orders
{
  "customerId": "guid",
  "items": [{ "productId": "guid", "quantity": 2 }]
}
```

**OrdersController:**
```csharp
[HttpPost]
public async Task<ActionResult> Post(Order order)
{
    order.Id = Guid.NewGuid();
    order.CreatedAt = DateTime.UtcNow;
    order.Status = OrderStatus.Pending;  // Initial status

    foreach (var item in order.Items)
    {
        item.Id = Guid.NewGuid();
        item.OrderId = order.Id;
    }

    await _orderRepo.AddAsync(order);
    return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
}
```

**Database State:** Order saved with Status = Pending

---

### 2. Worker Picks Up Order

**OrderWorker (every 10 seconds):**
```csharp
await processor.ProcessPendingOrdersAsync();
```

---

### 3. OrderProcessor Processes Order

**OrderProcessor:**

1. Query for Pending orders
2. Set Status = Processing
3. For each item:
   - Load product
   - Check stock availability
   - Deduct stock if available
   - Set UnitPrice from Product.Price
   - If insufficient stock → Status = Cancelled, break
4. If all items processed → Status = Completed
5. Save changes to database

---

### 4. Final State

**Completed Order:**
- Status = Completed
- Product stock deducted
- OrderItem.UnitPrice set
- Order.CreatedAt preserved

**Cancelled Order:**
- Status = Cancelled
- No stock deducted
- Logged warning

---

## Database Schema

### Tables

- **Customers** (Id, Name, Email)
- **Products** (Id, Name, Price, Stock)
- **Orders** (Id, CustomerId, CreatedAt, Status)
- **OrderItems** (Id, OrderId, ProductId, Quantity, UnitPrice)

### Relationships

```
Customers 1───< Orders 1───< OrderItems >───* Products
```

- One Customer has many Orders
- One Order has many OrderItems
- One Product appears in many OrderItems

### Configuration

File: `Infrastructure/AppDbContext.cs`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Customer>().HasKey(c => c.Id);
    modelBuilder.Entity<Product>().HasKey(p => p.Id);
    modelBuilder.Entity<Order>().HasKey(o => o.Id);
    modelBuilder.Entity<OrderItem>().HasKey(oi => oi.Id);

    modelBuilder.Entity<Order>()
        .HasMany(o => o.Items)
        .WithOne()
        .HasForeignKey(oi => oi.OrderId);
}
```

---

## Technology Stack

- **.NET 8** — Target framework
- **C#** — Programming language
- **ASP.NET Core** — Web API framework
- **Entity Framework Core 8** — ORM
- **SQLite** — Development database (file-based: `Api/app.db`)
- **xUnit** — Testing framework
- **Swagger/OpenAPI** — API documentation

---

## Design Decisions

### Why Clean Architecture?

**Testability:**
- Core and Infrastructure can be tested independently
- Mock repositories for controller tests
- In-memory database for integration tests

**Maintainability:**
- Clear boundaries reduce coupling
- Changes to infrastructure don't affect core domain
- Easy to understand dependencies

**Flexibility:**
- Can swap SQLite → PostgreSQL without touching Core
- Can add alternative repositories (Dapper, ADO.NET)
- Can change HTTP layer (minimal APIs, gRPC) without touching business logic

---

### Why SQLite?

**Development Benefits:**
- Zero configuration required
- File-based (easy to reset: `rm Api/app.db`)
- Sufficient for demonstration and testing
- No separate database server needed

**Limitations:**
- Single writer (no concurrent writes)
- Not recommended for production at scale
- Limited to file-based storage

**Production Path:** Swap to PostgreSQL, SQL Server, or MySQL by changing one line in Program.cs.

---

### Why Generic Repository?

**Advantages:**
- Reduces boilerplate for CRUD operations
- Consistent API across all entities
- Easy to mock for testing
- Single place to add cross-cutting concerns (logging, caching)

**Pattern:**

```csharp
IRepository<Customer> _customers;
IRepository<Product> _products;
IRepository<Order> _orders;

// All share same contract
await _customers.GetAsync(id);
await _products.GetAsync(id);
await _orders.GetAsync(id);
```

---

### Why Worker in Api Project?

**Development Simplicity:**
- One process to run (`dotnet run --project Api`)
- Single debugger session
- Shared configuration and DI container

**Production Flexibility:**
- Can run separately as standalone service
- Can scale independently
- Can deploy to different infrastructure (e.g., serverless)

**Separation Option:**

Create `Worker/Program.cs` that references Infrastructure:

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<AppDbContext>(...);
        services.AddScoped<IOrderProcessor, OrderProcessor>();
        services.AddHostedService<OrderWorker>();
    });

await builder.Build().RunAsync();
```

Then deploy Api and Worker separately.

---

## Related Documentation

- See [API_REFERENCE.md](API_REFERENCE.md) for endpoint details
- See [DATABASE.md](DATABASE.md) for schema and migrations
- See [WORKSPACE.md](WORKSPACE.md) for local development setup
