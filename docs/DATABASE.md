# Database Documentation

Database schema, relationships, and migration workflows for DotNetSample.

## Overview

DotNetSample uses **SQLite** for local development with **Entity Framework Core 8** for data access.

- **Database File:** `Api/app.db` (auto-created on first run)
- **ORM:** Entity Framework Core 8
- **Provider:** Microsoft.EntityFrameworkCore.Sqlite

---

## Schema

### Customers Table

```sql
CREATE TABLE Customers (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Email TEXT NOT NULL
);
```

**Fields:**
- `Id` — GUID (System.Guid), primary key
- `Name` — Customer name (string)
- `Email` — Customer email address (string)

**Indexes:**
- Primary key on `Id`

**C# Entity:**

```csharp
public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}
```

---

### Products Table

```sql
CREATE TABLE Products (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Price REAL NOT NULL,
    Stock INTEGER NOT NULL
);
```

**Fields:**
- `Id` — GUID, primary key
- `Name` — Product name (string)
- `Price` — Decimal price (stored as REAL in SQLite)
- `Stock` — Integer quantity available

**Business Rules:**
- `Stock` is decremented when orders are processed
- Negative stock is prevented by OrderProcessor logic
- `Price` is captured as `UnitPrice` on OrderItem during processing

**C# Entity:**

```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
```

---

### Orders Table

```sql
CREATE TABLE Orders (
    Id TEXT PRIMARY KEY,
    CustomerId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    Status INTEGER NOT NULL,
    FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
);
```

**Fields:**
- `Id` — GUID, primary key
- `CustomerId` — Foreign key to Customers table
- `CreatedAt` — DateTime (stored as ISO-8601 string in SQLite)
- `Status` — Integer enum (0=Pending, 1=Processing, 2=Completed, 3=Cancelled)

**Relationships:**
- Many-to-one with Customers (one customer can have many orders)
- One-to-many with OrderItems (one order can have many items)

**Status Values:**

```csharp
public enum OrderStatus
{
    Pending = 0,      // Order created, awaiting processing
    Processing = 1,   // Picked up by worker
    Completed = 2,    // Successfully processed, stock deducted
    Cancelled = 3     // Cancelled due to insufficient stock
}
```

**C# Entity:**

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

---

### OrderItems Table

```sql
CREATE TABLE OrderItems (
    Id TEXT PRIMARY KEY,
    OrderId TEXT NOT NULL,
    ProductId TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    UnitPrice REAL NOT NULL,
    FOREIGN KEY (OrderId) REFERENCES Orders(Id),
    FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
```

**Fields:**
- `Id` — GUID, primary key
- `OrderId` — Foreign key to Orders table
- `ProductId` — Foreign key to Products table
- `Quantity` — Integer quantity ordered
- `UnitPrice` — Decimal price at time of order (snapshot from Product.Price)

**Relationships:**
- Many-to-one with Orders
- Many-to-one with Products

**Notes:**
- `UnitPrice` is set during order processing (not at creation)
- `UnitPrice` captures the product price at the time of purchase (historical record)
- `Quantity` is the requested amount (stock is checked against this during processing)

**C# Entity:**

```csharp
public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

---

## Entity Relationships (ERD)

```
┌─────────────┐
│  Customers  │
│─────────────│
│ Id (PK)     │
│ Name        │
│ Email       │
└──────┬──────┘
       │
       │ 1:N
       │
┌──────▼──────┐         ┌─────────────┐
│   Orders    │         │  Products   │
│─────────────│         │─────────────│
│ Id (PK)     │         │ Id (PK)     │
│ CustomerId  │◄────┐   │ Name        │
│ CreatedAt   │     │   │ Price       │
│ Status      │     │   │ Stock       │
└──────┬──────┘     │   └──────┬──────┘
       │            │          │
       │ 1:N        │          │ N:1
       │            │          │
