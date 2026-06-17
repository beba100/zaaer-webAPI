using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.BarCode;
using DevExpress.XtraReports.UI;
using zaaerIntegration.Reporting.Base;
using zaaerIntegration.Reporting.DTOs.Invoice;

namespace zaaerIntegration.Reporting.Reports.Invoice;

public sealed class InvoiceReport_v1 : BaseReport<InvoiceReportDto>
{
    private const float ContentWidth = 727F;
    private const float TripleArWidth = 200F;
    private const float TripleValueWidth = 327F;
    private const float TripleEnWidth = 200F;
    private const float TripleRowHeight = 22F;
    private const float QrSize = 190F;
    private const float QrTop = 0F;
    private const float QrToMetaGap = 16F;
    private const float MetaPanelTop = QrTop + QrSize + QrToMetaGap;
    private const float MetaPanelHeight = 48F;
    private const float VisitorShellTop = MetaPanelTop + MetaPanelHeight + 10F;

    private static readonly Color ValueText = Color.FromArgb(100, 116, 139);
    private static readonly Color PanelBg = Color.FromArgb(248, 250, 252);
    private static readonly Color TableHeaderBg = Color.FromArgb(241, 245, 249);
    private static readonly Color BorderTone = Color.FromArgb(226, 232, 240);
    private static readonly Color MutedText = Color.FromArgb(148, 163, 184);
    private static readonly Color BodyText = Color.FromArgb(71, 85, 105);
    private static readonly Color HeaderText = Color.FromArgb(100, 116, 139);
    private static readonly Color Accent = Color.FromArgb(63, 111, 159);
    private static readonly Color AccentDark = Color.FromArgb(49, 89, 127);
    private static readonly Color AccentSoft = Color.FromArgb(232, 240, 247);
    private static readonly Color RowAltBg = Color.FromArgb(252, 253, 255);

    private TopMarginBand _topMargin = null!;
    private BottomMarginBand _bottomMargin = null!;
    private ReportHeaderBand _reportHeader = null!;
    private PageHeaderBand _pageHeader = null!;
    private DetailBand _detail = null!;
    private DetailReportBand _linesDetailReport = null!;
    private DetailBand _linesDetail = null!;
    private ReportFooterBand _reportFooter = null!;

    private XRPictureBox _logoPicture = null!;
    private XRLabel _hotelNameLabel = null!;
    private XRLabel _hotelDetailsLabel = null!;
    private XRLabel _titleArLabel = null!;
    private XRLabel _titleEnLabel = null!;
    private XRLabel _invoiceSubtitleLabel = null!;
    private XRLabel _qrCaptionLabel = null!;
    private XRLabel _qrStatusLabel = null!;
    private XRPanel _metaPanel = null!;
    private XRPanel _visitorShell = null!;
    private XRLabel _visitorTitleLabel = null!;
    private XRPanel _tableHeaderPanel = null!;
    private XRBarCode _qrBarcode = null!;
    private XRPictureBox _qrPicture = null!;
    private XRLabel _metaInvoiceNo = null!;
    private XRLabel _metaResNo = null!;
    private XRLabel _metaDateGreg = null!;
    private XRLabel _metaDateHijri = null!;
    private XRLabel _metaPeriod = null!;
    private XRPanel _visitorPanel = null!;
    private XRPanel _summaryPanel = null!;
    private XRPanel _amountWordsPanel = null!;
    private XRLabel _customerSignatureCaption = null!;
    private XRLine _customerSignatureLine = null!;
    private XRPictureBox _operatorSignaturePicture = null!;
    private XRLabel _operatorSignatureCaption = null!;
    private XRLine _operatorSignatureLine = null!;
    private XRLabel _lineDescriptionLabel = null!;
    private XRLabel _lineQtyLabel = null!;
    private XRLabel _linePriceLabel = null!;
    private XRLabel _linePriceAfterDiscountLabel = null!;
    private XRLabel _lineLodgingLabel = null!;
    private XRLabel _lineVatLabel = null!;
    private XRLabel _lineTotalLabel = null!;
    private XRLabel _receiptsLabel = null!;
    private XRLabel _zatcaStatusLabel = null!;
    private XRLabel _footerLabel = null!;

    public InvoiceReport_v1()
    {
        DisplayName = "Tax Invoice";
        Name = "InvoiceReport_v1";
        InitializeLayout();
        ConfigureInvoicePdfExport();
    }

    private void ConfigureInvoicePdfExport()
    {
        ExportOptions.Pdf.ConvertImagesToJpeg = false;
        ExportOptions.Pdf.ImageQuality = PdfJpegImageQuality.Highest;
    }

