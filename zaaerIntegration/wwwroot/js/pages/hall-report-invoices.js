(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-invoices",
            reportKey: "invoices",
            titleKey: "hallReports.title.invoices",
            exportPrefix: "hall-invoices",
            keyExpr: "documentId",
            load(query) {
                return common.hallSvc().getInvoicesReport(query.fromDate, query.toDate);
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeSimpleCountAmountSummaryFromRows(rows, "totalAmount");
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            renderKpi($host, summary, t, fmtMoney) {
                [
                    { label: t("hallReports.kpi.count"), value: summary.count ?? summary.Count ?? 0 },
                    { label: t("hallReports.kpi.totalAmount"), value: fmtMoney(summary.totalAmount ?? summary.TotalAmount) }
                ].forEach((c) => {
                    const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
                    $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
                    $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
                });
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hallReports.col.invoiceNo"), field: "documentNo", value: (r) => r.documentNo || r.DocumentNo },
                    { caption: t("hallReports.col.invoiceDate"), field: "documentDate", value: (r) => fmtDate(r.documentDate || r.DocumentDate) },
                    { caption: t("hallReports.col.amount"), field: "amount", value: (r) => fmtMoney(r.amount ?? r.Amount) },
                    { caption: t("hallReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "documentNo",
                        caption: ctx.t("hallReports.col.invoiceNo"),
                        cellTemplate(c, info) {
                            ctx.renderInvoiceLink(c, info.data);
                        }
                    },
                    ctx.dateColumn("documentDate", "hallReports.col.invoiceDate"),
                    ctx.moneyColumn("amount", "hallReports.col.amount"),
                    ctx.moneyColumn("amountPaid", "hallReports.col.paid"),
                    ctx.moneyColumn("amountRemaining", "hallReports.col.remaining"),
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hallReports.col.reservationNo"),
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    { dataField: "hallName", caption: ctx.t("hallReports.col.hall") },
                    { dataField: "customerName", caption: ctx.t("hallReports.col.customer") },
                    { dataField: "status", caption: ctx.t("hallReports.col.status"), width: 100 }
                ];
            }
        });
    });
})(window, jQuery);
