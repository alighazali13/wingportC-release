using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Interfaces;

public interface IPricingRuleRepository
{
    Task<PricingRule?> GetActiveByDeviceTypeAsync(DeviceType deviceType);
    Task<IEnumerable<PricingRule>> GetAllAsync();
    Task<PricingRule> AddAsync(PricingRule rule);
    Task UpdateAsync(PricingRule rule);
}
