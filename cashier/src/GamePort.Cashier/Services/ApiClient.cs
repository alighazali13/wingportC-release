using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GamePort.Cashier.Contracts.Common;
using GamePort.Cashier.Contracts.Requests;
using GamePort.Cashier.Contracts.Responses;

namespace GamePort.Cashier.Services;

public class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ISessionContext _session;

    public ApiClient(HttpClient httpClient, ISessionContext session)
    {
        _httpClient = httpClient;
        _session = session;
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        return await PostAsync<LoginResponse>("api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        }, cancellationToken, authenticate: false);
    }

    public Task<ApiResult<List<DeviceResponse>>> GetDevicesAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<DeviceResponse>>("api/devices", cancellationToken);

    public Task<ApiResult<RegisteredDeviceResponse>> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken cancellationToken = default)
        => PostAsync<RegisteredDeviceResponse>("api/devices", request, cancellationToken);

    public Task<ApiResult<bool>> SendDeviceCommandAsync(Guid deviceId, string commandType, string? payload = null, CancellationToken cancellationToken = default)
        => PostAsync<bool>($"api/devices/{deviceId}/commands", new SendDeviceCommandRequest
        {
            CommandType = commandType,
            Payload = payload
        }, cancellationToken);

    public Task<ApiResult<List<SessionResponse>>> GetActiveSessionsAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<SessionResponse>>("api/sessions/active", cancellationToken);

    public Task<ApiResult<SessionResponse>> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
        => PostAsync<SessionResponse>("api/sessions", request, cancellationToken);

    public Task<ApiResult<bool>> EndSessionAsync(Guid sessionId, string? reason = null, CancellationToken cancellationToken = default)
        => PostAsync<bool>("api/sessions/end", new EndSessionRequest
        {
            SessionId = sessionId,
            Reason = reason
        }, cancellationToken);

    public Task<ApiResult<List<CustomerResponse>>> SearchCustomersAsync(string? query, CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(query)
            ? "api/customers"
            : $"api/customers?query={Uri.EscapeDataString(query)}";

        return GetAsync<List<CustomerResponse>>(url, cancellationToken);
    }

    public Task<ApiResult<CustomerResponse>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default)
        => PostAsync<CustomerResponse>("api/customers", request, cancellationToken);

    public Task<ApiResult<WalletResponse>> GetWalletAsync(Guid customerId, CancellationToken cancellationToken = default)
        => GetAsync<WalletResponse>($"api/wallets/{customerId}", cancellationToken);

    public async Task<ApiResult<decimal>> RechargeWalletAsync(RechargeWalletRequest request, CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<JsonElement>("api/wallets/recharge", request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiResult<decimal>.Failure(result.Error ?? "شارژ ناموفق بود.", result.IsUnauthorized);
        }

        return result.Data.TryGetProperty("balance", out var balance) || result.Data.TryGetProperty("Balance", out balance)
            ? ApiResult<decimal>.Success(balance.GetDecimal())
            : ApiResult<decimal>.Failure("پاسخ نامعتبر از سرور.");
    }

    public Task<ApiResult<List<ReservationResponse>>> GetUpcomingReservationsAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<ReservationResponse>>("api/reservations/upcoming", cancellationToken);

    public Task<ApiResult<List<GameResponse>>> GetGamesAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<GameResponse>>("api/games", cancellationToken);

    public Task<ApiResult<List<AuditLogItem>>> GetRecentAuditAsync(int limit = 20, CancellationToken cancellationToken = default)
        => GetAsync<List<AuditLogItem>>($"api/audit/recent?limit={limit}", cancellationToken);

    public Task<ApiResult<SyncStatusResponse>> GetSyncStatusAsync(CancellationToken cancellationToken = default)
        => GetAsync<SyncStatusResponse>("api/sync/status", cancellationToken);

    public Task<ApiResult<bool>> RunSyncAsync(CancellationToken cancellationToken = default)
        => PostAsync<bool>("api/sync/run", new { }, cancellationToken);

    public Task<ApiResult<FinancialReportResponse>> GetFinancialReportAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (from.HasValue)
        {
            query.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        }

        if (to.HasValue)
        {
            query.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        }

        var url = query.Count == 0
            ? "api/reports/financial"
            : $"api/reports/financial?{string.Join('&', query)}";

        return GetAsync<FinancialReportResponse>(url, cancellationToken);
    }

    public Task<ApiResult<SystemInfoResponse>> GetSystemInfoAsync(CancellationToken cancellationToken = default)
        => GetAsync<SystemInfoResponse>("api/system/info", cancellationToken);

    public Task<ApiResult<List<BackupItem>>> GetBackupsAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<BackupItem>>("api/system/backups", cancellationToken);

    public async Task<ApiResult<string>> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<JsonElement>("api/system/backup", new { }, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiResult<string>.Failure(result.Error ?? "بکاپ ناموفق بود.", result.IsUnauthorized);
        }

        return result.Data.TryGetProperty("filePath", out var path) || result.Data.TryGetProperty("FilePath", out path)
            ? ApiResult<string>.Success(path.GetString() ?? string.Empty)
            : ApiResult<string>.Success(string.Empty);
    }

    public Task<ApiResult<List<EmployeeItem>>> GetEmployeesAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<EmployeeItem>>("api/employees", cancellationToken);

    private Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken cancellationToken)
        => SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);

    private Task<ApiResult<T>> PostAsync<T>(string url, object body, CancellationToken cancellationToken, bool authenticate = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        return SendAsync<T>(request, cancellationToken, authenticate);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken, bool authenticate = true)
    {
        try
        {
            if (authenticate && _session.IsAuthenticated)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
            }

            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return ApiResult<T>.Failure("دسترسی غیرمجاز. لطفاً دوباره وارد شوید.", unauthorized: true);
                }

                BaseResponse<T>? payload;
                try
                {
                    payload = await response.Content.ReadFromJsonAsync<BaseResponse<T>>(JsonOptions, cancellationToken);
                }
                catch (JsonException)
                {
                    return ApiResult<T>.Failure($"پاسخ نامعتبر از سرور ({(int)response.StatusCode}).");
                }

                if (payload is null)
                {
                    return ApiResult<T>.Failure("پاسخ خالی از سرور.");
                }

                if (!response.IsSuccessStatusCode || !payload.Success)
                {
                    var message = payload.Message
                                  ?? payload.Errors.FirstOrDefault()
                                  ?? $"خطا در ارتباط با سرور ({(int)response.StatusCode}).";
                    return ApiResult<T>.Failure(message);
                }

                if (payload.Data is null)
                {
                    return ApiResult<T>.Success(default!);
                }

                return ApiResult<T>.Success(payload.Data);
            }
        }
        catch (OperationCanceledException)
        {
            return ApiResult<T>.Failure("درخواست لغو شد.");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Failure($"اتصال به سرور محلی برقرار نشد: {ex.Message}");
        }
    }
}