    public override void BindData(InvoiceReportDto dto)
    {
        DataSource = new[] { dto };
        _linesDetailReport.DataSource = dto.Lines;

        _titleArLabel.Text = dto.InvoiceTitleAr;
        _titleEnLabel.Text = dto.InvoiceTitleEn;
        _logoPicture.Image = BytesToImage(dto.Header.LogoBytes);
        _logoPicture.Visible = _logoPicture.Image is not null;

        var hotelName = !string.IsNullOrWhiteSpace(dto.Header.HotelName)
            ? dto.Header.HotelName
            : dto.Header.DisplayName;
        _hotelNameLabel.Text = hotelName ?? string.Empty;
        _hotelDetailsLabel.Text = BuildHotelDetails(dto);
        ApplyInvoiceLayoutVariant(dto);

        var inv = dto.Invoice;
        var stay = dto.Stay;
        _metaInvoiceNo.Text = MetaCell("رقم الفاتورة", "Invoice No", inv.InvoiceNo);
        _metaResNo.Text = MetaCell("رقم الحجز", "Res. No", stay?.ReservationNo ?? "—");
        _metaDateGreg.Text = MetaCell("التاريخ", "Date", inv.InvoiceDate.ToString("dd/MM/yyyy"));
        _metaDateHijri.Text = MetaCell("التاريخ الهجري", "Hijri", inv.InvoiceDateHijri ?? "—");
        _metaPeriod.Text = MetaCell("فترة الفاتورة", "Period", stay?.PeriodText ?? BuildPeriod(inv));

        PopulateTripleColumnPanel(
            _visitorPanel,
            BuildVisitorRows(dto),
            dto.IsStandardInvoice ? 20F : 18F,
            zebra: true,
            rowHeight: dto.IsStandardInvoice ? 14F : 18F);
        PopulateTripleColumnPanel(
            _summaryPanel,
            BuildSummaryRows(dto),
            20F,
            highlightLastRow: true,
            rowHeight: 17F);

        PopulateAmountWordsRow(dto);
        _receiptsLabel.Text = BuildReceiptsBlock(dto);
        ApplyZatcaQr(dto);
        ApplySignatures(dto);

        _footerLabel.Text =
            $"فاتورة إلكترونية — ALEAIRY PMS  |  {dto.GeneratedAt:yyyy-MM-dd HH:mm}  |  www.aleairy.com";
    }

    private void ApplyInvoiceLayoutVariant(InvoiceReportDto dto)
    {
        if (dto.IsStandardInvoice)
        {
            ApplyStandardEnterpriseLayout(dto);
            return;
        }

        ApplySimplifiedHotelLayout(dto);
    }

    private void ApplyStandardEnterpriseLayout(InvoiceReportDto dto)
    {
        var headerAccent = Color.FromArgb(8, 47, 93);
        var panelBorder = Color.FromArgb(214, 222, 235);

        _reportHeader.HeightF = 456F;
        _pageHeader.HeightF = 36F;
        _linesDetail.HeightF = 36F;

        MoveControl(_qrBarcode, 12F, 18F, 86F, 86F);
        MoveControl(_qrPicture, 12F, 18F, 86F, 86F);
        MoveControl(_qrCaptionLabel, 0F, 104F, 124F, 20F);
        MoveControl(_qrStatusLabel, 12F, 126F, 104F, 26F);
        _qrCaptionLabel.Visible = true;
        _qrStatusLabel.Visible = true;
        _qrStatusLabel.Text = "فاتورة ضريبية\nTax Invoice";

        MoveControl(_logoPicture, 684F, 18F, 34F, 34F);
        MoveControl(_hotelNameLabel, 520F, 18F, 156F, 24F);
        MoveControl(_hotelDetailsLabel, 520F, 44F, 198F, 96F);
        _hotelNameLabel.TextAlignment = TextAlignment.TopRight;
        _hotelDetailsLabel.TextAlignment = TextAlignment.TopRight;
        _hotelNameLabel.ForeColor = headerAccent;

        MoveControl(_titleArLabel, 180F, 16F, 360F, 30F);
        MoveControl(_titleEnLabel, 180F, 48F, 360F, 18F);
        MoveControl(_invoiceSubtitleLabel, 180F, 72F, 360F, 32F);
        _titleArLabel.Font = GetBoldFont(17f);
        _titleEnLabel.Font = GetBoldFont(12f);
        _titleArLabel.ForeColor = headerAccent;
        _titleEnLabel.ForeColor = headerAccent;
        _invoiceSubtitleLabel.Visible = true;

        MoveControl(_metaPanel, 0F, 160F, ContentWidth, 62F);
        _metaPanel.BackColor = Color.White;
        _metaPanel.BorderColor = BorderTone;
        ArrangeMetaCardsAsModernCards();
        StyleMetaCards(panelBorder, Color.FromArgb(248, 250, 252), headerAccent);

        MoveControl(_visitorShell, 0F, 232F, ContentWidth, 206F);
        MoveControl(_visitorPanel, 8F, 26F, ContentWidth - 16F, 174F);
        _visitorTitleLabel.Text = "بيانات المشتري  |  Buyer Information";
        _visitorTitleLabel.ForeColor = headerAccent;
        _visitorPanel.BorderColor = panelBorder;
        _visitorPanel.BackColor = Color.FromArgb(252, 253, 255);

        StyleLineTable(headerAccent, Color.White, headerAccent, BorderTone);
        _summaryPanel.BackColor = Color.White;
    }

