using Erp.Common;
using Erp.Common.Statements;
using Erp.FiscalPT.Documents;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Storage;

namespace Erp.Sales.Application.Services;

/// <summary>
/// The customer side of the current account. Nothing is stored for it: the statement is read off
/// the documents and receipts themselves, so it can never disagree with them.
/// </summary>
public sealed class CustomerStatementService(
    ISalesDocumentStorage documentStorage,
    IPaymentStorage paymentStorage) : ICustomerStatementService
{
    /// <summary>Documents come before the receipts that settle them on the same day.</summary>
    private const int DocumentOrder = 0;

    private const int ReceiptOrder = 1;

    public async Task<AccountStatement> GetAsync(
        Guid companyId,
        string customerTaxId,
        string customerName,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerTaxId);

        var documents = await documentStorage.GetForCustomerAsync(companyId, customerTaxId, endDate, cancellationToken);
        var receipts = await paymentStorage.GetForPartyAsync(companyId, customerTaxId, endDate, cancellationToken);

        var movements = documents
            .Where(document => !document.IsVoided)
            .Select(ToMovement)
            .Concat(receipts
                .Where(receipt => !receipt.IsVoided)
                .Select(receipt => new AccountMovement(
                    receipt.TransactionDate,
                    Constants.StatementSources.Receipt,
                    receipt.Id,
                    receipt.PaymentType,
                    receipt.PaymentRefNo,
                    Debit: 0m,
                    Credit: receipt.GrossTotal,
                    Description: receipt.Description,
                    Order: ReceiptOrder)));

        return AccountStatementBuilder.Build(
            customerName,
            customerTaxId,
            AccountBalanceSide.Debit,
            movements,
            startDate,
            endDate);
    }

    /// <summary>
    /// An invoice charges, a credit note gives back, and a fatura-recibo does both at once — it
    /// carries its own receipt, so it shows the charge and the payment on the same line and leaves
    /// the balance where it was.
    /// </summary>
    private static AccountMovement ToMovement(SalesDocument document)
    {
        var (debit, credit) = document.DocumentType switch
        {
            SalesDocumentTypes.CreditNote => (0m, document.GrossTotal),
            SalesDocumentTypes.InvoiceReceipt => (document.GrossTotal, document.GrossTotal),
            _ => (document.GrossTotal, 0m)
        };

        return new AccountMovement(
            document.DocumentDate,
            Constants.StatementSources.SalesDocument,
            document.Id,
            document.DocumentType,
            document.DocumentNumber,
            debit,
            credit,
            Description: document.RectifiedDocumentNumber,
            Order: DocumentOrder);
    }
}
