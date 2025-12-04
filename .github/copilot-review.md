# Code Review Assistant

Intelligent code review agent for comprehensive quality checks tailored for Azure cloud applications.

## Review Areas

### Code Quality
- SOLID principles adherence
- DRY (Don't Repeat Yourself)
- Clean code practices
- Proper naming conventions
- Appropriate design patterns

### Best Practices
- Error handling and logging
- Input validation
- Resource disposal
- Thread safety
- Async/await usage

### Azure-Specific
- Proper use of Managed Identity
- Azure Key Vault integration
- Application Insights telemetry
- Service Bus patterns
- Retry policies

## Common Issues to Flag

### Error Handling
```csharp
// ❌ BAD - Swallowing exceptions
try
{
    await ProcessOrderAsync(order);
}
catch (Exception)
{
    // Silent failure
}

// ✅ GOOD - Proper error handling
try
{
    await ProcessOrderAsync(order);
}
catch (ValidationException ex)
{
    _logger.LogWarning(ex, "Order validation failed for order {OrderId}", order.Id);
    return BadRequest(new { error = ex.Message });
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process order {OrderId}", order.Id);
    throw;
}
```

### Resource Management
```csharp
// ❌ BAD - Not disposing
var connection = new SqlConnection(connectionString);

// ✅ GOOD - Using statement
using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
```

## Review Checklist

### ✓ Code Structure
- [ ] Single Responsibility Principle
- [ ] Methods are focused and concise
- [ ] No circular dependencies

### ✓ Error Handling
- [ ] All exceptions are logged
- [ ] No swallowed exceptions
- [ ] Proper exception types

### ✓ Performance
- [ ] No N+1 query problems
- [ ] Proper async/await usage
- [ ] Efficient data structures

### ✓ Security
- [ ] No hardcoded credentials
- [ ] Input validation
- [ ] Proper authentication/authorization
