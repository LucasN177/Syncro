using MudBlazor;

namespace Syncro.Layout;

public static class Theme
{
    public static readonly MudTheme FirstTheme = new()
    {
    PaletteLight = new PaletteLight()
    {
        Primary = "#4f46e5",
        PrimaryContrastText = "#ffffff",

        Secondary = "#0ea5e9",
        SecondaryContrastText = "#ffffff",

        Success = "#22c55e",
        Info = "#0284c7",
        Warning = "#f59e0b",
        Error = "#ef4444",

        Dark = "#111827",

        TextPrimary = "#0f172a",
        TextSecondary = "#64748b",

        Background = "#f8fafc",    // sehr clean
        Surface = "#ffffff",

        DrawerBackground = "#ffffff",
        DrawerText = "#334155",

        AppbarBackground = "#ffffff",
        AppbarText = "#0f172a"
    },

    PaletteDark = new PaletteDark()
    {
        Primary = "#818cf8",
        PrimaryContrastText = "#0b1020",

        Secondary = "#38bdf8",
        SecondaryContrastText = "#0b1020",

        Success = "#4ade80",
        Info = "#38bdf8",
        Warning = "#fbbf24",
        Error = "#f87171",

        Dark = "#020617",

        TextPrimary = "#e2e8f0",
        TextSecondary = "#94a3b8",

        Background = "#0f172a",
        Surface = "#111827",

        DrawerBackground = "#111827",
        DrawerText = "#cbd5f5",

        AppbarBackground = "#111827",
        AppbarText = "#e2e8f0"
    },

    Typography = new Typography()
    {
        Default = new DefaultTypography()
        {
            FontFamily = new[] { "Inter", "Roboto", "Helvetica", "Arial", "sans-serif" },
            FontSize = "0.95rem"
        },

        H1 = new H1Typography() { FontWeight = "700" },
        H2 = new H2Typography() { FontWeight = "700" },
        H3 = new H3Typography() { FontWeight = "600" },
        H4 = new H4Typography() { FontWeight = "600" },
        H5 = new H5Typography() { FontWeight = "500" },
        H6 = new H6Typography() { FontWeight = "500" },

        Button = new ButtonTypography()
        {
            TextTransform = "none", // kein ALL CAPS → moderner
            FontWeight = "500"
        }
    },

    Shadows = new Shadow()
    {
        Elevation = new[]
        {
            "none",
            "0px 1px 2px rgba(0,0,0,0.05)",
            "0px 1px 3px rgba(0,0,0,0.08)",
            "0px 4px 6px rgba(0,0,0,0.10)",
            "0px 10px 15px rgba(0,0,0,0.12)",
            "0px 20px 25px rgba(0,0,0,0.15)"
        }
    },

    ZIndex = new ZIndex()
    {
        Drawer = 1100,
        AppBar = 1200,
        Dialog = 1300,
        Snackbar = 1400,
        Tooltip = 1500
    },

    LayoutProperties = new LayoutProperties()
    {
        DefaultBorderRadius = "10px"
    }
};
}