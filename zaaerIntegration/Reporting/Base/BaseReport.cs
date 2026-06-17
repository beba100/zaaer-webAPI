using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraReports.UI;
using zaaerIntegration.Reporting.Abstractions;

namespace zaaerIntegration.Reporting.Base;

public abstract class BaseReport<TDto> : XtraReport
{
    private static readonly object FontLock = new();
    private static bool _fontsRegistered;
    private static string? _regularFamilyName;
    private static string? _boldFamilyName;
    private static PrivateFontCollection? _fontCollection;

    protected BaseReport()
    {
        ConfigureRtl();
        ConfigureArabicFonts();
        ConfigureExportDefaults();
        ConfigurePage();
    }

    public abstract void BindData(TDto dto);

    protected void ConfigureRtl()
    {
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = RightToLeftLayout.Yes;
    }

    protected void ConfigureArabicFonts()
    {
        EnsureFontsRegistered();
        var family = _regularFamilyName ?? "Arial";
        var font = new DXFont(family, 10f);
        Font = font;
    }

    protected Font GetRegularFont(float size)
    {
        EnsureFontsRegistered();
        if (_fontCollection is not null && _fontCollection.Families.Length > 0)
        {
            return new Font(_fontCollection.Families[0], size, FontStyle.Regular, GraphicsUnit.Point);
        }

        var family = _regularFamilyName ?? "Arial";
        return new Font(family, size, FontStyle.Regular, GraphicsUnit.Point);
    }

    protected Font GetBoldFont(float size)
    {
        EnsureFontsRegistered();
        if (_fontCollection is not null && _fontCollection.Families.Length > 0)
        {
            var familyIndex = _fontCollection.Families.Length > 1 ? 1 : 0;
            return new Font(_fontCollection.Families[familyIndex], size, FontStyle.Bold, GraphicsUnit.Point);
        }

        var family = _boldFamilyName ?? _regularFamilyName ?? "Arial";
        return new Font(family, size, FontStyle.Bold, GraphicsUnit.Point);
    }

    protected void ConfigureExportDefaults()
    {
        ExportOptions.Pdf.Compressed = true;
        ExportOptions.Pdf.DocumentOptions.Title = DisplayName;
        ExportOptions.Pdf.DocumentOptions.Author = "Zaaer PMS";
    }

    protected virtual void ConfigurePage()
    {
        PaperKind = DXPaperKind.A4;
        Margins = new Margins(50, 50, 50, 50);
        Version = "24.1";
    }

    protected static Image? BytesToImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var ms = new MemoryStream(bytes);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureFontsRegistered()
    {
        if (_fontsRegistered)
        {
            return;
        }

        lock (FontLock)
        {
            if (_fontsRegistered)
            {
                return;
            }

            var fontsDir = Path.Combine(AppContext.BaseDirectory, "Reporting", "Fonts");
            var regularPath = Path.Combine(fontsDir, "Tajawal-Regular.ttf");
            var boldPath = Path.Combine(fontsDir, "Tajawal-Bold.ttf");

            if (File.Exists(regularPath))
            {
                _fontCollection = new PrivateFontCollection();
                _fontCollection.AddFontFile(regularPath);
                _regularFamilyName = _fontCollection.Families[0].Name;

                if (File.Exists(boldPath))
                {
                    _fontCollection.AddFontFile(boldPath);
                    _boldFamilyName = _fontCollection.Families[^1].Name;
                }
                else
                {
                    _boldFamilyName = _regularFamilyName;
                }
            }
            else
            {
                _regularFamilyName = "Arial";
                _boldFamilyName = "Arial";
            }

            _fontsRegistered = true;
        }
    }
}
