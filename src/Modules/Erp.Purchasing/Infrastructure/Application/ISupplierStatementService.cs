using Erp.Common.Statements;

namespace Erp.Purchasing.Infrastructure.Application;

public interface ISupplierStatementService
{
    /// <summary>
    /// A supplier's current account: their invoices and our self-billed invoices as credits, credit
    /// notes and payments as debits, with the running balance of what we owe. Voided documents are
    /// left out, as they are everywhere else the debt is calculated.
    /// </summary>
    /// <param name="supplierName">How the statement names the supplier: the master file's name today.</param>
    Task<AccountStatement> GetAsync(
        Guid companyId,
        Guid supplierId,
        string supplierName,
        string supplierTaxId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);
}
