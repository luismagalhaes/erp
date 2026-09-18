using Erp.Common;
using Erp.Common.Statements;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Storage;

namespace Erp.Purchasing.Application.Services;

/// <summary>
/// The supplier side of the current account, read off the documents and payments themselves.
/// </summary>
/// <remarks>
/// A payment moves the account by the money that left, not by what its lines settled. A credit
/// note already lowered the balance when it was recorded; the payment that uses its credit must not
/// lower it a second time. That is why a payment settled entirely by credit shows as zero.
/// </remarks>
public sealed class SupplierStatementService(
    IPurchaseInvoiceStorage invoiceStorage,
    ISelfBilledInvoiceStorage selfBilledStorage,
    ISupplierPaymentStorage paymentStorage) : ISupplierStatementService
{
    /// <summary>Documents come before the payments that settle them on the same day.</summary>
    private const int DocumentOrder = 0;

    private const int PaymentOrder = 1;

    public async Task<AccountStatement> GetAsync(
        Guid companyId,
        Guid supplierId,
        string supplierName,
        string supplierTaxId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var invoices = await invoiceStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        var selfBilled = await selfBilledStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        var payments = await paymentStorage.GetAllAsync(companyId, supplierId, cancellationToken);

        var movements = invoices
            .Where(invoice => !invoice.IsVoided)
            .Select(invoice => ToMovement(
                Constants.StatementSources.PurchaseInvoice,
                invoice.Id,
                invoice.DocumentType,
                invoice.SupplierDocumentNumber,
                invoice.SupplierDocumentDate,
                invoice.GrossTotal,
                invoice.DueDate,
                invoice.Notes))
            .Concat(selfBilled
                .Where(invoice => !invoice.IsVoided)
                .Select(invoice => ToMovement(
                    Constants.StatementSources.SelfBilledInvoice,
                    invoice.Id,
                    invoice.DocumentType,
                    invoice.DocumentNumber,
                    invoice.IssueDate,
                    invoice.GrossTotal,
                    dueDate: null,
                    description: null)))
            .Concat(payments
                .Where(payment => !payment.IsVoided)
                .Select(payment => new AccountMovement(
                    payment.PaymentDate,
                    Constants.StatementSources.SupplierPayment,
                    payment.Id,
                    DocumentType: string.Empty,
                    payment.Number,
                    Debit: payment.Total,
                    Credit: 0m,
                    Description: payment.Description,
                    Order: PaymentOrder)));

        return AccountStatementBuilder.Build(
            supplierName,
            supplierTaxId,
            AccountBalanceSide.Credit,
            movements,
            startDate,
            endDate);
    }

    /// <summary>
    /// An invoice is what we owe, a credit note gives some of it back, and a fatura-recibo was paid
    /// when issued — the charge and the payment on the same line, leaving the balance untouched.
    /// </summary>
    private static AccountMovement ToMovement(
        string source,
        Guid id,
        string documentType,
        string number,
        DateOnly date,
        decimal grossTotal,
        DateOnly? dueDate,
        string? description)
    {
        var (debit, credit) = documentType switch
        {
            PurchaseDocumentTypes.CreditNote => (grossTotal, 0m),
            PurchaseDocumentTypes.InvoiceReceipt => (grossTotal, grossTotal),
            _ => (0m, grossTotal)
        };

        return new AccountMovement(date, source, id, documentType, number, debit, credit, dueDate, description, DocumentOrder);
    }
}
