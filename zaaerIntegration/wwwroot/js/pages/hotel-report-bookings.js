(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    function statusText(row) {
        return common.mapReservationStatusDisplay(row.status ?? row.Status);
    }

    function rentalTypeText(row) {
        return common.mapRentalTypeDisplay(row.rentalType ?? row.RentalType);
    }

    function bookingStatusCellClass(rawStatus) {
        const raw = String(rawStatus ?? "")
            .trim()
            .toLowerCase()
            .replace(/[\s-]+/g, "_");
        if (raw === "checked_out" || raw === "checkedout") {
            return "hall-reports-status-cell--checked-out";
        }
        if (raw === "checked_in" || raw === "checkedin") {
            return "hall-reports-status-cell--checked-in";
        }
        return null;
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-bookings",
            reportKey: "bookings",
            titleKey: "hotelReports.title.bookings",
            exportPrefix: "hotel-bookings",
            keyExpr: "reservationId",
            load(query) {
                return common.hotelSvc().getBookingsReport(query.fromDate, query.toDate);
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeBookingsSummaryFromRows(rows);
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            renderKpi($host, summary, t, fmtMoney) {
                const cards = [
                    { label: t("hotelReports.kpi.count"), value: summary.count ?? summary.Count ?? 0 },
                    { label: t("hotelReports.kpi.totalAmount"), value: fmtMoney(summary.totalAmount ?? summary.TotalAmount) },
                    { label: t("hotelReports.kpi.totalPaid"), value: fmtMoney(summary.totalPaid ?? summary.TotalPaid) },
                    { label: t("hotelReports.kpi.totalBalance"), value: fmtMoney(summary.totalBalance ?? summary.TotalBalance), tone: (summary.totalBalance ?? summary.TotalBalance) < 0 ? "negative" : null },
                    { label: t("hotelReports.kpi.totalRefunded"), value: fmtMoney(summary.totalRefunded ?? summary.TotalRefunded) },
                    { label: t("hotelReports.kpi.totalSecurityDeposit"), value: fmtMoney(summary.totalSecurityDeposit ?? summary.TotalSecurityDeposit) }
                ];
                cards.forEach((c) => {
                    const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
                    $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
                    const $value = $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
                    if (c.tone === "negative") {
                        $value.addClass("hall-reports-kpi__value--negative");
                    }
                });
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hotelReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo || "" },
                    { caption: t("hotelReports.col.source"), field: "source", value: (r) => r.source || r.Source || "" },
                    { caption: t("hotelReports.col.customer"), field: "customerName", value: (r) => r.customerName || r.CustomerName || "" },
                    { caption: t("hotelReports.col.company"), field: "companyName", value: (r) => r.companyName || r.CompanyName || "" },
                    { caption: t("hotelReports.col.unit"), field: "unitLabel", value: (r) => r.unitLabel || r.UnitLabel || "" },
                    { caption: t("hotelReports.col.status"), field: "status", value: (r) => statusText(r) },
                    { caption: t("hotelReports.col.rentalType"), field: "rentalType", value: (r) => rentalTypeText(r) },
                    { caption: t("hotelReports.col.checkIn"), field: "checkInDate", value: (r) => fmtDate(r.checkInDate || r.CheckInDate) },
                    { caption: t("hotelReports.col.checkOut"), field: "checkOutDate", value: (r) => fmtDate(r.checkOutDate || r.CheckOutDate) },
                    { caption: t("hotelReports.col.createdAt"), field: "createdAt", value: (r) => common.fmtDateTime(r.createdAt || r.CreatedAt) },
                    { caption: t("hotelReports.col.additions"), field: "totalExtra", value: (r) => fmtMoney(r.totalExtra ?? r.TotalExtra) },
                    { caption: t("hotelReports.col.tax"), field: "totalTax", value: (r) => fmtMoney(r.totalTax ?? r.TotalTax) },
                    { caption: t("hotelReports.col.total"), field: "totalAmount", value: (r) => fmtMoney(r.totalAmount ?? r.TotalAmount) },
                    { caption: t("hotelReports.col.securityDeposit"), field: "securityDeposit", value: (r) => fmtMoney(r.securityDeposit ?? r.SecurityDeposit) },
                    { caption: t("hotelReports.col.paid"), field: "amountPaid", value: (r) => fmtMoney(r.amountPaid ?? r.AmountPaid) },
                    { caption: t("hotelReports.col.refunded"), field: "refunded", value: (r) => fmtMoney(r.refunded ?? r.Refunded) },
                    { caption: t("hotelReports.col.balance"), field: "balanceAmount", value: (r) => fmtMoney(r.balanceAmount ?? r.BalanceAmount) }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hotelReports.col.reservationNo"),
                        width: 120,
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    { dataField: "source", caption: ctx.t("hotelReports.col.source"), width: 120 },
                    { dataField: "customerName", caption: ctx.t("hotelReports.col.customer"), minWidth: 150 },
                    {
                        dataField: "companyName",
                        caption: ctx.t("hotelReports.col.company"),
                        minWidth: 200,
                        width: 220,
                        cssClass: "hall-reports-col-company"
                    },
                    { dataField: "unitLabel", caption: ctx.t("hotelReports.col.unit"), width: 110 },
                    {
                        dataField: "status",
                        caption: ctx.t("hotelReports.col.status"),
                        width: 100,
                        calculateCellValue(row) {
                            return statusText(row);
                        }
                    },
                    {
                        dataField: "rentalType",
                        caption: ctx.t("hotelReports.col.rentalType"),
                        width: 90,
                        calculateCellValue(row) {
                            return rentalTypeText(row);
                        }
                    },
                    ctx.dateColumn("checkInDate", "hotelReports.col.checkIn", 108),
                    ctx.dateColumn("checkOutDate", "hotelReports.col.checkOut", 108),
                    {
                        dataField: "createdAt",
                        caption: ctx.t("hotelReports.col.createdAt"),
                        width: 150,
                        allowHeaderFiltering: false,
                        calculateCellValue(row) {
                            return common.fmtDateTime(row.createdAt ?? row.CreatedAt);
                        }
                    },
                    ctx.moneyColumn("totalExtra", "hotelReports.col.additions"),
                    ctx.moneyColumn("totalTax", "hotelReports.col.tax"),
                    ctx.moneyColumn("totalAmount", "hotelReports.col.total"),
                    ctx.moneyColumn("securityDeposit", "hotelReports.col.securityDeposit"),
                    ctx.moneyColumn("amountPaid", "hotelReports.col.paid"),
                    ctx.moneyColumn("refunded", "hotelReports.col.refunded"),
                    ctx.moneyColumn("balanceAmount", "hotelReports.col.balance"),
                    { dataField: "reservationRouteId", visible: false },
                    { dataField: "reservationZaaerId", visible: false }
                ];
            },
            onCellPrepared(e) {
                if (!e || e.rowType !== "data" || !e.column || e.column.dataField !== "status") {
                    return;
                }
                const cls = bookingStatusCellClass(e.data && (e.data.status ?? e.data.Status));
                if (cls) {
                    e.cellElement.addClass(cls);
                }
            }
        });
    });
})(window, jQuery);
