using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.FiscalPT.Signing;

/// <summary>
/// Loads the producer RSA key from configuration. In development, and only when explicitly
/// allowed, it falls back to a key kept outside the repository so the signature chain stays
/// verifiable across restarts.
/// </summary>
public sealed class SigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private const int RequiredKeySizeBits = 1024;

    // Named after Sales because that is where signing started. Kept as it is on purpose: renaming
    // it would orphan the key already on developers' machines and break the chains signed with it.
    private const string DevelopmentKeyFileName = "sales-dev-signing-key.pem";

    private readonly FiscalOptions _options;
    private readonly ILogger<SigningKeyProvider> _logger;
    private readonly Lock _gate = new();

    private RSA? _key;

    public SigningKeyProvider(IOptions<FiscalOptions> options, ILogger<SigningKeyProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string KeyVersion => _options.KeyVersion;

    public RSA GetPrivateKey() => EnsureKey();

    public RSA GetPublicKey()
    {
        var publicKey = RSA.Create();
        publicKey.ImportParameters(EnsureKey().ExportParameters(includePrivateParameters: false));
        return publicKey;
    }

    private RSA EnsureKey()
    {
        if (_key is not null)
            return _key;

        lock (_gate)
        {
            _key ??= LoadKey();
        }

        return _key;
    }

    private RSA LoadKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.PrivateKeyPem))
        {
            var configured = RSA.Create();
            configured.ImportFromPem(_options.PrivateKeyPem);
            WarnOnKeySize(configured);
            return configured;
        }

        if (!_options.AllowDevelopmentKeyGeneration)
        {
            throw new InvalidOperationException(
                "No signing key configured. Set Fiscal:PrivateKeyPem through user secrets or a secret store.");
        }

        return LoadOrCreateDevelopmentKey();
    }

    private RSA LoadOrCreateDevelopmentKey()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Erp",
            "Sales");

        var path = Path.Combine(directory, DevelopmentKeyFileName);

        // Portaria 363/2010 mandates a 1024 bit key; a stronger key would produce signatures
        // the tax authority cannot validate.
#pragma warning disable S4426 // Cryptographic keys should be robust
        var key = RSA.Create(RequiredKeySizeBits);
#pragma warning restore S4426

        if (File.Exists(path))
        {
            key.ImportFromPem(File.ReadAllText(path));
            _logger.LogWarning("Using the local development signing key at {Path}. Not valid for certification.", path);
            return key;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());

        _logger.LogWarning(
            "No signing key configured. Generated a development key at {Path}. Not valid for certification.", path);

        return key;
    }

    private void WarnOnKeySize(RSA key)
    {
        if (key.KeySize != RequiredKeySizeBits)
        {
            _logger.LogWarning(
                "The configured signing key is {KeySize} bits; Portaria 363/2010 requires {Required} bits.",
                key.KeySize,
                RequiredKeySizeBits);
        }
    }

    public void Dispose()
    {
        _key?.Dispose();
        _key = null;
    }
}
