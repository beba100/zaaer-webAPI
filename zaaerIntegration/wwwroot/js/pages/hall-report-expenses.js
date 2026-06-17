(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;
    const expenseApi = window.Zaaer.PmsExpenseService;

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-expenses",
            reportKey: "expenses",
            titleKey: "hallReports.title.expenses",
            exportPrefix: "hall-expenses",
            keyExpr: "expenseId",
            load(query) {
                return expenseApi.list(query.fromDate, query.toDate).then((data) => {
                    const items = (data && (data.items || data.Items)) || (Array.isArray(data) ? data : []);
                    const total = items.reduce((sum, row) => sum + (Number(row.totalAmount ?? row.TotalAmount) || 0), 0);
                    return { items, summary: { count: items.length, totalAmount: total } };
                });
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeSimpleCountAmountSummaryFromRows(rows, "totalAmount");
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
                    { caption: t("hallReports.col.expenseNo"), field: "expenseNo", value: (r) => r.expenseNo || r.ExpenseNo || r.number },
                    { caption: t("hallReports.col.docDate"), field: "dateTime", value: (r) => fmtDate(r.dateTime || r.DateTime || r.date) },
                    { caption: t("hallReports.col.amount"), field: "totalAmount", value: (r) => fmtMoney(r.totalAmount ?? r.TotalAmount) },
                    { caption: t("hallReports.col.category"), field: "expenseCategoryName", value: (r) => r.expenseCategoryName || r.ExpenseCategoryName || "" }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "expenseNo",
                        caption: ctx.t("hallReports.col.expenseNo"),
                        cellTemplate(c, info) {
                            ctx.renderExpenseLink(c, info.data);
                        }
                    },
                    ctx.dateColumn("dateTime", "hallReports.col.docDate"),
                    { dataField: "expenseCategoryName", caption: ctx.t("hallReports.col.category") },
                    ctx.moneyColumn("beforeTaxAmount", "hallReports.col.beforeTax"),
                    ctx.moneyColumn("taxAmount", "hallReports.col.tax"),
                    ctx.moneyColumn("totalAmount", "hallReports.col.amount"),
                    { dataField: "approvalStatus", caption: ctx.t("hallReports.col.approvalStatus"), width: 110 },
                    { dataField: "paymentSource", caption: ctx.t("hallReports.col.paymentSource") },
                    { dataField: "comment", caption: ctx.t("hallReports.col.notes") }
                ];
            }
        });
    });
})(window, jQuery);
