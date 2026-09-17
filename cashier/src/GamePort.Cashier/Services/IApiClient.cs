using GamePort.Cashier.Contracts.Requests;
using GamePort.Cashier.Contracts.Responses;

namespace GamePort.Cashier.Services;

public class AuditLogItem
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string? ActorName { get; set; }
    public string Source { get; set; } = string.Empty;
}

public interface IApiClient
{
    Task<ApiResult<LoginResponse>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task<ApiResult<List<DeviceResponse>>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<RegisteredDeviceResponse>> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<bool>> SendDeviceCommandAsync(Guid deviceId, string commandType, string? payload = null, CancellationToken cancellationToken = default);

    Task<ApiResult<List<SessionResponse>>> GetActiveSessionsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<SessionResponse>> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<bool>> EndSessionAsync(Guid sessionId, string? reason = null, CancellationToken cancellationToken = default);

    Task<ApiResult<List<CustomerResponse>>> SearchCustomersAsync(string? query, CancellationToken cancellationToken = default);
    Task<ApiResult<CustomerResponse>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<WalletResponse>> GetWalletAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<ApiResult<decimal>> RechargeWalletAsync(RechargeWalletRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<List<ReservationResponse>>> GetUpcomingReservationsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<List<GameResponse>>> GetGamesAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<List<AuditLogItem>>> GetRecentAuditAsync(int limit = 20, CancellationToken cancellationToken = default);
    Task<ApiResult<SyncStatusResponse>> GetSyncStatusAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<bool>> RunSyncAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<FinancialReportResponse>> GetFinancialReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    Task<ApiResult<SystemInfoResponse>> GetSystemInfoAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<List<BackupItem>>> GetBackupsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<string>> CreateBackupAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<List<EmployeeItem>>> GetEmployeesAsync(CancellationToken cancellationToken = default);
}

public class BackupItem
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EmployeeItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
