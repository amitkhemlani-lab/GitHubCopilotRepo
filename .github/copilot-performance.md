# Performance Optimization Assistant

Identifies performance bottlenecks and suggests optimizations.

## Areas to Analyze
- Database query efficiency
- API response times
- Memory usage
- Caching opportunities
- Async/await patterns

## Database Performance

### N+1 Query Problem
```csharp
// ❌ BAD - N+1 queries
var orders = await _context.Orders.ToListAsync();
foreach (var order in orders)
{
    var customer = await _context.Customers.FindAsync(order.CustomerId);
}

// ✅ GOOD - Single query
var orders = await _context.Orders
    .Include(o => o.Customer)
    .ToListAsync();
```

### Inefficient Filtering
```csharp
// ❌ BAD - Loading all then filtering
var allOrders = await _context.Orders.ToListAsync();
var filtered = allOrders.Where(o => o.Status == "Pending");

// ✅ GOOD - Filter in database
var filtered = await _context.Orders
    .Where(o => o.Status == "Pending")
    .ToListAsync();
```

## Caching Strategy

```csharp
public async Task<Product> GetProductAsync(int id)
{
    var cacheKey = $"product:{id}";
    
    // Try cache first
    var cached = await _cache.GetStringAsync(cacheKey);
    if (cached != null)
    {
        return JsonSerializer.Deserialize<Product>(cached);
    }
    
    // Load from database
    var product = await _context.Products.FindAsync(id);
    
    // Cache for 10 minutes
    await _cache.SetStringAsync(
        cacheKey, 
        JsonSerializer.Serialize(product),
        new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });
    
    return product;
}
```

## Async Best Practices

```csharp
// ❌ BAD - Blocking async
var result = GetDataAsync().Result;

// ✅ GOOD - Proper async
var result = await GetDataAsync();

// ✅ GOOD - Parallel async calls
var userTask = GetUserAsync(id);
var ordersTask = GetOrdersAsync(id);
await Task.WhenAll(userTask, ordersTask);
```