    private void ApplySimplifiedHotelLayout(InvoiceReportDto dto)
    {
        var ink = Color.FromArgb(32, 32, 32);
        var lightInk = Color.FromArgb(96, 96, 96);
        var softGray = Color.FromArgb(248, 248, 248);

        _reportHeader.HeightF = 448F;
        _pageHeader.HeightF = 36F;
        _linesDetail.HeightF = 36F;

        MoveControl(_titleArLabel, 0F, 12F, 300F, 26F);
        MoveControl(_titleEnLabel, 0F, 40F, 300F, 18F);
        _titleArLabel.TextAlignment = TextAlignment.MiddleLeft;
        _titleEnLabel.TextAlignment = TextAlignment.MiddleLeft;
        _titleArLabel.Font = GetBoldFont(14.5f);
        _titleEnLabel.Font = GetBoldFont(11f);
        _titleArLabel.ForeColor = ink;
        _titleEnLabel.ForeColor = ink;
        _invoiceSubtitleLabel.Visible = false;

        MoveControl(_logoPicture, 648F, 10F, 70F, 70F);
        MoveControl(_hotelNameLabel, 542F, 84F, 176F, 22F);
        MoveControl(_hotelDetailsLabel, 542F, 106F, 176F, 44F);
        _hotelNameLabel.TextAlignment = TextAlignment.TopRight;
        _hotelDetailsLabel.TextAlignment = TextAlignment.TopRight;
        _hotelNameLabel.ForeColor = ink;
        _hotelDetailsLabel.ForeColor = lightInk;

        MoveControl(_qrBarcode, 570F, 104F, 144F, 144F);
        MoveControl(_qrPicture, 570F, 104F, 144F, 144F);
        _qrCaptionLabel.Visible = false;
        _qrStatusLabel.Visible = false;

        MoveControl(_metaPanel, 0F, 110F, 520F, 138F);
        _metaPanel.BackColor = Color.White;
        _metaPanel.BorderColor = ink;
        ArrangeMetaCardsAsCompactRows();
        StyleMetaCards(ink, Color.White, ink);

        MoveControl(_visitorShell, 0F, 250F, ContentWidth, 196F);
        MoveControl(_visitorPanel, 8F, 24F, ContentWidth - 16F, 166F);
        _visitorTitleLabel.Text = "بيانات الزائر / مشتري الخدمة  |  Visitor / Buyer Information";
        _visitorTitleLabel.ForeColor = ink;
        _visitorPanel.BorderColor = ink;
        _visitorPanel.BackColor = Color.White;

        StyleLineTable(Color.White, ink, ink, ink);
        _summaryPanel.BackColor = Color.White;
    }

    private void StyleMetaCards(Color border, Color backColor, Color textColor)
    {
        foreach (var meta in new[] { _metaInvoiceNo, _metaResNo, _metaDateGreg, _metaDateHijri, _metaPeriod })
        {
            meta.BackColor = backColor;
            meta.BorderColor = border;
            meta.ForeColor = textColor;
        }
    }

    private void ArrangeMetaCardsAsModernCards()
    {
        var colW = (ContentWidth - 12F) / 5F;
        MoveControl(_metaInvoiceNo, 6F, 8F, colW, 46F);
        MoveControl(_metaResNo, 6F + colW, 8F, colW, 46F);
        MoveControl(_metaDateGreg, 6F + colW * 2F, 8F, colW, 46F);
        MoveControl(_metaDateHijri, 6F + colW * 3F, 8F, colW, 46F);
        MoveControl(_metaPeriod, 6F + colW * 4F, 8F, colW, 46F);
        foreach (var meta in new[] { _metaInvoiceNo, _metaResNo, _metaDateGreg, _metaDateHijri, _metaPeriod })
        {
            meta.TextAlignment = TextAlignment.MiddleCenter;
            meta.Font = GetRegularFont(8f);
        }
    }

    private void ArrangeMetaCardsAsCompactRows()
    {
        const float rowH = 26F;
        MoveControl(_metaInvoiceNo, 6F, 4F, 508F, rowH);
        MoveControl(_metaResNo, 6F, 4F + rowH, 508F, rowH);
        MoveControl(_metaDateGreg, 6F, 4F + rowH * 2F, 508F, rowH);
        MoveControl(_metaDateHijri, 6F, 4F + rowH * 3F, 508F, rowH);
        MoveControl(_metaPeriod, 6F, 4F + rowH * 4F, 508F, rowH);
        foreach (var meta in new[] { _metaInvoiceNo, _metaResNo, _metaDateGreg, _metaDateHijri, _metaPeriod })
        {
            meta.TextAlignment = TextAlignment.MiddleCenter;
            meta.Font = GetRegularFont(7.5f);
        }
    }

    private void StyleLineTable(Color headerBack, Color headerText, Color bodyText, Color border)
    {
        _tableHeaderPanel.BackColor = headerBack;
        _tableHeaderPanel.BorderColor = border;
        foreach (XRControl control in _tableHeaderPanel.Controls)
        {
            if (control is XRLabel label)
            {
                label.BackColor = headerBack;
                label.ForeColor = headerText;
                label.BorderColor = border;
                label.Font = GetBoldFont(8f);
            }
        }

        foreach (var label in new[]
                 {
                     _lineDescriptionLabel, _lineQtyLabel, _linePriceLabel,
                     _linePriceAfterDiscountLabel, _lineLodgingLabel, _lineVatLabel, _lineTotalLabel
                 })
        {
            label.ForeColor = bodyText;
            label.BorderColor = border;
        }
    }

