namespace Erp.Identity.Infrastructure.Application;

public interface IRecaptchaVerifier
{
    /// <summary>False when <c>Recaptcha:SecretKey</c> isn't configured — callers use this to skip
    /// requiring a token at all rather than always fail closed on an unconfigured environment.</summary>
    bool IsConfigured { get; }

    /// <param name="token">The client-side <c>g-recaptcha-response</c> token; a missing or blank
    /// one always fails without calling Google.</param>
    /// <param name="expectedAction">Must match the <c>action</c> the client passed to
    /// <c>grecaptcha.execute</c> — rejects a token obtained for a different form.</param>
    Task<bool> VerifyAsync(string? token, string? remoteIp, string expectedAction, CancellationToken cancellationToken = default);
}
