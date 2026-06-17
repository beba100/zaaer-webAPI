(function (window, DevExpress) {
    "use strict";

    let dateBoxDefaultsRegistered = false;
    let dataGridDefaultsRegistered = false;

    function registerDataGridDefaults() {
        if (dataGridDefaultsRegistered || !DevExpress || !DevExpress.ui || !DevExpress.ui.dxDataGrid) {
            return;
        }

        if (typeof DevExpress.ui.dxDataGrid.defaultOptions !== "function") {
            return;
        }

        const isAr = currentCulture() === "ar";
        const devicesConfig = [{ deviceType: "desktop" }, { deviceType: "tablet" }, { deviceType: "phone" }];
        devicesConfig.forEach((device) => {
            DevExpress.ui.dxDataGrid.defaultOptions({
                device,
                options: {
                    rtlEnabled: isAr,
                    scrolling: {
                        scrollByContent: true,
                        scrollByThumb: true,
                        showScrollbar: "always",
                        useNative: isAr
                    }
                }
            });
        });

        dataGridDefaultsRegistered = true;
    }

    function registerDateBoxDefaults() {
        if (dateBoxDefaultsRegistered || !DevExpress || !DevExpress.ui || !DevExpress.ui.dxDateBox) {
            return;
        }

        if (typeof DevExpress.ui.dxDateBox.defaultOptions !== "function") {
            return;
        }

        const devicesConfig = [{ deviceType: "desktop" }, { deviceType: "tablet" }, { deviceType: "phone" }];
        devicesConfig.forEach((device) => {
            DevExpress.ui.dxDateBox.defaultOptions({
                device,
                options: {
                    openOnFieldClick: true
                }
            });
        });

        dateBoxDefaultsRegistered = true;
    }

    const storageKey = "zaaer.ui.culture";
    const supportedCultures = ["ar", "en"];

    function normalizeCulture(culture) {
        const value = (culture || "").toLowerCase();
        return supportedCultures.includes(value) ? value : "ar";
    }

    function currentCulture() {
        const queryCulture = new URLSearchParams(window.location.search).get("culture");
        return normalizeCulture(queryCulture || window.localStorage.getItem(storageKey) || "ar");
    }

    function applyCulture(culture) {
        const normalized = normalizeCulture(culture);
        const isArabic = normalized === "ar";

        window.localStorage.setItem(storageKey, normalized);
        document.documentElement.lang = isArabic ? "ar-SA" : "en-US";
        document.documentElement.dir = isArabic ? "rtl" : "ltr";

        DevExpress.localization.locale(isArabic ? "ar" : "en");
        DevExpress.config({ rtlEnabled: isArabic });

        try {
            window.dispatchEvent(
                new CustomEvent("zaaer:culture-changed", { detail: { culture: normalized } })
            );
        } catch {
            /* ignore */
        }

        return normalized;
    }

    function translate(key) {
        const culture = currentCulture();
        const dictionary = (window.ZaaerI18n && window.ZaaerI18n[culture]) || {};
        return dictionary[key] || key;
    }

    function isArabic() {
        return currentCulture() === "ar";
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.LocalizationService = {
        init() {
            registerDateBoxDefaults();
            registerDataGridDefaults();
            return applyCulture(currentCulture());
        },
        setCulture: applyCulture,
        currentCulture,
        t: translate,
        get: translate,
        isArabic
    };
})(window, DevExpress);
