using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class PricingRuleRepository : IPricingRuleRepository
{
    private readonly CashierDbContext _context;

    public PricingRuleRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<PricingRule?> GetActiveByDeviceTypeAsync(DeviceType deviceType)
    {
        return await _context.PricingRules
            .Where(r => r.DeviceType == deviceType && r.IsActive)
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PricingRule>> GetAllAsync()
    {
        return await _context.PricingRules
            .OrderByDescending(r => r.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<PricingRule> AddAsync(PricingRule rule)
    {
        rule.Id = Guid.NewGuid();
        rule.CreatedAt = DateTime.UtcNow;
        _context.PricingRules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    public async Task UpdateAsync(PricingRule rule)
    {
        rule.UpdatedAt = DateTime.UtcNow;
        _context.PricingRules.Update(rule);
        await _context.SaveChangesAsync();
    }
}
