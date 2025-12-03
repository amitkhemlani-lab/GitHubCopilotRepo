using Microsoft.AspNetCore.Mvc;
using DotNetSample.Core;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace DotNetSample.Api.Controllers
{
    /// <summary>
    /// Controller that manages orders and order-related endpoints.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        /// <summary>
        /// Repository for order persistence operations.
        /// </summary>
        private readonly IRepository<Order> _orderRepo;

        /// <summary>
        /// Repository for order item persistence operations.
        /// </summary>
        private readonly IRepository<OrderItem> _itemRepo;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrdersController"/> class.
        /// </summary>
        /// <param name="orderRepo">Repository used to access orders.</param>
        /// <param name="itemRepo">Repository used to access order items.</param>
        public OrdersController(IRepository<Order> orderRepo, IRepository<OrderItem> itemRepo)
        {
            _orderRepo = orderRepo;
            _itemRepo = itemRepo;
        }

        /// <summary>
        /// Returns all orders.
        /// </summary>
        /// <returns>A collection of <see cref="Order"/>.</returns>
        [HttpGet]
        public async Task<IEnumerable<Order>> Get() => await _orderRepo.ListAsync();

        /// <summary>
        /// Gets a single order by identifier.
        /// </summary>
        /// <param name="id">Order identifier.</param>
        /// <returns>The <see cref="Order"/> when found; otherwise 404 Not Found.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> Get(Guid id)
        {
            var o = await _orderRepo.GetAsync(id);
            if (o == null) return NotFound();
            return o;
        }

        /// <summary>
        /// Returns a page of orders.
        /// </summary>
        /// <param name="pageNumber">1-based page number.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <returns>Paged list of orders.</returns>
        [HttpGet("paged")]
        public async Task<ActionResult<IEnumerable<Order>>> GetPaged(int pageNumber = 1, int pageSize = 10)
        {               
            var orders = await _orderRepo.ListAsync();
            var pagedOrders = orders
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Ok(pagedOrders);
        }   

        /// <summary>
        /// Reprocesses orders currently in Pending state by marking them Processing.
        /// </summary>
        /// <remarks>
        /// This endpoint iterates pending orders, updates their status to Processing,
        /// and persists the changes. Use with caution in production scenarios.
        /// </remarks>
        /// <returns>An object containing the number of reprocessed orders.</returns>
        [HttpPost("reprocess-pending")]
        public async Task<ActionResult> ReprocessPendingOrders()
        {
            var orders = await _orderRepo.ListAsync();
            var pendingOrders = orders.Where(o => o.Status == OrderStatus.Pending).ToList();  
            foreach (var order in pendingOrders)
            {
                order.Status = OrderStatus.Processing;
                await _orderRepo.UpdateAsync(order);
            }                       
            return Ok(new { ReprocessedCount = pendingOrders.Count });
        }  


        /// <summary>
        /// Gets orders for a specific customer.
        /// </summary>
        /// <param name="customerId">Customer identifier to filter orders by.</param>
        /// <returns>Orders that belong to the specified customer.</returns>
        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetByCustomer(Guid customerId)
        {
            var orders = await _orderRepo.ListAsync();
            var customerOrders = orders.Where(o => o.CustomerId == customerId).ToList();
            return Ok(customerOrders);
        }                       


        /// <summary>
        /// Gets orders within the specified inclusive date range with pagination.
        /// </summary>
        /// <param name="startDate">Inclusive start date (ISO-8601).</param>
        /// <param name="endDate">Inclusive end date (ISO-8601).</param>
        /// <param name="pageNumber">Page number (1-based).</param>
        /// <param name="pageSize">Page size.</param>
        /// <returns>Paged orders that fall within the date range.</returns>
        [HttpGet("daterange")]
        public async Task<ActionResult<IEnumerable<Order>>> GetByDateRange(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 10)
        {
            var orders = await _orderRepo.ListAsync();
            var filteredOrders = orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Ok(filteredOrders);
        }

      
        /// <summary>
        /// Creates a new order. The server assigns Id, CreatedAt and initial Status.
        /// </summary>
        /// <param name="order">
        /// Order payload. Provide <c>CustomerId</c> and <c>Items</c> (each item needs <c>ProductId</c> and <c>Quantity</c>).
        /// </param>
        /// <returns>201 Created with the created order resource.</returns>
        [HttpPost]
        public async Task<ActionResult> Post(Order order)
        {
            order.Id = Guid.NewGuid();
            order.CreatedAt = DateTime.UtcNow;
            order.Status = OrderStatus.Pending;
            foreach (var item in order.Items)
            {
                item.Id = Guid.NewGuid();
                item.OrderId = order.Id;
            }

            await _orderRepo.AddAsync(order);
            return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
        }
    }
}
