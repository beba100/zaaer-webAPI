(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    function applyModeText(row) {
        const raw = `${row.applyMode ?? row.ApplyMode ?? ""}`.trim();
        if (!raw) {
            return "";
        }
        const key = `hotelReports.unitTransfer.applyMode.${raw.charAt(0).toLowerCase()}${raw.slice(1)}`;
        const label = common.t(key);
        return label !== key ? label : raw;
    }

    function unitCellText(label, linkId) {
        const name = `${label || ""}`.trim();
        if (name) {
            return name;
        }
        const id = Number(linkId);
        return Number.isFinite(id) && id > 0 ? String(id) : "";
    }

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-unit-transfers",
            reportKey: "unit_transfers",
            titleKey: "hotelReports.title.unitTransfers",
            exportPrefix: "hotel-unit-transfers",
            keyExpr: "switchId",
            defaultFromDate: "today",
            load(query) {
                return common.hotelSvc().getUnitTransfersReport(query.fromDate, query.toDate);
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hallReports.col.serial"), field: "serial", value: (_r, index) => index + 1 },
                    { caption: t("hotelReports.col.transferDate"), field: "createdAt", value: (r) => common.fmtDateTime(r.createdAt || r.CreatedAt) },
                    { caption: t("hotelReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo || "" },
                    { caption: t("hotelReports.col.customer"), field: "customerName", value: (r) => r.customerName || r.CustomerName || "" },
                    { caption: t("hotelReports.col.fromUnit"), field: "fromUnitLabel", value: (r) => unitCellText(r.fromUnitLabel || r.FromUnitLabel, r.fromApartmentId ?? r.FromApartmentId) },
                    { caption: t("hotelReports.col.fromRoomType"), field: "fromRoomTypeName", value: (r) => r.fromRoomTypeName || r.FromRoomTypeName || "" },
                    { caption: t("hotelReports.col.toUnit"), field: "toUnitLabel", value: (r) => unitCellText(r.toUnitLabel || r.ToUnitLabel, r.toApartmentId ?? r.ToApartmentId) },
                    { caption: t("hotelReports.col.toRoomType"), field: "toRoomTypeName", value: (r) => r.toRoomTypeName || r.ToRoomTypeName || "" },
                    { caption: t("hotelReports.col.applyMode"), field: "applyMode", value: (r) => applyModeText(r) },
                    { caption: t("hotelReports.col.createdByUser"), field: "createdByUserName", value: (r) => r.createdByUserName || r.CreatedByUserName || "" },
                    { caption: t("hotelReports.col.notes"), field: "comment", value: (r) => r.comment || r.Comment || "" }
                ];
            },
            columns(ctx) {
                return [
                    ctx.buildSerialNumberColumn("hallReports.col.serial"),
                    {
                        dataField: "createdAt",
                        caption: ctx.t("hotelReports.col.transferDate"),
                        width: 150,
                        allowHeaderFiltering: false,
                        calculateCellValue(row) {
                            return common.fmtDateTime(row.createdAt ?? row.CreatedAt);
                        }
                    },
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hotelReports.col.reservationNo"),
                        width: 110,
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    { dataField: "customerName", caption: ctx.t("hotelReports.col.customer"), minWidth: 130 },
                    {
                        dataField: "fromUnitLabel",
                        caption: ctx.t("hotelReports.col.fromUnit"),
                        width: 80,
                        calculateCellValue(row) {
                            return unitCellText(row.fromUnitLabel ?? row.FromUnitLabel, row.fromApartmentId ?? row.FromApartmentId);
                        }
                    },
                    {
                        dataField: "fromRoomTypeName",
                        caption: ctx.t("hotelReports.col.fromRoomType"),
                        minWidth: 140
                    },
                    {
                        dataField: "toUnitLabel",
                        caption: ctx.t("hotelReports.col.toUnit"),
                        width: 80,
                        calculateCellValue(row) {
                            return unitCellText(row.toUnitLabel ?? row.ToUnitLabel, row.toApartmentId ?? row.ToApartmentId);
                        }
                    },
                    {
                        dataField: "toRoomTypeName",
                        caption: ctx.t("hotelReports.col.toRoomType"),
                        minWidth: 140
                    },
                    {
                        dataField: "applyMode",
                        caption: ctx.t("hotelReports.col.applyMode"),
                        width: 120,
                        calculateCellValue(row) {
                            return applyModeText(row);
                        }
                    },
                    {
                        dataField: "createdByUserName",
                        caption: ctx.t("hotelReports.col.createdByUser"),
                        minWidth: 130
                    },
                    {
                        dataField: "comment",
                        caption: ctx.t("hotelReports.col.notes"),
                        minWidth: 280,
                        wordWrapEnabled: true
                    },
                    { dataField: "reservationRouteId", visible: false },
                    { dataField: "reservationZaaerId", visible: false }
                ];
            }
        });
    });
})(window, jQuery);
