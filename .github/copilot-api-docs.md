# API Documentation Generator

Creates comprehensive API documentation following OpenAPI 3.0 standards.

## Documentation Requirements
- OpenAPI/Swagger annotations
- Request/response examples
- Error codes and descriptions
- Authentication requirements
- Rate limiting information

## HTTP Status Codes
- 200 OK: Success
- 201 Created: Resource created
- 400 Bad Request: Invalid request
- 401 Unauthorized: Authentication required
- 403 Forbidden: Not authorized
- 404 Not Found: Resource doesn't exist
- 500 Internal Server Error: Server error

## Example (.NET):
```csharp
/// <summary>
/// Creates a new order
/// </summary>
/// <param name="request">Order creation request</param>
/// <returns>Created order with ID</returns>
/// <response code="201">Order successfully created</response>
/// <response code="400">Invalid request data</response>
[HttpPost("api/orders")]
[Authorize]
[ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<ActionResult<OrderResponse>> CreateOrder(
    [FromBody] CreateOrderRequest request)
{
    var order = await _orderService.CreateAsync(request);
    return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
}
```
