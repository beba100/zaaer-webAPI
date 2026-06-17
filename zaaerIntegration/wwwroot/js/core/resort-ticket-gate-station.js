(function (window) {
    "use strict";

    function readStationCode() {
        const params = new URLSearchParams(window.location.search);
        const raw = (params.get("station") || params.get("stationCode") || "").trim();
        return raw ? raw.toLowerCase().replace(/\s+/g, "_") : "";
    }

    function categoryLabel(t, code) {
        const key = `resortTickets.category.${code}`;
        return (t && t(key)) || code;
    }

    function resolveStationLabel(stationCode, types, t, isAr) {
        if (!stationCode) {
            return "";
        }

        if (stationCode === "entry" || stationCode === "games" || stationCode === "pool") {
            return categoryLabel(t, stationCode);
        }

        const list = Array.isArray(types) ? types : [];
        const match = list.find((row) => {
            const code = String(row.code || row.Code || "").toLowerCase();
            return code === stationCode;
        });
        if (!match) {
            return stationCode;
        }
        if (isAr) {
            return match.nameAr || match.NameAr || match.nameEn || match.NameEn || stationCode;
        }
        return match.nameEn || match.NameEn || match.nameAr || match.NameAr || stationCode;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ResortTicketGateStation = {
        readStationCode,
        resolveStationLabel,
        buildGateUrl(stationCode) {
            const base = "/resort-ticket-gate.html";
            if (!stationCode) {
                return base;
            }
            return `${base}?station=${encodeURIComponent(stationCode)}`;
        }
    };
})(window);
