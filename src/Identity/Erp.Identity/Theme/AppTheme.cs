using MudBlazor;

namespace Erp.Identity.Theme;

/// <summary>
/// Single source of truth for the Identity host's look and feel — the same palette, typography and
/// layout metrics as <c>Erp.Main/Theme/AppTheme.cs</c>, duplicated here on purpose: the Identity
/// host does not reference Erp.Main or Erp.Common, so the two copies are kept in sync by hand.
/// </summary>
public static class AppTheme
{
    private const string FontFamily = "Inter";

    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#20B3A3",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#0E7C6F",
            Tertiary = "#5FC98A",
            Info = "#17A398",
            Success = "#2E9E5B",
            Warning = "#C77700",
            Error = "#C62F3B",
            Background = "#F5F7F6",
            BackgroundGray = "#EDF0EF",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1A1D1C",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#2E3332",
            DrawerIcon = "#6E7674",
            TextPrimary = "#1A1D1C",
            TextSecondary = "#697471",
            ActionDefault = "#697471",
            Divider = "#E1E5E4",
            TableLines = "#E8ECEB",
            TableStriped = "#FAFBFB",
            TableHover = "#F1FAF8",
            LinesDefault = "#E1E5E4",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#2ECEBC",
            PrimaryContrastText = "#062622",
            Secondary = "#79E0A8",
            Tertiary = "#A5E3C4",
            Info = "#38C7B8",
            Success = "#3FC77B",
            Warning = "#E0A22B",
            Error = "#EF5F6B",
            Background = "#121513",
            BackgroundGray = "#0D100F",
            Surface = "#1B1F1E",
            AppbarBackground = "#1B1F1E",
            AppbarText = "#ECEFEE",
            DrawerBackground = "#1B1F1E",
            DrawerText = "#CBD2D0",
            DrawerIcon = "#8E9895",
            TextPrimary = "#ECEFEE",
            TextSecondary = "#A0A9A7",
            ActionDefault = "#A0A9A7",
            Divider = "#2C3231",
            TableLines = "#2C3231",
            TableStriped = "#1F2423",
            TableHover = "#25302D",
            LinesDefault = "#2C3231",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "264px",
            DrawerMiniWidthLeft = "64px",
            AppbarHeight = "60px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = [FontFamily, "Segoe UI", "Helvetica", "Arial", "sans-serif"],
                FontSize = "0.875rem",
                LineHeight = "1.5",
            },
            H4 = new H4Typography
            {
                FontFamily = [FontFamily, "Segoe UI", "Helvetica", "Arial", "sans-serif"],
                FontSize = "1.5rem",
                FontWeight = "600",
                LineHeight = "1.3",
            },
            H5 = new H5Typography
            {
                FontFamily = [FontFamily, "Segoe UI", "Helvetica", "Arial", "sans-serif"],
                FontSize = "1.25rem",
                FontWeight = "600",
            },
            H6 = new H6Typography
            {
                FontFamily = [FontFamily, "Segoe UI", "Helvetica", "Arial", "sans-serif"],
                FontSize = "1rem",
                FontWeight = "600",
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = [FontFamily, "Segoe UI", "Helvetica", "Arial", "sans-serif"],
                FontWeight = "500",
            },
            Button = new ButtonTypography
            {
                FontFamily = [FontFamily, "Segoe UI", "Helvetica", "Arial", "sans-serif"],
                FontWeight = "600",
                TextTransform = "none",
            },
        },
    };
}
