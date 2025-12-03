# Development Workflows

Common development scenarios, debugging techniques, and how-to guides for DotNetSample.

## Adding a New API Endpoint

### Scenario: Add GET /api/orders/status/{status}

**1. Add method to OrdersController:**

File: `Api/Controllers/OrdersController.cs`

```csharp
/// <summary>
/// Gets orders by status.
/// </summary>
/// <param name="status">Order status to filter by.</param>
/// <returns>Orders with the specified status.</returns>
[HttpGet("status/{status}")]
public async Task<ActionResult<IEnumerable<Order>>> GetByStatus(OrderStatus status)
{
    var orders = await _orderRepo.ListAsync();
    var filteredOrders = orders.Where(o => o.Status == status).ToList();
    return Ok(filteredOrders);
}
```

**2. Test the endpoint:**

```bash
# Start API
dotnet run --project Api

# Test endpoint (0=Pending, 1=Processing, 2=Completed, 3=Cancelled)
curl http://localhost:5181/api/orders/status/0
curl http://localhost:5181/api/orders/status/2
```

**3. Update API documentation:**

File: `docs/API_REFERENCE.md`

Add section:

```markdown
### GET /api/orders/status/{status}

Gets orders by status.

**Path Parameters:**
- `status` (int, required) — OrderStatus enum value (0=Pending, 1=Processing, 2=Completed, 3=Cancelled)

**Response:** `200 OK`
```

**4. Write tests (optional):**

File: `Tests/OrdersControllerTests.cs` (create if needed)

```csharp
[Fact]
public async Task GetByStatus_ReturnsOnlyPendingOrders()
{
    // Arrange, Act, Assert
}
```

---

## Adding a New Domain Entity

### Scenario: Add Invoice Entity

**1. Add entity to Core:**

File: `Core/DomainModels.cs`

```csharp
public class Invoice
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; }
    public DateTime IssuedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsPaid { get; set; }
}
```

---

**2. Add DbSet to AppDbContext:**

File: `Infrastructure/AppDbContext.cs`

```csharp
public DbSet<Invoice> Invoices { get; set; }
```

---

**3. Configure relationships:**

File: `Infrastructure/AppDbContext.cs` (in `OnModelCreating`)

```csharp
modelBuilder.Entity<Invoice>().HasKey(i => i.Id);

modelBuilder.Entity<Invoice>()
    .HasOne(i => i.Order)
    .WithMany()
    .HasForeignKey(i => i.OrderId);
```

---

**4. Create migration:**

```bash
dotnet ef migrations add AddInvoice \
  --project Infrastructure \
  --startup-project Api \
  -o Migrations
```

**5. Review migration:**

Check `Infrastructure/Migrations/<timestamp>_AddInvoice.cs` to verify SQL.

---

**6. Apply migration:**

```bash
dotnet ef database update --project Infrastructure --startup-project Api
```

Or just run the API (auto-migrates):

```bash
dotnet run --project Api
```

---

**7. Create controller:**

File: `Api/Controllers/InvoicesController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using DotNetSample.Core;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DotNetSample.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IRepository<Invoice> _invoiceRepo;

        public InvoicesController(IRepository<Invoice> invoiceRepo)
        {
            _invoiceRepo = invoiceRepo;
        }

        [HttpGet]
        public async Task<IEnumerable<Invoice>> Get() => await _invoiceRepo.ListAsync();

        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> Get(Guid id)
        {
            var invoice = await _invoiceRepo.GetAsync(id);
            if (invoice == null) return NotFound();
            return invoice;
        }

        [HttpPost]
        public async Task<ActionResult> Post(Invoice invoice)
        {
            invoice.Id = Guid.NewGuid();
            invoice.IssuedAt = DateTime.UtcNow;
            await _invoiceRepo.AddAsync(invoice);
            return CreatedAtAction(nameof(Get), new { id = invoice.Id }, invoice);
        }
    }
}
```

---

**8. Update documentation:**

