(function (window, $) {
    "use strict";

    const catalog = window.Zaaer && window.Zaaer.PmsLodgingReportsCatalog;
    const favorites = window.Zaaer && window.Zaaer.PmsLodgingReportsFavorites;
    const loc = window.Zaaer && window.Zaaer.LocalizationService;
    const api = window.Zaaer && window.Zaaer.ApiService;
    const rbac = window.Zaaer && window.Zaaer.PmsRbacNav;
    const shell = window.Zaaer && window.Zaaer.PmsAdminShell;
    let searchRenderTimer = null;

    function t(key) {
        return loc && typeof loc.t === "function" ? loc.t(key) : key;
    }

    function canSeeReport(report, mode) {
        if (!report || !api || typeof api.hasPermission !== "function") {
            return true;
        }
        const keys = rbac && typeof rbac.resolveLodgingReportPermissionKeys === "function"
            ? rbac.resolveLodgingReportPermissionKeys(report.reportKey, mode)
            : [`hotel.reports.${report.reportKey}`, "hotel.reports"];
        if (report.reportKey === "expenses") {
            keys.push("finance.expense.view");
        }
        if (report.reportKey === "deposits") {
            keys.push("finance.deposit.view");
        }
        return keys.some((key) => api.hasPermission(key));
    }

    function buildVisibleReports(mode, query) {
        const q = `${query || ""}`.trim().toLowerCase();
        return (catalog.getAll() || [])
            .filter((report) => canSeeReport(report, mode))
            .map((report) => ({
                id: report.id,
                reportKey: report.reportKey,
                link: report.link,
                icon: report.icon,
                title: t(report.titleKey)
            }))
            .filter((report) => !q || report.title.toLowerCase().indexOf(q) >= 0);
    }

    function openReport(link, newTab) {
        if (!link) {
            return;
        }
        if (newTab) {
            window.open(link, "_blank", "noopener,noreferrer");
            return;
        }
        window.location.href = link;
    }

    function renderReportCard(report, mode, favoriteIds, $host) {
        const isFavorite = favoriteIds.has(report.id);
        const $card = $("<article/>")
            .addClass("lodging-report-card")
            .toggleClass("lodging-report-card--favorite", isFavorite)
            .appendTo($host);

        const $head = $("<div/>").addClass("lodging-report-card__head").appendTo($card);

        const $favHost = $("<div/>").addClass("lodging-report-card__fav-wrap").appendTo($head);
        $favHost.dxButton({
            stylingMode: "text",
            hint: isFavorite ? t("hotelReports.hub.action.removeFavorite") : t("hotelReports.hub.action.addFavorite"),
            elementAttr: {
                class: isFavorite
                    ? "lodging-report-card__fav lodging-report-card__fav--active"
                    : "lodging-report-card__fav",
                "aria-label": isFavorite ? t("hotelReports.hub.action.removeFavorite") : t("hotelReports.hub.action.addFavorite"),
                title: isFavorite ? t("hotelReports.hub.action.removeFavorite") : t("hotelReports.hub.action.addFavorite")
            },
            template(_data, container) {
                const $btn = $("<span/>")
                    .addClass("lodging-report-card__star")
                    .toggleClass("lodging-report-card__star--active", isFavorite)
                    .text(isFavorite ? "★" : "☆")
                    .attr("aria-hidden", "true");
                $(container).append($btn);
            },
            onClick() {
                const nowFavorite = favorites.toggle(report.id, mode);
                DevExpress.ui.notify(
                    nowFavorite ? t("hotelReports.hub.toast.addedFavorite") : t("hotelReports.hub.toast.removedFavorite"),
                    "success",
                    1800
                );
            }
        });

        $("<span/>")
            .addClass(`lodging-report-card__icon dx-icon dx-icon-${report.icon || "doc"}`)
            .appendTo($head);

        const $meta = $("<div/>").addClass("lodging-report-card__meta").appendTo($head);
        $("<h3/>").addClass("lodging-report-card__title").text(report.title).appendTo($meta);

        const $actions = $("<div/>").addClass("lodging-report-card__actions").appendTo($card);
        const $main = $("<div/>").addClass("lodging-report-card__actions-main").appendTo($actions);

        $("<div/>").appendTo($main).dxButton({
            text: t("hotelReports.hub.action.open"),
            type: "default",
            icon: "chevronnext",
            height: 28,
            onClick() {
                openReport(report.link, false);
            }
        });
        $("<div/>").appendTo($main).dxButton({
            text: t("hotelReports.hub.action.openNewTab"),
            stylingMode: "outlined",
            icon: "export",
            height: 28,
            onClick() {
                openReport(report.link, true);
            }
        });
    }

    const state = {
        mode: null,
        query: ""
    };

    function renderHub(currentState) {
        const mode = currentState.mode;
        const reports = buildVisibleReports(mode, currentState.query);
        const favoriteList = favorites.list(mode);
        const favoriteIds = new Set(favoriteList);
        const reportById = new Map(reports.map((report) => [report.id, report]));
        const favoriteReports = favoriteList
            .map((id) => reportById.get(id))
            .filter(Boolean);
        const otherReports = reports
            .filter((report) => !favoriteIds.has(report.id))
            .sort((a, b) => a.title.localeCompare(b.title, undefined, { sensitivity: "base" }));

        const $favSection = $("#hubFavoritesSection");
        const $favGrid = $("#hubFavoritesGrid");
        const $allGrid = $("#hubReportsGrid");
        const $empty = $("#hubEmpty");

        $favGrid.empty();
        $allGrid.empty();

        if (!reports.length) {
            $favSection.prop("hidden", true);
            $empty.prop("hidden", false).text(t("hotelReports.hub.empty"));
            return;
        }

        $empty.prop("hidden", true);

        if (favoriteReports.length) {
            $favSection.prop("hidden", false);
            favoriteReports.forEach((report) => renderReportCard(report, mode, favoriteIds, $favGrid));
        } else {
            $favSection.prop("hidden", true);
        }

        otherReports.forEach((report) => renderReportCard(report, mode, favoriteIds, $allGrid));
    }

    function scheduleRenderHub() {
        if (searchRenderTimer) {
            window.clearTimeout(searchRenderTimer);
        }
        searchRenderTimer = window.setTimeout(() => {
            searchRenderTimer = null;
            renderHub(state);
        }, 120);
    }

    function initSearch() {
        $("#hubSearch").dxTextBox({
            placeholder: t("hotelReports.hub.searchPlaceholder"),
            mode: "search",
            showClearButton: true,
            valueChangeEvent: "input",
            onValueChanged(e) {
                state.query = e.value || "";
                scheduleRenderHub();
            }
        });
    }

    function initStaticText(mode) {
        const groupTitle = shell && typeof shell.lodgingReportsNavGroupLabel === "function"
            ? shell.lodgingReportsNavGroupLabel(mode || {})
            : t("hotelReports.hub.title");
        $("#hubTitle").text(groupTitle);
        $("#hubSubtitle").text(t("hotelReports.hub.subtitle"));
        $("#hubFavoritesTitle").text(t("hotelReports.hub.section.favorites"));
        $("#hubAllTitle").text(t("hotelReports.hub.section.all"));
    }

    $(function () {
        if (!loc || !api || !catalog || !favorites) {
            return;
        }
        loc.init();
        if (!api.requireToken()) {
            return;
        }

        initSearch();

        const boot = () => $.when(
            shell && typeof shell.fetchPropertyMode === "function" ? shell.fetchPropertyMode() : { isHotel: true, isResort: false },
            typeof api.ensurePermissionsReady === "function" ? api.ensurePermissionsReady() : $.when()
        ).then((mode) => {
            if (!mode || (!mode.isHotel && !mode.isResort)) {
                DevExpress.ui.notify(t("hotelReports.forbidden"), "warning", 4000);
                return;
            }

            const permissionKeys = rbac && typeof rbac.lodgingReportNavPermissions === "function"
                ? rbac.lodgingReportNavPermissions(mode)
                : ["nav.menu.hotel.reports", "hotel.reports"];
            if (!permissionKeys.some((key) => api.hasPermission(key))) {
                DevExpress.ui.notify(t("hotelReports.forbidden"), "warning", 4000);
                return;
            }

            state.mode = mode;
            initStaticText(mode);
            shell.init({
                navKey: catalog.hubNavId,
                titleKey: "hotelReports.hub.title",
                propertyMode: mode,
                permissionsReady: true
            });
            renderHub(state);
        });

        boot();

        window.addEventListener("zaaer:culture-changed", () => {
            initStaticText(state.mode);
            renderHub(state);
        });
        window.addEventListener("pms:lodging-report-favorites-changed", () => {
            renderHub(state);
        });
    });
})(window, jQuery);
