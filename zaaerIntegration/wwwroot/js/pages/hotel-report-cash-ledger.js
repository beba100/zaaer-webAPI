(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    function movementLabelText(row) {
        const raw = row.movementLabel ?? row.MovementLabel ?? "";
        return common.mapMovementLabelDisplay(raw);
    }

    function descriptionText(row) {
        let raw = `${row.description ?? row.Description ?? ""}`.trim();
        if (!raw) {
            return "";
        }
        const bankDeposit = common.mapMovementLabelDisplay("إيداع بنكي");
        return raw
            .replace(/تحويل بنكي/g, bankDeposit)
            .replace(/إيداع بنكي/g, bankDeposit);
    }

    const cashApi = window.Zaaer.PmsCashLedgerService;

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-cash-ledger",
            reportKey: "cash_ledger",
            titleKey: "hotelReports.title.cashLedger",
            exportPrefix: "hotel-cash-ledger",
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
                    { label: t("hotelReports.kpi.openingBalance"), value: fmtMoney(summary.openingBalance) },
                    { label: t("hotelReports.kpi.cashIn"), value: fmtMoney(summary.cashIn) },
                    { label: t("hotelReports.kpi.cashOut"), value: fmtMoney(summary.cashOut) },
                    {
                        label: t("hotelReports.kpi.closingBalance"),
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
                    { caption: t("hotelReports.col.docDate"), field: "transactionDate", value: (r) => fmtDate(r.transactionDate || r.TransactionDate) },
                    {
                        caption: t("hotelReports.col.movementLabel"),
                        field: "movementLabel",
                        value: (r) => movementLabelText(r)
                    },
                    { caption: t("hotelReports.col.sourceNo"), field: "sourceNo", value: (r) => r.sourceNo || r.SourceNo || "" },
                    { caption: t("hotelReports.col.cashIn"), field: "creditAmount", value: (r) => fmtMoney(r.creditAmount ?? r.CreditAmount) },
                    { caption: t("hotelReports.col.cashOut"), field: "debitAmount", value: (r) => fmtMoney(r.debitAmount ?? r.DebitAmount) },
                    { caption: t("hotelReports.col.balanceEffect"), field: "balanceAmount", value: (r) => fmtMoney(r.balanceAmount ?? r.BalanceAmount) },
                    {
                        caption: t("hotelReports.col.notes"),
                        field: "description",
                        value: (r) => descriptionText(r)
                    }
                ];
            },
            columns(ctx) {
                return [
                    ctx.dateColumn("transactionDate", "hotelReports.col.docDate", 116),
                    {
                        dataField: "movementLabel",
                        caption: ctx.t("hotelReports.col.movementLabel"),
                        minWidth: 140,
                        calculateCellValue(row) {
                            return movementLabelText(row);
                        }
                    },
                    { dataField: "sourceNo", caption: ctx.t("hotelReports.col.sourceNo"), width: 130 },
                    { dataField: "sourceType", caption: ctx.t("hotelReports.col.sourceType"), width: 130, visible: false },
                    { dataField: "sourceSubtype", caption: ctx.t("hotelReports.col.voucherCode"), width: 130, visible: false },
                    { dataField: "sourceZaaerId", caption: ctx.t("hotelReports.col.sourceZaaerId"), width: 125, visible: false },
                    ctx.moneyColumn("creditAmount", "hotelReports.col.cashIn"),
                    ctx.moneyColumn("debitAmount", "hotelReports.col.cashOut"),
                    ctx.moneyColumn("balanceAmount", "hotelReports.col.balanceEffect"),
                    {
                        dataField: "description",
                        caption: ctx.t("hotelReports.col.notes"),
                        minWidth: 220,
                        calculateCellValue(row) {
                            return descriptionText(row);
                        }
                    }
                ];
            }
        });
    });
})(window, jQuery);
