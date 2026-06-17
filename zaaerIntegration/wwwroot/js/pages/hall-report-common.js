(function (window, $) {
    "use strict";

    const CACHE = "20260614resTab1";

    /** Reuse one browser tab for reservation detail links from reports. */
    const RESERVATION_DETAIL_TAB_TARGET = "pmsReservationDetail";

    function loc() {
        return window.Zaaer && window.Zaaer.LocalizationService;
    }

    function api() {
        return window.Zaaer && window.Zaaer.ApiService;
    }

    function t(key) {
        const l = loc();
        return l && typeof l.t === "function" ? l.t(key) : key;
    }

    function gridOpts() {
        return window.Zaaer && window.Zaaer.PmsGridOptions;
    }

    function hallSvc() {
        return window.Zaaer && window.Zaaer.HallEventsService;
    }

    function formatLocalDateParam(value) {
        if (!value) {
            return null;
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return null;
        }
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function startOfMonthDate() {
        const n = new Date();
        return new Date(n.getFullYear(), n.getMonth(), 1);
    }

    function todayDate() {
        return new Date();
    }

    function resolveDefaultFromDate(cfg) {
        return cfg && cfg.defaultFromDate === "today" ? todayDate() : startOfMonthDate();
    }

    function rowField(row, camel) {
        if (!row) {
            return undefined;
        }
        if (row[camel] !== undefined) {
            return row[camel];
        }
        const pascal = `${camel.charAt(0).toUpperCase()}${camel.slice(1)}`;
        return row[pascal];
    }

    function rowMoney(row, field) {
        const n = Number(rowField(row, field));
        return Number.isNaN(n) ? 0 : n;
    }

    function sumRowMoney(rows, field) {
        return (rows || []).reduce((sum, row) => sum + rowMoney(row, field), 0);
    }

    function isGridFilterActive(grid) {
        if (!grid) {
            return false;
        }
        const search = `${grid.option("searchPanel.text") || ""}`.trim();
        if (search) {
            return true;
        }
        return !!grid.getCombinedFilter(true);
    }

    function getFilteredRowsFromGrid(grid, allRows) {
        if (!grid || !Array.isArray(allRows)) {
            return allRows || [];
        }
        if (!isGridFilterActive(grid)) {
            return allRows;
        }
        const filterExpr = grid.getCombinedFilter(true);
        if (!filterExpr) {
            return allRows;
        }
        if (!window.DevExpress || !window.DevExpress.data || typeof window.DevExpress.data.query !== "function") {
            return allRows;
        }
        return window.DevExpress.data.query(allRows).filter(filterExpr).toArray();
    }

    function isDailyJournalInflowCode(code) {
        const normalized = `${code || ""}`.trim().toLowerCase();
        return normalized === "receipt" || normalized === "service_receipt" || normalized === "security_deposit";
    }

    function isDailyJournalOutflowCode(code) {
        const normalized = `${code || ""}`.trim().toLowerCase();
        return normalized === "refund" || normalized === "security_deposit_refund";
    }

    function buildVoucherBreakdownFromRows(rows) {
        const grouped = {};
        (rows || []).forEach((row) => {
            const code = `${rowField(row, "voucherCode") || ""}`.trim().toLowerCase();
            if (!code) {
                return;
            }
            if (!grouped[code]) {
                grouped[code] = {
                    voucherCode: code,
                    voucherLabel: mapVoucherLabelDisplay(row),
                    count: 0,
                    totalAmount: 0
                };
            }
            grouped[code].count += 1;
            grouped[code].totalAmount += rowMoney(row, "amountPaid");
        });
        return Object.values(grouped).sort((a, b) => {
            const order = { receipt: 1, service_receipt: 2, security_deposit: 3, refund: 4, security_deposit_refund: 5 };
            return (order[a.voucherCode] || 99) - (order[b.voucherCode] || 99);
        });
    }

    function buildPaymentMethodBreakdownFromRows(rows) {
        const grouped = {};
        (rows || []).forEach((row) => {
            const label = mapPaymentMethodDisplay(rowField(row, "paymentMethod")) || "—";
            if (!grouped[label]) {
                grouped[label] = { paymentMethodLabel: label, count: 0, totalAmount: 0 };
            }
            grouped[label].count += 1;
            grouped[label].totalAmount += rowMoney(row, "amountPaid");
        });
        return Object.values(grouped).sort((a, b) => b.totalAmount - a.totalAmount || a.paymentMethodLabel.localeCompare(b.paymentMethodLabel));
    }

    function computeDailyJournalSummaryFromRows(rows) {
        let inflow = 0;
        let outflow = 0;
        (rows || []).forEach((row) => {
            const code = rowField(row, "voucherCode");
            const amount = rowMoney(row, "amountPaid");
            if (isDailyJournalInflowCode(code)) {
                inflow += amount;
            } else if (isDailyJournalOutflowCode(code)) {
                outflow += amount;
            }
        });
        return {
            totalAmount: inflow - outflow,
            voucherBreakdown: buildVoucherBreakdownFromRows(rows),
            paymentMethodBreakdown: buildPaymentMethodBreakdownFromRows(rows)
        };
    }

    function computeOnlineBookingsSummaryFromRows(rows) {
        const grouped = {};
        (rows || []).forEach((row) => {
            const source = `${rowField(row, "source") || ""}`.trim() || "—";
            if (!grouped[source]) {
                grouped[source] = { source, count: 0, totalAmount: 0 };
            }
            grouped[source].count += 1;
            grouped[source].totalAmount += rowMoney(row, "totalAmount");
        });
        return {
            count: rows.length,
            totalAmount: sumRowMoney(rows, "totalAmount"),
            sourceBreakdown: Object.values(grouped).sort((a, b) => b.totalAmount - a.totalAmount || a.source.localeCompare(b.source))
        };
    }

    function computeBookingsSummaryFromRows(rows) {
        return {
            count: rows.length,
            totalAmount: sumRowMoney(rows, "totalAmount"),
            totalPaid: sumRowMoney(rows, "amountPaid"),
            totalBalance: sumRowMoney(rows, "balanceAmount"),
            totalRefunded: sumRowMoney(rows, "refunded"),
            totalSecurityDeposit: sumRowMoney(rows, "securityDeposit")
        };
    }

    function computeSimpleCountAmountSummaryFromRows(rows, amountField) {
        return {
            count: rows.length,
            totalAmount: sumRowMoney(rows, amountField || "amount")
        };
    }

    function appendNetAmountKpiValue($host, amount, fmtMoneyFn) {
        $host.empty();
        const $amount = $("<span/>").addClass("hall-reports-kpi__amount").text(fmtMoneyFn(amount)).appendTo($host);
        if (isArabicUi()) {
            $("<img/>", {
                src: "/logo/sar-symbol.svg",
                alt: "",
                class: "hall-reports-kpi__currency-icon",
                width: 18,
                height: 18
            }).appendTo($host);
        } else {
            $("<span/>").addClass("hall-reports-kpi__currency-code").text("SAR").appendTo($host);
        }
        return $amount;
    }

    function buildSerialNumberColumn(captionKey) {
        return {
            caption: t(captionKey || "hallReports.col.serial"),
            width: 56,
            alignment: "center",
            allowFiltering: false,
            allowSorting: false,
            allowHeaderFiltering: false,
            cellTemplate(container, info) {
                const pageIndex = info.component.pageIndex();
                const pageSize = info.component.pageSize();
                const serial = pageIndex * pageSize + info.row.rowIndex + 1;
                $("<span/>").text(serial).appendTo(container);
            }
        };
    }

    function fmtMoney(value) {
        const n = Number(value);
        if (Number.isNaN(n)) {
            return "";
        }
        return n.toLocaleString("en-GB", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function fmtDate(value) {
        if (!value) {
            return "";
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const day = String(d.getDate()).padStart(2, "0");
        const month = String(d.getMonth() + 1).padStart(2, "0");
        return `${day}/${month}/${d.getFullYear()}`;
    }

    function isArabicUi() {
        const l = loc();
        return !!(l && typeof l.isArabic === "function" && l.isArabic());
    }

    function normalizePaymentMethodKey(name) {
        return String(name || "")
            .trim()
            .toLowerCase()
            .replace(/\s+/g, " ");
    }

    function mapPaymentMethodDisplay(name) {
        const raw = name == null ? "" : String(name).trim();
        if (!raw) {
            return "";
        }

        const key = normalizePaymentMethodKey(raw);
        const arabicMap = {
            cash: "نقدي",
            mada: "مدى",
            expedia: "اكسبيديا",
            "master card": "ماستر كارد",
            mastercard: "ماستر كارد",
            visa: "فيزا",
            agoda: "أجودا",
            "bank transfer": "تحويل بنكي",
            globaleit: "جلوبال ايت",
            wego: "ويجو",
            rehlat: "رحلات",
            otherotas: "مواقع أخرى",
            webbeds: "ويب بيدز"
        };
        const englishMap = {
            cash: "Cash",
            mada: "Mada",
            expedia: "Expedia",
            "master card": "Master Card",
            mastercard: "Master Card",
            visa: "Visa",
            agoda: "Agoda",
            "bank transfer": "Bank transfer",
            globaleit: "Globaleit",
            wego: "Wego",
            rehlat: "Rehlat",
            otherotas: "Other OTAs",
            webbeds: "WebBeds"
        };

        if (isArabicUi()) {
            return arabicMap[key] || raw;
        }

        return englishMap[key] || raw;
    }

    function normalizeStatusKey(value) {
        return String(value || "")
            .trim()
            .toLowerCase()
            .replace(/[\s-]+/g, "_");
    }

    function translateKeyCandidates(baseKey, slug) {
        const raw = String(slug || "").trim().toLowerCase();
        if (!raw) {
            return "";
        }
        const normalized = normalizeStatusKey(raw);
        const keys = [`${baseKey}.${normalized}`, `${baseKey}.${raw.replace(/\s+/g, "_")}`];
        for (let i = 0; i < keys.length; i += 1) {
            const translated = t(keys[i]);
            if (translated !== keys[i]) {
                return translated;
            }
        }
        return "";
    }

    function mapStatusDisplay(status) {
        const raw = status == null ? "" : String(status).trim();
        if (!raw) {
            return "";
        }
        return (
            translateKeyCandidates("hotelReports.status", raw)
            || translateKeyCandidates("hallReports.status", raw)
            || raw
        );
    }

    function mapReceiptStatusDisplay(status) {
        return mapStatusDisplay(status);
    }

    function resolveVoucherCodeFromRow(row) {
        if (!row) {
            return "";
        }
        const code = `${row.voucherCode || row.VoucherCode || ""}`.trim().toLowerCase();
        if (code) {
            return code;
        }
        const type = `${row.receiptType || row.ReceiptType || ""}`.trim().toLowerCase();
        if (type === "refund" || type === "receipt") {
            return type;
        }
        const label = `${row.voucherLabel || row.VoucherLabel || ""}`.trim();
        const byLabel = {
            "سند قبض إيجار": "receipt",
            "سند قبض ايجار": "receipt",
            "سند قبض خدمات": "service_receipt",
            "سند قبض تأمين": "security_deposit",
            "سند صرف إيجار": "refund",
            "سند صرف تأمين": "security_deposit_refund",
            "rent receipt": "receipt",
            "service receipt": "service_receipt",
            "security deposit receipt": "security_deposit",
            "rent disbursement": "refund",
            "security deposit refund": "security_deposit_refund"
        };
        return byLabel[label] || byLabel[label.toLowerCase()] || type || label.toLowerCase();
    }

    function mapVoucherLabelDisplay(row) {
        const code = resolveVoucherCodeFromRow(row);
        if (!code) {
            return "";
        }
        const translated =
            translateKeyCandidates("hotelReports.voucher", code)
            || translateKeyCandidates("hallReports.voucher", code);
        if (translated) {
            return translated;
        }
        return row.voucherLabel ?? row.VoucherLabel ?? row.receiptType ?? row.ReceiptType ?? code;
    }

    function mapCreditTypeDisplay(value) {
        const raw = value == null ? "" : String(value).trim();
        if (!raw) {
            return "";
        }
        return (
            translateKeyCandidates("hotelReports.creditType", raw)
            || translateKeyCandidates("hallReports.creditType", raw)
            || raw
        );
    }

    function mapInvoiceStatusDisplay(status) {
        const raw = status == null ? "" : String(status).trim();
        if (!raw) {
            return "";
        }
        return (
            translateKeyCandidates("hotelReports.invoiceStatus", raw)
            || mapStatusDisplay(raw)
        );
    }

    function mapApprovalStatusDisplay(status) {
        const raw = status == null ? "" : String(status).trim();
        if (!raw) {
            return "";
        }
        const normalized = normalizeStatusKey(raw);
        const expenseKey = `expenses.approvalAction.${normalized}`;
        const expenseLabel = t(expenseKey);
        if (expenseLabel !== expenseKey) {
            return expenseLabel;
        }
        return mapStatusDisplay(raw);
    }

    function mapPaymentSourceDisplay(value) {
        const raw = value == null ? "" : String(value).trim();
        if (!raw) {
            return "";
        }
        return (
            translateKeyCandidates("hotelReports.paymentSource", raw)
            || translateKeyCandidates("hallReports.paymentSource", raw)
            || mapPaymentMethodDisplay(raw)
            || raw
        );
    }

    function mapMovementLabelDisplay(label) {
        const raw = String(label || "").trim();
        if (!raw) {
            return "";
        }
        const normalizedArabic = raw.replace(/تحويل بنكي/g, "إيداع بنكي");
        const slugByLabel = {
            "رصيد افتتاحي": "opening_balance",
            "سند قبض إيجار": "receipt",
            "سند قبض ايجار": "receipt",
            "سند قبض خدمات": "service_receipt",
            "سند قبض تأمين": "security_deposit",
            "سند صرف إيجار": "refund",
            "سند صرف تأمين": "security_deposit_refund",
            "إيداع بنكي": "bank_deposit",
            "تحويل بنكي": "bank_deposit",
            "مصروف": "expense",
            "إلغاء أثر مصروف": "expense_reversal"
        };
        const slug = slugByLabel[normalizedArabic] || slugByLabel[raw.toLowerCase()];
        if (slug) {
            const translated =
                translateKeyCandidates("hotelReports.movement", slug)
                || translateKeyCandidates("hallReports.movement", slug);
            if (translated) {
                return translated;
            }
        }
        return isArabicUi() ? normalizedArabic : raw;
    }

    function unwrapData(res) {
        if (!res) {
            return res;
        }
        return res.data !== undefined ? res.data : (res.Data !== undefined ? res.Data : res);
    }

    function unwrapItems(res) {
        const data = unwrapData(res);
        if (Array.isArray(data)) {
            return data;
        }
        if (data && Array.isArray(data.items)) {
            return data.items;
        }
        if (data && Array.isArray(data.Items)) {
            return data.Items;
        }
        return [];
    }

    function unwrapSummary(res) {
        const data = unwrapData(res);
        if (!data) {
            return null;
        }
        return data.summary || data.Summary || null;
    }

    function hotelCodeParam() {
        const a = api();
        return a && typeof a.getHotelCode === "function" ? a.getHotelCode() : "";
    }

    function reservationRouteId(row) {
        if (!row) {
            return null;
        }
        const z = row.reservationZaaerId ?? row.ReservationZaaerId ?? row.zaaerId ?? row.ZaaerId;
        if (z != null && Number(z) > 0) {
            return Number(z);
        }
        const rid = row.reservationRouteId ?? row.ReservationRouteId
            ?? row.reservationId ?? row.ReservationId;
        return rid != null && Number(rid) > 0 ? Number(rid) : null;
    }

    function reservationDetailUrl(routeId) {
        if (!routeId) {
            return "";
        }
        const params = new URLSearchParams();
        params.set("id", String(routeId));
        const hc = hotelCodeParam();
        if (hc) {
            params.set("hotelCode", hc);
        }
        return `/reservation-detail.html?${params.toString()}`;
    }

    function voucherDetailUrl(routeId, docZaaerId, tab) {
        const base = reservationDetailUrl(routeId);
        if (!base) {
            return "";
        }
        const params = new URLSearchParams(base.split("?")[1] || "");
        params.set("section", "payments");
        if (tab) {
            params.set("tab", tab);
        }
        if (docZaaerId != null && docZaaerId !== "") {
            params.set("docZaaerId", String(docZaaerId));
        }
        return `/reservation-detail.html?${params.toString()}`;
    }

    function invoiceViewerUrl(row) {
        const linkedZaaerId = row.linkedInvoiceZaaerId ?? row.LinkedInvoiceZaaerId;
        const linkedInternalId = row.linkedInvoiceId ?? row.LinkedInvoiceId;
        const zaaerId =
            linkedZaaerId != null && Number(linkedZaaerId) > 0
                ? linkedZaaerId
                : row.documentZaaerId ?? row.DocumentZaaerId;
        const internalId =
            linkedInternalId != null && Number(linkedInternalId) > 0
                ? linkedInternalId
                : row.documentId ?? row.DocumentId;
        const params = new URLSearchParams();
        if (zaaerId != null && Number(zaaerId) > 0) {
            params.set("zaaerId", String(zaaerId));
        } else if (internalId != null && Number(internalId) > 0) {
            params.set("invoiceId", String(internalId));
        } else {
            return "";
        }
        const hc = hotelCodeParam();
        if (hc) {
            params.set("hotelCode", hc);
        }
        return `/invoice-report-viewer.html?${params.toString()}`;
    }

    function depositDetailUrl(receiptId) {
        if (!receiptId) {
            return "";
        }
        const params = new URLSearchParams();
        params.set("receiptId", String(receiptId));
        const hc = hotelCodeParam();
        if (hc) {
            params.set("hotelCode", hc);
        }
        return `/deposits.html?${params.toString()}`;
    }

    function expenseDetailUrl(expenseId) {
        if (!expenseId) {
            return "";
        }
        const params = new URLSearchParams();
        params.set("expenseId", String(expenseId));
        const hc = hotelCodeParam();
        if (hc) {
            params.set("hotelCode", hc);
        }
        return `/expenses.html?${params.toString()}`;
    }

    function renderLinkCell(container, text, href, target) {
        const label = (text || "").trim() || "—";
        if (!href) {
            $("<span/>").text(label).appendTo(container);
            return;
        }
        const $a = $("<a/>")
            .addClass("hall-reports-link")
            .attr("href", href)
            .text(label);
        if (target === false) {
            /* same tab */
        } else if (typeof target === "string" && target) {
            $a.attr("target", target).attr("rel", "noopener noreferrer");
        } else if (target !== false) {
            $a.attr("target", "_blank").attr("rel", "noopener noreferrer");
        }
        $("<span/>").addClass("dx-icon dx-icon-link").prependTo($a);
        $a.appendTo(container);
    }

    function openCustomerReadOnlyPopup(customerId) {
        const svc = window.Zaaer && window.Zaaer.PmsCustomerService;
        const id = Number(customerId);
        if (!svc || !id || Number.isNaN(id)) {
            DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3000);
            return;
        }

        const $host = $("<div/>").appendTo("body");
        $host.dxPopup({
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            showTitle: true,
            title: t("hotelReports.customer.viewTitle"),
            visible: true,
            showCloseButton: true,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup guest-picker-popup hall-reports-customer-popup" },
            onHidden() {
                $host.remove();
            },
            contentTemplate() {
                const $wrap = $("<div/>").addClass("hall-reports-customer-popup__body");
                $("<div/>").addClass("hall-reports-customer-popup__loading").text(t("common.loading")).appendTo($wrap);
                return $wrap;
            },
            onShown(e) {
                const popup = e.component;
                const $body = $(popup.content()).find(".hall-reports-customer-popup__body");
                svc.getCustomer(id).then((customer) => {
                    if (!customer) {
                        popup.hide();
                        DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3000);
                        return;
                    }
                    $body.empty();
                    const primaryId = (customer.identifications || customer.Identifications || [])
                        .find((item) => item.isPrimary || item.IsPrimary) || {};
                    const fields = [
                        { label: t("reservationDetail.guest.name"), value: rowField(customer, "customerName") },
                        { label: t("reservationDetail.guest.phone"), value: rowField(customer, "mobileNo") },
                        { label: t("reservationDetail.guest.email"), value: rowField(customer, "email") },
                        { label: t("reservationDetail.guest.nationality"), value: rowField(customer, "nationalityNameAr") || rowField(customer, "nationalityName") },
                        { label: t("reservationDetail.guest.idType"), value: rowField(primaryId, "idTypeNameAr") || rowField(primaryId, "idTypeName") },
                        { label: t("reservationDetail.guest.idNo"), value: rowField(primaryId, "idNumber") },
                        { label: t("reservationDetail.guest.birth"), value: fmtDate(rowField(customer, "birthday") || rowField(customer, "birthdateGregorian")) }
                    ];
                    const $grid = $("<div/>").addClass("hall-reports-customer-popup__grid").appendTo($body);
                    fields.forEach((field) => {
                        const value = `${field.value || ""}`.trim();
                        if (!value) {
                            return;
                        }
                        const $card = $("<div/>").addClass("hall-reports-customer-popup__field").appendTo($grid);
                        $("<div/>").addClass("hall-reports-customer-popup__label").text(field.label).appendTo($card);
                        $("<div/>").addClass("hall-reports-customer-popup__value").text(value).appendTo($card);
                    });
                    if (!$grid.children().length) {
                        $("<div/>").addClass("hall-reports-customer-popup__empty").text(t("activityLog.detailsNotFound")).appendTo($body);
                    }
                }).catch(() => {
                    popup.hide();
                    DevExpress.ui.notify(t("common.error"), "error", 3200);
                });
            }
        });
    }

    function renderCustomerLink(container, row) {
        const name = `${rowField(row, "customerName") || ""}`.trim() || "—";
        const customerId = rowField(row, "customerId");
        if (!customerId) {
            $("<span/>").text(name).appendTo(container);
            return;
        }
        const $a = $("<a/>", { href: "#", role: "button" })
            .addClass("hall-reports-link hall-reports-customer-link")
            .text(name)
            .on("click", (ev) => {
                ev.preventDefault();
                openCustomerReadOnlyPopup(customerId);
            });
        $("<span/>").addClass("dx-icon dx-icon-user").prependTo($a);
        $a.appendTo(container);
    }

    function renderReservationLink(container, row) {
        const no = row.reservationNo || row.ReservationNo || "";
        renderLinkCell(
            container,
            no,
            reservationDetailUrl(reservationRouteId(row)),
            RESERVATION_DETAIL_TAB_TARGET
        );
    }

    function renderVoucherLink(container, row, tab) {
        renderPaymentVoucherLink(container, row, { voucherTab: tab });
    }

    function canViewPaymentVoucherFromReport(kind) {
        const a = api();
        if (!a || typeof a.hasPermission !== "function") {
            return false;
        }
        return a.hasPermission("payments.view");
    }

    function canEditPaymentVoucherFromReport(kind) {
        const a = api();
        if (!a || typeof a.hasPermission !== "function") {
            return false;
        }
        if (kind === "disbursements") {
            return a.hasPermission("payments.refund_voucher.edit");
        }
        return a.hasPermission("payments.receipt_voucher.edit");
    }

    function paymentVoucherKindFromRow(row) {
        const code = `${row.voucherCode || row.VoucherCode || row.voucherLabel || row.VoucherLabel || ""}`
            .trim()
            .toLowerCase();
        if (code === "refund" || code === "security_deposit_refund" || code.includes("صرف")) {
            return "disbursements";
        }
        return "receipts";
    }

    function buildReportPaymentVoucherContext(row, editRow) {
        const routeId = reservationRouteId(row);
        const reservationZaaerId = row.reservationZaaerId ?? row.ReservationZaaerId ?? null;
        const reservationNo = row.reservationNo || row.ReservationNo || "";
        const match = editRow || {};
        const hotelId = match.hotelId || row.hotelId || row.HotelId || null;

        return {
            detail: {
                reservationId: routeId,
                zaaerId: reservationZaaerId,
                hotelId,
                customerId: match.customerId || row.customerId || row.CustomerId,
                header: { reservationNo }
            },
            reservationId: routeId,
            reservationRouteId: reservationZaaerId || routeId,
            reservationZaaerId: reservationZaaerId,
            hotelId,
            customerId: match.customerId || row.customerId || row.CustomerId || null,
            customerZaaerId: null,
            corporateId: null,
            reservationNo
        };
    }

    function openPaymentVoucherFromReportRow(row, options) {
        options = options || {};
        const zaaerId = row.receiptZaaerId ?? row.ReceiptZaaerId
            ?? row.documentZaaerId ?? row.DocumentZaaerId;
        if (!zaaerId) {
            DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
            return;
        }

        const kind = paymentVoucherKindFromRow(row);
        if (!canViewPaymentVoucherFromReport(kind) && !canEditPaymentVoucherFromReport(kind)) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
        const voucherUi = window.Zaaer && window.Zaaer.ReservationPaymentVoucherUi;
        if (!svc || !voucherUi) {
            DevExpress.ui.notify(t("common.error"), "error", 3200);
            return;
        }

        function openWithMatch(match) {
            if (!match || !match.zaaerId) {
                DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                return;
            }

            const context = buildReportPaymentVoucherContext(row, match);
            if (!context.hotelId) {
                DevExpress.ui.notify(t("common.error"), "error", 3200);
                return;
            }

            const readOnly = !canEditPaymentVoucherFromReport(kind) && canViewPaymentVoucherFromReport(kind);
            const popupOptions = {
                context,
                editRow: match,
                readOnly,
                afterSave: typeof options.afterSave === "function" ? options.afterSave : null
            };

            if (kind === "disbursements") {
                voucherUi.openDisbursementEdit(popupOptions);
            } else {
                voucherUi.openReceiptEdit(popupOptions);
            }
        }

        if (typeof svc.loadPaymentReceiptByZaaerId === "function") {
            svc.loadPaymentReceiptByZaaerId(zaaerId)
                .then(openWithMatch)
                .catch((err) => {
                    DevExpress.ui.notify((err && err.message) || t("common.error"), "error", 3200);
                });
            return;
        }

        const routeId = reservationRouteId(row);
        if (!routeId || typeof svc.loadPaymentRows !== "function") {
            DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
            return;
        }

        svc.loadPaymentRows({ kind, reservationId: routeId })
            .then((rows) => {
                const match = (rows || []).find((r) => Number(r.zaaerId) === Number(zaaerId));
                openWithMatch(match);
            })
            .catch((err) => {
                DevExpress.ui.notify((err && err.message) || t("common.error"), "error", 3200);
            });
    }

    function renderPaymentVoucherLink(container, row, options) {
        const no = row.receiptNo || row.ReceiptNo || row.documentNo || row.DocumentNo || "";
        const label = `${no}`.trim() || "—";
        const $btn = $("<button type='button' class='hall-reports-link hall-reports-link--button'/>").text(label);
        $("<span class='dx-icon dx-icon-link'/>").prependTo($btn);
        $btn.on("click", (e) => {
            e.preventDefault();
            openPaymentVoucherFromReportRow(row, options);
        });
        $btn.appendTo(container);
    }

    function canViewDepositFromReport() {
        const a = api();
        return !!(a && typeof a.hasPermission === "function" && a.hasPermission("finance.deposit.view"));
    }

    function canEditDepositFromReport() {
        const a = api();
        return !!(a && typeof a.hasPermission === "function" && a.hasPermission("finance.deposit.update"));
    }

    function openDepositFromReportRow(row) {
        const receiptId = row.receiptId ?? row.ReceiptId;
        if (!receiptId) {
            DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
            return;
        }
        if (!canViewDepositFromReport()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        const depositUi = window.Zaaer && window.Zaaer.DepositFormUi;
        const depositApi = window.Zaaer && window.Zaaer.PmsDepositService;
        const readOnly = !canEditDepositFromReport();
        const openForm = (detailRow) => {
            if (!detailRow) {
                DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                return;
            }
            if (depositUi && typeof depositUi.open === "function") {
                const ready = typeof depositUi.ensureReady === "function"
                    ? depositUi.ensureReady()
                    : $.Deferred().resolve().promise();
                ready.then(() => depositUi.open(detailRow, { readOnly }));
                return;
            }
            window.location.href = depositDetailUrl(receiptId);
        };

        if (depositUi && typeof depositUi.open === "function" && row.receiptDate) {
            openForm(row);
            return;
        }

        if (!depositApi || typeof depositApi.getById !== "function") {
            window.location.href = depositDetailUrl(receiptId);
            return;
        }

        depositApi.getById(receiptId).then(openForm).catch((err) => {
            DevExpress.ui.notify((err && err.message) || t("common.error"), "error", 3200);
        });
    }

    function renderDepositLink(container, row) {
        const no = row.receiptNo || row.ReceiptNo || "";
        const label = `${no}`.trim() || "—";
        const $btn = $("<button type='button' class='hall-reports-link hall-reports-link--button'/>").text(label);
        $("<span class='dx-icon dx-icon-link'/>").prependTo($btn);
        $btn.on("click", (e) => {
            e.preventDefault();
            openDepositFromReportRow(row);
        });
        $btn.appendTo(container);
    }

    function canViewExpenseFromReport() {
        const a = api();
        return !!(a && typeof a.hasPermission === "function" && a.hasPermission("finance.expense.view"));
    }

    function canEditExpenseFromReport() {
        const a = api();
        return !!(a && typeof a.hasPermission === "function" && a.hasPermission("finance.expense.update"));
    }

    function openExpenseFromReportRow(row) {
        const expenseId = row.expenseId ?? row.ExpenseId;
        if (!expenseId) {
            DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
            return;
        }
        if (!canViewExpenseFromReport()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        const expenseUi = window.Zaaer && window.Zaaer.ExpenseFormUi;
        const expenseApi = window.Zaaer && window.Zaaer.PmsExpenseService;
        const readOnly = !canEditExpenseFromReport();
        const openForm = (detailRow) => {
            if (!detailRow) {
                DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                return;
            }
            if (expenseUi && typeof expenseUi.open === "function") {
                const ready = typeof expenseUi.ensureReady === "function"
                    ? expenseUi.ensureReady()
                    : $.Deferred().resolve().promise();
                ready.then(() => expenseUi.open(detailRow, { readOnly }));
                return;
            }
            window.location.href = expenseDetailUrl(expenseId);
        };

        if (expenseUi && typeof expenseUi.open === "function" && (row.dateTime || row.DateTime)) {
            openForm(row);
            return;
        }

        if (!expenseApi || typeof expenseApi.getById !== "function") {
            window.location.href = expenseDetailUrl(expenseId);
            return;
        }

        expenseApi.getById(expenseId).then(openForm).catch((err) => {
            DevExpress.ui.notify((err && err.message) || t("common.error"), "error", 3200);
        });
    }

    function renderExpenseLink(container, row) {
        const no = row.expenseNo || row.ExpenseNo || row.number || row.Number || "";
        const label = `${no}`.trim() || "—";
        const $btn = $("<button type='button' class='hall-reports-link hall-reports-link--button'/>").text(label);
        $("<span class='dx-icon dx-icon-link'/>").prependTo($btn);
        $btn.on("click", (e) => {
            e.preventDefault();
            openExpenseFromReportRow(row);
        });
        $btn.appendTo(container);
    }

    function renderInvoiceLink(container, row) {
        const linkedNo = `${row.linkedInvoiceNo ?? row.LinkedInvoiceNo ?? ""}`.trim();
        const fallbackNo = `${row.documentNo ?? row.DocumentNo ?? ""}`.trim();
        const no = linkedNo || fallbackNo;
        if (!no) {
            $("<span/>").text("—").appendTo(container);
            return;
        }
        const href = invoiceViewerUrl(row);
        if (!href) {
            $("<span/>").text(no).appendTo(container);
            return;
        }
        renderLinkCell(container, no, href);
    }

    function canViewCreditNoteFromReport() {
        const a = api();
        return !!(a && typeof a.hasPermission === "function" && a.hasPermission("finance.credit_note.view"));
    }

    function unwrapReportApiData(response) {
        if (!response) {
            return null;
        }
        const top = response.data != null ? response.data : response;
        if (top && typeof top === "object" && top.data != null) {
            return top.data;
        }
        return top;
    }

    function openCreditNoteViewPopup(detail, reportRow) {
        const row = detail || {};
        const report = reportRow || {};
        const creditNoteNo =
            row.creditNoteNo
            || report.documentNo
            || report.DocumentNo
            || "";
        const $host = $("<div>").appendTo("body");
        $host.dxPopup({
            title:
                t("reservationDetail.payments.creditNote.popupTitle")
                + (creditNoteNo ? ` — ${creditNoteNo}` : ""),
            visible: true,
            showCloseButton: true,
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate(contentEl) {
                const $formHost = $("<div/>")
                    .css({
                        background: "var(--pms-panel-bg-strong, #f3f4f6)",
                        borderRadius: "12px",
                        padding: "14px"
                    })
                    .appendTo(contentEl);
                $formHost.dxForm({
                    readOnly: true,
                    formData: {
                        creditNoteNo,
                        creditNoteDate: row.creditNoteDate || report.documentDate || report.DocumentDate,
                        creditAmount: row.creditAmount ?? report.amount ?? report.Amount,
                        creditType: row.creditType || report.creditType || report.CreditType || "",
                        invoiceNo: row.invoiceNo || report.linkedInvoiceNo || report.LinkedInvoiceNo || "",
                        zatcaStatus: row.zatcaStatus || report.status || report.Status || "",
                        reason: row.reason || report.reason || report.Reason || "",
                        notes: row.notes || report.notes || report.Notes || ""
                    },
                    colCount: 1,
                    labelLocation: "top",
                    items: [
                        { dataField: "creditNoteNo", label: { text: t("hallReports.col.creditNoteNo") } },
                        {
                            dataField: "creditNoteDate",
                            label: { text: t("hallReports.col.docDate") },
                            editorType: "dxDateBox",
                            editorOptions: { displayFormat: "dd/MM/yyyy", readOnly: true }
                        },
                        {
                            dataField: "creditAmount",
                            label: { text: t("hallReports.col.amount") },
                            editorType: "dxNumberBox",
                            editorOptions: { format: "#,##0.00", readOnly: true }
                        },
                        { dataField: "creditType", label: { text: t("hallReports.col.creditType") } },
                        { dataField: "invoiceNo", label: { text: t("hallReports.col.linkedInvoiceNo") } },
                        { dataField: "zatcaStatus", label: { text: t("hallReports.col.status") } },
                        { dataField: "reason", label: { text: t("hallReports.col.reason") } },
                        { dataField: "notes", label: { text: t("hallReports.col.notes") } }
                    ]
                });
            },
            toolbarItems: [
                {
                    toolbar: "bottom",
                    location: "after",
                    widget: "dxButton",
                    options: {
                        text: t("common.close"),
                        onClick() {
                            $host.dxPopup("instance").hide();
                        }
                    }
                }
            ],
            onHidden() {
                $host.remove();
            }
        });
    }

    function openCreditNoteFromReportRow(row) {
        if (!canViewCreditNoteFromReport()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        const zaaerId = row.documentZaaerId ?? row.DocumentZaaerId;
        const apiInst = api();
        if (!zaaerId || !apiInst || typeof apiInst.get !== "function") {
            openCreditNoteViewPopup({
                creditNoteNo: row.documentNo || row.DocumentNo,
                creditNoteDate: row.documentDate || row.DocumentDate,
                creditAmount: row.amount ?? row.Amount,
                creditType: row.creditType || row.CreditType,
                invoiceNo: row.linkedInvoiceNo || row.LinkedInvoiceNo,
                zatcaStatus: row.status || row.Status,
                reason: row.reason || row.Reason,
                notes: row.notes || row.Notes
            }, row);
            return;
        }

        apiInst.get(`/api/v1/pms/credit-notes/by-zaaer/${encodeURIComponent(zaaerId)}`)
            .then((response) => {
                const detail = unwrapReportApiData(response);
                if (!detail || !detail.creditNoteId) {
                    DevExpress.ui.notify(t("activityLog.detailsNotFound"), "warning", 3200);
                    return;
                }
                openCreditNoteViewPopup(detail, row);
            })
            .catch((err) => {
                DevExpress.ui.notify((err && err.message) || t("common.error"), "error", 3200);
            });
    }

    function renderCreditNoteLink(container, row) {
        const no = `${row.documentNo || row.DocumentNo || ""}`.trim() || "—";
        const $btn = $("<button type='button' class='hall-reports-link hall-reports-link--button'/>").text(no);
        $("<span class='dx-icon dx-icon-link'/>").prependTo($btn);
        $btn.on("click", (e) => {
            e.preventDefault();
            openCreditNoteFromReportRow(row);
        });
        $btn.appendTo(container);
    }

    function moneyColumn(field, captionKey, width) {
        return {
            dataField: field,
            caption: t(captionKey),
            dataType: "number",
            width: width || 118,
            alignment: "right",
            cssClass: "hall-reports-amount",
            customizeText(info) {
                return fmtMoney(info.value);
            }
        };
    }

    function dateColumn(field, captionKey, width) {
        return {
            dataField: field,
            caption: t(captionKey),
            width: width || 108,
            allowHeaderFiltering: false,
            calculateCellValue(row) {
                return fmtDate(row[field] ?? row[field.charAt(0).toUpperCase() + field.slice(1)]);
            }
        };
    }

    function getJsPdfConstructor() {
        if (window.jspdf && typeof window.jspdf.jsPDF === "function") {
            return window.jspdf.jsPDF;
        }
        if (typeof window.jsPDF === "function") {
            return window.jsPDF;
        }
        return null;
    }

    function resolveSaveAs() {
        if (typeof saveAs === "function") {
            return saveAs;
        }
        if (typeof window.saveAs === "function") {
            return window.saveAs;
        }
        return null;
    }

    function exportGridExcel(e, fileName) {
        const ExcelJS = window.ExcelJS;
        const saveAsFn = resolveSaveAs();
        if (!ExcelJS || !DevExpress.excelExporter || typeof DevExpress.excelExporter.exportDataGrid !== "function") {
            DevExpress.ui.notify(t("hallReports.export.excelFailed"), "error", 3200);
            if (e) {
                e.cancel = true;
            }
            return;
        }
        if (!saveAsFn) {
            DevExpress.ui.notify(t("hallReports.export.excelFailed"), "error", 3200);
            if (e) {
                e.cancel = true;
            }
            return;
        }
        if (e) {
            e.cancel = true;
        }
        const workbook = new ExcelJS.Workbook();
        const sheet = workbook.addWorksheet("Report");
        DevExpress.excelExporter.exportDataGrid({
            component: e.component,
            worksheet: sheet,
            autoFilterEnabled: true
        }).then(() => workbook.xlsx.writeBuffer()).then((buffer) => {
            saveAsFn(
                new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }),
                `${fileName}.xlsx`
            );
        }).catch((err) => {
            console.error(err);
            DevExpress.ui.notify(t("hallReports.export.excelFailed"), "error", 3200);
        });
    }

    function triggerGridExcelExport(grid, fileName) {
        if (!grid) {
            return;
        }
        exportGridExcel({ component: grid, cancel: false }, fileName);
    }

    function clearGridFilters(grid) {
        if (!grid) {
            return;
        }
        grid.clearFilter();
        grid.clearSorting();
        grid.option("searchPanel.text", "");
    }

    function exportRowsPdf(title, columns, rows, fileName) {
        const JsPdf = getJsPdfConstructor();
        if (!JsPdf || typeof JsPdf.prototype.autoTable !== "function") {
            DevExpress.ui.notify(t("common.error"), "error", 3000);
            return;
        }
        const doc = new JsPdf({ orientation: "landscape", unit: "pt", format: "a4" });
        doc.setFontSize(12);
        doc.text(title, 40, 32);
        doc.autoTable({
            startY: 44,
            head: [columns.map((c) => c.caption)],
            body: rows.map((row) => columns.map((c) => {
                const val = typeof c.value === "function" ? c.value(row) : row[c.field];
                return val == null ? "" : String(val);
            }))
        });
        doc.save(`${fileName}.pdf`);
    }

    function reportNavTitleKey(titleKey) {
        const raw = `${titleKey || ""}`.trim();
        if (!raw) {
            return "";
        }
        return raw.replace(".title.", ".nav.");
    }

    function resolveReportHeaderTitle(cfg) {
        const navKey = reportNavTitleKey(cfg.titleKey);
        if (navKey) {
            const navLabel = t(navKey);
            if (navLabel !== navKey) {
                return navLabel;
            }
        }
        return cfg.titleKey ? t(cfg.titleKey) : "";
    }

    function resolveReportGroupLabel(cfg) {
        const shell = window.Zaaer && window.Zaaer.PmsAdminShell;
        if (cfg.propertyContext === "lodging" && shell && typeof shell.lodgingReportsNavGroupLabel === "function") {
            return shell.lodgingReportsNavGroupLabel(cfg.propertyMode || {});
        }
        return t("hallOps.nav.reports");
    }

    function applyReportPageTitle(cfg) {
        if (!cfg || !cfg.titleKey) {
            return;
        }

        const $bc = $(".room-board-breadcrumb");
        if (!$bc.length) {
            return;
        }

        let $titleHost = $("#pmsReportPageTitle");
        if (!$titleHost.length) {
            $("<span/>")
                .addClass("dx-icon dx-icon-chevronright room-board-bc-sep pms-report-bc-sep")
                .attr("aria-hidden", "true")
                .appendTo($bc);
            $titleHost = $("<div/>", { id: "pmsReportPageTitle", class: "pms-report-page-title" }).appendTo($bc);
        }

        const groupLabel = resolveReportGroupLabel(cfg);
        const reportLabel = resolveReportHeaderTitle(cfg);
        if (!reportLabel) {
            return;
        }

        $titleHost.empty();
        $("<span/>")
            .addClass("pms-report-page-title__icon dx-icon dx-icon-chart")
            .attr("aria-hidden", "true")
            .appendTo($titleHost);

        const $text = $("<div/>").addClass("pms-report-page-title__text").appendTo($titleHost);
        $("<span/>").addClass("pms-report-page-title__group").text(groupLabel).appendTo($text);
        $("<strong/>").addClass("pms-report-page-title__name").text(reportLabel).appendTo($text);

        document.title = `${reportLabel} — Aleairy PMS`;
    }

    function initReportPage(config) {
        const cfg = config || {};
        const locInst = loc();
        const apiInst = api();
        if (!locInst || !apiInst) {
            return;
        }

        const permissionKey = cfg.permissionKey || "hall.reports";
        const forbiddenKey = cfg.forbiddenKey || "hallReports.forbidden";
        const financeForbiddenKey = cfg.financeForbiddenKey || "hallReports.financeForbidden";
        const rbac = window.Zaaer && window.Zaaer.PmsRbacNav;
        const permissionKeys = Array.isArray(cfg.permissionKeys) && cfg.permissionKeys.length
            ? cfg.permissionKeys
            : (cfg.reportKey && rbac && typeof rbac.resolveHallReportPermissionKeys === "function"
                ? rbac.resolveHallReportPermissionKeys(cfg.reportKey)
                : [permissionKey]);

        locInst.init();
        if (!apiInst.requireToken()) {
            return;
        }

        if (!permissionKeys.some((key) => apiInst.hasPermission(key))) {
            DevExpress.ui.notify(t(forbiddenKey), "warning", 4000);
            return;
        }

        const shellInitOpts = { navKey: cfg.navKey || "nav-hall-report-bookings" };
        if (cfg.propertyContext === "lodging") {
            shellInitOpts.onHotelChanged = function onLodgingReportHotelChanged() {
                const shell = window.Zaaer && window.Zaaer.PmsAdminShell;
                if (!shell || typeof shell.fetchPropertyMode !== "function") {
                    return;
                }
                shell.fetchPropertyMode().then((mode) => {
                    applyReportPageTitle(Object.assign({}, cfg, { propertyMode: mode }));
                });
            };
        }

        window.Zaaer.PmsAdminShell.init(shellInitOpts);
        applyReportPageTitle(cfg);

        const filterState = {
            fromDate: resolveDefaultFromDate(cfg),
            toDate: todayDate()
        };
        let lastRows = [];
        let lastSummary = null;
        let gridInst = null;
        let fromInst;
        let toInst;
        let kpiRefreshTimer = null;

        const $filters = $("#hallReportsFilters");
        const $kpi = $("#hallReportsKpi");
        const $gridHost = $("#hallReportsGrid");

        function buildExportBasename() {
            const hotel = hotelCodeParam() || "hall";
            const from = formatLocalDateParam(filterState.fromDate) || "from";
            const to = formatLocalDateParam(filterState.toDate) || "to";
            return `${cfg.exportPrefix || "hall-report"}-${hotel}-${from}-${to}`;
        }

        function renderKpi(summary) {
            if (!$kpi.length || typeof cfg.renderKpi !== "function") {
                $kpi.empty().hide();
                return;
            }
            $kpi.show().empty();
            cfg.renderKpi($kpi, summary || {}, t, fmtMoney);
        }

        function resolveKpiSummary() {
            if (!gridInst || !isGridFilterActive(gridInst)) {
                return lastSummary;
            }
            const rows = getFilteredRowsFromGrid(gridInst, lastRows);
            if (typeof cfg.computeKpiFromRows === "function") {
                return cfg.computeKpiFromRows(rows, lastSummary);
            }
            if (!lastSummary) {
                return { count: rows.length };
            }
            return Object.assign({}, lastSummary, { count: rows.length });
        }

        function refreshKpiFromGrid() {
            if (kpiRefreshTimer) {
                clearTimeout(kpiRefreshTimer);
            }
            kpiRefreshTimer = setTimeout(() => {
                kpiRefreshTimer = null;
                renderKpi(resolveKpiSummary());
            }, 0);
        }

        function loadData() {
            const from = formatLocalDateParam(filterState.fromDate);
            const to = formatLocalDateParam(filterState.toDate);
            if (!from || !to) {
                DevExpress.ui.notify(t("common.error"), "warning", 2500);
                return $.Deferred().reject().promise();
            }
            if (gridInst) {
                gridInst.beginCustomLoading("");
            }
            return cfg.load({ fromDate: from, toDate: to, filterState })
                .then((res) => {
                    lastRows = unwrapItems(res);
                    lastSummary = unwrapSummary(res);
                    if (typeof cfg.computeKpiFromRows === "function") {
                        lastSummary = cfg.computeKpiFromRows(lastRows, lastSummary);
                    }
                    if (gridInst) {
                        gridInst.option("dataSource", lastRows);
                    }
                    renderKpi(lastSummary);
                })
                .catch((err) => {
                    const status = err && (err.status || err.Status);
                    if (status === 403) {
                        DevExpress.ui.notify(t(financeForbiddenKey), "warning", 4000);
                    } else {
                        DevExpress.ui.notify(t("common.error"), "error", 3200);
                    }
                    lastRows = [];
                    lastSummary = null;
                    if (gridInst) {
                        gridInst.option("dataSource", []);
                    }
                    renderKpi(null);
                })
                .always(() => {
                    if (gridInst) {
                        gridInst.endCustomLoading();
                    }
                });
        }

        function initFilters() {
            $("<div class='hall-reports-filter-field'/>").appendTo($filters).dxDateBox({
                label: t("hallReports.filter.from"),
                type: "date",
                openOnFieldClick: true,
                displayFormat: "dd/MM/yyyy",
                value: filterState.fromDate,
                onValueChanged(e) {
                    filterState.fromDate = e.value;
                }
            });
            fromInst = $filters.find(".hall-reports-filter-field").first().dxDateBox("instance");

            $("<div class='hall-reports-filter-field'/>").appendTo($filters).dxDateBox({
                label: t("hallReports.filter.to"),
                type: "date",
                openOnFieldClick: true,
                displayFormat: "dd/MM/yyyy",
                value: filterState.toDate,
                onValueChanged(e) {
                    filterState.toDate = e.value;
                }
            });
            toInst = $filters.find(".hall-reports-filter-field").eq(1).dxDateBox("instance");

            if (typeof cfg.initExtraFilters === "function") {
                cfg.initExtraFilters($filters, filterState, t);
            }

            const $dateActions = $("<div class='hall-reports-filter-date-actions'/>").appendTo($filters);
            $("<div/>").appendTo($dateActions).dxButton({
                text: t("hallReports.filter.apply"),
                type: "default",
                stylingMode: "contained",
                icon: "find",
                onClick: loadData
            });
            $("<div/>").appendTo($dateActions).dxButton({
                text: t("hallReports.filter.reset"),
                stylingMode: "outlined",
                icon: "refresh",
                onClick() {
                    filterState.fromDate = resolveDefaultFromDate(cfg);
                    filterState.toDate = todayDate();
                    if (fromInst) {
                        fromInst.option("value", filterState.fromDate);
                    }
                    if (toInst) {
                        toInst.option("value", filterState.toDate);
                    }
                    if (typeof cfg.resetExtraFilters === "function") {
                        cfg.resetExtraFilters(filterState);
                    }
                    clearGridFilters(gridInst);
                    loadData();
                }
            });

            const $exportActions = $("<div class='hall-reports-filter-export-actions'/>").appendTo($filters);
            $("<div/>").appendTo($exportActions).dxButton({
                text: t("hallReports.export.excel"),
                icon: "exportxlsx",
                stylingMode: "outlined",
                onClick() {
                    triggerGridExcelExport(gridInst, buildExportBasename());
                }
            });
            $("<div/>").appendTo($exportActions).dxButton({
                text: t("hallReports.export.pdf"),
                icon: "exportpdf",
                stylingMode: "outlined",
                onClick() {
                    if (typeof cfg.pdfColumns === "function") {
                        exportRowsPdf(
                            t(cfg.titleKey),
                            cfg.pdfColumns(t, fmtDate, fmtMoney),
                            lastRows,
                            buildExportBasename()
                        );
                    }
                }
            });
            $("<div/>").appendTo($exportActions).dxButton({
                text: t("hallReports.export.print"),
                icon: "print",
                stylingMode: "outlined",
                onClick() {
                    window.print();
                }
            });
        }

        function initGrid() {
            const po = gridOpts();
            const columns = typeof cfg.columns === "function" ? cfg.columns({
                t,
                fmtMoney,
                fmtDate,
                renderReservationLink,
                renderVoucherLink,
                renderPaymentVoucherLink,
                openPaymentVoucherFromReportRow,
                renderDepositLink,
                openDepositFromReportRow,
                renderExpenseLink,
                openExpenseFromReportRow,
                renderInvoiceLink,
                renderCreditNoteLink,
                openCreditNoteFromReportRow,
                renderLinkCell,
                renderCustomerLink,
                openCustomerReadOnlyPopup,
                buildSerialNumberColumn,
                appendNetAmountKpiValue,
                moneyColumn,
                dateColumn,
                mapPaymentMethodDisplay,
                mapReceiptStatusDisplay,
                mapStatusDisplay,
                mapVoucherLabelDisplay,
                mapCreditTypeDisplay,
                mapInvoiceStatusDisplay,
                mapApprovalStatusDisplay,
                mapPaymentSourceDisplay,
                mapMovementLabelDisplay,
                reservationDetailUrl,
                voucherDetailUrl,
                invoiceViewerUrl,
                depositDetailUrl,
                expenseDetailUrl,
                reservationRouteId
            }) : [];

            gridInst = $gridHost.dxDataGrid(
                po.merge(po.adminBaseline(), {
                    dataSource: [],
                    height: "calc(100vh - 280px)",
                    keyExpr: cfg.keyExpr || "documentId",
                    noDataText: t("hallReports.empty"),
                    elementAttr: { class: ["pms-grid-compact", cfg.gridExtraClass].filter(Boolean).join(" ") },
                    columnAutoWidth: false,
                    paging: { pageSize: 50 },
                    pager: po.adminPager(),
                    scrolling: po.scrollingOptions({ useNative: false }),
                    export: {
                        enabled: true,
                        allowExportSelectedData: false,
                        fileName: buildExportBasename()
                    },
                    onExporting(e) {
                        exportGridExcel(e, buildExportBasename());
                    },
                    onRowPrepared(e) {
                        if (typeof cfg.onRowPrepared === "function") {
                            cfg.onRowPrepared(e);
                        }
                    },
                    onCellPrepared(e) {
                        if (typeof cfg.onCellPrepared === "function") {
                            cfg.onCellPrepared(e);
                        }
                    },
                    onContentReady() {
                        refreshKpiFromGrid();
                    },
                    onOptionChanged(e) {
                        if (!e || !e.fullName) {
                            return;
                        }
                        if (
                            e.fullName.indexOf("filter") === 0
                            || e.fullName.indexOf("searchPanel") === 0
                            || e.fullName.indexOf("headerFilter") === 0
                        ) {
                            refreshKpiFromGrid();
                        }
                    },
                    columns
                })
            ).dxDataGrid("instance");
        }

        initFilters();
        initGrid();
        loadData();
        if (typeof cfg.onReady === "function") {
            cfg.onReady({ reload: loadData });
        }
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.HallReportCommon = {
        CACHE,
        t,
        fmtMoney,
        fmtDate,
        mapPaymentMethodDisplay,
        mapReceiptStatusDisplay,
        mapStatusDisplay,
        mapVoucherLabelDisplay,
        mapCreditTypeDisplay,
        mapInvoiceStatusDisplay,
        mapApprovalStatusDisplay,
        mapPaymentSourceDisplay,
        mapMovementLabelDisplay,
        formatLocalDateParam,
        initReportPage,
        renderLinkCell,
        renderCustomerLink,
        openCustomerReadOnlyPopup,
        buildSerialNumberColumn,
        appendNetAmountKpiValue,
        getFilteredRowsFromGrid,
        isGridFilterActive,
        computeDailyJournalSummaryFromRows,
        computeOnlineBookingsSummaryFromRows,
        computeBookingsSummaryFromRows,
        computeSimpleCountAmountSummaryFromRows,
        buildPaymentMethodBreakdownFromRows,
        buildVoucherBreakdownFromRows,
        resolveDefaultFromDate,
        renderReservationLink,
        renderVoucherLink,
        renderPaymentVoucherLink,
        openPaymentVoucherFromReportRow,
        renderDepositLink,
        openDepositFromReportRow,
        renderExpenseLink,
        openExpenseFromReportRow,
        paymentVoucherKindFromRow,
        renderInvoiceLink,
        renderCreditNoteLink,
        openCreditNoteFromReportRow,
        reservationDetailUrl,
        voucherDetailUrl,
        invoiceViewerUrl,
        depositDetailUrl,
        expenseDetailUrl,
        reservationRouteId,
        moneyColumn,
        dateColumn,
        unwrapData,
        unwrapItems,
        unwrapSummary,
        hallSvc,
        api,
        startOfMonthDate,
        todayDate
    };
})(window, jQuery);
