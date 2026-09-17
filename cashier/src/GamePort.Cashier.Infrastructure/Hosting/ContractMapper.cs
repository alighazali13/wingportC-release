using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Infrastructure.Hosting;

public static class ContractMapper
{
    public static DeviceResponse ToResponse(this Device device) => new()
    {
        Id = device.Id,
        Name = device.Name,
        Type = device.Type.ToString(),
        Status = device.Status.ToString(),
        IpAddress = device.IpAddress,
        MacAddress = device.MacAddress,
        ClientIdentity = device.ClientIdentity,
        IsConnected = device.IsConnected,
        LastHeartbeat = device.LastHeartbeat,
        CurrentGameId = device.CurrentGameId
    };

    public static RegisteredDeviceResponse ToRegisteredResponse(this Device device, string clientSecret) => new()
    {
        Id = device.Id,
        Name = device.Name,
        Type = device.Type.ToString(),
        Status = device.Status.ToString(),
        IpAddress = device.IpAddress,
        MacAddress = device.MacAddress,
        ClientIdentity = device.ClientIdentity,
        IsConnected = device.IsConnected,
        LastHeartbeat = device.LastHeartbeat,
        CurrentGameId = device.CurrentGameId,
        ClientSecret = clientSecret
    };

    public static SessionResponse ToResponse(this Session session) => new()
    {
        Id = session.Id,
        CustomerId = session.CustomerId,
        DeviceId = session.DeviceId,
        DeviceType = session.DeviceType.ToString(),
        StartTime = session.StartTime,
        PlannedEndTime = session.PlannedEndTime,
        ActualEndTime = session.ActualEndTime,
        PausedAt = session.PausedAt,
        TotalPausedSeconds = session.TotalPausedSeconds,
        PriceAtStart = session.PriceAtStart,
        Status = session.Status.ToString()
    };

    public static CustomerResponse ToResponse(this Customer customer, decimal? walletBalance = null) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        PhoneNumber = customer.PhoneNumber,
        MaxConcurrentSessions = customer.MaxConcurrentSessions,
        WalletBalance = walletBalance,
        CreatedAt = customer.CreatedAt
    };

    public static WalletResponse ToResponse(this Wallet wallet) => new()
    {
        WalletId = wallet.Id,
        CustomerId = wallet.CustomerId,
        Balance = wallet.Balance
    };

    public static WalletTransactionResponse ToResponse(this WalletTransaction transaction) => new()
    {
        Id = transaction.Id,
        Amount = transaction.Amount,
        BalanceAfter = transaction.BalanceAfter,
        Type = transaction.Type,
        Description = transaction.Description,
        RelatedEntityId = transaction.RelatedEntityId,
        RelatedEntityType = transaction.RelatedEntityType,
        CreatedAt = transaction.CreatedAt
    };

    public static ReservationResponse ToResponse(this Reservation reservation) => new()
    {
        Id = reservation.Id,
        CustomerId = reservation.CustomerId,
        CustomerName = reservation.Customer?.Name,
        DeviceId = reservation.DeviceId,
        DeviceName = reservation.Device?.Name,
        DeviceType = reservation.DeviceType.ToString(),
        StartTime = reservation.StartTime,
        EndTime = reservation.EndTime,
        Status = reservation.Status.ToString(),
        Source = reservation.Source,
        Notes = reservation.Notes,
        ConvertedSessionId = reservation.ConvertedSessionId
    };

    public static GameResponse ToResponse(this Game game) => new()
    {
        Id = game.Id,
        Name = game.Name,
        Version = game.Version,
        ExecutablePath = game.ExecutablePath,
        IconPath = game.IconPath,
        IsActive = game.IsActive,
        SupportedDeviceType = game.SupportedDeviceType.ToString(),
        LaunchConfiguration = game.LaunchConfiguration
    };

    public static DeviceGameResponse ToResponse(this DeviceGame deviceGame) => new()
    {
        DeviceId = deviceGame.DeviceId,
        GameId = deviceGame.GameId,
        GameName = deviceGame.Game?.Name ?? string.Empty,
        IsInstalled = deviceGame.IsInstalled,
        InstalledVersion = deviceGame.InstalledVersion,
        IsActive = deviceGame.Game?.IsActive ?? false
    };
}