┌──────▼────────────┴──────────▼──┐
│         OrderItems               │
│──────────────────────────────────│
│ Id (PK)                          │
│ OrderId (FK → Orders)            │
│ ProductId (FK → Products)        │
│ Quantity                         │
│ UnitPrice                        │
└──────────────────────────────────┘
```

**Explanation:**
- One Customer can have **many** Orders
- One Order can have **many** OrderItems
- One Product can appear in **many** OrderItems
- OrderItems creates a many-to-many relationship between Orders and Products with additional data (Quantity, UnitPrice)

---

## Seed Data

The database is seeded with initial data via `Infrastructure/DbSeeder.cs` on first run.

**Seeding Logic:**

```csharp
public static void Seed(AppDbContext db)
{
    if (db.Customers.Any()) return;  // Only seed if empty

    var c1 = new Customer { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com" };
    var c2 = new Customer { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com" };
    db.Customers.AddRange(c1, c2);

    var p1 = new Product { Id = Guid.NewGuid(), Name = "Widget", Price = 9.99m, Stock = 100 };
    var p2 = new Product { Id = Guid.NewGuid(), Name = "Gadget", Price = 19.99m, Stock = 50 };
    db.Products.AddRange(p1, p2);

    db.SaveChanges();
}
```

**Seed Data:**

**Customers:**
- Alice (alice@example.com)
- Bob (bob@example.com)

**Products:**
- Widget ($9.99, Stock: 100)
- Gadget ($19.99, Stock: 50)

**When Seeding Occurs:**
- Automatically at application startup (called from `Api/Program.cs`)
- Only if the `Customers` table is empty
- Does not recreate data if it already exists

---

## EF Core Configuration

### DbContext

**File:** `Infrastructure/AppDbContext.cs`

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>().HasKey(c => c.Id);
        modelBuilder.Entity<Product>().HasKey(p => p.Id);
        modelBuilder.Entity<Order>().HasKey(o => o.Id);
        modelBuilder.Entity<OrderItem>().HasKey(oi => oi.Id);

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(oi => oi.OrderId);
    }
}
```

**DbSets:**
- `Customers` — Customer entity set
- `Products` — Product entity set
- `Orders` — Order entity set
- `OrderItems` — OrderItem entity set

**Configuration:**
- Primary keys defined for all entities
- One-to-many relationship: Order → OrderItems (with foreign key)

---

### Connection String

**File:** `Api/appsettings.json` or `Api/appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=app.db"
  }
}
```

**Fallback:**

If not in config, defaults to `"Data Source=app.db"` in `Api/Program.cs`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));
```

---

## Migrations

### Current Migrations

- `20251130212817_InitialCreate` — Initial schema creation (Customers, Products, Orders, OrderItems)

### Creating a New Migration

From the repository root:

```bash
dotnet ef migrations add <MigrationName> \
  --project Infrastructure \
  --startup-project Api \
  -o Migrations
```

**Example:**

```bash
dotnet ef migrations add AddProductCategory \
  --project Infrastructure \
  --startup-project Api \
  -o Migrations
```

**Why these flags?**
- `--project Infrastructure` — Where `AppDbContext` lives
- `--startup-project Api` — Where connection string and configuration are located
- `-o Migrations` — Output directory for migration files (Infrastructure/Migrations/)

**What Happens:**
1. EF Core compares current DbContext with last migration
2. Generates migration file with `Up()` and `Down()` methods
3. Creates timestamped file in `Infrastructure/Migrations/`

---

### Applying Migrations

**Manually:**

```bash
dotnet ef database update \
  --project Infrastructure \
  --startup-project Api
```

**Automatically (Recommended):**

Migrations are auto-applied at startup in `Api/Program.cs`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();  // Apply pending migrations
    DbSeeder.Seed(db);      // Seed data if empty
}
```

This ensures the database is always up-to-date when the API starts.

---

### Removing Last Migration

If you need to undo the last migration (before applying it):

```bash
dotnet ef migrations remove \
  --project Infrastructure \
  --startup-project Api
```

**Warning:** This only works if the migration hasn't been applied to the database yet.

