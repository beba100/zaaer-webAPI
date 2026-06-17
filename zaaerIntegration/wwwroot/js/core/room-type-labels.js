(function (window) {
    "use strict";

  /**
   * Maps normalized Arabic room_type names from the DB to i18n slugs in en.js (roomTypes.*).
   */
    const SLUG_BY_NORMALIZED = {
        "غرفة (حمام)": "singleBath",
        "غرفة (حمام و مطبخ)": "singleBathKitchen",
        "غرفة (حمام و صالة)": "singleBathLounge",
        "غرفة (حمام مشترك)": "singleSharedBath",
        "غرفتين (حمام)": "doubleBath",
        "غرفتين (حمام و صالة)": "doubleBathLounge",
        "غرفتين (حمام و صالة و مطبخ)": "doubleBathLoungeKitchen",
        "غرفتين (حمامين و صالة و مطبخ)": "doubleTwoBathsLoungeKitchen",
        "ثلاث غرف (حمام و صالة)": "tripleBathLounge"
    };

    function normalizeRoomTypeName(name) {
        return String(name || "")
            .trim()
            .replace(/\|/g, " و ")
            .replace(/\(\s+/g, "(")
            .replace(/\s+\)/g, ")")
            .replace(/غرفه/g, "غرفة")
            .replace(/\s+/g, " ")
            .replace(/[\u200e\u200f]/g, "");
    }

    function isArabicUi() {
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        if (loc && typeof loc.isArabic === "function") {
            return loc.isArabic();
        }
        if (loc && typeof loc.currentCulture === "function") {
            return loc.currentCulture() === "ar";
        }
        return document.documentElement.dir === "rtl"
            && !String(document.documentElement.lang || "").toLowerCase().startsWith("en");
    }

    function translate(name, t) {
        if (!name) {
            return name;
        }
        if (isArabicUi()) {
            return name;
        }

        const translateFn = typeof t === "function"
            ? t
            : (window.Zaaer && window.Zaaer.LocalizationService && typeof window.Zaaer.LocalizationService.t === "function"
                ? window.Zaaer.LocalizationService.t.bind(window.Zaaer.LocalizationService)
                : null);

        if (!translateFn) {
            return name;
        }

        const slug = SLUG_BY_NORMALIZED[normalizeRoomTypeName(name)];
        if (!slug) {
            return name;
        }

        const key = `roomTypes.${slug}`;
        const label = translateFn(key);
        return label === key ? name : label;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.RoomTypeLabels = {
        normalize: normalizeRoomTypeName,
        display: translate
    };
})(window);
