using System;
using System.Linq;
using DotNetSample.Core;

namespace DotNetSample.Infrastructure
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext db)
        {
            if (db.Customers.Any()) return;

            var c1 = new Customer { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com" };
            var c2 = new Customer { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com" };

            db.Customers.AddRange(c1, c2);

            var p1 = new Product { Id = Guid.NewGuid(), Name = "Widget", Price = 9.99m, Stock = 100 };
            var p2 = new Product { Id = Guid.NewGuid(), Name = "Gadget", Price = 19.99m, Stock = 50 };
            db.Products.AddRange(p1, p2);

            db.SaveChanges();
        }
    }
}
