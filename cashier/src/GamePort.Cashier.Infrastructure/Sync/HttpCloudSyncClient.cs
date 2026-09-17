using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Contracts.Sync;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamePort.Cashier.Infrastructure.Sync;

public class HttpCloudSyncClient : ICloudSyncClient
{
    private readonly HttpClient _httpClient;
    private readonly CloudOptions _options;
    private readonly ILogger<HttpCloudSyncClient> _logger;

    public HttpCloudSyncClient(
        HttpClient httpClient,
        IOptions<CloudOptions> options,
        ILogger<HttpCloudSyncClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(3, _options.TimeoutSeconds));
    }

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<CloudPushResult> PushAsync(IReadOnlyList<CloudSyncEvent> events, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new CloudPushResult(false, "Cloud is not configured.");
        }

        try
        {
            var request = new CloudPushRequest
            {
                Events = events.Select(e => new CloudSyncEventDto
                {
                    Sequence = e.Sequence,
                    EventType = e.EventType,
                    Payload = e.Payload,
                    IdempotencyKey = e.IdempotencyKey,
                    OccurredAt = e.OccurredAt
                }).ToList()
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "sync/push")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json")
            };
            AddApiKey(httpRequest);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new CloudPushResult(false, $"Cloud responded {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<CloudPushResponse>(cancellationToken: cancellationToken);
            if (body is { Accepted: false })
            {
                return new CloudPushResult(false, body.Error ?? "Cloud rejected the batch.");
            }

            return new CloudPushResult(true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            _logger.LogWarning(ex, "Cloud push failed.");
            return new CloudPushResult(false, ex.Message);
        }
    }

    public async Task<CloudPullResult> PullAsync(long cursor, int batchSize, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new CloudPullResult(false, cursor, Array.Empty<CloudChange>(), "Cloud is not configured.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"sync/pull?cursor={cursor}&batchSize={batchSize}");
            AddApiKey(httpRequest);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new CloudPullResult(false, cursor, Array.Empty<CloudChange>(), $"Cloud responded {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<CloudPullResponse>(cancellationToken: cancellationToken);
            if (body is null)
            {
                return new CloudPullResult(false, cursor, Array.Empty<CloudChange>(), "Empty cloud response.");
            }

            var changes = body.Changes
                .Select(c => new CloudChange(c.Cursor, c.ExternalId, c.MessageType, c.Payload))
                .ToList();

            return new CloudPullResult(true, body.NextCursor, changes, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            _logger.LogWarning(ex, "Cloud pull failed.");
            return new CloudPullResult(false, cursor, Array.Empty<CloudChange>(), ex.Message);
        }
    }

    private void AddApiKey(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Cloud-Api-Key", _options.ApiKey);
        }
    }
}
