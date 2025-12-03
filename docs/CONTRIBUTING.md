# Contributing to DotNetSample

Thank you for considering contributing to DotNetSample! This document provides guidelines for contributing code, maintaining quality, and collaborating effectively.

## Development Setup

See [WORKSPACE.md](WORKSPACE.md) for detailed setup instructions.

**Quick Start:**

```bash
git clone <repository-url>
cd DotNetSample
dotnet restore
dotnet build
dotnet test
```

---

## Branching Strategy

- **main** — Production-ready code, protected branch
- **develop** — Integration branch for features (if using git-flow)
- **feature/*** — Feature branches (e.g., `feature/add-payment-api`)
- **bugfix/*** — Bug fix branches (e.g., `bugfix/order-validation`)
- **hotfix/*** — Urgent production fixes

**Example:**

```bash
git checkout -b feature/add-inventory-tracking
```

---

## Workflow

### 1. Create a Branch

```bash
git checkout -b feature/your-feature-name
```

**Naming conventions:**
- `feature/` — New functionality
- `bugfix/` — Bug fixes
- `refactor/` — Code refactoring
- `docs/` — Documentation updates
- `test/` — Test additions or improvements

---

### 2. Make Changes

Follow coding standards (see below) and ensure:
- Code compiles without errors
- Tests pass
- Documentation is updated if needed

---

### 3. Run Tests

```bash
dotnet test
```

All tests must pass before submitting a pull request.

---

### 4. Commit Changes

Use [Conventional Commits](https://www.conventionalcommits.org/) format:

```bash
git add .
git commit -m "feat: add inventory tracking endpoint"
```

See [Commit Message Format](#commit-message-format) below for details.

---

### 5. Push Branch

```bash
git push origin feature/your-feature-name
```

---

### 6. Create Pull Request

1. Navigate to the repository on GitHub
2. Click **New Pull Request**
3. Target: `main` (or `develop` if using git-flow)
4. Fill out PR template:
   - **Description:** What does this PR do?
   - **Related Issues:** Closes #123
   - **Testing:** How was this tested?
5. Request review from maintainers
6. Address feedback and update PR

---

## Coding Standards

### C# Style Guide

Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

**Key Points:**

**Naming:**
- **PascalCase** for public members, classes, methods, properties
- **camelCase** for private fields (prefix with `_`)
- **PascalCase** for local variables and parameters
- Meaningful, descriptive names

**Example:**

```csharp
public class OrderProcessor : IOrderProcessor
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(AppDbContext db, ILogger<OrderProcessor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ProcessPendingOrdersAsync()
    {
        var pendingOrders = await _db.Orders
            .Where(o => o.Status == OrderStatus.Pending)
            .ToListAsync();

        foreach (var order in pendingOrders)
        {
            await ProcessOrderAsync(order);
        }
    }
}
```

---

### Methods and Functions

- **Keep methods small** — Single responsibility principle
- **Meaningful names** — Method names should describe what they do
- **Limit parameters** — Maximum 3-4 parameters; use objects for more
- **Async suffix** — Async methods should end with `Async`

**Example:**

```csharp
// Good
public async Task<Order> GetOrderByIdAsync(Guid id)
{
    return await _orderRepo.GetAsync(id);
}

// Avoid
public async Task<Order> Get(Guid i)  // Unclear name, unclear parameter
{
    return await _orderRepo.GetAsync(i);
}
```

---

### XML Documentation Comments

Add XML comments for public APIs:

```csharp
/// <summary>
/// Processes pending orders and updates their status.
/// </summary>
/// <returns>A task representing the asynchronous operation.</returns>
public async Task ProcessPendingOrdersAsync()
{
    // Implementation
}
```

---

### Project Organization

**Controllers:**
- Thin HTTP layer
- Delegate to repositories and services
- No business logic in controllers

```csharp
[HttpGet]
public async Task<IEnumerable<Order>> Get() => await _orderRepo.ListAsync();
```

**Core:**
- Pure domain models
- No dependencies on infrastructure
- Interfaces define contracts

**Infrastructure:**
- Data access implementations
- Business logic services
- EF Core configurations

**Tests:**
- One test class per production class
- Follow AAA pattern (Arrange, Act, Assert)

---

### Dependency Injection

Always use **constructor injection**:

```csharp
public class OrdersController : ControllerBase
{
    private readonly IRepository<Order> _orderRepo;

    public OrdersController(IRepository<Order> orderRepo)
    {
        _orderRepo = orderRepo;
    }
}
```

**Avoid:**
- Service locator pattern
- Manual instantiation of dependencies
- Static dependencies

---

## Testing Guidelines

### Test Structure

Follow **Arrange-Act-Assert (AAA)** pattern:

```csharp
[Fact]
public async Task ProcessPendingOrders_ShouldDeductStock()
{
    // Arrange
    var product = new Product { Id = Guid.NewGuid(), Stock = 10 };
    var order = new Order
    {
        Id = Guid.NewGuid(),
        Status = OrderStatus.Pending,
        Items = new List<OrderItem>
        {
            new OrderItem { ProductId = product.Id, Quantity = 2 }
        }
    };

    // Act
    await _processor.ProcessPendingOrdersAsync();

    // Assert
    Assert.Equal(8, product.Stock);
}
```

---

### Test Naming

Use descriptive test names that explain the scenario:

**Format:** `MethodName_Scenario_ExpectedBehavior`

**Examples:**

```csharp
[Fact]
public void CreateOrder_WithValidData_ReturnsCreatedStatus() { }

[Fact]
public void ProcessOrder_InsufficientStock_CancelsOrder() { }

[Fact]
public async Task GetCustomer_NonExistentId_ReturnsNotFound() { }
```

---

### Test Coverage

- **All business logic** in `Infrastructure` should have tests
- **Controllers** can have integration tests
- Aim for **>80% code coverage** for critical paths

**Running tests with coverage:**

```bash
dotnet test /p:CollectCoverage=true
```

---

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~OrderProcessorTests"

# Run tests in a class
dotnet test --filter "FullyQualifiedName~DomainTests"

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

---

## Database Changes

### Adding a New Entity

**1. Add entity to `Core/DomainModels.cs`:**

```csharp
public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}
```

**2. Add DbSet to `Infrastructure/AppDbContext.cs`:**

```csharp
public DbSet<Category> Categories { get; set; }
```

**3. Configure entity (if needed):**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Category>().HasKey(c => c.Id);
    modelBuilder.Entity<Category>()
        .Property(c => c.Name)
        .IsRequired()
        .HasMaxLength(100);
}
```

**4. Create migration:**

```bash
dotnet ef migrations add AddCategory \
  --project Infrastructure \
  --startup-project Api \
  -o Migrations
```

**5. Review generated migration:**

Open `Infrastructure/Migrations/<timestamp>_AddCategory.cs` and verify the SQL.

**6. Test migration:**

```bash
dotnet ef database update --project Infrastructure --startup-project Api
```

**7. Update seed data if needed** in `Infrastructure/DbSeeder.cs`

---

### Modifying an Entity

1. Update entity in `Core/DomainModels.cs`
2. Create migration: `dotnet ef migrations add UpdateEntityName`
3. Review generated migration code
4. Test migration on development database

---

## Documentation

Update documentation when making changes:

**When to update:**

| Change | File to Update |
|--------|----------------|
| Add API endpoint | `docs/API_REFERENCE.md` |
| Change architecture | `docs/ARCHITECTURE.md` |
| Modify database schema | `docs/DATABASE.md` |
| Add deployment steps | `docs/DEPLOYMENT.md` |
| Update dev workflow | `docs/WORKSPACE.md` or `docs/DEVELOPMENT_WORKFLOWS.md` |

**Documentation style:**
- Clear, concise, practical
- Include code examples
- Update related files (don't create orphaned sections)

---

## Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

**Format:**

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat` — New feature
- `fix` — Bug fix
- `docs` — Documentation changes
- `style` — Code style changes (formatting, no logic change)
- `refactor` — Code refactoring (no feature/bug change)
- `test` — Adding or updating tests
- `chore` — Build, CI, dependencies, tooling

**Scope (optional):**
- `api` — Api project
- `core` — Core project
- `infrastructure` — Infrastructure project
- `worker` — Worker project
- `tests` — Tests project

---

### Examples

**New feature:**

```
feat(orders): add date range filtering endpoint

Adds GET /api/orders/daterange with startDate, endDate, and pagination
parameters. Allows querying orders within a specific date range.

Closes #42
```

**Bug fix:**

```
fix(worker): prevent concurrent order processing

Adds lock mechanism to ensure only one worker processes orders at a time.
Prevents race conditions when multiple instances run.

Fixes #58
```

**Documentation:**

```
docs(api): update API reference with new endpoints

Documents the new date range endpoint and reprocessing endpoint
in API_REFERENCE.md.
```

**Refactoring:**

```
refactor(infrastructure): extract order validation logic

Moves order validation from OrderProcessor to separate OrderValidator class
for better testability and separation of concerns.
```

---

## Code Review Process

### For Reviewers

**What to check:**
- Code quality and adherence to standards
- Tests exist and pass
- Documentation is updated
- No security vulnerabilities (SQL injection, XSS, exposed secrets)
- Performance considerations
- Edge cases handled

**How to review:**
1. Read the description and understand the change
2. Check code changes line-by-line
3. Test locally if possible
4. Leave constructive feedback
5. Approve when ready or request changes

---

### For Contributors

**Responding to feedback:**
1. Respond to comments promptly
2. Make requested changes
3. Explain decisions if you disagree (politely)
4. Mark conversations as resolved after addressing
5. Request re-review after updates

**Updating PR:**

```bash
git add .
git commit -m "fix: address review feedback"
git push origin feature/your-feature-name
```

---

## CI/CD Pipeline

See `.github/workflows/` for CI pipeline configuration.

**Current checks:**
- `dotnet build` — Ensures code compiles
- `dotnet test` — Runs all tests

**All checks must pass before merging.**

**Future additions:**
- Code coverage reporting
- Static analysis (Roslyn analyzers, SonarQube)
- Deployment to staging environment
- Performance benchmarks

---

## Security Considerations

**Do NOT commit:**
- Passwords or API keys
- Connection strings with credentials
- Private keys or certificates
- Sensitive customer data

**Use:**
- User secrets (`dotnet user-secrets`) for local development
- Environment variables for production
- Azure Key Vault or AWS Secrets Manager for cloud deployments

**Example:**

```bash
# Set user secret
dotnet user-secrets set "ConnectionStrings:Default" "Server=..." --project Api
```

---

## Getting Help

**Questions or issues?**
- Open an issue on GitHub
- Ask in pull request comments
- Check [WORKSPACE.md](WORKSPACE.md) for common setup issues
- Review [ARCHITECTURE.md](ARCHITECTURE.md) for design decisions

**Communication:**
- Be respectful and professional
- Provide context and examples
- Share error messages and logs when reporting bugs

---

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

## Thank You!

Your contributions make DotNetSample better. We appreciate your time and effort!

---

## Related Documentation

- [WORKSPACE.md](WORKSPACE.md) — Local development setup
- [ARCHITECTURE.md](ARCHITECTURE.md) — System design
- [DATABASE.md](DATABASE.md) — Database schema and migrations
- [API_REFERENCE.md](API_REFERENCE.md) — API endpoint documentation
- [DEVELOPMENT_WORKFLOWS.md](DEVELOPMENT_WORKFLOWS.md) — Common development tasks