- `docs/DATABASE.md` — Add Invoice table schema
- `docs/API_REFERENCE.md` — Add /api/invoices endpoints

---

## Debugging the Application

### Debugging the API

#### Visual Studio Code

**1. Open project in VS Code:**

```bash
code .
```

**2. Install C# Dev Kit extension:**
- Press `Ctrl+Shift+X` (Extensions)
- Search for "C# Dev Kit"
- Install

**3. Set breakpoints:**
- Open `Api/Controllers/OrdersController.cs`
- Click left of line number to set breakpoint

**4. Press F5 to launch debugger:**
- Select ".NET Core Launch (web)" configuration
- API starts with debugger attached

**5. Make API request:**

```bash
curl http://localhost:5181/api/orders
```

Debugger will pause at breakpoint.

---

#### Visual Studio

**1. Open solution:**

```bash
start DotNetSample.sln
```

**2. Set `Api` as startup project:**
- Right-click `Api` project → **Set as Startup Project**

**3. Set breakpoints** in controller or service

**4. Press F5** to run with debugger

---

### Debugging the Worker

The Worker runs as a hosted service inside the Api process.

**1. Set breakpoint** in `Worker/OrderWorker.cs` or `Infrastructure/OrderProcessor.cs`:

```csharp
_logger.LogInformation("Processing order {OrderId}", order.Id);  // Breakpoint here
```

**2. Run Api with debugger** (F5)

**3. Create a pending order** via API:

```bash
curl -X POST "http://localhost:5181/api/orders" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "your-customer-id",
    "items": [{"productId": "your-product-id", "quantity": 1}]
  }'
```

**4. Wait ~10 seconds** for worker to trigger

**5. Debugger hits breakpoint** in worker code

---

**Tip: Reduce Worker Delay for Faster Debugging**

File: `Worker/OrderWorker.cs`

```csharp
await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);  // Changed from 10
```

Restart the API after making this change.

---

### Debugging Tests

#### VS Code

1. Open test file (`Tests/DomainTests.cs`)
2. Click **Run Test** or **Debug Test** code lens above test method

---

#### Visual Studio

1. Right-click test method → **Debug Test(s)**

---

#### Command Line

```bash
# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Attach debugger (wait for debugger to attach)
dotnet test --logger "console;verbosity=detailed" --filter "FullyQualifiedName~OrderProcessorTests"
```

---

## Working with the Database

### Inspecting the Database

#### SQLite CLI

```bash
# Open database
sqlite3 Api/app.db

# List tables
.tables

# View schema
.schema Orders

# Query data
SELECT * FROM Orders WHERE Status = 0;
SELECT * FROM Products WHERE Stock < 10;

# Exit
.exit
```

---

#### DB Browser for SQLite (GUI)

1. Download from https://sqlitebrowser.org/
2. Open `Api/app.db`
3. **Browse Data** tab → Select table → View/edit data
4. **Execute SQL** tab → Run custom queries

**Example queries:**

```sql
-- Orders with their customers
SELECT o.Id, c.Name, o.CreatedAt, o.Status
FROM Orders o
JOIN Customers c ON o.CustomerId = c.Id;

-- Products low on stock
SELECT Name, Stock FROM Products WHERE Stock < 50;

-- Order items with product details
SELECT oi.OrderId, p.Name, oi.Quantity, oi.UnitPrice
FROM OrderItems oi
JOIN Products p ON oi.ProductId = p.Id;
```

---

### Resetting Development Data

**Option 1: Delete database file (fastest)**

```bash
rm Api/app.db
dotnet run --project Api  # Recreates and re-seeds
```

**Option 2: EF Core command**

```bash
dotnet ef database drop --project Infrastructure --startup-project Api
dotnet ef database update --project Infrastructure --startup-project Api
```

---

### Adding Custom Seed Data

File: `Infrastructure/DbSeeder.cs`

