(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    function rentalTypeText(row) {
        return common.mapRentalTypeDisplay(row.rentalType ?? row.RentalType);
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-departures",
            reportKey: "departures",
            titleKey: "hotelReports.title.departures",
            exportPrefix: "hotel-departures",
            keyExpr: "reservationId",
            defaultFromDate: "today",
            load(query) {
                return common.hotelSvc().getDeparturesReport(query.fromDate, query.toDate);
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hallReports.col.serial"), field: "serial", value: (_r, index) => index + 1 },
                    { caption: t("hotelReports.col.departureDate"), field: "departureDate", value: (r) => fmtDate(r.departureDate || r.DepartureDate) },
                    { caption: t("hotelReports.col.unit"), field: "unitLabel", value: (r) => r.unitLabel || r.UnitLabel || "" },
                    { caption: t("hotelReports.col.unitRent"), field: "unitRentAmount", value: (r) => fmtMoney(r.unitRentAmount ?? r.UnitRentAmount) },
                    { caption: t("hotelReports.col.rentalType"), field: "rentalType", value: (r) => rentalTypeText(r) },
                    { caption: t("hotelReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo || "" },
                    { caption: t("hotelReports.col.customer"), field: "customerName", value: (r) => r.customerName || r.CustomerName || "" },
                    { caption: t("hotelReports.col.mobile"), field: "mobileNo", value: (r) => r.mobileNo || r.MobileNo || "" }
                ];
            },
            columns(ctx) {
                return [
                    ctx.buildSerialNumberColumn("hallReports.col.serial"),
                    ctx.dateColumn("departureDate", "hotelReports.col.departureDate", 116),
                    { dataField: "unitLabel", caption: ctx.t("hotelReports.col.unit"), width: 100 },
                    ctx.moneyColumn("unitRentAmount", "hotelReports.col.unitRent", 120),
                    {
                        dataField: "rentalType",
                        caption: ctx.t("hotelReports.col.rentalType"),
                        width: 90,
                        calculateCellValue(row) {
                            return rentalTypeText(row);
                        }
                    },
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hotelReports.col.reservationNo"),
                        width: 120,
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    { dataField: "customerName", caption: ctx.t("hotelReports.col.customer"), minWidth: 140 },
                    { dataField: "mobileNo", caption: ctx.t("hotelReports.col.mobile"), width: 120 }
                ];
            }
        });
    });
})(window, jQuery);
