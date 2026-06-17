(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;

    function paymentMethodText(row) {
        const raw = row.paymentMethod ?? row.PaymentMethod ?? "";
        return common.mapPaymentMethodDisplay(raw);
    }

    function receiptStatusText(row) {
        const raw = row.receiptStatus ?? row.ReceiptStatus ?? "";
        return common.mapReceiptStatusDisplay(raw);
    }

    function financeKpi($host, summary, t, fmtMoney) {
        const netAmount = summary.totalAmount ?? summary.TotalAmount ?? 0;
        const breakdown = summary.voucherBreakdown || summary.VoucherBreakdown || [];
        const paymentBreakdown = summary.paymentMethodBreakdown || summary.PaymentMethodBreakdown || [];

        $host.addClass("hall-reports-kpi-row--daily-journal hall-reports-kpi-row--daily-journal-extended").empty();

        const $netCard = $("<div class='hall-reports-kpi hall-reports-kpi--net'/>").appendTo($host);
        $("<div class='hall-reports-kpi__label'/>").text(t("hallReports.kpi.netAmount")).appendTo($netCard);
        const $netValue = $("<div class='hall-reports-kpi__value hall-reports-kpi__value--with-currency'/>").appendTo($netCard);
        const $amount = common.appendNetAmountKpiValue($netValue, netAmount, fmtMoney);
        if (netAmount < 0) {
            $amount.addClass("hall-reports-kpi__value--negative");
        } else if (netAmount > 0) {
            $amount.addClass("hall-reports-kpi__value--positive");
        }

        const $stats = $("<div class='hall-reports-voucher-stats'/>").appendTo($host);
        $("<div class='hall-reports-voucher-stats__title'/>").text(t("hallReports.kpi.voucherStats")).appendTo($stats);

        if (!breakdown.length) {
            $("<div class='hall-reports-voucher-stats__empty'/>").text(t("hallReports.kpi.voucherStatsEmpty")).appendTo($stats);
        } else {
            const $grid = $("<div class='hall-reports-voucher-stats__grid'/>").appendTo($stats);
            breakdown.forEach((item) => {
                const code = `${item.voucherCode || item.VoucherCode || ""}`.trim().toLowerCase();
                const isOutflow = code === "refund" || code === "security_deposit_refund";
                const $card = $("<div class='hall-reports-voucher-stat'/>")
                    .addClass(isOutflow ? "hall-reports-voucher-stat--outflow" : "hall-reports-voucher-stat--inflow")
                    .appendTo($grid);
                $("<div class='hall-reports-voucher-stat__name'/>")
                    .text(item.voucherLabel || item.VoucherLabel || code)
                    .appendTo($card);
                const $meta = $("<div class='hall-reports-voucher-stat__meta'/>").appendTo($card);
                $("<span class='hall-reports-voucher-stat__count'/>")
                    .text(`${t("hallReports.kpi.voucherCount")}: ${item.count ?? item.Count ?? 0}`)
                    .appendTo($meta);
                $("<span class='hall-reports-voucher-stat__amount'/>")
                    .text(fmtMoney(item.totalAmount ?? item.TotalAmount))
                    .appendTo($meta);
            });
        }

        const $paymentStats = $("<div class='hall-reports-voucher-stats hall-reports-voucher-stats--payment-methods'/>").appendTo($host);
        $("<div class='hall-reports-voucher-stats__title'/>").text(t("hallReports.kpi.paymentMethodStats")).appendTo($paymentStats);

        if (!paymentBreakdown.length) {
            $("<div class='hall-reports-voucher-stats__empty'/>").text(t("hallReports.kpi.paymentMethodStatsEmpty")).appendTo($paymentStats);
            return;
        }

        const $paymentGrid = $("<div class='hall-reports-voucher-stats__grid'/>").appendTo($paymentStats);
        paymentBreakdown.forEach((item) => {
            const total = item.totalAmount ?? item.TotalAmount ?? 0;
            const $card = $("<div class='hall-reports-voucher-stat hall-reports-voucher-stat--inflow'/>").appendTo($paymentGrid);
            $("<div class='hall-reports-voucher-stat__name'/>")
                .text(item.paymentMethodLabel || item.PaymentMethodLabel || "—")
                .appendTo($card);
            const $meta = $("<div class='hall-reports-voucher-stat__meta'/>").appendTo($card);
            $("<span class='hall-reports-voucher-stat__amount'/>").text(fmtMoney(total)).appendTo($meta);
        });
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-daily-journal",
            reportKey: "daily_journal",
            titleKey: "hallReports.title.dailyJournal",
            exportPrefix: "daily-journal",
            keyExpr: "receiptId",
            defaultFromDate: "today",
            onReady(api) {
                window.__hallDailyJournalReload = api && typeof api.reload === "function" ? api.reload : null;
            },
            load(query) {
                return common.hallSvc().getDailyJournalReport(query.fromDate, query.toDate);
            },
            renderKpi: financeKpi,
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeDailyJournalSummaryFromRows(rows);
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hallReports.col.docDate"), field: "receiptDate", value: (r) => fmtDate(r.receiptDate || r.ReceiptDate) },
                    { caption: t("hallReports.col.docNo"), field: "receiptNo", value: (r) => r.receiptNo || r.ReceiptNo || "" },
                    { caption: t("hallReports.col.customer"), field: "customerName", value: (r) => r.customerName || r.CustomerName || "" },
                    { caption: t("hallReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo || "" },
                    { caption: t("hallReports.col.amount"), field: "amountPaid", value: (r) => fmtMoney(r.amountPaid ?? r.AmountPaid) },
                    { caption: t("hallReports.col.voucherCode"), field: "voucherLabel", value: (r) => r.voucherLabel || r.VoucherLabel || r.voucherCode || r.VoucherCode || "" },
                    { caption: t("hallReports.col.paymentMethod"), field: "paymentMethod", value: (r) => paymentMethodText(r) },
                    { caption: t("hallReports.col.status"), field: "receiptStatus", value: (r) => receiptStatusText(r) },
                ];
            },
            columns(ctx) {
                return [
                    ctx.dateColumn("receiptDate", "hallReports.col.docDate", 116),
                    {
                        dataField: "receiptNo",
                        caption: ctx.t("hallReports.col.docNo"),
                        width: 130,
                        calculateCellValue(row) {
                            return row.receiptNo ?? row.ReceiptNo ?? "";
                        },
                        cellTemplate(c, info) {
                            common.renderPaymentVoucherLink(c, info.data, {
                                afterSave() {
                                    if (typeof window.__hallDailyJournalReload === "function") {
                                        window.__hallDailyJournalReload();
                                    }
                                }
                            });
                        }
                    },
                    { dataField: "customerName", caption: ctx.t("hallReports.col.customer"), minWidth: 160 },
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hallReports.col.reservationNo"),
                        width: 130,
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    ctx.moneyColumn("amountPaid", "hallReports.col.amount"),
                    {
                        dataField: "voucherLabel",
                        caption: ctx.t("hallReports.col.voucherCode"),
                        minWidth: 150,
                        calculateCellValue(row) {
                            return row.voucherLabel ?? row.VoucherLabel ?? row.voucherCode ?? row.VoucherCode ?? "";
                        }
                    },
                    {
                        dataField: "paymentMethod",
                        caption: ctx.t("hallReports.col.paymentMethod"),
                        width: 120,
                        calculateCellValue(row) {
                            return paymentMethodText(row);
                        }
                    },
                    {
                        dataField: "receiptStatus",
                        caption: ctx.t("hallReports.col.status"),
                        width: 100,
                        calculateCellValue(row) {
                            return receiptStatusText(row);
                        }
                    },
                    { dataField: "voucherCode", caption: ctx.t("hallReports.col.voucherCode"), width: 120, visible: false },
                    { dataField: "receiptZaaerId", caption: ctx.t("hallReports.col.sourceZaaerId"), width: 110, visible: false },
                    { dataField: "reservationZaaerId", caption: ctx.t("hallReports.col.sourceZaaerId"), width: 110, visible: false }
                ];
            }
        });
    });
})(window, jQuery);
