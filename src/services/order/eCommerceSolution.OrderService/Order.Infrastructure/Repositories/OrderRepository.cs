using Microsoft.EntityFrameworkCore;
using Order.Domain.Entities;
using Order.Domain.Repositories;
using Order.Infrastructure.Data;

namespace Order.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public OrderRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<Order.Domain.Entities.Order?> GetByIdAsync(string id)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order != null)
        {
            var items = await _context.OrderItems
                .Where(oi => oi.OrderId == id)
                .ToListAsync();
            order.OrderItems = items;
        }
        return order;
    }

    public async Task<IEnumerable<Order.Domain.Entities.Order>> GetByUserIdAsync(string userId)
    {
        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .ToListAsync();

        foreach (var order in orders)
        {
            var items = await _context.OrderItems
                .Where(oi => oi.OrderId == order.Id)
                .ToListAsync();
            order.OrderItems = items;
        }

        return orders;
    }

    public async Task<Order.Domain.Entities.Order> CreateAsync(Order.Domain.Entities.Order order)
    {
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<bool> UpdateAsync(Order.Domain.Entities.Order order)
    {
        _context.Orders.Update(order);
        var result = await _context.SaveChangesAsync();
        return result > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return false;

        _context.Orders.Remove(order);
        var result = await _context.SaveChangesAsync();
        return result > 0;
    }

    public async Task<IEnumerable<Order.Domain.Entities.Order>> GetByStatusAsync(OrderStatus status)
    {
        var orders = await _context.Orders
            .Where(o => o.Status == status)
            .ToListAsync();

        foreach (var order in orders)
        {
            var items = await _context.OrderItems
                .Where(oi => oi.OrderId == order.Id)
                .ToListAsync();
            order.OrderItems = items;
        }

        return orders;
    }
}