    private static void MoveControl(XRControl control, float x, float y, float w, float h)
    {
        control.LocationFloat = new DevExpress.Utils.PointFloat(x, y);
        control.SizeF = new SizeF(w, h);
    }

    private static string BuildHotelDetails(InvoiceReportDto dto)
    {
        var h = dto.Header;
        var lines = new List<string>();
        var address = JoinParts(h.City, h.Address);
        if (!string.IsNullOrWhiteSpace(address))
        {
            lines.Add(address);
        }

        if (!string.IsNullOrWhiteSpace(h.Phone))
        {
            lines.Add($"Tel: {h.Phone}");
        }

        if (!string.IsNullOrWhiteSpace(h.TaxNumber))
        {
            lines.Add($"VAT No: {h.TaxNumber}");
        }

        if (!string.IsNullOrWhiteSpace(h.CrNumber))
        {
            lines.Add($"CR: {h.CrNumber}");
        }

        return string.Join("\n", lines);
    }

    private static IEnumerable<(string Ar, string Value, string En)> BuildVisitorRows(InvoiceReportDto dto)
    {
        if (dto.IsStandardInvoice)
        {
            yield return ("الاسم", dto.Customer?.CustomerName ?? "—", "Name");
            yield return ("الشركة", dto.Corporate?.CompanyName ?? "—", "Company");
            yield return ("الرقم الضريبي", dto.Corporate?.TaxNumber ?? "—", "VAT No");
            yield return ("السجل التجاري", dto.Corporate?.CrNumber ?? "—", "CR");
            var address = JoinParts(dto.Corporate?.City, dto.Corporate?.Address)
                          ?? dto.Customer?.Address
                          ?? "—";
            yield return ("العنوان", address, "Address");
            if (!string.IsNullOrWhiteSpace(dto.Corporate?.Phone ?? dto.Customer?.MobileNo))
            {
                yield return ("الهاتف", dto.Corporate?.Phone ?? dto.Customer?.MobileNo ?? "—", "Phone");
            }
        }
        else
        {
            yield return ("الاسم", dto.Customer?.CustomerName ?? "—", "Name");
            yield return ("الجوال", dto.Customer?.MobileNo ?? "—", "Mobile");
            if (!string.IsNullOrWhiteSpace(dto.Customer?.Email))
            {
                yield return ("البريد", dto.Customer!.Email!, "Email");
            }
        }

        if (dto.Stay == null)
        {
            yield break;
        }

        yield return ("الوصول", dto.Stay.CheckInDate?.ToString("dd/MM/yyyy") ?? "—", "Arrival");
        yield return ("المغادرة", dto.Stay.CheckOutDate?.ToString("dd/MM/yyyy") ?? "—", "Departure");
        yield return ("الوحدات", dto.Stay.UnitsText ?? "—", "Units");
        yield return ("المدة", dto.Stay.Nights is > 0 ? $"{dto.Stay.Nights} ليلة" : "—", "Duration");
    }

    private static IEnumerable<(string Ar, string Value, string En)> BuildSummaryRows(InvoiceReportDto dto)
    {
        yield return ("الإجمالي قبل الضريبة", $"{dto.Tax.Subtotal:N2} ر.س", "Subtotal");
        yield return ("مجموع الخصومات", "0.00 ر.س", "Discounts");
        if (dto.Tax.LodgingTaxAmount > 0)
        {
            yield return (
                $"ضريبة الإقامة ({dto.Tax.LodgingTaxRate:N2}%)",
                $"{dto.Tax.LodgingTaxAmount:N2} ر.س",
                "Lodging tax");
        }

        yield return (
            $"ضريبة القيمة المضافة ({dto.Tax.VatRate:N0}%)",
            $"{dto.Tax.VatAmount:N2} ر.س",
            "VAT");
        yield return ("الإجمالي", $"{dto.Tax.TotalAmount:N2} ر.س", "Total");
    }

    private void PopulateAmountWordsRow(InvoiceReportDto dto)
    {
        _amountWordsPanel.Controls.Clear();
        _amountWordsPanel.Visible = false;
    }

    private void ApplySignatures(InvoiceReportDto dto)
    {
        _operatorSignaturePicture.Image = BytesToImage(dto.OperatorSignatureBytes);
        var hasSignatureImage = _operatorSignaturePicture.Image is not null;
        _operatorSignaturePicture.Visible = hasSignatureImage;

        var operatorName = string.IsNullOrWhiteSpace(dto.OperatorDisplayName)
            ? "—"
            : dto.OperatorDisplayName;
        _operatorSignatureCaption.Text = $"توقيع المستخدم / User Signature\n{operatorName}";
    }

    private static string BuildReceiptsBlock(InvoiceReportDto dto)
    {
        if (dto.Payments.Receipts.Count == 0)
        {
            return string.Empty;
        }

        var refs = dto.Payments.Receipts
            .Select(r => r.ReceiptNo)
            .Where(n => !string.IsNullOrWhiteSpace(n));
        return "سندات القبض / Receipts: " + string.Join(" ، ", refs);
    }

