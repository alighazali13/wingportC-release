using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.Interfaces;

public interface IOutboxRepository
{
    Task<OutboxEvent> AddAsync(OutboxEvent outboxEvent);
    Task<IEnumerable<OutboxEvent>> GetPendingAsync(int batchSize);
    Task<IEnumerable<OutboxEvent>> GetDueAsync(DateTime referenceTime, int batchSize);
    Task<int> CountByStatusAsync(OutboxStatus status);
    Task UpdateAsync(OutboxEvent outboxEvent);
}
