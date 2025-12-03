using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

var client = new HttpClient { BaseAddress = new Uri("http://localhost:5181") };

Console.WriteLine("DotNetSample API Client\n");

var products = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
Console.WriteLine($"Products: {products?.Count ?? 0}");

var customers = await client.GetFromJsonAsync<List<CustomerDto>>("/api/customers");
Console.WriteLine($"Customers: {customers?.Count ?? 0}");

if (customers?.Count > 0 && products?.Count > 0)
{
    var order = new CreateOrderDto
    {
        CustomerId = customers[0].Id,
        Items = new[] { new CreateOrderItemDto { ProductId = products[0].Id, Quantity = 1 } }
    };

    var resp = await client.PostAsJsonAsync("/api/orders", order);
    var respBody = await resp.Content.ReadAsStringAsync();
    Console.WriteLine($"POST /api/orders => {resp.StatusCode}\n{respBody}");
}

public class ProductDto { public Guid Id { get; init; } public string Name { get; init; } public decimal Price { get; init; } public int Stock { get; init; } }
public class CustomerDto { public Guid Id { get; init; } public string Name { get; init; } public string Email { get; init; } }
public class CreateOrderItemDto { public Guid ProductId { get; set; } public int Quantity { get; set; } }
public class CreateOrderDto { public Guid CustomerId { get; set; } public CreateOrderItemDto[] Items { get; set; } }