```csharp
public static void Seed(AppDbContext db)
{
    if (db.Customers.Any()) return;

    // Add customers
    var alice = new Customer { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com" };
    var bob = new Customer { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com" };
    var charlie = new Customer { Id = Guid.NewGuid(), Name = "Charlie", Email = "charlie@example.com" };  // New
    db.Customers.AddRange(alice, bob, charlie);

    // Add products
    var widget = new Product { Id = Guid.NewGuid(), Name = "Widget", Price = 9.99m, Stock = 100 };
    var gadget = new Product { Id = Guid.NewGuid(), Name = "Gadget", Price = 19.99m, Stock = 50 };
    var gizmo = new Product { Id = Guid.NewGuid(), Name = "Gizmo", Price = 14.99m, Stock = 75 };  // New
    db.Products.AddRange(widget, gadget, gizmo);

    db.SaveChanges();
}
```

**Apply changes:**

```bash
rm Api/app.db
dotnet run --project Api
```

---

## Testing Workflows

### Running Specific Tests

```bash
# Run all tests
dotnet test

# Run tests in a specific project
dotnet test Tests/Tests.csproj

# Run tests in a specific file
dotnet test --filter "FullyQualifiedName~DomainTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~DomainTests.OrderItem_TotalPrice_Calculation"

# Run tests matching pattern
dotnet test --filter "FullyQualifiedName~Order"
```

---

### Writing Integration Tests

Create integration tests using in-memory database:

**File:** `Tests/OrderIntegrationTests.cs`

```csharp
using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using DotNetSample.Core;
using DotNetSample.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetSample.Tests
{
    public class OrderIntegrationTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task ProcessOrder_DeductsStock()
        {
            // Arrange
            var db = GetInMemoryDbContext();
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Widget",
                Price = 10.00m,
                Stock = 10
            };
            db.Products.Add(product);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Quantity = 2
                    }
                }
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var processor = new OrderProcessor(db, NullLogger<OrderProcessor>.Instance);

            // Act
            await processor.ProcessPendingOrdersAsync();

            // Assert
            Assert.Equal(8, product.Stock);
            Assert.Equal(OrderStatus.Completed, order.Status);
            Assert.Equal(10.00m, order.Items[0].UnitPrice);
        }
    }
}
```

**Run:**

```bash
dotnet test --filter "FullyQualifiedName~OrderIntegrationTests"
```

---

### Test Coverage

**Install coverlet:**

```bash
dotnet add Tests package coverlet.collector
dotnet add Tests package coverlet.msbuild
```

**Run with coverage:**

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

**View coverage report:**

```bash
# Install report generator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:Tests/coverage.opencover.xml \
  -targetdir:Tests/coverage-report \
  -reporttypes:Html

# Open report
open Tests/coverage-report/index.html  # macOS
xdg-open Tests/coverage-report/index.html  # Linux
start Tests/coverage-report/index.html  # Windows
```

---

## Performance Testing

### Load Testing with Apache Bench

**Install Apache Bench:**
- macOS: Comes with Apache (`which ab`)
- Ubuntu: `sudo apt-get install apache2-utils`
- Windows: Download Apache binaries

**Test GET endpoint:**

```bash
ab -n 1000 -c 10 http://localhost:5181/api/products
```

**Parameters:**
- `-n 1000` — Total requests
- `-c 10` — Concurrent requests

**Output:**

```
Requests per second:    450.23 [#/sec] (mean)
Time per request:       22.21 [ms] (mean)
```

---

**Test POST endpoint:**

Create `order.json`:

```json
{
  "customerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "items": [
    {"productId": "1a2b3c4d-5e6f-7890-abcd-ef1234567890", "quantity": 1}
  ]
}
```

**Run test:**

```bash
ab -n 100 -c 5 -p order.json -T application/json http://localhost:5181/api/orders
```

---

### Profiling with dotnet-trace

**Install dotnet-trace:**

```bash
dotnet tool install --global dotnet-trace
```

**Start API:**

```bash
dotnet run --project Api
```

**Find process ID:**

```bash
dotnet-trace ps
```

**Collect trace:**

```bash
dotnet-trace collect --process-id <pid> --format speedscope
```

