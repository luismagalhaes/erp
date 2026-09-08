namespace Erp.FiscalPT.Signing;

public sealed class DocumentSigner(ISigningKeyProvider keyProvider) : IDocumentSigner
{
    public DocumentSignature Sign(
        DateOnly documentDate,
        DateTime systemEntryDateUtc,
        string documentNumber,
        decimal grossTotal,
        string previousHash)
    {
        var signatureString = DocumentSignatureString.Build(
            documentDate,
            systemEntryDateUtc,
            documentNumber,
            grossTotal,
            previousHash);

        var hash = RsaDocumentSigner.Sign(signatureString, keyProvider.GetPrivateKey());

        return new DocumentSignature(hash, keyProvider.KeyVersion);
    }

    public bool Verify(
        DateOnly documentDate,
        DateTime systemEntryDateUtc,
        string documentNumber,
        decimal grossTotal,
        string previousHash,
        string hash)
    {
        var signatureString = DocumentSignatureString.Build(
            documentDate,
            systemEntryDateUtc,
            documentNumber,
            grossTotal,
            previousHash);

        using var publicKey = keyProvider.GetPublicKey();
        return RsaDocumentSigner.Verify(signatureString, hash, publicKey);
    }
}
