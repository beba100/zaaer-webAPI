(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    function sourceSummaryLabel(item) {
        return item.source || item.Source || "—";
    }

    function onlineBookingsKpi($host, summary, t, fmtMoney) {
        const netAmount = summary.totalAmount ?? summary.TotalAmount ?? 0;
        const breakdown = summary.sourceBreakdown || summary.SourceBreakdown || [];

        $host.addClass("hall-reports-kpi-row--daily-journal").empty();

        const $netCard = $("<div class='hall-reports-kpi hall-reports-kpi--net'/>").appendTo($host);
        $("<div class='hall-reports-kpi__label'/>").text(t("hotelReports.kpi.netAmount")).appendTo($netCard);
        const $netValue = $("<div class='hall-reports-kpi__value hall-reports-kpi__value--with-currency'/>").appendTo($netCard);
        const $amount = common.appendNetAmountKpiValue($netValue, netAmount, fmtMoney);
        if (netAmount < 0) {
            $amount.addClass("hall-reports-kpi__value--negative");
        } else if (netAmount > 0) {
            $amount.addClass("hall-reports-kpi__value--positive");
        }

        const $stats = $("<div class='hall-reports-voucher-stats'/>").appendTo($host);
        $("<div class='hall-reports-voucher-stats__title'/>")
            .text(t("hotelReports.kpi.onlineSourceStats"))
            .appendTo($stats);

        if (!breakdown.length) {
            $("<div class='hall-reports-voucher-stats__empty'/>")
                .text(t("hotelReports.kpi.onlineSourceStatsEmpty"))
                .appendTo($stats);
            return;
        }

        const $grid = $("<div class='hall-reports-voucher-stats__grid'/>").appendTo($stats);
        breakdown.forEach((item) => {
            const total = item.totalAmount ?? item.TotalAmount ?? 0;
            const $card = $("<div class='hall-reports-voucher-stat hall-reports-voucher-stat--inflow'/>").appendTo($grid);
            $("<div class='hall-reports-voucher-stat__name'/>").text(sourceSummaryLabel(item)).appendTo($card);
            const $meta = $("<div class='hall-reports-voucher-stat__meta'/>").appendTo($card);
            $("<span class='hall-reports-voucher-stat__count'/>")
                .text(`${t("hotelReports.kpi.voucherCount")}: ${item.count ?? item.Count ?? 0}`)
                .appendTo($meta);
            $("<span class='hall-reports-voucher-stat__amount'/>")
                .text(fmtMoney(total))
                .appendTo($meta);
        });
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-online-bookings",
            reportKey: "online_bookings",
            titleKey: "hotelReports.title.onlineBookings",
            exportPrefix: "hotel-online-bookings",
            keyExpr: "reservationId",
            load(query) {
                return common.hotelSvc().getOnlineBookingsReport(query.fromDate, query.toDate);
            },
            renderKpi: onlineBookingsKpi,
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeOnlineBookingsSummaryFromRows(rows);
                if (!serverSummary) {
                    return computed;
                }
                return Object.assign({}, serverSummary, computed);
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hotelReports.col.reservationDate"), field: "reservationDate", value: (r) => fmtDate(r.reservationDate || r.ReservationDate) },
                    { caption: t("hotelReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo || "" },
                    { caption: t("hotelReports.col.source"), field: "source", value: (r) => r.source || r.Source || "" },
                    { caption: t("hotelReports.col.customer"), field: "customerName", value: (r) => r.customerName || r.CustomerName || "" },
                    { caption: t("hotelReports.col.total"), field: "totalAmount", value: (r) => fmtMoney(r.totalAmount ?? r.TotalAmount) }
                ];
            },
            columns(ctx) {
                return [
                    ctx.dateColumn("reservationDate", "hotelReports.col.reservationDate", 116),
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hotelReports.col.reservationNo"),
                        width: 120,
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    { dataField: "source", caption: ctx.t("hotelReports.col.source"), width: 140 },
                    {
                        dataField: "customerName",
                        caption: ctx.t("hotelReports.col.customer"),
                        minWidth: 160,
                        cellTemplate(c, info) {
                            ctx.renderCustomerLink(c, info.data);
                        }
                    },
                    ctx.moneyColumn("totalAmount", "hotelReports.col.total", 110)
                ];
            }
        });
    });
})(window, jQuery);
