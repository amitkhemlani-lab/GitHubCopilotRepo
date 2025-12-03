using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DotNetSample.Core;
using Microsoft.Extensions.Logging;

namespace DotNetSample.Infrastructure
{
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
            var pending = await _db.Orders
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Pending)
                .ToListAsync();

            foreach (var order in pending)
            {
                _logger.LogInformation("Processing order {OrderId}", order.Id);
                order.Status = OrderStatus.Processing;
                foreach (var item in order.Items)
                {
                    var product = await _db.Products.FindAsync(item.ProductId);
                    if (product == null) continue;
                    if (product.Stock >= item.Quantity)
                    {
                        product.Stock -= item.Quantity;
                        item.UnitPrice = product.Price;
                    }
                    else
                    {
                        order.Status = OrderStatus.Cancelled;
                        _logger.LogWarning("Order {OrderId} cancelled due to insufficient stock for product {ProductId}", order.Id, item.ProductId);
                        break;
                    }
                }

                if (order.Status != OrderStatus.Cancelled)
                {
                    order.Status = OrderStatus.Completed;
                }

                await _db.SaveChangesAsync();
            }
        }
    }
}
