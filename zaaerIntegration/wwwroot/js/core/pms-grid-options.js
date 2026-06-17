(function (window) {
    "use strict";

    function loc() {
        return window.Zaaer && window.Zaaer.LocalizationService;
    }

    function isArabic() {
        const L = loc();
        if (L && typeof L.isArabic === "function") {
            return L.isArabic();
        }
        return document.documentElement.dir === "rtl";
    }

    function deepMerge(target, source) {
        if (!source) {
            return target;
        }

        const result = Object.assign({}, target);
        Object.keys(source).forEach((key) => {
            const sv = source[key];
            const tv = result[key];
            if (
                sv &&
                typeof sv === "object" &&
                !Array.isArray(sv) &&
                tv &&
                typeof tv === "object" &&
                !Array.isArray(tv)
            ) {
                result[key] = deepMerge(tv, sv);
            } else {
                result[key] = sv;
            }
        });
        return result;
    }

    function mergeClassAttr(baseAttr, extraAttr) {
        const base = (baseAttr && baseAttr.class) || "";
        const extra = (extraAttr && extraAttr.class) || "";
        const cls = [base, extra].filter(Boolean).join(" ").trim();
        return Object.assign({}, baseAttr || {}, extraAttr || {}, cls ? { class: cls } : {});
    }

    function scrollingOptions(extra) {
        return deepMerge(
            {
                mode: "standard",
                columnRenderingMode: "standard",
                scrollByContent: true,
                scrollByThumb: true,
                showScrollbar: "always",
                useNative: isArabic()
            },
            extra || {}
        );
    }

    function searchPanelOptions(extra) {
        return deepMerge({ visible: true, width: 260 }, extra || {});
    }

    function headerFilterOptions(extra) {
        return deepMerge({ visible: true, search: { enabled: true } }, extra || {});
    }

    function baseline(extra) {
        const base = {
            rtlEnabled: isArabic(),
            showBorders: true,
            columnAutoWidth: true,
            wordWrapEnabled: false,
            rowAlternationEnabled: true,
            hoverStateEnabled: true,
            showColumnLines: true,
            showRowLines: true,
            width: "100%",
            columnMinWidth: 64,
            elementAttr: { class: "pms-grid-compact" },
            headerFilter: headerFilterOptions(),
            searchPanel: searchPanelOptions(),
            scrolling: scrollingOptions()
        };

        if (!extra) {
            return base;
        }

        const merged = deepMerge(base, extra);
        merged.elementAttr = mergeClassAttr(base.elementAttr, extra.elementAttr);
        return merged;
    }

    function adminBaseline(extra) {
        return baseline(
            deepMerge(
                {
                    elementAttr: { class: "pms-grid-compact pms-admin-datagrid" },
                    searchPanel: searchPanelOptions({ width: 280 })
                },
                extra || {}
            )
        );
    }

    function adminPager(extra) {
        return deepMerge(
            {
                visible: true,
                displayMode: "full",
                showInfo: true,
                showNavigationButtons: true,
                showPageSizeSelector: true,
                allowedPageSizes: [20, 50, 100]
            },
            extra || {}
        );
    }

    function pickerPagingOptions(extra) {
        return deepMerge({ pageSize: 50 }, extra || {});
    }

    function pickerPagerOptions(extra) {
        return deepMerge(
            {
                visible: true,
                displayMode: "full",
                showInfo: true,
                showNavigationButtons: true,
                showPageSizeSelector: true,
                allowedPageSizes: [10, 20, 50]
            },
            extra || {}
        );
    }

    function merge(base, extra) {
        if (!extra) {
            return Object.assign({}, base);
        }

        const merged = deepMerge(base, extra);
        merged.elementAttr = mergeClassAttr(base.elementAttr, extra.elementAttr);
        return merged;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsGridOptions = {
        isArabic,
        scrollingOptions,
        searchPanelOptions,
        headerFilterOptions,
        baseline,
        adminBaseline,
        adminPager,
        pickerPagingOptions,
        pickerPagerOptions,
        merge
    };
})(window);
