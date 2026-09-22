namespace Erp.Main.Components.Common;

/// <summary>Which partner file <see cref="PartnerAutocomplete"/> and <see cref="PartnerPickerDialog"/>
/// search — a company's customers and suppliers are kept as two separate lists, behind two different
/// API routes, even though both are <see cref="Erp.Main.Models.Core.Partner"/> records.</summary>
public enum PartnerKind
{
    Customer,
    Supplier
}