    private static string BuildPeriod(InvoiceReportHeaderDto inv)
    {
        if (inv.PeriodFrom.HasValue && inv.PeriodTo.HasValue)
        {
            return $"{inv.PeriodFrom:dd/MM/yyyy} - {inv.PeriodTo:dd/MM/yyyy}";
        }

        return "—";
    }

    private void ApplyZatcaQr(InvoiceReportDto dto)
    {
        _qrBarcode.Visible = false;
        _qrPicture.Visible = false;
        _zatcaStatusLabel.Visible = false;

        // Vector QR from TLV bytes — matches legacy rpt_TAXReceipt (base64 TLV, Byte mode, ECC Q).
        if (dto.ZatcaQrTlvBytes is { Length: > 0 })
        {
            _qrBarcode.BinaryData = dto.ZatcaQrTlvBytes;
            ConfigureZatcaQrSymbology(_qrBarcode);
            ApplyQrControlLayout(_qrBarcode);
            _qrBarcode.Visible = true;
            return;
        }

        if (dto.QrImageBytes is { Length: > 0 })
        {
            _qrPicture.Image = BytesToImage(dto.QrImageBytes);
            ApplyQrControlLayout(_qrPicture);
            _qrPicture.Visible = _qrPicture.Image is not null;
            _qrPicture.Sizing = ImageSizeMode.ZoomImage;
            return;
        }

        _zatcaStatusLabel.Text = string.IsNullOrWhiteSpace(dto.ZatcaStatus)
            ? "لم يتم إرسال الفاتورة إلى ZATCA"
            : $"ZATCA: {dto.ZatcaStatus}";
        _zatcaStatusLabel.Visible = true;
    }

    private static void ApplyQrControlLayout(XRControl control)
    {
        control.RightToLeft = RightToLeft.No;
    }

    private void PopulateTripleColumnPanel(
        XRPanel panel,
        IEnumerable<(string Ar, string Value, string En)> rows,
        float startY,
        bool zebra = false,
        bool highlightLastRow = false,
        float rowHeight = TripleRowHeight)
    {
        panel.Controls.Clear();
        var list = rows.ToList();
        var y = startY;
        for (var i = 0; i < list.Count; i += 1)
        {
            var (ar, value, en) = list[i];
            var isLast = highlightLastRow && i == list.Count - 1;
            var isAlt = zebra && i % 2 == 1;
            var currentRowHeight = isLast ? Math.Max(rowHeight + 4F, 22F) : rowHeight;
            panel.Controls.Add(CreateTripleRow(ar, value, en, y, isAlt, isLast, currentRowHeight));
            y += currentRowHeight;
        }

        panel.HeightF = startY + y - startY + 8F;
    }

    private XRPanel CreateTripleRow(
        string ar,
        string value,
        string en,
        float y,
        bool alternateRow = false,
        bool highlightTotal = false,
        float? fixedRowHeight = null)
    {
        var rowHeight = fixedRowHeight ?? (highlightTotal ? 30F : TripleRowHeight);
        var row = new XRPanel
        {
            LocationFloat = new DevExpress.Utils.PointFloat(0F, y),
            SizeF = new SizeF(ContentWidth, rowHeight),
            Borders = BorderSide.Bottom,
            BorderColor = BorderTone,
            BorderWidth = 0.5f,
            BackColor = highlightTotal ? AccentSoft : alternateRow ? RowAltBg : Color.Transparent
        };

        var arLabel = CreateLabel(0F, 0F, TripleArWidth, rowHeight, highlightTotal ? 9f : 8.5f, highlightTotal, ar, TextAlignment.MiddleRight);
        arLabel.ForeColor = highlightTotal ? AccentDark : HeaderText;
        var valueLabel = CreateLabel(TripleArWidth, 0F, TripleValueWidth, rowHeight, highlightTotal ? 10f : 8.5f, highlightTotal, value, TextAlignment.MiddleCenter);
        valueLabel.ForeColor = highlightTotal ? AccentDark : ValueText;
        var enLabel = CreateLabel(TripleArWidth + TripleValueWidth, 0F, TripleEnWidth, rowHeight, highlightTotal ? 9f : 8.5f, false, en, TextAlignment.MiddleLeft);
        enLabel.ForeColor = highlightTotal ? AccentDark : MutedText;

        row.Controls.AddRange(new XRControl[] { arLabel, valueLabel, enLabel });
        return row;
    }

