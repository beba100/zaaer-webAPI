(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    function financeColumns(ctx, voucherTab) {
        return [
            {
                dataField: "documentNo",
                caption: ctx.t("hotelReports.col.docNo"),
                cellTemplate(c, info) {
                    ctx.renderVoucherLink(c, info.data, voucherTab);
                }
            },
            ctx.dateColumn("documentDate", "hotelReports.col.docDate"),
            ctx.moneyColumn("amount", "hotelReports.col.amount"),
            {
                dataField: "paymentMethod",
                caption: ctx.t("hotelReports.col.paymentMethod"),
                calculateCellValue(row) {
                    return ctx.mapPaymentMethodDisplay(row.paymentMethod ?? row.PaymentMethod);
                }
            },
            {
                dataField: "voucherLabel",
                caption: ctx.t("hotelReports.col.voucherCode"),
                width: 140,
                calculateCellValue(row) {
                    return ctx.mapVoucherLabelDisplay(row);
                }
            },
            {
                dataField: "reservationNo",
                caption: ctx.t("hotelReports.col.reservationNo"),
                cellTemplate(c, info) {
                    ctx.renderReservationLink(c, info.data);
                }
            },
            { dataField: "unitLabel", caption: ctx.t("hotelReports.col.unit") },
            { dataField: "customerName", caption: ctx.t("hotelReports.col.customer") },
            {
                dataField: "status",
                caption: ctx.t("hotelReports.col.status"),
                width: 100,
                calculateCellValue(row) {
                    return ctx.mapReceiptStatusDisplay(row.status ?? row.Status);
                }
            },
            { dataField: "notes", caption: ctx.t("hotelReports.col.notes") }
        ];
    }

    function financeKpi($host, summary, t, fmtMoney) {
        [
            { label: t("hotelReports.kpi.count"), value: summary.count ?? summary.Count ?? 0 },
            { label: t("hotelReports.kpi.totalAmount"), value: fmtMoney(summary.totalAmount ?? summary.TotalAmount) }
        ].forEach((c) => {
            const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
            $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
            $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
        });
    }

    function financePdfColumns(t, fmtDate, fmtMoney) {
        return [
            { caption: t("hotelReports.col.docNo"), field: "documentNo", value: (r) => r.documentNo || r.DocumentNo },
            { caption: t("hotelReports.col.docDate"), field: "documentDate", value: (r) => fmtDate(r.documentDate || r.DocumentDate) },
            { caption: t("hotelReports.col.amount"), field: "amount", value: (r) => fmtMoney(r.amount ?? r.Amount) },
            {
                caption: t("hotelReports.col.paymentMethod"),
                field: "paymentMethod",
                value: (r) => common.mapPaymentMethodDisplay(r.paymentMethod ?? r.PaymentMethod)
            },
            {
                caption: t("hotelReports.col.voucherCode"),
                field: "voucherLabel",
                value: (r) => common.mapVoucherLabelDisplay(r)
            },
            {
                caption: t("hotelReports.col.status"),
                field: "status",
                value: (r) => common.mapReceiptStatusDisplay(r.status ?? r.Status)
            },
            { caption: t("hotelReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo }
        ];
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-receipts",
            reportKey: "receipts",
            titleKey: "hotelReports.title.receipts",
            exportPrefix: "hotel-receipts",
            keyExpr: "documentId",
            load(query) {
                return common.hotelSvc().getReceiptsReport(query.fromDate, query.toDate);
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeSimpleCountAmountSummaryFromRows(rows, "amount");
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            renderKpi: financeKpi,
            pdfColumns: financePdfColumns,
            columns(ctx) {
                return financeColumns(ctx, "receipts");
            }
        });
    });
})(window, jQuery);
