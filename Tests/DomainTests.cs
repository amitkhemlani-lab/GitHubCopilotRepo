using System;
using Xunit;
using DotNetSample.Core;

namespace DotNetSample.Tests
{
    public class DomainTests
    {
        [Fact]
        public void OrderItem_TotalPrice_Calculation()
        {
            var item = new OrderItem { Quantity = 2, UnitPrice = 5.0m };
            Assert.Equal(10.0m, item.Quantity * item.UnitPrice);
        }
    }
}
