(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;
    const depositApi = window.Zaaer.PmsDepositService;

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-deposits",
            reportKey: "deposits",
            titleKey: "hallReports.title.deposits",
            exportPrefix: "hall-deposits",
            keyExpr: "receiptId",
            load(query) {
                return depositApi.list(query.fromDate, query.toDate).then((data) => {
                    const items = (data && (data.items || data.Items)) || [];
                    const summary = (data && (data.summary || data.Summary)) || {};
                    return { items, summary: { count: summary.count ?? summary.Count ?? items.length, totalAmount: summary.totalAmount ?? summary.TotalAmount } };
                });
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeSimpleCountAmountSummaryFromRows(rows, "displayAmount");
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            renderKpi($host, summary, t, fmtMoney) {
                [
                    { label: t("hallReports.kpi.count"), value: summary.count ?? 0 },
                    { label: t("hallReports.kpi.totalAmount"), value: fmtMoney(summary.totalAmount) }
                ].forEach((c) => {
                    const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
                    $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
                    $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
                });
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hallReports.col.docNo"), field: "receiptNo", value: (r) => r.receiptNo || r.ReceiptNo },
                    { caption: t("hallReports.col.docDate"), field: "receiptDate", value: (r) => fmtDate(r.receiptDate || r.ReceiptDate) },
                    { caption: t("hallReports.col.amount"), field: "displayAmount", value: (r) => fmtMoney(r.displayAmount ?? r.DisplayAmount ?? r.amountPaid) },
                    { caption: t("hallReports.col.bank"), field: "bankName", value: (r) => r.bankName || r.BankName || "" }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "receiptNo",
                        caption: ctx.t("hallReports.col.docNo"),
                        cellTemplate(c, info) {
                            ctx.renderDepositLink(c, info.data);
                        }
                    },
                    ctx.dateColumn("receiptDate", "hallReports.col.docDate"),
                    {
                        dataField: "displayAmount",
                        caption: ctx.t("hallReports.col.amount"),
                        dataType: "number",
                        alignment: "right",
                        cssClass: "hall-reports-amount",
                        calculateCellValue(row) {
                            return row.displayAmount ?? row.DisplayAmount ?? row.amountPaid ?? row.AmountPaid;
                        },
                        customizeText(info) {
                            return ctx.fmtMoney(info.value);
                        }
                    },
                    { dataField: "bankName", caption: ctx.t("hallReports.col.bank") },
                    { dataField: "paymentMethod", caption: ctx.t("hallReports.col.paymentMethod") },
                    { dataField: "receiptStatus", caption: ctx.t("hallReports.col.status"), width: 100 },
                    { dataField: "notes", caption: ctx.t("hallReports.col.notes") }
                ];
            }
        });
    });
})(window, jQuery);
