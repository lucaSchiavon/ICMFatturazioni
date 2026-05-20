using MudBlazor;

namespace ICMFatturazioni.Web.Theme;

/// <summary>
/// Tematizzazione MudBlazor per ICM Fatturazioni (light + dark).
/// Mappatura derivata da <c>brand-guidelines.md</c> sezione
/// "Implementazione con MudBlazor". Tenere allineata qualsiasi modifica
/// alla palette ai token di brand: questa classe è il <i>veicolo</i>
/// del brand su MudBlazor, non la sua autorità.
/// </summary>
public static class IcmTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            // --- Brand ---
            Primary           = "#245F8C", // icm-blue-500
            PrimaryDarken     = "#1D4E73", // icm-blue-600 (hover)
            PrimaryLighten    = "#7FA7CC", // icm-blue-300 (disabled)
            Secondary         = "#2A3A52", // icm-navy-700
            AppbarBackground  = "#1E2A3B", // icm-navy-900
            AppbarText        = "#FFFFFF",

            // --- Superfici ---
            Background        = "#F7F9FB", // gray-50
            BackgroundGray    = "#EEF2F6", // gray-100
            Surface           = "#FFFFFF",
            DrawerBackground  = "#FFFFFF",
            DrawerText        = "#334155", // gray-700

            // --- Semantici ---
            Success           = "#16A34A",
            Warning           = "#D97706",
            Error             = "#DC2626",
            Info              = "#245F8C", // coincide col Primary per scelta brand

            // --- Testo / azioni ---
            TextPrimary       = "#0F172A", // gray-900
            TextSecondary     = "#475569", // gray-600
            ActionDefault     = "#64748B", // gray-500
            ActionDisabled    = "#94A3B8", // gray-400

            // --- Bordi / divisori ---
            LinesDefault      = "#E2E8F0", // gray-200
            TableLines        = "#E2E8F0",
            Divider           = "#E2E8F0",
        },
        PaletteDark = new PaletteDark
        {
            // Bozza ADR D17. Verifica WCAG AA su tutte le coppie testo/sfondo.
            Primary           = "#7FA7CC", // icm-blue-300 dark
            PrimaryDarken     = "#5A92BC",
            PrimaryLighten    = "#AEC8E2",
            Secondary         = "#AEC8E2",
            AppbarBackground  = "#0F2238",
            AppbarText        = "#F1F5F9",

            Background        = "#1E2A3B", // icm-navy-900
            BackgroundGray    = "#252F42",
            Surface           = "#252F42",
            DrawerBackground  = "#252F42",
            DrawerText        = "#CBD5E1",

            Success           = "#34D399",
            Warning           = "#FBBF24",
            Error             = "#F87171",
            Info              = "#7FA7CC",

            TextPrimary       = "#F1F5F9",
            TextSecondary     = "#94A3B8",
            ActionDefault     = "#94A3B8",
            ActionDisabled    = "#64748B",

            LinesDefault      = "#3A4660",
            TableLines        = "#3A4660",
            Divider           = "#3A4660",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "system-ui", "-apple-system", "Segoe UI", "sans-serif"],
                FontSize   = "13px", // text-sm: corpo standard gestionale
                LineHeight = "20px",
                FontWeight = "400",
            },
            H1 = new H1Typography { FontSize = "24px", LineHeight = "32px", FontWeight = "600" },
            H2 = new H2Typography { FontSize = "20px", LineHeight = "28px", FontWeight = "600" },
            H3 = new H3Typography { FontSize = "17px", LineHeight = "24px", FontWeight = "500" },
            Body1   = new Body1Typography   { FontSize = "13px", LineHeight = "20px", FontWeight = "400" },
            Body2   = new Body2Typography   { FontSize = "12px", LineHeight = "16px", FontWeight = "400" },
            Button  = new ButtonTypography  { FontSize = "13px", LineHeight = "20px", FontWeight = "500", TextTransform = "none" },
            Caption = new CaptionTypography { FontSize = "12px", LineHeight = "16px", FontWeight = "500" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            AppbarHeight        = "56px",
            DrawerWidthLeft     = "256px",
            DrawerMiniWidthLeft = "64px",
        },
    };
}
