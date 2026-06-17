(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;
    const expenseApi = window.Zaaer.PmsExpenseService;

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-expenses",
            reportKey: "expenses",
            titleKey: "hotelReports.title.expenses",
            exportPrefix: "hotel-expenses",
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
                    { label: t("hotelReports.kpi.count"), value: summary.count ?? 0 },
                    { label: t("hotelReports.kpi.totalAmount"), value: fmtMoney(summary.totalAmount) }
                ].forEach((c) => {
                    const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
                    $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
                    $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
                });
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hotelReports.col.expenseNo"), field: "expenseNo", value: (r) => r.expenseNo || r.ExpenseNo || r.number },
                    { caption: t("hotelReports.col.docDate"), field: "dateTime", value: (r) => fmtDate(r.dateTime || r.DateTime || r.date) },
                    { caption: t("hotelReports.col.amount"), field: "totalAmount", value: (r) => fmtMoney(r.totalAmount ?? r.TotalAmount) },
                    { caption: t("hotelReports.col.category"), field: "expenseCategoryName", value: (r) => r.expenseCategoryName || r.ExpenseCategoryName || "" }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "expenseNo",
                        caption: ctx.t("hotelReports.col.expenseNo"),
                        cellTemplate(c, info) {
                            ctx.renderExpenseLink(c, info.data);
                        }
                    },
                    ctx.dateColumn("dateTime", "hotelReports.col.docDate"),
                    { dataField: "expenseCategoryName", caption: ctx.t("hotelReports.col.category") },
                    ctx.moneyColumn("beforeTaxAmount", "hotelReports.col.beforeTax"),
                    ctx.moneyColumn("taxAmount", "hotelReports.col.tax"),
                    ctx.moneyColumn("totalAmount", "hotelReports.col.amount"),
                    {
                        dataField: "approvalStatus",
                        caption: ctx.t("hotelReports.col.approvalStatus"),
                        width: 110,
                        calculateCellValue(row) {
                            return ctx.mapApprovalStatusDisplay(row.approvalStatus ?? row.ApprovalStatus);
                        }
                    },
                    {
                        dataField: "paymentSource",
                        caption: ctx.t("hotelReports.col.paymentSource"),
                        calculateCellValue(row) {
                            return ctx.mapPaymentSourceDisplay(row.paymentSource ?? row.PaymentSource);
                        }
                    },
                    { dataField: "comment", caption: ctx.t("hotelReports.col.notes") }
                ];
            }
        });
    });
})(window, jQuery);
