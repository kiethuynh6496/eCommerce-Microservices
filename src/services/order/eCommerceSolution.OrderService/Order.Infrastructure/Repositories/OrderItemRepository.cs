using Microsoft.EntityFrameworkCore;
using Order.Domain.Entities;
using Order.Domain.Repositories;
using Order.Infrastructure.Data;

namespace Order.Infrastructure.Repositories;

public class OrderItemRepository : IOrderItemRepository
{
    private readonly OrderDbContext _context;

    public OrderItemRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<OrderItem?> GetByIdAsync(string id)
    {
        return await _context.OrderItems.FirstOrDefaultAsync(oi => oi.Id == id);
    }

    public async Task<IEnumerable<OrderItem>> GetByOrderIdAsync(string orderId)
    {
        return await _context.OrderItems
            .Where(oi => oi.OrderId == orderId)
            .ToListAsync();
    }

    public async Task<OrderItem> CreateAsync(OrderItem orderItem)
    {
        await _context.OrderItems.AddAsync(orderItem);
        await _context.SaveChangesAsync();
        return orderItem;
    }

    public async Task<bool> UpdateAsync(OrderItem orderItem)
    {
        _context.OrderItems.Update(orderItem);
        var result = await _context.SaveChangesAsync();
        return result > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var orderItem = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.Id == id);
        if (orderItem == null) return false;

        _context.OrderItems.Remove(orderItem);
        var result = await _context.SaveChangesAsync();
        return result > 0;
    }

    public async Task<bool> DeleteByOrderIdAsync(string orderId)
    {
        var orderItems = await _context.OrderItems
            .Where(oi => oi.OrderId == orderId)
            .ToListAsync();

        if (!orderItems.Any()) return true;

        _context.OrderItems.RemoveRange(orderItems);
        var result = await _context.SaveChangesAsync();
        return result > 0;
    }
}