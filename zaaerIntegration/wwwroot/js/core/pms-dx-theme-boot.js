(function (window) {
    "use strict";

    var STORAGE_KEY = "dx-theme";
    var LEGACY_KEY = "pms.dxTheme";
    var DEFAULT_THEME = "material.teal.light.compact";

    function cssSuffix(themeId) {
        return themeId.indexOf("generic.") === 0 ? themeId.slice("generic.".length) : themeId;
    }

    function cssBasePath() {
        var links = document.querySelectorAll('link[href*="Lib/css/dx."]');
        for (var i = 0; i < links.length; i++) {
            var href = links[i].getAttribute("href") || "";
            if (href.indexOf("dx.common") >= 0) {
                continue;
            }
            var idx = href.indexOf("Lib/css/");
            if (idx >= 0) {
                return href.substring(0, idx + "Lib/css/".length);
            }
        }
        return "/Lib/css/";
    }

    function isMaterialLightCompactThemeId(themeId) {
        return (
            !!themeId &&
            themeId.indexOf("material.") === 0 &&
            themeId.indexOf(".light.compact") !== -1
        );
    }

    function readStoredTheme() {
        var themeId = null;
        try {
            themeId = window.localStorage.getItem(STORAGE_KEY);
            if (!themeId) {
                themeId = window.localStorage.getItem(LEGACY_KEY);
                if (themeId) {
                    window.localStorage.setItem(STORAGE_KEY, themeId);
                    window.localStorage.removeItem(LEGACY_KEY);
                }
            }
        } catch {
            themeId = null;
        }
        themeId = themeId || DEFAULT_THEME;
        if (!isMaterialLightCompactThemeId(themeId)) {
            themeId = DEFAULT_THEME;
        }
        return themeId;
    }

    function flatThemeIds(catalog) {
        var ids = [];
        (catalog.material || []).forEach(function (group) {
            (group.items || []).forEach(function (item) {
                ids.push(item.id);
            });
        });
        return ids;
    }

    function ensureThemeLinks(catalog, activeThemeId) {
        var base = cssBasePath();
        var head = document.head;
        var ids = flatThemeIds(catalog);

        document.querySelectorAll('link[href*="Lib/css/dx."]').forEach(function (link) {
            var href = link.getAttribute("href") || "";
            if (href.indexOf("dx.common") >= 0) {
                return;
            }
            if (link.getAttribute("rel") !== "dx-theme") {
                link.parentNode.removeChild(link);
            }
        });

        ids.forEach(function (id) {
            var selector = 'link[rel="dx-theme"][data-theme="' + id + '"]';
            var link = document.querySelector(selector);
            if (!link) {
                link = document.createElement("link");
                link.rel = "dx-theme";
                link.setAttribute("data-theme", id);
                link.href = base + "dx." + cssSuffix(id) + ".css";
                head.appendChild(link);
            }
            link.setAttribute("data-active", id === activeThemeId ? "true" : "false");
        });
    }

    window.__PMS_DX_THEME_CATALOG = {
        material: [
            {
                title: "Material Light Compact",
                items: [
                    { id: "material.blue.light.compact", color: "#2196f3" },
                    { id: "material.lime.light.compact", color: "#cddc39" },
                    { id: "material.orange.light.compact", color: "#ff9800" },
                    { id: "material.purple.light.compact", color: "#9c27b0" },
                    { id: "material.teal.light.compact", color: "#009688" }
                ]
            }
        ]
    };

    window.__pmsDxThemeBoot = ensureThemeLinks(window.__PMS_DX_THEME_CATALOG, readStoredTheme());
})(window);
