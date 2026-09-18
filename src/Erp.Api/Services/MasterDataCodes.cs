using System.Globalization;
using Erp.Common;
using Erp.SeriesRegistry.Infrastructure.Application;

namespace Erp.Api.Services;

/// <summary>
/// Gives master data — customers, suppliers, families, subfamilies, brands — a sequential code
/// (1, 2, 3...) when the caller leaves it blank.
/// </summary>
/// <remarks>
/// Composed here, in the host, for the same reason the series of a new company are: the catalogue
/// does not know about the counters and the counters do not know about the catalogue. A code sent
/// explicitly is kept as it is — that is how an import brings its own codes — and the counter
/// simply steps over any number that such an import already took.
/// </remarks>
public sealed class MasterDataCodes(IDocumentNumbers documentNumbers, IUnitOfWork unitOfWork)
{
    /// <summary>A counter that keeps hitting taken codes is broken, not unlucky.</summary>
    private const int MaxAttempts = 1000;

    /// <param name="requestedCode">The caller's code; blank means "give me the next one".</param>
    /// <param name="counterKey">One of <see cref="Constants.CodeCounters"/>.</param>
    /// <param name="codeExists">Whether the company already uses a code.</param>
    /// <param name="create">Creates the record with the code it is given, saving the changes.</param>
    public async Task<T> CreateAsync<T>(
        Guid companyId,
        string? requestedCode,
        string counterKey,
        Func<string, CancellationToken, Task<bool>> codeExists,
        Func<string, Task<T>> create,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedCode))
            return await create(requestedCode.Trim());

        // The counter row stays locked until the record carrying the number is saved, so two
        // records created at once cannot take the same code.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var number = await documentNumbers.NextSequenceAsync(companyId, counterKey, cancellationToken);
            var code = number.ToString(CultureInfo.InvariantCulture);

            if (await codeExists(code, cancellationToken))
                continue;

            var created = await create(code);
            await transaction.CommitAsync(cancellationToken);

            return created;
        }

        throw new InvalidOperationException($"No free code could be found for counter '{counterKey}'.");
    }
}
