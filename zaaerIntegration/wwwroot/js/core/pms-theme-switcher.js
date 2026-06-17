(function (window, $) {
    "use strict";

    var STORAGE_KEY = "dx-theme";
    var DEFAULT_THEME = "material.teal.light.compact";
    var THEME_BUILDER_URL = "https://devexpress.github.io/ThemeBuilder/master";

    var THEME_FAMILY = "material";

    function catalog() {
        return window.__PMS_DX_THEME_CATALOG || { material: [] };
    }

    function t(key) {
        return window.Zaaer && window.Zaaer.LocalizationService
            ? window.Zaaer.LocalizationService.t(key)
            : key;
    }

    function familyLabel() {
        return t("userMenu.themeFamily.material");
    }

    function isMaterialLightCompactThemeId(themeId) {
        return (
            !!themeId &&
            themeId.indexOf("material.") === 0 &&
            themeId.indexOf(".light.compact") !== -1
        );
    }

    function themeLabel(themeId) {
        return themeId
            .replace(/^generic\./, "Generic ")
            .replace(/^material\./, "Material ")
            .replace(/^fluent\./, "Fluent ")
            .replace(/\./g, " ")
            .replace(/\b\w/g, function (ch) { return ch.toUpperCase(); });
    }

    function currentThemeId() {
        if (window.DevExpress && DevExpress.ui && DevExpress.ui.themes) {
            try {
                var active = DevExpress.ui.themes.current();
                if (active && isMaterialLightCompactThemeId(active)) {
                    return active;
                }
            } catch {
                /* ignore */
            }
        }

        try {
            var stored = window.localStorage.getItem(STORAGE_KEY);
            if (stored && isMaterialLightCompactThemeId(stored)) {
                return stored;
            }
            return DEFAULT_THEME;
        } catch {
            return DEFAULT_THEME;
        }
    }

    function applyTheme(themeId) {
        if (!themeId || !window.DevExpress || !DevExpress.ui || !DevExpress.ui.themes) {
            return;
        }

        try {
            window.localStorage.setItem(STORAGE_KEY, themeId);
        } catch {
            /* ignore */
        }

        DevExpress.ui.themes.ready(function () {
            if (DevExpress.viz && typeof DevExpress.viz.refreshTheme === "function") {
                DevExpress.viz.refreshTheme();
            }
        });
        DevExpress.ui.themes.current(themeId);
    }

    function restoreStoredTheme() {
        applyTheme(currentThemeId());
    }

    function materialCatalogGroups() {
        return (catalog().material || [])
            .map(function (group) {
                var items = (group.items || []).filter(function (item) {
                    return isMaterialLightCompactThemeId(item.id);
                });
                if (!items.length) {
                    return null;
                }
                return { title: group.title, items: items };
            })
            .filter(Boolean);
    }

    function renderThemeList($host, activeThemeId, onPick) {
        $host.empty();
        var groups = materialCatalogGroups();

        groups.forEach(function (group) {
            var $section = $("<div class=\"pms-dx-theme-section\" />").appendTo($host);
            $("<div class=\"pms-dx-theme-section-title\" />").text(group.title).appendTo($section);

            (group.items || []).forEach(function (item) {
                var selected = item.id === activeThemeId;
                var $row = $("<button type=\"button\" class=\"pms-dx-theme-item\" />")
                    .toggleClass("pms-dx-theme-item--active", selected)
                    .attr("data-theme-id", item.id)
                    .appendTo($section);

                $("<span class=\"pms-dx-theme-item-dot\" />")
                    .css("background-color", item.color || "#3f6f9f")
                    .appendTo($row);

                $("<span class=\"pms-dx-theme-item-label\" />").text(themeLabel(item.id)).appendTo($row);

                $row.on("click", function () {
                    onPick(item.id);
                });
            });
        });

        $("<a class=\"pms-dx-theme-builder\" target=\"_blank\" rel=\"noopener noreferrer\" />")
            .attr("href", THEME_BUILDER_URL)
            .append($("<span class=\"dx-icon dx-icon-palette\" aria-hidden=\"true\" />"))
            .append($("<span />").text("ThemeBuilder"))
            .appendTo($host);
    }

    function showThemePickerPopup() {
        var activeThemeId = currentThemeId();
        var $host = $("<div />").appendTo("body");
        var $listHost;

        function closeThemePicker() {
            try {
                $host.dxPopup("instance").hide();
            } catch {
                $host.remove();
            }
        }

        $host.dxPopup({
            showTitle: false,
            visible: true,
            width: Math.min(320, Math.max(280, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "70vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.18)",
            hideOnOutsideClick: true,
            animation: {
                show: { type: "fade", duration: 180, from: 0, to: 1 },
                hide: { type: "fade", duration: 140, from: 1, to: 0 }
            },
            wrapperAttr: { class: "pms-dx-theme-panel-popup" },
            onHidden: function () {
                $host.remove();
            },
            contentTemplate: function (contentElement) {
                var $panel = $("<div class=\"pms-dx-theme-panel\" />").appendTo(contentElement);

                var $tabsHost = $("<div class=\"pms-dx-theme-tabs\" />").appendTo($panel);
                $listHost = $("<div class=\"pms-dx-theme-list\" />").appendTo($panel);

                function pickTheme(themeId) {
                    if (themeId !== currentThemeId()) {
                        applyTheme(themeId);
                    }
                    closeThemePicker();
                }

                $tabsHost.dxTabs({
                    dataSource: [{ id: THEME_FAMILY, text: familyLabel() }],
                    selectedIndex: 0,
                    width: "100%"
                });

                renderThemeList($listHost, activeThemeId, pickTheme);
            }
        });
    }

    $(function () {
        restoreStoredTheme();
    });

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsThemeSwitcher = {
        currentThemeId: currentThemeId,
        applyTheme: applyTheme,
        showThemePickerPopup: showThemePickerPopup
    };
})(window, jQuery);
