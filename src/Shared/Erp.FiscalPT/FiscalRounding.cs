namespace Erp.FiscalPT;

/// <summary>
/// Single rounding rule for every monetary amount in the fiscal pipeline. Keeping it in one
/// place is what stops line totals, document totals and the signed GrossTotal from drifting.
/// </summary>
public static class FiscalRounding
{
    public const int AmountDecimals = 2;

    public static decimal Amount(decimal value) =>
        Math.Round(value, AmountDecimals, MidpointRounding.AwayFromZero);
}
