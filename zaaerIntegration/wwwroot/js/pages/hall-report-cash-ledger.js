(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;
    const cashApi = window.Zaaer.PmsCashLedgerService;

    function formatMovementLabel(value) {
        const text = `${value || ""}`.trim();
        return text === "تحويل بنكي" ? "إيداع بنكي" : text;
    }

    function formatDescription(value) {
        const text = `${value || ""}`.trim();
        if (!text) return text;
        return text
            .replace(/تحويل بنكي/g, "إيداع بنكي")
            .replace(/تحويل/g, "إيداع");
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-cash-ledger",
            reportKey: "cash_ledger",
            titleKey: "hallReports.title.cashLedger",
            exportPrefix: "cash-ledger",
            keyExpr: "ledgerId",
            load(query) {
                return cashApi.report(query.fromDate, query.toDate).then((data) => {
                    const items = (data && (data.items || data.Items)) || [];
                    return {
                        items,
                        summary: {
                            count: items.length,
                            openingBalance: data.openingBalance ?? data.OpeningBalance ?? 0,
                            cashIn: data.cashIn ?? data.CashIn ?? 0,
                            cashOut: data.cashOut ?? data.CashOut ?? 0,
                            closingBalance: data.closingBalance ?? data.ClosingBalance ?? 0
                        }
                    };
                });
            },
            computeKpiFromRows(rows, serverSummary) {
                const opening = serverSummary && (serverSummary.openingBalance ?? serverSummary.OpeningBalance) || 0;
                const cashIn = rows.reduce((sum, row) => sum + (Number(row.creditAmount ?? row.CreditAmount) || 0), 0);
                const cashOut = rows.reduce((sum, row) => sum + (Number(row.debitAmount ?? row.DebitAmount) || 0), 0);
                return {
                    count: rows.length,
                    openingBalance: opening,
                    cashIn,
                    cashOut,
                    closingBalance: opening + cashIn - cashOut
                };
            },
            renderKpi($host, summary, t, fmtMoney) {
                const closingBalance = summary.closingBalance ?? 0;
                [
                    { label: t("hallReports.kpi.openingBalance"), value: fmtMoney(summary.openingBalance) },
                    { label: t("hallReports.kpi.cashIn"), value: fmtMoney(summary.cashIn) },
                    { label: t("hallReports.kpi.cashOut"), value: fmtMoney(summary.cashOut) },
                    {
                        label: t("hallReports.kpi.closingBalance"),
                        value: fmtMoney(closingBalance),
                        tone: closingBalance < 0 ? "negative" : closingBalance > 0 ? "positive" : null
                    }
                ].forEach((c) => {
                    const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
                    $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
                    const $value = $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
                    if (c.tone === "negative") {
                        $value.addClass("hall-reports-kpi__value--negative");
                    } else if (c.tone === "positive") {
                        $value.addClass("hall-reports-kpi__value--positive");
                    }
                });
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hallReports.col.docDate"), field: "transactionDate", value: (r) => fmtDate(r.transactionDate || r.TransactionDate) },
                    {
                        caption: t("hallReports.col.movementLabel"),
                        field: "movementLabel",
                        value: (r) => formatMovementLabel(r.movementLabel || r.MovementLabel)
                    },
                    { caption: t("hallReports.col.sourceNo"), field: "sourceNo", value: (r) => r.sourceNo || r.SourceNo || "" },
                    { caption: t("hallReports.col.cashIn"), field: "creditAmount", value: (r) => fmtMoney(r.creditAmount ?? r.CreditAmount) },
                    { caption: t("hallReports.col.cashOut"), field: "debitAmount", value: (r) => fmtMoney(r.debitAmount ?? r.DebitAmount) },
                    { caption: t("hallReports.col.balanceEffect"), field: "balanceAmount", value: (r) => fmtMoney(r.balanceAmount ?? r.BalanceAmount) },
                    {
                        caption: t("hallReports.col.notes"),
                        field: "description",
                        value: (r) => formatDescription(r.description || r.Description)
                    }
                ];
            },
            columns(ctx) {
                return [
                    ctx.dateColumn("transactionDate", "hallReports.col.docDate", 116),
                    {
                        dataField: "movementLabel",
                        caption: ctx.t("hallReports.col.movementLabel"),
                        minWidth: 140,
                        calculateCellValue(row) {
                            return formatMovementLabel(row.movementLabel ?? row.MovementLabel);
                        }
                    },
                    { dataField: "sourceNo", caption: ctx.t("hallReports.col.sourceNo"), width: 130 },
                    { dataField: "sourceType", caption: ctx.t("hallReports.col.sourceType"), width: 130, visible: false },
                    { dataField: "sourceSubtype", caption: ctx.t("hallReports.col.voucherCode"), width: 130, visible: false },
                    { dataField: "sourceZaaerId", caption: ctx.t("hallReports.col.sourceZaaerId"), width: 125, visible: false },
                    ctx.moneyColumn("creditAmount", "hallReports.col.cashIn"),
                    ctx.moneyColumn("debitAmount", "hallReports.col.cashOut"),
                    ctx.moneyColumn("balanceAmount", "hallReports.col.balanceEffect"),
                    {
                        dataField: "description",
                        caption: ctx.t("hallReports.col.notes"),
                        minWidth: 220,
                        calculateCellValue(row) {
                            return formatDescription(row.description ?? row.Description);
                        }
                    }
                ];
            }
        });
    });
})(window, jQuery);
