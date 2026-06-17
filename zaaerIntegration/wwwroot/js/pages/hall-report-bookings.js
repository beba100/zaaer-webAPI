(function (window, $) {
    "use strict";

    const common = window.Zaaer.HallReportCommon;

    $(function () {
        common.initReportPage({
            navKey: "nav-hall-report-bookings",
            reportKey: "bookings",
            titleKey: "hallReports.title.bookings",
            exportPrefix: "hall-bookings",
            keyExpr: "reservationId",
            load(query) {
                return common.hallSvc().getBookingsReport(query.fromDate, query.toDate);
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeBookingsSummaryFromRows(rows);
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            renderKpi($host, summary, t, fmtMoney) {
                const cards = [
                    { label: t("hallReports.kpi.count"), value: summary.eventCount ?? summary.EventCount ?? 0 },
                    { label: t("hallReports.kpi.totalRent"), value: fmtMoney(summary.totalRent ?? summary.TotalRent) },
                    { label: t("hallReports.kpi.totalDeposit"), value: fmtMoney(summary.totalDeposit ?? summary.TotalDeposit) },
                    { label: t("hallReports.kpi.totalBalance"), value: fmtMoney(summary.totalBalance ?? summary.TotalBalance) }
                ];
                cards.forEach((c) => {
                    const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
                    $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
                    $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
                });
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hallReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo },
                    { caption: t("hallReports.col.eventDate"), field: "eventDate", value: (r) => fmtDate(r.eventDate || r.EventDate) },
                    { caption: t("hallReports.col.hall"), field: "hallName", value: (r) => r.hallName || r.HallName },
                    { caption: t("hallReports.col.customer"), field: "customerName", value: (r) => r.customerName || r.CustomerName },
                    { caption: t("hallReports.col.rent"), field: "totalAmount", value: (r) => fmtMoney(r.totalAmount ?? r.TotalAmount) }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hallReports.col.reservationNo"),
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    ctx.dateColumn("eventDate", "hallReports.col.eventDate"),
                    { dataField: "eventDateHijriDisplay", caption: ctx.t("hallReports.col.eventDateHijri"), width: 120 },
                    { dataField: "hallName", caption: ctx.t("hallReports.col.hall") },
                    { dataField: "customerName", caption: ctx.t("hallReports.col.customer") },
                    { dataField: "occasionName", caption: ctx.t("hallReports.col.occasion") },
                    { dataField: "eventStatusLabelAr", caption: ctx.t("hallReports.col.status"), width: 110 },
                    ctx.moneyColumn("totalAmount", "hallReports.col.rent"),
                    ctx.moneyColumn("depositAmount", "hallReports.col.deposit"),
                    ctx.moneyColumn("remainingBalance", "hallReports.col.balance"),
                    {
                        caption: ctx.t("hallReports.col.time"),
                        width: 120,
                        calculateCellValue(row) {
                            const s = row.eventStartTime || row.EventStartTime || "";
                            const e = row.eventEndTime || row.EventEndTime || "";
                            return s && e ? `${s} - ${e}` : s || e;
                        }
                    }
                ];
            }
        });
    });
})(window, jQuery);
