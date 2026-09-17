using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Interfaces;

public interface IInboxRepository
{
    Task<bool> ExistsAsync(string externalId);
    Task<InboxMessage> AddAsync(InboxMessage message);
    Task<int> CountByStatusAsync(InboxStatus status);
    Task UpdateAsync(InboxMessage message);
}
