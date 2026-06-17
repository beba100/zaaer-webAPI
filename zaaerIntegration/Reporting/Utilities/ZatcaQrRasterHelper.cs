using QRCoder;

namespace zaaerIntegration.Reporting.Utilities;

/// <summary>Renders ZATCA TLV bytes to a high-resolution PNG for reliable PDF display and scanning.</summary>
public static class ZatcaQrRasterHelper
{
    /// <param name="pixelsPerModule">Module size in pixels; 6–8 with quiet zone suits ZATCA scanners.</param>
    public static byte[]? RenderPng(byte[]? tlvBytes, int pixelsPerModule = 10)
    {
        if (tlvBytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            using var generator = new QRCodeGenerator();
            // Quartile (Q) — matches legacy ZATCA / DevExpress invoice templates.
            var qrData = generator.CreateQrCode(tlvBytes, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(qrData);
            return png.GetGraphic(pixelsPerModule, drawQuietZones: true);
        }
        catch
        {
            return null;
        }
    }
}
