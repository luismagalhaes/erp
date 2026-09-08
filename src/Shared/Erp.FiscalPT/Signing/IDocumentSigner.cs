namespace Erp.FiscalPT.Signing;

/// <param name="Hash">Base64 RSA signature stored with the document.</param>
/// <param name="HashControl">Version of the private key used.</param>
public sealed record DocumentSignature(string Hash, string HashControl);

public interface IDocumentSigner
{
    /// <summary>
    /// Signs a document with the producer private key, chaining it to the previous document
    /// of the same series.
    /// </summary>
    DocumentSignature Sign(
        DateOnly documentDate,
        DateTime systemEntryDateUtc,
        string documentNumber,
        decimal grossTotal,
        string previousHash);

    /// <summary>Re-checks a stored signature; used by the chain verification job.</summary>
    bool Verify(
        DateOnly documentDate,
        DateTime systemEntryDateUtc,
        string documentNumber,
        decimal grossTotal,
        string previousHash,
        string hash);
}