---

### Listing Migrations

```bash
dotnet ef migrations list \
  --project Infrastructure \
  --startup-project Api
```

---

## Resetting the Database

### Option 1: Delete the Database File

```bash
rm Api/app.db
dotnet run --project Api  # Recreates database and seeds data
```

This is the simplest way during development.

---

### Option 2: Use EF Core Commands

```bash
# Drop the database
dotnet ef database drop \
  --project Infrastructure \
  --startup-project Api

# Recreate and apply migrations
dotnet ef database update \
  --project Infrastructure \
  --startup-project Api
```

---

## SQLite Tools

### View Database in CLI

```bash
# Open database file
sqlite3 Api/app.db

# List tables
.tables

# View schema
.schema Customers

# Query data
SELECT * FROM Customers;
SELECT * FROM Orders WHERE Status = 0;

# Exit
.exit
```

---

### GUI Tools

**DB Browser for SQLite:**
- Download: https://sqlitebrowser.org/
- Open `Api/app.db` in the application
- Browse Data tab → Select table
- Execute SQL tab for custom queries

**DBeaver:**
- Download: https://dbeaver.io/
- Create new SQLite connection
- Point to `Api/app.db`
- Full-featured database IDE

---

## Production Considerations

### Switching to PostgreSQL

**1. Update `Api/Program.cs`:**

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

**2. Add NuGet package:**

```bash
dotnet add Api package Npgsql.EntityFrameworkCore.PostgreSQL
```

**3. Update connection string in `appsettings.Production.json`:**

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=dotnetsample;Username=postgres;Password=yourpassword"
  }
}
```

**4. Generate new migrations:**

```bash
dotnet ef migrations add InitialPostgreSQL \
  --project Infrastructure \
  --startup-project Api \
  -o Migrations
```

EF Core will create provider-specific SQL for PostgreSQL.

---

### Switching to SQL Server

**1. Update `Api/Program.cs`:**

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
```

**2. Add NuGet package:**

```bash
dotnet add Api package Microsoft.EntityFrameworkCore.SqlServer
```

**3. Update connection string:**

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=DotNetSample;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

---

### SQLite Limitations

**Concurrency:**
- SQLite supports only **one writer at a time**
- Multiple concurrent writes will fail or serialize
- Not suitable for high-write workloads

**Storage:**
- File-based only (no network storage)
- Limited to disk size
- No built-in replication

**Data Types:**
- Limited type system (TEXT, INTEGER, REAL, BLOB)
- Decimal stored as REAL (potential precision issues)

**Recommendation:** Use **PostgreSQL** or **SQL Server** for production workloads with:
- Concurrent users
- High transaction volume
- Scalability requirements
- Advanced features (stored procedures, full-text search, etc.)

---

## Troubleshooting

### "Database is locked" Error

**Cause:** Another process has the database open, or a transaction is not committed.

**Solution:**
1. Ensure only one API instance is running
2. Check for DbContext instances not disposed
3. Restart the API

---

### "Table already exists" Error

**Cause:** Trying to apply a migration that creates a table that already exists.

**Solution:**
1. Delete `Api/app.db`
2. Delete all files in `Infrastructure/Migrations/`
3. Recreate initial migration:
   ```bash
   dotnet ef migrations add InitialCreate \
     --project Infrastructure \
     --startup-project Api \
     -o Migrations
   ```

---

### Migration Not Applied

**Cause:** Auto-migration disabled or migration file not generated.

**Solution:**
1. Verify `db.Database.Migrate()` is called in `Api/Program.cs`
2. Check that migration files exist in `Infrastructure/Migrations/`
3. Manually apply: `dotnet ef database update`

---

## Related Documentation

- See [ARCHITECTURE.md](ARCHITECTURE.md) for DbContext and repository patterns
- See [WORKSPACE.md](WORKSPACE.md) for local development setup
- See [API_REFERENCE.md](API_REFERENCE.md) for entity schemas in API responses
