(function (window, $, DevExpress) {
    "use strict";

    const HIJRI_TZ = "Asia/Riyadh";

    function hijriPartsAtUtcMs(ts) {
        const fmt = new Intl.DateTimeFormat("en-US-u-ca-islamic-umalqura", {
            timeZone: HIJRI_TZ,
            day: "numeric",
            month: "numeric",
            year: "numeric"
        });
        const o = {};
        fmt.formatToParts(new Date(ts)).forEach((p) => {
            if (p.type !== "literal") {
                o[p.type] = Number(p.value);
            }
        });
        return { y: o.year, m: o.month, d: o.day };
    }

    function cmpHijriParts(p, y, m, d) {
        if (p.y !== y) {
            return p.y < y ? -1 : 1;
        }
        if (p.m !== m) {
            return p.m < m ? -1 : 1;
        }
        if (p.d !== d) {
            return p.d < d ? -1 : 1;
        }
        return 0;
    }

    function hijriYmdToGregorianDate(y, m, d) {
        if (!Number.isFinite(y) || !Number.isFinite(m) || !Number.isFinite(d)) {
            return null;
        }

        const epoch = Date.UTC(1925, 0, 1) + 43200000;
        const last = Math.floor((Date.UTC(2075, 11, 31) - Date.UTC(1925, 0, 1)) / 86400000);
        let lo = 0;
        let hi = last;
        let foundTs = null;

        while (lo <= hi) {
            const mid = Math.floor((lo + hi) / 2);
            const ts = epoch + mid * 86400000;
            const p = hijriPartsAtUtcMs(ts);
            const c = cmpHijriParts(p, y, m, d);
            if (c === 0) {
                foundTs = ts;
                break;
            }
            if (c < 0) {
                lo = mid + 1;
            } else {
                hi = mid - 1;
            }
        }

        return foundTs == null ? null : new Date(foundTs);
    }

    function normalizeWesternDigits(s) {
        return `${s || ""}`
            .replace(/[\u0660-\u0669]/g, (ch) => String(ch.charCodeAt(0) - 0x0660))
            .replace(/[\u06f0-\u06f9]/g, (ch) => String(ch.charCodeAt(0) - 0x06f0));
    }

    function parseFlexibleHijriToGregorian(text) {
        const s = normalizeWesternDigits(`${text || ""}`.trim()).replace(/-/g, "/");
        if (!s) {
            return null;
        }

        let m = /^(\d{4})\s*\/\s*(\d{1,2})\s*\/\s*(\d{1,2})$/.exec(s);
        if (m) {
            return hijriYmdToGregorianDate(Number(m[1]), Number(m[2]), Number(m[3]));
        }

        m = /^(\d{1,2})\s*\/\s*(\d{1,2})\s*\/\s*(\d{4})$/.exec(s);
        if (m) {
            return hijriYmdToGregorianDate(Number(m[3]), Number(m[2]), Number(m[1]));
        }

        const digits = s.replace(/\D/g, "");
        if (digits.length === 8) {
            const gy = hijriYmdToGregorianDate(
                Number(digits.slice(0, 4)),
                Number(digits.slice(4, 6)),
                Number(digits.slice(6, 8))
            );
            if (gy) {
                return gy;
            }

            return hijriYmdToGregorianDate(
                Number(digits.slice(4, 8)),
                Number(digits.slice(2, 4)),
                Number(digits.slice(0, 2))
            );
        }

        return null;
    }

    function insertSlashesYmdDigits(digitsRaw) {
        const digits = `${digitsRaw || ""}`.replace(/\D/g, "").slice(0, 8);
        if (digits.length <= 4) {
            return digits;
        }
        if (digits.length <= 6) {
            return `${digits.slice(0, 4)}/${digits.slice(4)}`;
        }
        return `${digits.slice(0, 4)}/${digits.slice(4, 6)}/${digits.slice(6, 8)}`;
    }

    function finalizeEightDigitHijri(digitsRaw) {
        const digits = `${digitsRaw || ""}`.replace(/\D/g, "").slice(0, 8);
        if (digits.length !== 8) {
            return null;
        }

        const ymd = `${digits.slice(0, 4)}/${digits.slice(4, 6)}/${digits.slice(6, 8)}`;
        if (hijriYmdToGregorianDate(Number(digits.slice(0, 4)), Number(digits.slice(4, 6)), Number(digits.slice(6, 8)))) {
            return ymd;
        }

        const dmy = `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4, 8)}`;
        if (hijriYmdToGregorianDate(Number(digits.slice(4, 8)), Number(digits.slice(2, 4)), Number(digits.slice(0, 2)))) {
            return dmy;
        }

        return ymd;
    }

    function hijriLib() {
        return window.Zaaer && window.Zaaer.PmsHijriCalendars;
    }

    function formatHijriSlashFromDate(value) {
        if (value == null || value === "") {
            return "";
        }
        const lib = hijriLib();
        if (lib && typeof lib.isReady === "function" && lib.isReady()) {
            return lib.formatSlashFromGregorian(value);
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const p = hijriPartsAtUtcMs(d.getTime());
        return `${p.y}/${String(p.m).padStart(2, "0")}/${String(p.d).padStart(2, "0")}`;
    }

    function formatHijriStorageFromDate(value) {
        if (value == null || value === "") {
            return "";
        }
        const lib = hijriLib();
        if (lib && typeof lib.isReady === "function" && lib.isReady()) {
            return lib.formatStorageFromGregorian(value);
        }
        const slash = formatHijriSlashFromDate(value);
        if (!slash) {
            return "";
        }
        const m = /^(\d{4})\/(\d{2})\/(\d{2})$/.exec(slash);
        return m ? `${m[1]}-${m[2]}-${m[3]}` : "";
    }

    function formatDualDateLabel(gregorianValue, storedHijri, isArabic) {
        const g = gregorianValue instanceof Date ? gregorianValue : new Date(gregorianValue);
        if (Number.isNaN(g.getTime())) {
            return storedHijri || "";
        }
        const greg = g.toLocaleDateString(isArabic ? "ar-SA" : "en-GB");
        let hijri = "";
        if (storedHijri && `${storedHijri}`.trim()) {
            const raw = `${storedHijri}`.trim();
            hijri = raw.indexOf("/") >= 0 ? raw : raw.replace(/-/g, "/");
        } else {
            hijri = formatHijriSlashFromDate(g);
        }
        return isArabic ? `${greg} — ${hijri} هـ` : `${greg} — ${hijri} H`;
    }

    function hijriPartsFromGregorian(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return null;
        }
        return hijriPartsAtUtcMs(d.getTime());
    }

    function currentHijriYearMonth() {
        const p = hijriPartsFromGregorian(new Date());
        return p ? { year: p.y, month: p.m } : { year: 1447, month: 1 };
    }

    /**
     * Attach Hijri event-date text editor (Um Al-Qura, 8-digit entry).
     * options: { label, t, onGregorianChange(date|null), readOnly }
     */
    function attachEventDateTextBox($host, options) {
        const opts = options || {};
        const t = typeof opts.t === "function" ? opts.t : (k) => k;
        let suppress = false;

        $host.dxTextBox({
            label: opts.label || t("hallOps.col.eventDateHijri"),
            labelMode: "floating",
            readOnly: !!opts.readOnly,
            inputAttr: { autocomplete: "off", "aria-label": opts.label || t("hallOps.col.eventDateHijri") },
            onInput(e) {
                if (suppress) {
                    return;
                }
                const raw = normalizeWesternDigits(`${e.event && e.event.target ? e.event.target.value : ""}`);
                const digits = raw.replace(/\D/g, "").slice(0, 8);
                const formatted = insertSlashesYmdDigits(digits);
                if (formatted !== raw) {
                    suppress = true;
                    e.component.option("value", formatted);
                    suppress = false;
                }
            },
            onFocusOut(e) {
                if (suppress || opts.readOnly) {
                    return;
                }
                const digits = normalizeWesternDigits(`${e.component.option("value") || ""}`).replace(/\D/g, "");
                if (!digits.length) {
                    if (typeof opts.onGregorianChange === "function") {
                        opts.onGregorianChange(null);
                    }
                    return;
                }
                if (digits.length !== 8) {
                    DevExpress.ui.notify(t("reservationDetail.guest.hijriEightDigitsToast"), "warning", 4200);
                    return;
                }
                const finalized = finalizeEightDigitHijri(digits);
                if (finalized) {
                    suppress = true;
                    e.component.option("value", finalized);
                    suppress = false;
                }
                const greg = parseFlexibleHijriToGregorian(finalized || e.component.option("value"));
                if (!greg && typeof opts.onGregorianChange === "function") {
                    DevExpress.ui.notify(t("reservationDetail.guest.hijriEightDigitsToast"), "warning", 4200);
                    return;
                }
                if (typeof opts.onGregorianChange === "function") {
                    opts.onGregorianChange(greg);
                }
            }
        });

        return {
            setFromGregorian(date) {
                const inst = $host.dxTextBox("instance");
                if (!inst) {
                    return;
                }
                suppress = true;
                inst.option("value", date ? formatHijriSlashFromDate(date) : "");
                suppress = false;
            },
            getStorageValue() {
                const inst = $host.dxTextBox("instance");
                const text = inst ? inst.option("value") : "";
                const greg = parseFlexibleHijriToGregorian(text);
                if (greg) {
                    return formatHijriStorageFromDate(greg);
                }
                return "";
            },
            getGregorianDate() {
                const inst = $host.dxTextBox("instance");
                const text = inst ? inst.option("value") : "";
                return parseFlexibleHijriToGregorian(text);
            }
        };
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsHijriDate = {
        hijriPartsFromGregorian,
        currentHijriYearMonth,
        formatHijriSlashFromDate,
        formatHijriStorageFromDate,
        formatDualDateLabel,
        parseFlexibleHijriToGregorian,
        hijriYmdToGregorianDate,
        attachEventDateTextBox
    };
})(window, window.jQuery, window.DevExpress);
