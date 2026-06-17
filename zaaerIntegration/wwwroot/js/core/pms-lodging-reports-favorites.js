(function (window) {
    "use strict";

    const STORAGE_VERSION = "v1";

    function resolveUserKey() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (api && typeof api.decodeTokenPayload === "function") {
            const payload = api.decodeTokenPayload();
            const raw = payload && (
                payload.sub
                || payload.userId
                || payload.UserId
                || payload.username
                || payload.Username
            );
            if (raw) {
                return String(raw);
            }
        }
        return "anonymous";
    }

    function resolvePropertyKind(mode) {
        return mode && mode.isResort ? "resort" : "hotel";
    }

    function resolveHotelCode() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        return (api && typeof api.getHotelCode === "function" ? api.getHotelCode() : "") || "default";
    }

    function buildStorageKey(mode) {
        return [
            "pms.lodgingReports.favorites",
            STORAGE_VERSION,
            resolvePropertyKind(mode),
            resolveHotelCode(),
            resolveUserKey()
        ].join(":");
    }

    function readRaw(mode) {
        try {
            const raw = window.localStorage.getItem(buildStorageKey(mode));
            if (!raw) {
                return [];
            }
            const parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed.filter((id) => typeof id === "string") : [];
        } catch {
            return [];
        }
    }

    function writeRaw(mode, ids) {
        window.localStorage.setItem(buildStorageKey(mode), JSON.stringify(ids));
        try {
            window.dispatchEvent(new CustomEvent("pms:lodging-report-favorites-changed", {
                detail: { ids: ids.slice(), mode: resolvePropertyKind(mode) }
            }));
        } catch {
            /* ignore */
        }
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsLodgingReportsFavorites = {
        list(mode) {
            return readRaw(mode);
        },
        isFavorite(reportId, mode) {
            return readRaw(mode).indexOf(reportId) >= 0;
        },
        toggle(reportId, mode) {
            const ids = readRaw(mode);
            const index = ids.indexOf(reportId);
            if (index >= 0) {
                ids.splice(index, 1);
            } else {
                ids.push(reportId);
            }
            writeRaw(mode, ids);
            return ids.indexOf(reportId) >= 0;
        },
        sortReports(reports, mode) {
            const favoriteOrder = readRaw(mode);
            const favIndex = new Map(favoriteOrder.map((id, index) => [id, index]));
            const favoritesList = [];
            const others = [];

            (reports || []).forEach((report) => {
                if (favIndex.has(report.id)) {
                    favoritesList.push(report);
                } else {
                    others.push(report);
                }
            });

            favoritesList.sort((a, b) => favIndex.get(a.id) - favIndex.get(b.id));
            others.sort((a, b) => a.title.localeCompare(b.title, undefined, { sensitivity: "base" }));
            return favoritesList.concat(others);
        },
        orderFavoriteReports(reports, mode) {
            const favoriteOrder = readRaw(mode);
            const reportById = new Map((reports || []).map((report) => [report.id, report]));
            return favoriteOrder
                .map((id) => reportById.get(id))
                .filter(Boolean);
        }
    };
})(window);
