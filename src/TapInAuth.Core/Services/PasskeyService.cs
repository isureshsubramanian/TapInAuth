using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TapInAuth.Auditing;
using TapInAuth.Credentials;
using TapInAuth.Options;
using TapInAuth.Stores;

namespace TapInAuth.Core.Services;

/// <summary>
/// Orchestrates WebAuthn (FIDO2) passkey ceremonies. Wraps <see cref="Fido2NetLib"/> for the protocol
/// layer and adds TapInAuth concerns on top: tenant scoping, credential storage, audit, claims principal.
/// </summary>
/// <remarks>
/// Ceremony state (the challenge plus its associated options) is held in an HMAC-signed cookie by
/// <c>TapInAuth.AspNetCore</c>'s endpoint layer — <em>this</em> service is stateless beyond its DI.
/// </remarks>
public sealed class PasskeyService
{
    private readonly IFido2 _fido2;
    private readonly ITapInAuthUserStore _userStore;
    private readonly ICredentialStore _credentialStore;
    private readonly IAuditSink _audit;
    private readonly TapInAuthClaimsPrincipalFactory _principalFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<TapInAuthOptions> _options;
    private readonly ILogger<PasskeyService> _logger;

    /// <summary>Construct a passkey service.</summary>
    public PasskeyService(
        IFido2 fido2,
        ITapInAuthUserStore userStore,
        ICredentialStore credentialStore,
        IAuditSink audit,
        TapInAuthClaimsPrincipalFactory principalFactory,
        TimeProvider timeProvider,
        IOptions<TapInAuthOptions> options,
        ILogger<PasskeyService> logger)
    {
        _fido2 = fido2 ?? throw new ArgumentNullException(nameof(fido2));
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _principalFactory = principalFactory ?? throw new ArgumentNullException(nameof(principalFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start a passkey registration ceremony for an authenticated user.
    /// Returns the <see cref="CredentialCreateOptions"/> the client passes to <c>navigator.credentials.create()</c>,
    /// plus the same options serialized so the caller can stash them in an HMAC-signed cookie.
    /// </summary>
    public CredentialCreateOptions StartRegistration(TenantContext tenant, TapInAuthUser user, string? friendlyDeviceName = null)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(user);

        var fidoUser = new Fido2User
        {
            DisplayName = user.DisplayName ?? user.Email,
            Name = user.Email,
            Id = user.Id.ToByteArray(),
        };

        // Force discoverable credentials and platform/cross-platform authenticators; require user-verification.
        var authenticatorSelection = new AuthenticatorSelection
        {
            ResidentKey = ResidentKeyRequirement.Required,
            UserVerification = UserVerificationRequirement.Preferred,
            AuthenticatorAttachment = null,    // let the client pick
        };

        var existing = _credentialStore.ListForUserAsync(tenant, user.Id).GetAwaiter().GetResult();
        var exclude = existing
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = exclude,
            AuthenticatorSelection = authenticatorSelection,
            AttestationPreference = AttestationConveyancePreference.None,
            Extensions = new AuthenticationExtensionsClientInputs
            {
                CredProps = true,
            },
        });

        _logger.LogInformation("Passkey registration ceremony started for {UserId} in tenant {Tenant}", user.Id, tenant.Id);
        return options;
    }

    /// <summary>
    /// Complete a passkey registration. Verifies the attestation, stores the credential, and emits audit.
    /// </summary>
    public async Task<Credential?> CompleteRegistrationAsync(
        TenantContext tenant,
        TapInAuthUser user,
        AuthenticatorAttestationRawResponse rawResponse,
        CredentialCreateOptions originalOptions,
        string? deviceName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(rawResponse);
        ArgumentNullException.ThrowIfNull(originalOptions);

        try
        {
            // The IsCredentialIdUniqueToUserAsyncDelegate lets us refuse a credential that's already registered.
            var result = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = rawResponse,
                OriginalOptions = originalOptions,
                IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                {
                    var found = await _credentialStore.FindByCredentialIdAsync(tenant, args.CredentialId, ct).ConfigureAwait(false);
                    return found is null;
                },
            }, cancellationToken).ConfigureAwait(false);

