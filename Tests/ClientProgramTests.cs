using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;

namespace Tests
{
    public class ClientProgramTests
    {
        // DTOs mirrored from Client/Program.cs
        public class ProductDto { public Guid Id { get; init; } public string Name { get; init; } public decimal Price { get; init; } public int Stock { get; init; } }
        public class CustomerDto { public Guid Id { get; init; } public string Name { get; init; } public string Email { get; init; } }
        public class CreateOrderItemDto { public Guid ProductId { get; set; } public int Quantity { get; set; } }
        public class CreateOrderDto { public Guid CustomerId { get; set; } public CreateOrderItemDto[] Items { get; set; } }

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
            public List<HttpRequestMessage> Requests { get; } = new();

            public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            {
                _responder = responder ?? throw new ArgumentNullException(nameof(responder));
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return await _responder(request, cancellationToken).ConfigureAwait(false);
            }
        }

        private static HttpResponseMessage JsonResponse<T>(T obj, HttpStatusCode status = HttpStatusCode.OK)
        {
            var json = JsonSerializer.Serialize(obj);
            var resp = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return resp;
        }

        [Fact]
        public async Task PostIsMadeWhenProductsAndCustomersExist()
        {
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var products = new[] { new ProductDto { Id = productId, Name = "Product A", Price = 9.99m, Stock = 5 } };
            var customers = new[] { new CustomerDto { Id = customerId, Name = "Alice", Email = "alice@example.com" } };

            CreateOrderDto capturedOrder = null;
            int postCount = 0;

            var handler = new FakeHttpMessageHandler(async (req, ct) =>
            {
                if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath == "/api/products")
                    return JsonResponse(products);

                if (req.Method == HttpMethod.Get && req.RequestUri.AbsolutePath == "/api/customers")
                    return JsonResponse(customers);

                if (req.Method == HttpMethod.Post && req.RequestUri.AbsolutePath == "/api/orders")
                {
                    postCount++;
                    var body = await req.Content.ReadAsStringAsync().ConfigureAwait(false);
                    capturedOrder = JsonSerializer.Deserialize<CreateOrderDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return JsonResponse(new { ok = true }, HttpStatusCode.Created);
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5181") };

            var fetchedProducts = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
            var fetchedCustomers = await client.GetFromJsonAsync<List<CustomerDto>>("/api/customers");

            if (fetchedCustomers?.Count > 0 && fetchedProducts?.Count > 0)
            {
                var order = new CreateOrderDto
                {
                    CustomerId = fetchedCustomers[0].Id,
                    Items = new[] { new CreateOrderItemDto { ProductId = fetchedProducts[0].Id, Quantity = 1 } }
                };

                await client.PostAsJsonAsync("/api/orders", order);
            }

            Assert.Equal(1, postCount);
            Assert.NotNull(capturedOrder);
            Assert.Equal(customerId, capturedOrder.CustomerId);
            Assert.Single(capturedOrder.Items);
            Assert.Equal(productId, capturedOrder.Items[0].ProductId);
            Assert.Equal(1, capturedOrder.Items[0].Quantity);
        }

        [Fact]
        public async Task NoPostWhenMissingData()
        {
            var handler = new FakeHttpMessageHandler(async (req, ct) =>
            {
                if (req.Method == HttpMethod.Get && (req.RequestUri.AbsolutePath == "/api/products" || req.RequestUri.AbsolutePath == "/api/customers"))
                    return JsonResponse(Array.Empty<object>());

                // If POST occurs, return 500 so test would notice — but tests assert POST not made by tracking requests
                if (req.Method == HttpMethod.Post)
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5181") };

            var fetchedProducts = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
            var fetchedCustomers = await client.GetFromJsonAsync<List<CustomerDto>>("/api/customers");

            if (fetchedCustomers?.Count > 0 && fetchedProducts?.Count > 0)
            {
                var order = new CreateOrderDto
                {
                    CustomerId = fetchedCustomers[0].Id,
                    Items = new[] { new CreateOrderItemDto { ProductId = fetchedProducts[0].Id, Quantity = 1 } }
                };

                await client.PostAsJsonAsync("/api/orders", order);
            }

            // Ensure no POST request was seen
            Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post && r.RequestUri.AbsolutePath == "/api/orders");
        }
    }
}
