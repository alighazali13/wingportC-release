using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IPricingGroupRepository
{
    Task<PricingGroup?> GetByIdAsync(Guid id);
    Task<IEnumerable<PricingGroup>> GetAllAsync();
    Task<PricingGroup> CreateAsync(PricingGroup group);
    Task UpdateAsync(PricingGroup group);
    Task DeleteAsync(Guid id);
    Task<bool> HasDevicesAsync(Guid pricingGroupId);
    Task<bool> HasPricingRulesAsync(Guid pricingGroupId);
}
