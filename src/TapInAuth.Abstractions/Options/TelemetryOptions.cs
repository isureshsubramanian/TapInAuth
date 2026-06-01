namespace TapInAuth.Options;

/// <summary>Telemetry configuration. Off by default — see <see cref="Enabled"/>.</summary>
public sealed class TelemetryOptions
{
    /// <summary>Whether anonymous, aggregate telemetry is sent to the project. Off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>The collector endpoint URL. Defaults to the project's official collector.</summary>
    public string EndpointUrl { get; set; } = "https://telemetry.tapinauth.io/ingest";

    /// <summary>How often the collected counters are flushed.</summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromHours(24);
}