            var now = _timeProvider.GetUtcNow();
            var credential = new Credential
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = user.Id,
                CredentialId = result.Id,
                PublicKey = result.PublicKey,
                SignatureCounter = result.SignCount,
                Aaguid = result.AaGuid == Guid.Empty ? null : result.AaGuid,
                DeviceName = deviceName,
                CreatedAt = now,
            };
            await _credentialStore.SaveAsync(credential, cancellationToken).ConfigureAwait(false);

            await _audit.WriteAsync(new AuditEvent(
                now, tenant.Id, AuditEventType.CredentialRegistered,
                user.Id.ToString(), user.Email, null, null, deviceName, true),
                cancellationToken).ConfigureAwait(false);

            return credential;
        }
        catch (Fido2VerificationException ex)
        {
            _logger.LogWarning(ex, "Passkey registration failed verification for {UserId}", user.Id);
            await _audit.WriteAsync(new AuditEvent(
                _timeProvider.GetUtcNow(), tenant.Id, AuditEventType.CredentialRegistered,
                user.Id.ToString(), user.Email, null, null, ex.Message, false),
                cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>
    /// Start a passkey assertion (sign-in) ceremony. Returns options for <c>navigator.credentials.get()</c>.
    /// For discoverable credentials, no email is required; the authenticator picks an account.
    /// </summary>
    public AssertionOptions StartAssertion(TenantContext tenant, TapInAuthUser? user = null)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var allowed = new List<PublicKeyCredentialDescriptor>();
        if (user is not null)
        {
            var existing = _credentialStore.ListForUserAsync(tenant, user.Id).GetAwaiter().GetResult();
            allowed.AddRange(existing.Select(c => new PublicKeyCredentialDescriptor(c.CredentialId)));
        }

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowed,
            UserVerification = UserVerificationRequirement.Preferred,
        });

        return options;
    }

    /// <summary>
    /// Complete a passkey assertion. Verifies the signature, looks up the credential, updates its counter,
    /// and returns the matching <see cref="TapInAuthUser"/> or null on failure.
    /// </summary>
    public async Task<TapInAuthUser?> CompleteAssertionAsync(
        TenantContext tenant,
        AuthenticatorAssertionRawResponse rawResponse,
        AssertionOptions originalOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(rawResponse);
        ArgumentNullException.ThrowIfNull(originalOptions);

        // In Fido2.AspNet 4.x, rawResponse.Id is a base64url-encoded string; decode it
        // to the raw credential-id bytes our store keys on.
        var credentialIdBytes = TapInAuth.Core.Security.TokenGenerator.Base64UrlDecode(rawResponse.Id);

        var credential = await _credentialStore.FindByCredentialIdAsync(tenant, credentialIdBytes, cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            _logger.LogWarning("Passkey assertion: unknown credential id {Cid} in tenant {Tenant}", rawResponse.Id, tenant.Id);
            return null;
        }

        try
        {
            var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = rawResponse,
                OriginalOptions = originalOptions,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = credential.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = async (args, ct) =>
                {
                    // The user handle returned by the client must match the credential's owner.
                    var handle = new Guid(args.UserHandle);
                    return handle == credential.UserId;
                },
            }, cancellationToken).ConfigureAwait(false);

            var now = _timeProvider.GetUtcNow();
            await _credentialStore.UpdateAfterUseAsync(tenant, credential.Id, result.SignCount, now, cancellationToken).ConfigureAwait(false);

            var user = await _userStore.FindByIdAsync(tenant, credential.UserId, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                _logger.LogWarning("Passkey assertion: credential's user {UserId} missing in tenant {Tenant}", credential.UserId, tenant.Id);
                return null;
            }

            await _audit.WriteAsync(new AuditEvent(
                now, tenant.Id, AuditEventType.CredentialAsserted,
                user.Id.ToString(), user.Email, null, null, credential.DeviceName, true),
                cancellationToken).ConfigureAwait(false);

            return user;
        }
        catch (Fido2VerificationException ex)
        {
            _logger.LogWarning(ex, "Passkey assertion failed verification (credential {Cid})", credential.Id);
            await _audit.WriteAsync(new AuditEvent(
                _timeProvider.GetUtcNow(), tenant.Id, AuditEventType.CredentialAsserted,
                credential.UserId.ToString(), null, null, null, ex.Message, false),
                cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Build a claims principal for a user who just authenticated with a passkey.</summary>
    public System.Security.Claims.ClaimsPrincipal BuildPrincipal(TenantContext tenant, TapInAuthUser user)
        => _principalFactory.Create(user, tenant, TapInAuthMethod.Passkey, _timeProvider.GetUtcNow());

    /// <summary>Serialize a <see cref="CredentialCreateOptions"/> for cookie persistence between requests.</summary>
    public static string Serialize(CredentialCreateOptions options) => options.ToJson();

    /// <summary>Serialize an <see cref="AssertionOptions"/> for cookie persistence between requests.</summary>
    public static string Serialize(AssertionOptions options) => options.ToJson();

    /// <summary>Deserialize a <see cref="CredentialCreateOptions"/> from its JSON form.</summary>
    public static CredentialCreateOptions DeserializeCreateOptions(string json) =>
        JsonSerializer.Deserialize<CredentialCreateOptions>(json)
        ?? throw new InvalidOperationException("Failed to deserialize CredentialCreateOptions.");

    /// <summary>Deserialize an <see cref="AssertionOptions"/> from its JSON form.</summary>
    public static AssertionOptions DeserializeAssertionOptions(string json) =>
        JsonSerializer.Deserialize<AssertionOptions>(json)
        ?? throw new InvalidOperationException("Failed to deserialize AssertionOptions.");
}
