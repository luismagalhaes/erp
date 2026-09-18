using Erp.Common.Statements;

namespace Erp.Sales.Infrastructure.Application;

public interface ICustomerStatementService
{
    /// <summary>
    /// A customer's current account: invoices and debit notes as debits, credit notes and receipts
    /// as credits, with the running balance of what the customer owes. Voided documents are left
    /// out, as they are everywhere else the debt is calculated.
    /// </summary>
    /// <param name="customerTaxId">The customer, by the tax id the documents carry.</param>
    /// <param name="customerName">How the statement names the customer: the master file's name today.</param>
    Task<AccountStatement> GetAsync(
        Guid companyId,
        string customerTaxId,
        string customerName,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);
}
