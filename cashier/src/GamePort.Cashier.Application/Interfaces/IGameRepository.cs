using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(Guid id);
    Task<IEnumerable<Game>> GetAllAsync();
    Task<Game> CreateAsync(Game game);
    Task UpdateAsync(Game game);
    Task DeleteAsync(Guid id);
}
