(function (window) {
    "use strict";

    const base = window.Zaaer && window.Zaaer.HallReportCommon;
    if (!base) {
        return;
    }

    function buildLodgingPermissionKeys(cfg, mode) {
        const rbac = window.Zaaer && window.Zaaer.PmsRbacNav;
        const keys = [];

        if (cfg.reportKey && rbac && typeof rbac.resolveLodgingReportPermissionKeys === "function") {
            keys.push.apply(keys, rbac.resolveLodgingReportPermissionKeys(cfg.reportKey, mode));
        } else if (Array.isArray(cfg.permissionKeys) && cfg.permissionKeys.length) {
            keys.push.apply(keys, cfg.permissionKeys);
        } else {
            keys.push(cfg.permissionKey || "hotel.reports");
        }

        const reportKey = `${cfg.reportKey || ""}`.trim().toLowerCase().replace(/-/g, "_");
        if (reportKey === "expenses") {
            keys.push("finance.expense.view");
        }
        if (reportKey === "deposits") {
            keys.push("finance.deposit.view");
        }

        return keys.filter((value, index, list) => value && list.indexOf(value) === index);
    }

    function initHotelReportPage(config) {
        const cfg = config || {};
        const shell = window.Zaaer && window.Zaaer.PmsAdminShell;
        const starter = shell && typeof shell.fetchPropertyMode === "function"
            ? shell.fetchPropertyMode()
            : $.Deferred().resolve({ isHotel: true, isResort: false, isHall: false }).promise();

        starter.then((mode) => {
            base.initReportPage(Object.assign({
                propertyContext: "lodging",
                propertyMode: mode,
                permissionKeys: buildLodgingPermissionKeys(cfg, mode),
                forbiddenKey: "hotelReports.forbidden",
                financeForbiddenKey: "hotelReports.financeForbidden"
            }, cfg));
        });
    }

    function fmtDateTime(value) {
        if (!value) {
            return "";
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const day = String(d.getDate()).padStart(2, "0");
        const month = String(d.getMonth() + 1).padStart(2, "0");
        const year = d.getFullYear();
        let hours = d.getHours();
        const minutes = String(d.getMinutes()).padStart(2, "0");
        const ampm = hours >= 12 ? "PM" : "AM";
        hours = hours % 12;
        if (hours === 0) {
            hours = 12;
        }
        return `${day}/${month}/${year} ${hours}:${minutes} ${ampm}`;
    }

    function mapReservationStatusDisplay(status) {
        const raw = status == null ? "" : String(status).trim();
        if (!raw) {
            return "";
        }
        const key = `hotelReports.status.${raw.toLowerCase()}`;
        const translated = base.t(key);
        return translated !== key ? translated : raw;
    }

    function mapRentalTypeDisplay(value) {
        const raw = value == null ? "" : String(value).trim();
        if (!raw) {
            return "";
        }
        const key = `hotelReports.rentalType.${raw.toLowerCase()}`;
        const translated = base.t(key);
        return translated !== key ? translated : raw;
    }

    function mapReceiptStatusDisplay(status) {
        const raw = status == null ? "" : String(status).trim();
        if (!raw) {
            return "";
        }
        const key = `hotelReports.status.${raw.toLowerCase()}`;
        const translated = base.t(key);
        return translated !== key ? translated : raw;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.HotelReportCommon = Object.assign({}, base, {
        initReportPage: initHotelReportPage,
        fmtDateTime,
        mapReservationStatusDisplay,
        mapRentalTypeDisplay,
        mapReceiptStatusDisplay,
        hotelSvc() {
            return window.Zaaer && window.Zaaer.HotelReportsService;
        }
    });
})(window);
