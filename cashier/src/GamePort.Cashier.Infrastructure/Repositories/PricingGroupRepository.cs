using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class PricingGroupRepository : IPricingGroupRepository
{
    private readonly CashierDbContext _context;

    public PricingGroupRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<PricingGroup?> GetByIdAsync(Guid id)
    {
        return await _context.PricingGroups
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);
    }

    public async Task<IEnumerable<PricingGroup>> GetAllAsync()
    {
        return await _context.PricingGroups
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<PricingGroup> CreateAsync(PricingGroup group)
    {
        group.Id = group.Id == Guid.Empty ? Guid.NewGuid() : group.Id;
        group.CreatedAt = DateTime.UtcNow;
        _context.PricingGroups.Add(group);
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task UpdateAsync(PricingGroup group)
    {
        group.UpdatedAt = DateTime.UtcNow;
        _context.PricingGroups.Update(group);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var group = await _context.PricingGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group != null)
        {
            group.IsDeleted = true;
            group.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasDevicesAsync(Guid pricingGroupId)
    {
        return await _context.Devices.AnyAsync(d => d.PricingGroupId == pricingGroupId && !d.IsDeleted);
    }

    public async Task<bool> HasPricingRulesAsync(Guid pricingGroupId)
    {
        return await _context.PricingRules.AnyAsync(r => r.PricingGroupId == pricingGroupId && !r.IsDeleted);
    }
}