    private static string JoinParts(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a))
        {
            return b ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(b) ? a : $"{a} — {b}";
    }

    private static string MetaCell(string ar, string en, string value) => $"{ar}\n{en}\n{value}";

    private void InitializeLayout()
    {
        _topMargin = new TopMarginBand { HeightF = 24F };
        _bottomMargin = new BottomMarginBand { HeightF = 24F };
        _reportHeader = new ReportHeaderBand { HeightF = 448F };
        _pageHeader = new PageHeaderBand { HeightF = 36F };
        _detail = new DetailBand { HeightF = 1F };
        _linesDetailReport = new DetailReportBand();
        _linesDetail = new DetailBand { HeightF = 36F };
        _reportFooter = new ReportFooterBand { HeightF = 248F };

        BuildReportHeader();
        BuildPageHeader();
        BuildLinesDetail();
        BuildReportFooter();

        Bands.AddRange(new Band[]
        {
            _topMargin, _bottomMargin, _reportHeader, _pageHeader, _detail, _linesDetailReport, _reportFooter
        });

        PageWidth = 827;
        PageHeight = 1169;
    }

    private void BuildReportHeader()
    {
        var accentBar = CreatePanel(0F, 0F, ContentWidth, 4F, Accent);
        accentBar.Borders = BorderSide.None;

        var qrX = ContentWidth - QrSize;
        _logoPicture = CreatePicture(0F, 10F, 72F, 72F);
        _qrBarcode = CreateZatcaQrBarcode(qrX, QrTop, QrSize, QrSize);
        _qrPicture = CreatePicture(qrX, QrTop, QrSize, QrSize);
        _qrPicture.Visible = false;
        ApplyQrControlLayout(_qrPicture);
        _qrPicture.Padding = new PaddingInfo(8, 8, 8, 8, 100F);
        _qrCaptionLabel = CreateLabel(0F, 104F, 150F, 20F, 7.5f, false,
            "رمز الاستجابة السريعة للتحقق\nQR Code for Verification", TextAlignment.MiddleCenter);
        _qrCaptionLabel.ForeColor = HeaderText;
        _qrStatusLabel = CreateLabel(24F, 126F, 102F, 24F, 8f, true,
            "فاتورة ضريبية\nTax Invoice", TextAlignment.MiddleCenter);
        _qrStatusLabel.ForeColor = Color.FromArgb(22, 101, 52);
        _qrStatusLabel.BackColor = Color.FromArgb(220, 252, 231);
        _qrStatusLabel.Borders = BorderSide.All;
        _qrStatusLabel.BorderColor = Color.FromArgb(134, 239, 172);

        _hotelNameLabel = CreateLabel(0F, 86F, 220F, 24F, 11.5f, true, string.Empty, TextAlignment.TopCenter);
        _hotelNameLabel.ForeColor = AccentDark;
        _hotelDetailsLabel = CreateLabel(0F, 110F, 220F, 52F, 8f, false, string.Empty, TextAlignment.TopCenter);
        _hotelDetailsLabel.ForeColor = MutedText;

        _titleArLabel = CreateLabel(180F, 12F, 360F, 26F, 15f, true, "فاتورة ضريبية", TextAlignment.MiddleCenter);
        _titleArLabel.ForeColor = AccentDark;
        _titleEnLabel = CreateLabel(180F, 38F, 360F, 16F, 9f, false, "SIMPLIFIED TAX INVOICE", TextAlignment.MiddleCenter);
        _titleEnLabel.ForeColor = MutedText;
        _invoiceSubtitleLabel = CreateLabel(180F, 58F, 360F, 30F, 8f, true,
            "ضريبة القيمة المضافة - معاودة التحصيل\nVAT - Reverse Charge", TextAlignment.MiddleCenter);
        _invoiceSubtitleLabel.ForeColor = AccentDark;

        _metaPanel = CreatePanel(0F, MetaPanelTop, ContentWidth, MetaPanelHeight + 4F, PanelBg);
        _metaPanel.Padding = new PaddingInfo(6, 6, 6, 6, 100F);
        var colW = (ContentWidth - 12F) / 5F;
        _metaInvoiceNo = MetaLabel(6F, 8F, colW, 44F);
        _metaResNo = MetaLabel(6F + colW, 8F, colW, 44F);
        _metaDateGreg = MetaLabel(6F + colW * 2F, 8F, colW, 44F);
        _metaDateHijri = MetaLabel(6F + colW * 3F, 8F, colW, 44F);
        _metaPeriod = MetaLabel(6F + colW * 4F, 8F, colW, 44F);
        foreach (var meta in new[] { _metaInvoiceNo, _metaResNo, _metaDateGreg, _metaDateHijri, _metaPeriod })
        {
            meta.BackColor = Color.White;
            meta.Borders = BorderSide.All;
            meta.BorderColor = BorderTone;
            meta.Padding = new PaddingInfo(4, 4, 4, 4, 100F);
        }
        _metaPanel.Controls.AddRange(new XRControl[]
        {
            _metaInvoiceNo, _metaResNo, _metaDateGreg, _metaDateHijri, _metaPeriod
        });

        _visitorShell = CreatePanel(0F, VisitorShellTop, ContentWidth, 196F, Color.White);
        _visitorShell.Padding = new PaddingInfo(0, 0, 0, 0, 100F);
        _visitorTitleLabel = CreateLabel(8F, 6F, ContentWidth - 16F, 18F, 9.5f, true,
            "بيانات الزائر  |  Visitor Information", TextAlignment.TopRight);
        _visitorTitleLabel.ForeColor = AccentDark;
        _visitorPanel = CreatePanel(8F, 26F, ContentWidth - 16F, 168F, Color.White);
        _visitorPanel.Borders = BorderSide.All;
        _visitorPanel.BorderColor = BorderTone;
        _visitorShell.Controls.AddRange(new XRControl[] { _visitorTitleLabel, _visitorPanel });

        _reportHeader.Controls.AddRange(new XRControl[]
        {
            accentBar,
            _logoPicture, _qrBarcode, _qrPicture,
            _qrCaptionLabel, _qrStatusLabel,
            _hotelNameLabel, _hotelDetailsLabel,
            _titleArLabel, _titleEnLabel, _invoiceSubtitleLabel,
            _metaPanel, _visitorShell
        });
    }

    private void BuildPageHeader()
    {
        _tableHeaderPanel = CreatePanel(0F, 0F, ContentWidth, 36F, TableHeaderBg);
        // Exact column order matching enterprise tax invoice images (RTL):
        // Description | Qty | Price | Price After Discount | Lodging Tax (2.5%) | VAT (15%) | Total
        AddHeaderCells(_tableHeaderPanel, 36F, new (float x, float w, string t)[]
        {
            (0F, 210F, "العنصر والوصف\nItem Description"),
            (210F, 48F, "الكمية\nQty"),
            (258F, 78F, "السعر\nPrice"),
            (336F, 82F, "السعر بعد الخصم\nPrice After Discount"),
            (418F, 82F, "رسم إشغال الإيواء\n(2.50%)\nLodging Tax"),
            (500F, 78F, "ضريبة القيمة المضافة\n(15.00%)\nVAT"),
            (578F, 149F, "المجموع\nTotal")
        });
        _pageHeader.Controls.Add(_tableHeaderPanel);
    }

    private void BuildLinesDetail()
    {
        // Match exact column widths and bindings from BuildPageHeader
        _lineDescriptionLabel = BoundLabel(0F, 0F, 210F, 36F, "Description");
        _lineDescriptionLabel.TextAlignment = TextAlignment.TopRight;

        _lineQtyLabel = BoundLabel(210F, 0F, 48F, 36F, "Quantity", "{0:N0}");

        _linePriceLabel = BoundLabel(258F, 0F, 78F, 36F, "UnitPrice", "{0:N2}");

        _linePriceAfterDiscountLabel = BoundLabel(336F, 0F, 82F, 36F, "PriceAfterDiscount", "{0:N2}");

        _lineLodgingLabel = BoundLabel(418F, 0F, 82F, 36F, "LodgingTaxAmount", "{0:N2}");

        _lineVatLabel = BoundLabel(500F, 0F, 78F, 36F, "VatAmount", "{0:N2}");

        _lineTotalLabel = BoundLabel(578F, 0F, 149F, 36F, "TotalWithVat", "{0:N2}");

        foreach (var lbl in new[]
                 {
                     _lineDescriptionLabel, _lineQtyLabel, _linePriceLabel,
                     _linePriceAfterDiscountLabel, _lineLodgingLabel, _lineVatLabel, _lineTotalLabel
                 })
        {
            lbl.Borders = BorderSide.All;
            lbl.BorderColor = BorderTone;
            lbl.Padding = new PaddingInfo(4, 4, 4, 4, 100F);
            lbl.Font = new Font("Arial", 9f);
        }

        _linesDetail.Controls.AddRange(new XRControl[]
        {
            _lineDescriptionLabel, _lineQtyLabel, _linePriceLabel,
            _linePriceAfterDiscountLabel, _lineLodgingLabel, _lineVatLabel, _lineTotalLabel
        });
        _linesDetailReport.Bands.Add(_linesDetail);
        _linesDetailReport.DataMember = string.Empty;
    }

    private void BuildReportFooter()
    {
        var topLine = HLine(0F, 0F, ContentWidth);

        var summaryShell = CreatePanel(0F, 6F, ContentWidth, 128F, Color.White);
        summaryShell.Borders = BorderSide.All;
        summaryShell.BorderColor = BorderTone;
        var summaryTitle = CreateLabel(8F, 5F, ContentWidth - 16F, 16F, 8.5f, true,
            "ملخص المبالغ  |  Amount Summary", TextAlignment.TopRight);
        summaryTitle.ForeColor = AccentDark;
        _summaryPanel = CreatePanel(8F, 22F, ContentWidth - 16F, 100F, Color.White);
        _summaryPanel.Borders = BorderSide.None;
        summaryShell.Controls.AddRange(new XRControl[] { summaryTitle, _summaryPanel });

        _amountWordsPanel = CreatePanel(0F, 140F, ContentWidth, 28F, Color.White);
        _amountWordsPanel.Borders = BorderSide.All;
        _amountWordsPanel.BorderColor = BorderTone;
        _amountWordsPanel.Visible = false;

        var signaturesPanel = CreatePanel(0F, 146F, ContentWidth, 56F, PanelBg);
        _customerSignatureCaption = CreateLabel(8F, 4F, 340F, 22F, 8f, false,
            "توقيع العميل / Customer Signature", TextAlignment.TopRight);
        _customerSignatureCaption.ForeColor = HeaderText;
        _customerSignatureLine = HLine(8F, 42F, 340F);

        _operatorSignatureCaption = CreateLabel(379F, 4F, 340F, 22F, 8f, false,
            "توقيع المستخدم / User Signature", TextAlignment.TopRight);
        _operatorSignatureCaption.ForeColor = HeaderText;
        _operatorSignaturePicture = CreatePicture(480F, 24F, 220F, 24F);
        _operatorSignaturePicture.Sizing = ImageSizeMode.ZoomImage;
        _operatorSignatureLine = HLine(379F, 42F, 340F);

        signaturesPanel.Controls.AddRange(new XRControl[]
        {
            _customerSignatureCaption, _customerSignatureLine,
            _operatorSignatureCaption, _operatorSignaturePicture, _operatorSignatureLine
        });

        _zatcaStatusLabel = CreateLabel(0F, 208F, ContentWidth, 16F, 8f, false, string.Empty, TextAlignment.TopRight);
        _zatcaStatusLabel.ForeColor = MutedText;
        _receiptsLabel = CreateLabel(0F, 226F, ContentWidth, 18F, 8f, false, string.Empty, TextAlignment.TopRight);
        _receiptsLabel.ForeColor = MutedText;
        _footerLabel = CreateLabel(0F, 248F, ContentWidth, 14F, 7.5f, false, string.Empty, TextAlignment.MiddleCenter);
        _footerLabel.ForeColor = MutedText;

        _reportFooter.HeightF = 268F;
        _reportFooter.Controls.AddRange(new XRControl[]
        {
            topLine, summaryShell, _amountWordsPanel, signaturesPanel,
            _zatcaStatusLabel, _receiptsLabel, _footerLabel
        });
    }

    private void AddHeaderCells(XRPanel panel, float h, (float x, float w, string t)[] cols)
    {
        foreach (var (x, w, t) in cols)
        {
            var cell = CreateLabel(x, 0F, w, h, 8.5f, false, t, TextAlignment.MiddleCenter);
            cell.ForeColor = HeaderText;
            cell.Borders = BorderSide.All;
            cell.BorderColor = BorderTone;
            cell.BackColor = TableHeaderBg;
            panel.Controls.Add(cell);
        }
    }

    private XRLabel MetaLabel(float x, float y, float w, float h)
    {
        var lbl = CreateLabel(x, y, w, h, 8f, false, string.Empty, TextAlignment.MiddleCenter);
        lbl.ForeColor = ValueText;
        return lbl;
    }

    private XRLabel BoundLabel(float x, float y, float w, float h, string field, string? fmt = null)
    {
        var lbl = CreateLabel(x, y, w, h, 8.5f, false, string.Empty, TextAlignment.MiddleCenter);
        lbl.ForeColor = ValueText;
        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", field));
        if (!string.IsNullOrWhiteSpace(fmt))
        {
            lbl.TextFormatString = fmt;
        }

        return lbl;
    }

    private static XRPanel CreatePanel(float x, float y, float w, float h, Color bg) =>
        new()
        {
            LocationFloat = new DevExpress.Utils.PointFloat(x, y),
            SizeF = new SizeF(w, h),
            BackColor = bg,
            Borders = BorderSide.All,
            BorderColor = BorderTone
        };

    private static XRLine HLine(float x, float y, float w) =>
        new()
        {
            LocationFloat = new DevExpress.Utils.PointFloat(x, y),
            SizeF = new SizeF(w, 1F),
            ForeColor = BorderTone
        };

    private XRLabel CreateLabel(float x, float y, float w, float h, float size, bool bold, string text, TextAlignment align)
    {
        var lbl = new XRLabel
        {
            LocationFloat = new DevExpress.Utils.PointFloat(x, y),
            SizeF = new SizeF(w, h),
            Text = text,
            TextAlignment = align,
            WordWrap = true,
            Multiline = true,
            Font = bold ? GetBoldFont(size) : GetRegularFont(size),
            ForeColor = BodyText,
            Padding = new PaddingInfo(4, 4, 4, 4, 100F)
        };
        return lbl;
    }

    private static XRPictureBox CreatePicture(float x, float y, float w, float h) =>
        new()
        {
            LocationFloat = new DevExpress.Utils.PointFloat(x, y),
            SizeF = new SizeF(w, h),
            Sizing = ImageSizeMode.ZoomImage
        };

    private static void ConfigureZatcaQrSymbology(XRBarCode barcode)
    {
        if (barcode.Symbology is not QRCodeGenerator qr)
        {
            barcode.Symbology = new QRCodeGenerator();
            qr = (QRCodeGenerator)barcode.Symbology;
        }

        qr.CompactionMode = QRCodeCompactionMode.Byte;
        qr.ErrorCorrectionLevel = QRCodeErrorCorrectionLevel.Q;
        qr.IncludeQuietZone = true;
        barcode.AutoModule = true;
        barcode.ShowText = false;
        barcode.BarCodeOrientation = BarCodeOrientation.Normal;
        // Legacy ZATCA receipt template padding (26, 26, 0, 0).
        barcode.Padding = new PaddingInfo(26, 26, 0, 0, 100F);
    }

    private static XRBarCode CreateZatcaQrBarcode(float x, float y, float w, float h)
    {
        var barcode = new XRBarCode
        {
            LocationFloat = new DevExpress.Utils.PointFloat(x, y),
            SizeF = new SizeF(w, h),
            Symbology = new QRCodeGenerator()
        };
        ConfigureZatcaQrSymbology(barcode);
        ApplyQrControlLayout(barcode);
        return barcode;
    }
}
