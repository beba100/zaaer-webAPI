(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    function currentLocaleTag() {
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        if (loc && typeof loc.currentCulture === "function") {
            return loc.currentCulture() === "ar" ? "ar-SA" : "en-US";
        }
        return document.documentElement.lang === "ar" || document.documentElement.dir === "rtl" ? "ar-SA" : "en-US";
    }

    function parseRowDate(row) {
        const raw = row.date ?? row.Date;
        if (!raw) {
            return null;
        }
        const d = raw instanceof Date ? raw : new Date(raw);
        if (Number.isNaN(d.getTime())) {
            return null;
        }
        return d;
    }

    function readMoney(row, field) {
        const pascal = field.charAt(0).toUpperCase() + field.slice(1);
        return Number(row[field] ?? row[pascal] ?? 0) || 0;
    }

    function enrichRows(items) {
        const dayNameFormatter = new Intl.DateTimeFormat(currentLocaleTag(), { weekday: "long" });
        return (items || []).map((row) => {
            const date = parseRowDate(row);
            return Object.assign({}, row, {
                dayName: date ? dayNameFormatter.format(date) : ""
            });
        });
    }

    function operationColumns(ctx) {
        return [
            ctx.moneyColumn("cashAmount", "hotelReports.monthClosing.col.cash", 100),
            ctx.moneyColumn("madaAmount", "hotelReports.monthClosing.col.mada", 110),
            ctx.moneyColumn("otherPaidAmount", "hotelReports.monthClosing.col.otherPaid", 100),
            ctx.moneyColumn("bankTransferAmount", "hotelReports.monthClosing.col.bankTransfer", 110)
        ];
    }

    function pdfMoneyColumns(t, fmtDate, fmtMoney) {
        return [
            { caption: t("hotelReports.col.date"), field: "date", value: (r) => fmtDate(r.date || r.Date) },
            { caption: t("hotelReports.targets.days.dayName"), field: "dayName", value: (r) => r.dayName || r.DayName || "" },
            { caption: t("hotelReports.monthClosing.col.rentInsuranceNet"), field: "rentInsuranceNet", value: (r) => fmtMoney(readMoney(r, "rentInsuranceNet")) },
            { caption: t("hotelReports.monthClosing.col.deposits"), field: "depositsAmount", value: (r) => fmtMoney(readMoney(r, "depositsAmount")) },
            { caption: t("hotelReports.monthClosing.col.expenses"), field: "expensesAmount", value: (r) => fmtMoney(readMoney(r, "expensesAmount")) },
            { caption: t("hotelReports.monthClosing.col.cash"), field: "cashAmount", value: (r) => fmtMoney(readMoney(r, "cashAmount")) },
            { caption: t("hotelReports.monthClosing.col.mada"), field: "madaAmount", value: (r) => fmtMoney(readMoney(r, "madaAmount")) },
            { caption: t("hotelReports.monthClosing.col.otherPaid"), field: "otherPaidAmount", value: (r) => fmtMoney(readMoney(r, "otherPaidAmount")) },
            { caption: t("hotelReports.monthClosing.col.bankTransfer"), field: "bankTransferAmount", value: (r) => fmtMoney(readMoney(r, "bankTransferAmount")) },
            { caption: t("hotelReports.monthClosing.col.netExTax"), field: "netExTax", value: (r) => fmtMoney(readMoney(r, "netExTax")) }
        ];
    }

    function renderKpi($host, summary, t, fmtMoney) {
        if (!summary) {
            return;
        }

        const cards = [
            { label: t("hotelReports.monthClosing.col.rentInsuranceNet"), value: fmtMoney(summary.rentInsuranceNet ?? summary.RentInsuranceNet) },
            { label: t("hotelReports.monthClosing.col.deposits"), value: fmtMoney(summary.depositsAmount ?? summary.DepositsAmount) },
            { label: t("hotelReports.monthClosing.col.expenses"), value: fmtMoney(summary.expensesAmount ?? summary.ExpensesAmount) },
            { label: t("hotelReports.monthClosing.col.netExTax"), value: fmtMoney(summary.netExTax ?? summary.NetExTax) }
        ];

        $host.empty();
        cards.forEach((card) => {
            const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
            $("<div class='hall-reports-kpi__label'/>").text(card.label).appendTo($card);
            $("<div class='hall-reports-kpi__value'/>").text(card.value).appendTo($card);
        });
    }

    $(function () {
        let reloadReport = null;

        common.initReportPage({
            navKey: "nav-hotel-report-month-end-closing",
            reportKey: "month_end_closing",
            titleKey: "hotelReports.title.monthEndClosing",
            exportPrefix: "hotel-month-end-closing",
            keyExpr: "date",
            gridExtraClass: "month-closing-grid",
            onReady(api) {
                reloadReport = api && typeof api.reload === "function" ? api.reload : null;
            },
            load(query) {
                return common.hotelSvc().getMonthEndClosingReport(query.fromDate, query.toDate).then((data) => {
                    const items = enrichRows((data && (data.items || data.Items)) || []);
                    const summary = data && (data.summary || data.Summary);
                    return { items, summary };
                });
            },
            renderKpi,
            pdfColumns: pdfMoneyColumns,
            columns(ctx) {
                return [
                    ctx.dateColumn("date", "hotelReports.col.date", 110),
                    {
                        dataField: "dayName",
                        caption: ctx.t("hotelReports.targets.days.dayName"),
                        minWidth: 100,
                        allowHeaderFiltering: false,
                        allowSorting: false
                    },
                    ctx.moneyColumn("rentInsuranceNet", "hotelReports.monthClosing.col.rentInsuranceNet", 110),
                    ctx.moneyColumn("depositsAmount", "hotelReports.monthClosing.col.deposits", 100),
                    ctx.moneyColumn("expensesAmount", "hotelReports.monthClosing.col.expenses", 100),
                    {
                        caption: ctx.t("hotelReports.monthClosing.group.totalOperations"),
                        alignment: "center",
                        cssClass: "month-closing-ops-band",
                        columns: operationColumns(ctx)
                    },
                    ctx.moneyColumn("netExTax", "hotelReports.monthClosing.col.netExTax", 110)
                ];
            }
        });

        window.addEventListener("zaaer:culture-changed", () => {
            if (typeof reloadReport === "function") {
                reloadReport();
            }
        });
    });
})(window, jQuery);
