(function (window) {
    "use strict";

    const KSA_TZ = "Asia/Riyadh";

    function unwrapData(raw) {
        if (raw == null || typeof raw !== "object") {
            return raw;
        }

        if (raw.data !== undefined) {
            return raw.data;
        }

        if (raw.Data !== undefined) {
            return raw.Data;
        }

        return raw;
    }

    function dateFromParts(p) {
        return new Date(
            Number(p.year),
            Number(p.month) - 1,
            Number(p.day),
            Number(p.hour) || 0,
            Number(p.minute) || 0,
            Number(p.second) || 0,
            0
        );
    }

    function nowFromIntl() {
        const d = new Date();
        const fmt = new Intl.DateTimeFormat("en-GB", {
            timeZone: KSA_TZ,
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
            hour12: false
        });
        const parts = fmt.formatToParts(d);
        const get = (type) => {
            const hit = parts.find((x) => x.type === type);
            return hit ? hit.value : "0";
        };

        return dateFromParts({
            year: Number(get("year")),
            month: Number(get("month")),
            day: Number(get("day")),
            hour: Number(get("hour")),
            minute: Number(get("minute")),
            second: Number(get("second"))
        });
    }

    function parseServerPayload(raw) {
        const inner = unwrapData(raw);
        if (!inner || typeof inner !== "object") {
            return null;
        }

        const year = inner.year ?? inner.Year;
        const month = inner.month ?? inner.Month;
        const day = inner.day ?? inner.Day;
        const hour = inner.hour ?? inner.Hour;
        const minute = inner.minute ?? inner.Minute;
        const second = inner.second ?? inner.Second;

        if ([year, month, day].some((x) => x === undefined || x === null)) {
            return null;
        }

        const dt = dateFromParts({ year, month, day, hour, minute, second });
        return Number.isNaN(dt.getTime()) ? null : dt;
    }

    /**
     * Current KSA clock from API (KsaTime.Now on server), with Intl fallback.
     */
    function fetchNow() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (!api || typeof api.get !== "function") {
            return Promise.resolve(nowFromIntl());
        }

        return api
            .get("/api/v1/pms/lookups/ksa-now")
            .then((res) => parseServerPayload(res) || nowFromIntl())
            .catch(() => nowFromIntl());
    }

    /** Calendar date at 00:00:00 (local Date parts, business calendar day). */
    function dateOnlyAtMidnight(date) {
        const d = date instanceof Date ? date : new Date(date);
        if (Number.isNaN(d.getTime())) {
            return null;
        }

        return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0, 0);
    }

    /**
     * Same as KsaTime.CombineDateWithCurrentTime — calendar day of dateOnly + current KSA time-of-day.
     */
    function combineDateWithCurrentTime(dateOnly, ksaNow) {
        const d = dateOnlyAtMidnight(dateOnly);
        if (!d) {
            return null;
        }

        const now = ksaNow instanceof Date && !Number.isNaN(ksaNow.getTime()) ? ksaNow : nowFromIntl();
        return new Date(
            d.getFullYear(),
            d.getMonth(),
            d.getDate(),
            now.getHours(),
            now.getMinutes(),
            now.getSeconds(),
            0
        );
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.KsaTime = {
        fetchNow,
        nowFromIntl,
        dateOnlyAtMidnight,
        combineDateWithCurrentTime
    };
})(window);
