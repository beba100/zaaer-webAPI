(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    function paymentMethodText(row) {
        const raw = row.paymentMethod ?? row.PaymentMethod ?? "";
        return common.mapPaymentMethodDisplay(raw);
    }

    function receiptStatusText(row) {
        const raw = row.receiptStatus ?? row.ReceiptStatus ?? "";
        return common.mapReceiptStatusDisplay(raw);
    }

    function paymentMethodSummaryLabel(item) {
        const raw = item.paymentMethodLabel ?? item.PaymentMethodLabel
            ?? item.paymentMethodKey ?? item.PaymentMethodKey ?? "";
        return common.mapPaymentMethodDisplay(raw);
    }

    function voucherLabelText(row) {
        return common.mapVoucherLabelDisplay(row);
    }

    function financeKpi($host, summary, t, fmtMoney) {
        const netAmount = summary.totalAmount ?? summary.TotalAmount ?? 0;
        const breakdown = summary.paymentMethodBreakdown || summary.PaymentMethodBreakdown || [];

        $host.addClass("hall-reports-kpi-row--daily-journal").empty();

        const $netCard = $("<div class='hall-reports-kpi hall-reports-kpi--net'/>").appendTo($host);
        $("<div class='hall-reports-kpi__label'/>").text(t("hotelReports.kpi.totalAmount")).appendTo($netCard);
        const $netValue = $("<div class='hall-reports-kpi__value'/>").text(fmtMoney(netAmount)).appendTo($netCard);
        if (netAmount < 0) {
            $netValue.addClass("hall-reports-kpi__value--negative");
        } else if (netAmount > 0) {
            $netValue.addClass("hall-reports-kpi__value--positive");
        }

        const $stats = $("<div class='hall-reports-voucher-stats'/>").appendTo($host);
        $("<div class='hall-reports-voucher-stats__title'/>").text(t("hotelReports.kpi.paymentMethodStats")).appendTo($stats);

        if (!breakdown.length) {
            $("<div class='hall-reports-voucher-stats__empty'/>").text(t("hotelReports.kpi.paymentMethodStatsEmpty")).appendTo($stats);
            return;
        }

        const $grid = $("<div class='hall-reports-voucher-stats__grid'/>").appendTo($stats);
        breakdown.forEach((item) => {
            const total = item.totalAmount ?? item.TotalAmount ?? 0;
            const $card = $("<div class='hall-reports-voucher-stat'/>")
                .addClass(total < 0 ? "hall-reports-voucher-stat--outflow" : "hall-reports-voucher-stat--inflow")
                .appendTo($grid);
            $("<div class='hall-reports-voucher-stat__name'/>")
                .text(paymentMethodSummaryLabel(item))
                .appendTo($card);
            const $meta = $("<div class='hall-reports-voucher-stat__meta'/>").appendTo($card);
            $("<span class='hall-reports-voucher-stat__count'/>")
                .text(`${t("hotelReports.kpi.voucherCount")}: ${item.count ?? item.Count ?? 0}`)
                .appendTo($meta);
            $("<span class='hall-reports-voucher-stat__amount'/>")
                .text(fmtMoney(total))
                .appendTo($meta);
        });
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-network-cash",
            reportKey: "network_cash",
            titleKey: "hotelReports.title.networkCashPayments",
            exportPrefix: "hotel-network-cash-payments",
            keyExpr: "receiptId",
            onReady(api) {
                window.__hotelNetworkCashReload = api && typeof api.reload === "function" ? api.reload : null;
            },
            load(query) {
                return common.hotelSvc().getNetworkCashPaymentsReport(query.fromDate, query.toDate);
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = {
                    totalAmount: rows.reduce((sum, row) => sum + (Number(row.amountPaid ?? row.AmountPaid) || 0), 0),
                    paymentMethodBreakdown: common.buildPaymentMethodBreakdownFromRows(rows)
                };
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            renderKpi: financeKpi,
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hotelReports.col.docDate"), field: "receiptDate", value: (r) => fmtDate(r.receiptDate || r.ReceiptDate) },
                    { caption: t("hotelReports.col.docNo"), field: "receiptNo", value: (r) => r.receiptNo || r.ReceiptNo || "" },
                    { caption: t("hotelReports.col.customer"), field: "customerName", value: (r) => r.customerName || r.CustomerName || "" },
                    { caption: t("hotelReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo || "" },
                    { caption: t("hotelReports.col.amount"), field: "amountPaid", value: (r) => fmtMoney(r.amountPaid ?? r.AmountPaid) },
                    { caption: t("hotelReports.col.voucherCode"), field: "voucherLabel", value: (r) => voucherLabelText(r) },
                    { caption: t("hotelReports.col.paymentMethod"), field: "paymentMethod", value: (r) => paymentMethodText(r) },
                    { caption: t("hotelReports.col.status"), field: "receiptStatus", value: (r) => receiptStatusText(r) },
                ];
            },
            columns(ctx) {
                return [
                    ctx.dateColumn("receiptDate", "hotelReports.col.docDate", 116),
                    {
                        dataField: "receiptNo",
                        caption: ctx.t("hotelReports.col.docNo"),
                        width: 130,
                        calculateCellValue(row) {
                            return row.receiptNo ?? row.ReceiptNo ?? "";
                        },
                        cellTemplate(c, info) {
                            common.renderPaymentVoucherLink(c, info.data, {
                                afterSave() {
                                    if (typeof window.__hotelNetworkCashReload === "function") {
                                        window.__hotelNetworkCashReload();
                                    }
                                }
                            });
                        }
                    },
                    { dataField: "customerName", caption: ctx.t("hotelReports.col.customer"), minWidth: 160 },
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hotelReports.col.reservationNo"),
                        width: 130,
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    ctx.moneyColumn("amountPaid", "hotelReports.col.amount"),
                    {
                        dataField: "voucherLabel",
                        caption: ctx.t("hotelReports.col.voucherCode"),
                        minWidth: 150,
                        calculateCellValue(row) {
                            return voucherLabelText(row);
                        }
                    },
                    {
                        dataField: "paymentMethod",
                        caption: ctx.t("hotelReports.col.paymentMethod"),
                        width: 120,
                        calculateCellValue(row) {
                            return paymentMethodText(row);
                        }
                    },
                    {
                        dataField: "receiptStatus",
                        caption: ctx.t("hotelReports.col.status"),
                        width: 100,
                        calculateCellValue(row) {
                            return receiptStatusText(row);
                        }
                    },
                    { dataField: "voucherCode", caption: ctx.t("hotelReports.col.voucherCode"), width: 120, visible: false },
                    { dataField: "receiptZaaerId", caption: ctx.t("hotelReports.col.sourceZaaerId"), width: 110, visible: false },
                    { dataField: "reservationZaaerId", caption: ctx.t("hotelReports.col.sourceZaaerId"), width: 110, visible: false }
                ];
            }
        });
    });
})(window, jQuery);
