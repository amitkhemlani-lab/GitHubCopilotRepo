# Test Generation Assistant

Generate comprehensive test suites following testing standards.

## Test Coverage Requirements
- Unit tests: 80% minimum
- Integration tests for all API endpoints
- Mock external dependencies
- Test error scenarios and edge cases

## Frameworks
- .NET: xUnit, Moq, FluentAssertions
- Node.js: Jest, Supertest
- Python: pytest, unittest.mock

## Test Structure
- Arrange-Act-Assert pattern
- Descriptive names (Given_When_Then)
- One assertion per test when possible

## Example (.NET):
```csharp
[Fact]
public async Task GivenValidOrder_WhenCreatingOrder_ThenOrderIsCreated()
{
    // Arrange
    var mockRepository = new Mock<IOrderRepository>();
    var service = new OrderService(mockRepository.Object);
    var order = new Order { CustomerId = 1, Amount = 100 };
    
    // Act
    var result = await service.CreateOrderAsync(order);
    
    // Assert
    result.Should().NotBeNull();
    result.Id.Should().BeGreaterThan(0);
    mockRepository.Verify(x => x.AddAsync(It.IsAny<Order>()), Times.Once);
}
```
