using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TapInAuth.Risk.HCaptcha;

/// <summary><see cref="IRiskSignalProvider"/> backed by hCaptcha's <c>siteverify</c> endpoint.</summary>
public sealed class HCaptchaRiskSignalProvider : IRiskSignalProvider
{
    private const string SiteVerifyUrl = "https://api.hcaptcha.com/siteverify";

    private readonly HttpClient _http;
    private readonly IOptions<HCaptchaOptions> _options;
    private readonly ILogger<HCaptchaRiskSignalProvider> _logger;

    /// <summary>Construct the provider.</summary>
    public HCaptchaRiskSignalProvider(
        HttpClient http,
        IOptions<HCaptchaOptions> options,
        ILogger<HCaptchaRiskSignalProvider> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<RiskAssessment> EvaluateAsync(RiskContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var opts = _options.Value;

        string? token = null;
        context.Headers?.TryGetValue("h-captcha-response", out token);
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogInformation("hCaptcha: no token submitted for {Email}", context.Email);
            return new RiskAssessment(opts.FailureLevel, "missing_token");
        }

        try
        {
            var body = new Dictionary<string, string>
            {
                ["secret"] = opts.SecretKey,
                ["response"] = token,
            };
            if (!string.IsNullOrEmpty(context.IpAddress))
            {
                body["remoteip"] = context.IpAddress;
            }
            using var content = new FormUrlEncodedContent(body);
            using var response = await _http.PostAsync(SiteVerifyUrl, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("hCaptcha: siteverify returned {Status}", response.StatusCode);
                return new RiskAssessment(opts.FailureLevel, "siteverify_http_" + (int)response.StatusCode);
            }
            var result = await response.Content.ReadFromJsonAsync<HCaptchaResponse>(cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return new RiskAssessment(opts.FailureLevel, "siteverify_no_body");
            }
            if (!result.Success)
            {
                var reason = result.ErrorCodes is { Count: > 0 } ? string.Join(",", result.ErrorCodes) : "verification_failed";
                _logger.LogInformation("hCaptcha: verification failed for {Email}: {Reason}", context.Email, reason);
                return new RiskAssessment(opts.FailureLevel, reason);
            }
            return new RiskAssessment(RiskLevel.Low);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "hCaptcha: siteverify call failed for {Email}", context.Email);
            return new RiskAssessment(opts.FailureLevel, "siteverify_exception");
        }
    }

    private sealed class HCaptchaResponse
    {
        [JsonPropertyName("success")]      public bool Success { get; set; }
        [JsonPropertyName("error-codes")]  public List<string>? ErrorCodes { get; set; }
        [JsonPropertyName("challenge_ts")] public string? ChallengeTimestamp { get; set; }
        [JsonPropertyName("hostname")]     public string? Hostname { get; set; }
    }
}
