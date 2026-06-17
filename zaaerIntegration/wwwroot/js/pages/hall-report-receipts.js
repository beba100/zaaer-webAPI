(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;

    function financeColumns(ctx, voucherTab) {
        return [
            {
                dataField: "documentNo",
                caption: ctx.t("hallReports.col.docNo"),
                cellTemplate(c, info) {
                    ctx.renderVoucherLink(c, info.data, voucherTab);
                }
            },
            ctx.dateColumn("documentDate", "hallReports.col.docDate"),
            ctx.moneyColumn("amount", "hallReports.col.amount"),
            { dataField: "paymentMethod", caption: ctx.t("hallReports.col.paymentMethod") },
            {
                dataField: "voucherLabel",
                caption: ctx.t("hallReports.col.voucherCode"),
                width: 140,
                calculateCellValue(row) {
                    return row.voucherLabel ?? row.VoucherLabel ?? row.voucherCode ?? row.VoucherCode ?? "";
                }
            },
            {
                dataField: "reservationNo",
                caption: ctx.t("hallReports.col.reservationNo"),
                cellTemplate(c, info) {
                    ctx.renderReservationLink(c, info.data);
                }
            },
            { dataField: "hallName", caption: ctx.t("hallReports.col.hall") },
            { dataField: "customerName", caption: ctx.t("hallReports.col.customer") },
            { dataField: "status", caption: ctx.t("hallReports.col.status"), width: 100 },
            { dataField: "notes", caption: ctx.t("hallReports.col.notes") }
        ];
    }

    function financeKpi($host, summary, t, fmtMoney) {
        [
            { label: t("hallReports.kpi.count"), value: summary.count ?? summary.Count ?? 0 },
            { label: t("hallReports.kpi.totalAmount"), value: fmtMoney(summary.totalAmount ?? summary.TotalAmount) }
        ].forEach((c) => {
            const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
            $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
            $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
        });
    }

    function financePdfColumns(t, fmtDate, fmtMoney) {
        return [
            { caption: t("hallReports.col.docNo"), field: "documentNo", value: (r) => r.documentNo || r.DocumentNo },
            { caption: t("hallReports.col.docDate"), field: "documentDate", value: (r) => fmtDate(r.documentDate || r.DocumentDate) },
            { caption: t("hallReports.col.amount"), field: "amount", value: (r) => fmtMoney(r.amount ?? r.Amount) },
            { caption: t("hallReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo }
        ];
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-receipts",
            reportKey: "receipts",
            titleKey: "hallReports.title.receipts",
            exportPrefix: "hall-receipts",
            keyExpr: "documentId",
            load(query) {
                return common.hallSvc().getReceiptsReport(query.fromDate, query.toDate);
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
