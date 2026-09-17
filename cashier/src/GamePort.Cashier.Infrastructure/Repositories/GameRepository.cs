using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePort.Cashier.Infrastructure.Repositories;

public class GameRepository : IGameRepository
{
    private readonly CashierDbContext _context;

    public GameRepository(CashierDbContext context)
    {
        _context = context;
    }

    public async Task<Game?> GetByIdAsync(Guid id)
    {
        return await _context.Games.FindAsync(id);
    }

    public async Task<IEnumerable<Game>> GetAllAsync()
    {
        return await _context.Games.ToListAsync();
    }

    public async Task<Game> CreateAsync(Game game)
    {
        game.Id = Guid.NewGuid();
        game.CreatedAt = DateTime.UtcNow;
        _context.Games.Add(game);
        await _context.SaveChangesAsync();
        return game;
    }

    public async Task UpdateAsync(Game game)
    {
        game.UpdatedAt = DateTime.UtcNow;
        _context.Games.Update(game);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game is not null)
        {
            game.IsDeleted = true;
            game.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
