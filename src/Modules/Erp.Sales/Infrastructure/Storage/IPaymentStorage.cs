using Erp.Sales.Domain;

namespace Erp.Sales.Infrastructure.Storage;

public interface IPaymentStorage
{
    Task<IReadOnlyList<Payment>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Receipts of a period, fully loaded, in issuing order. Read by the SAF-T export.</summary>
    Task<IReadOnlyList<Payment>> GetForPeriodAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>Signature of the last receipt issued in the series, or empty for the first one.</summary>
    Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much of each of those invoices is already settled by receipts that are not voided.
    /// Invoices with nothing settled are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetSettledAmountsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    Task AddStatusChangeAsync(PaymentStatusChange statusChange, CancellationToken cancellationToken = default);
}
