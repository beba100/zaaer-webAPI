(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-credit-notes",
            reportKey: "credit_notes",
            titleKey: "hallReports.title.creditNotes",
            exportPrefix: "hall-credit-notes",
            keyExpr: "documentId",
            load(query) {
                return common.hallSvc().getCreditNotesReport(query.fromDate, query.toDate);
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
                    { caption: t("hallReports.col.creditNoteNo"), field: "documentNo", value: (r) => r.documentNo || r.DocumentNo },
                    { caption: t("hallReports.col.docDate"), field: "documentDate", value: (r) => fmtDate(r.documentDate || r.DocumentDate) },
                    { caption: t("hallReports.col.amount"), field: "amount", value: (r) => fmtMoney(r.amount ?? r.Amount) },
                    { caption: t("hallReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "documentNo",
                        caption: ctx.t("hallReports.col.creditNoteNo"),
                        cellTemplate(c, info) {
                            ctx.renderCreditNoteLink(c, info.data);
                        }
                    },
                    ctx.dateColumn("documentDate", "hallReports.col.docDate"),
                    ctx.moneyColumn("amount", "hallReports.col.amount"),
                    { dataField: "creditType", caption: ctx.t("hallReports.col.creditType") },
                    { dataField: "reason", caption: ctx.t("hallReports.col.reason") },
                    {
                        dataField: "linkedInvoiceNo",
                        caption: ctx.t("hallReports.col.linkedInvoiceNo"),
                        calculateCellValue(row) {
                            return row.linkedInvoiceNo ?? row.LinkedInvoiceNo ?? "";
                        },
                        cellTemplate(c, info) {
                            ctx.renderInvoiceLink(c, info.data);
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
                    { dataField: "customerName", caption: ctx.t("hallReports.col.customer") }
                ];
            }
        });
    });
})(window, jQuery);