**Generate load** (in another terminal):

```bash
ab -n 1000 -c 10 http://localhost:5181/api/products
```

**Stop tracing** (Ctrl+C)

**View trace:**

Upload `trace.speedscope.json` to https://www.speedscope.app/

---

### Database Query Profiling

**Enable query logging:**

File: `Api/Program.cs`

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db")
           .EnableSensitiveDataLogging()  // Shows parameter values
           .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information));
```

**View queries in console:**

```
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[@__p_0='0' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
      SELECT "o"."Id", "o"."CreatedAt", "o"."CustomerId", "o"."Status"
      FROM "Orders" AS "o"
      WHERE "o"."Status" = @__p_0
```

---

## Troubleshooting Common Issues

### Port Already in Use

**Error:**

```
Unable to bind to http://localhost:5181 on the IPv4 loopback interface: 'Address already in use'.
```

**Solution:**

```bash
# Find process using port 5181
lsof -i :5181  # macOS/Linux
netstat -ano | findstr :5181  # Windows

# Kill process
kill -9 <PID>  # macOS/Linux
taskkill /PID <PID> /F  # Windows

# Or change port in Api/Properties/launchSettings.json
```

---

### Migration Errors

**Error:** `A migration has already been applied to the database`

**Solution:**

```bash
# Option 1: Drop and recreate
dotnet ef database drop --project Infrastructure --startup-project Api
dotnet ef database update --project Infrastructure --startup-project Api

# Option 2: Delete database file
rm Api/app.db
dotnet run --project Api
```

**Error:** `Build failed`

**Solution:**

```bash
# Build first, then create migration
dotnet build
dotnet ef migrations add MigrationName --project Infrastructure --startup-project Api -o Migrations
```

---

### Worker Not Running

**Check logs:**

```bash
dotnet run --project Api | grep "Processing order"
```

**Verify registration:**

File: `Api/Program.cs`

```csharp
builder.Services.AddHostedService<OrderWorker>();
```

**Check for pending orders:**

```bash
sqlite3 Api/app.db "SELECT * FROM Orders WHERE Status = 0;"
```

**Force reprocessing:**

```bash
curl -X POST http://localhost:5181/api/orders/reprocess-pending
```

---

### Tests Failing

**Solution 1: Clean and rebuild**

```bash
dotnet clean
dotnet build
dotnet test
```

**Solution 2: Check for database conflicts**

Tests using in-memory database should use unique database names:

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())  // Unique per test
    .Options;
```

**Solution 3: Review test output**

```bash
dotnet test --logger "console;verbosity=detailed"
```

---

### NuGet Package Restore Fails

**Solution:**

```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore

# Build
dotnet build
```

---

## Tips and Tricks

### Quickly Create Sample Data

```bash
# Get customer and product IDs
curl http://localhost:5181/api/customers | jq '.[0].id'
curl http://localhost:5181/api/products | jq '.[0].id'

# Create order
curl -X POST "http://localhost:5181/api/orders" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "CUSTOMER_ID_HERE",
    "items": [{"productId": "PRODUCT_ID_HERE", "quantity": 2}]
  }'
```

---

### Watch Mode for Tests

```bash
dotnet watch test
```

Automatically reruns tests when code changes.

---

### Hot Reload for API

```bash
dotnet watch run --project Api
```

API reloads when code changes (no restart needed for many changes).

---

### View Swagger While Developing

```bash
dotnet run --project Api
```

Navigate to: `http://localhost:5181/swagger`

Interactive API documentation and testing.

---

## Related Documentation

- [WORKSPACE.md](WORKSPACE.md) — Basic setup and running locally
- [ARCHITECTURE.md](ARCHITECTURE.md) — System design and patterns
- [DATABASE.md](DATABASE.md) — Database schema and migrations
- [API_REFERENCE.md](API_REFERENCE.md) — Endpoint documentation
- [CONTRIBUTING.md](CONTRIBUTING.md) — Code standards and guidelines
