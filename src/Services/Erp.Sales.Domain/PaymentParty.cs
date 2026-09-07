namespace Erp.Sales.Domain;

/// <summary>Who paid. Copied into the receipt at issuing time, like every other snapshot.</summary>
/// <param name="TaxId">999999990 stands for an unidentified final consumer.</param>
public sealed record PaymentParty(string TaxId, string Name);
