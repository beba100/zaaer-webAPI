(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-disbursements",
            reportKey: "disbursements",
            titleKey: "hallReports.title.disbursements",
            exportPrefix: "hall-disbursements",
            keyExpr: "documentId",
            load(query) {
                return common.hallSvc().getDisbursementsReport(query.fromDate, query.toDate);
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeSimpleCountAmountSummaryFromRows(rows, "amount");
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
                    { caption: t("hallReports.col.docNo"), field: "documentNo", value: (r) => r.documentNo || r.DocumentNo },
                    { caption: t("hallReports.col.docDate"), field: "documentDate", value: (r) => fmtDate(r.documentDate || r.DocumentDate) },
                    { caption: t("hallReports.col.amount"), field: "amount", value: (r) => fmtMoney(r.amount ?? r.Amount) },
                    { caption: t("hallReports.col.reason"), field: "reason", value: (r) => r.reason || r.Reason || "" }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "documentNo",
                        caption: ctx.t("hallReports.col.docNo"),
                        cellTemplate(c, info) {
                            ctx.renderVoucherLink(c, info.data, "disbursements");
                        }
                    },
                    ctx.dateColumn("documentDate", "hallReports.col.docDate"),
                    ctx.moneyColumn("amount", "hallReports.col.amount"),
                    { dataField: "receiptType", caption: ctx.t("hallReports.col.voucherCode") },
                    { dataField: "reason", caption: ctx.t("hallReports.col.reason") },
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hallReports.col.reservationNo"),
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    { dataField: "hallName", caption: ctx.t("hallReports.col.hall") },
                    { dataField: "customerName", caption: ctx.t("hallReports.col.customer") },
                    { dataField: "paymentMethod", caption: ctx.t("hallReports.col.paymentMethod") },
                    { dataField: "notes", caption: ctx.t("hallReports.col.notes") }
                ];
            }
        });
    });
})(window, jQuery);
