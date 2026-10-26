namespace GamePort.Cashier.Services.Updates;

/// <summary>
/// Application update service. Abstraction over Velopack so the rest
/// of the application does not depend on Velopack types directly.
/// </summary>
public interface IAppUpdateService
{
    /// <summary>Current installed application version.</summary>
    string CurrentVersion { get; }

    /// <summary>Latest known available version, or null if none.</summary>
    string? AvailableVersion { get; }

    /// <summary>Current update state.</summary>
    UpdateState State { get; }

    /// <summary>Raised when State or AvailableVersion changes.</summary>
    event Action? StateChanged;

    /// <summary>
    /// Checks for updates in the background. Non-blocking.
    /// Never throws for network failures — sets State=Failed instead.
    /// </summary>
    Task CheckForUpdatesAsync();

    /// <summary>
    /// Downloads the available update in the background.
    /// Non-blocking. Progress reported via DownloadProgress.
    /// </summary>
    Task DownloadUpdateAsync();

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// Only call when State == Ready.
    /// </summary>
    void ApplyUpdateAndRestart();
}
