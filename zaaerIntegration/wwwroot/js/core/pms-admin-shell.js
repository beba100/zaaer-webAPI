(function (window, $) {
    "use strict";

    const NAV_COLLAPSED_KEY = "zaaer.pmsAdmin.sidebarCollapsed";
    const NAV_EXPANDED_KEYS_KEY = "zaaer.pmsAdmin.navExpandedKeys";
    const ROOM_BOARD_URL = "/room-board.html";
    const RESORT_CASHIER_URL = "/resort-tickets.html";

    let backNavUrl = ROOM_BOARD_URL;
    let backNavLabel = "";

    function t(key) {
        return window.Zaaer.LocalizationService.t(key);
    }

    function isResortTicketFinanceNavKey(key) {
        return (
            key === "nav-resort-ticket-finance-group" ||
            key === "nav-resort-ticket-finance" ||
            key === "nav-resort-ticket-receipts" ||
            key === "nav-resort-ticket-invoices"
        );
    }

    function isResortTicketNavKey(key) {
        if (!key) {
            return false;
        }

        if (key === "nav-resort-tickets-group" || key === "nav-resort-ticket-finance-group") {
            return true;
        }

        if (key === "nav-resort-tickets") {
            return true;
        }

        return String(key).indexOf("nav-resort-ticket-") === 0;
    }

    let navTreeAccordionLock = false;
    let lastNavTreeKey = "nav-rbac-users";
    let lastNavTreeMode = { isResort: false, isHall: false, isHotel: true };
    let propertyModeCacheKey = null;
    let propertyModeCachePromise = null;

    function readPersistedNavExpandedKeys() {
        try {
            const raw = window.sessionStorage.getItem(NAV_EXPANDED_KEYS_KEY);
            if (!raw) {
                return [];
            }
            const parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed.filter(Boolean) : [];
        } catch (_err) {
            return [];
        }
    }

    function persistNavExpandedKeys(keys) {
        if (!Array.isArray(keys) || !keys.length) {
            return;
        }
        try {
            window.sessionStorage.setItem(NAV_EXPANDED_KEYS_KEY, JSON.stringify(keys));
        } catch (_err) {
            /* storage may be unavailable */
        }
    }

    function mergeNavExpandedKeys() {
        const merged = [];
        for (let i = 0; i < arguments.length; i += 1) {
            const list = arguments[i];
            if (!Array.isArray(list)) {
                continue;
            }
            list.forEach((key) => {
                if (key && merged.indexOf(key) === -1) {
                    merged.push(key);
                }
            });
        }
        return merged;
    }

    function collectNavAncestorKeys(items, targetKey, trail) {
        const path = trail || [];
        if (!Array.isArray(items) || !targetKey) {
            return null;
        }

        for (let i = 0; i < items.length; i += 1) {
            const item = items[i];
            const itemId = item && item.id;
            if (!itemId) {
                continue;
            }

            const nextTrail = path.concat(itemId);
            if (itemId === targetKey) {
                return path;
            }

            if (Array.isArray(item.items) && item.items.length) {
                const nested = collectNavAncestorKeys(item.items, targetKey, nextTrail);
                if (nested) {
                    return nested;
                }
            }
        }

        return null;
    }

    function resolveExpandedNavKeys(selectedKey, items) {
        const keys = ["nav-root"];
        const ancestors = collectNavAncestorKeys(items, selectedKey);
        if (Array.isArray(ancestors)) {
            ancestors.forEach((key) => {
                if (key && keys.indexOf(key) === -1) {
                    keys.push(key);
                }
            });
        }
        return keys;
    }

    function applyNavTreeExpandedKeys(instance, keys) {
        if (!instance || !Array.isArray(keys) || !keys.length) {
            return;
        }

        navTreeAccordionLock = true;
        instance.option("expandedItemKeys", keys.slice());
        window.setTimeout(() => {
            keys.forEach((key) => {
                try {
                    instance.expandItem(key);
                } catch (_err) {
                    /* node may not exist yet */
                }
            });
            window.setTimeout(() => {
                navTreeAccordionLock = false;
            }, 220);
        }, 0);
    }

    function collapseNavTreeSiblings(component, node) {
        if (!component || !node || !node.parent) {
            return;
        }

        const siblings = node.parent.children || [];
        siblings.forEach((sibling) => {
            if (!sibling || sibling.key === node.key || !sibling.expanded) {
                return;
            }

            try {
                component.collapseItem(sibling.key);
            } catch (_err) {
                /* sibling may not be collapsible */
            }
        });
    }

    function createNavTreeAccordionHandler() {
        return function onNavTreeItemExpanded(e) {
            if (navTreeAccordionLock || !e || !e.component || !e.node) {
                return;
            }

            navTreeAccordionLock = true;
            try {
                collapseNavTreeSiblings(e.component, e.node);
            } finally {
                window.setTimeout(() => {
                    navTreeAccordionLock = false;
                }, 180);
            }
        };
    }

    function handleNavTreeItemNavigate(e, items, selectedKey) {
        const itemData = e && e.itemData;
        const itemId = itemData && itemData.id;
        const link = itemData && itemData.link;
        if (!link || link === "#") {
            return false;
        }

        if (String(window.location.pathname).toLowerCase().endsWith(link.toLowerCase())) {
            return true;
        }

        const expandedKeys = resolveExpandedNavKeys(itemId || selectedKey, items);
        persistNavExpandedKeys(expandedKeys);
        window.location.href = link;
        return true;
    }

    function isResortTicketSubPage() {
        return /\/resort-ticket-/i.test(String(window.location.pathname || ""));
    }

    function isResortTicketSubNavKey(key) {
        return (
            !!key &&
            key !== "nav-resort-tickets" &&
            key !== "nav-resort-tickets-group" &&
            isResortTicketNavKey(key)
        );
    }

    function shouldShowResortCashierBack(_isResort, selectedKey) {
        if (isResortCashierPage()) {
            return false;
        }

        return isResortTicketSubPage() || isResortTicketSubNavKey(selectedKey);
    }

    function resolveResortNavMode(isResortFromApi, selectedKey) {
        if (isResortFromApi) {
            return true;
        }

        if (isResortTicketSubPage() || isResortTicketNavKey(selectedKey)) {
            return true;
        }

        return false;
    }

    function readLookupIsResort(data) {
        return !!(data && (data.isResort || data.IsResort));
    }

    function readPropertyMode(data) {
        const isResort = readLookupIsResort(data);
        const isHall = !!(data && (data.isHall || data.IsHall));
        const isHotel = data
            ? !!(data.isHotel || data.IsHotel || (!isResort && !isHall))
            : !isResort && !isHall;
        return {
            propertyType: (data && (data.propertyType || data.PropertyType)) || (isHall ? "hall" : isResort ? "resort" : "hotel"),
            isResort,
            isHall,
            isHotel
        };
    }

    function filterPropertyNavItems(items, mode) {
        const isResort = !!(mode && mode.isResort);
        const isHall = !!(mode && mode.isHall);
        const isHotel = !!(mode && mode.isHotel);
        if (!Array.isArray(items)) {
            return [];
        }

        return items
            .map((item) => {
                const next = { ...item };
                if (Array.isArray(next.items)) {
                    next.items = filterPropertyNavItems(next.items, mode);
                }
                return next;
            })
            .filter((item) => {
                if (item.resortOnly && !isResort) {
                    return false;
                }
                if (item.hallOnly && !isHall) {
                    return false;
                }
                if (item.lodgingOnly && !(isHotel || isResort)) {
                    return false;
                }
                if (item.hotelOnly && !(isHotel || isResort)) {
                    return false;
                }
                if (Array.isArray(item.items)) {
                    if (item.items.length > 0) {
                        return true;
                    }
                    return !!(item.link && item.link !== "#");
                }
                return true;
            });
    }

    function filterResortNavItems(items, isResort) {
        if (!Array.isArray(items)) {
            return [];
        }

        return items
            .map((item) => {
                const next = { ...item };
                if (Array.isArray(next.items)) {
                    next.items = filterResortNavItems(next.items, isResort);
                }
                return next;
            })
            .filter((item) => {
                if (item.resortOnly && !isResort) {
                    return false;
                }

                if (Array.isArray(item.items)) {
                    if (item.items.length > 0) {
                        return true;
                    }

                    return !!(item.link && item.link !== "#");
                }

                return true;
            });
    }

    function boardHomeNavLabel(modeOrResort) {
        const mode = typeof modeOrResort === "object"
            ? modeOrResort
            : { isResort: !!modeOrResort, isHall: false };
        if (mode.isHall) {
            return t("property.venue.boardTitle");
        }
        return mode.isResort ? t("roomBoard.resortTitle") : t("roomBoard.title");
    }

    function propertyNavLabel(mode, key) {
        if (mode && mode.isHall) {
            return t(`property.venue.nav.${key}`);
        }
        return t(`property.nav.${key}`);
    }

    function createHallOperationsNavGroup(isHall) {
        return {
            id: "nav-hall-events-group",
            text: t("hallOps.nav.group"),
            icon: "event",
            expanded: false,
            hallOnly: true,
            items: [
                {
                    id: "nav-hall-operations",
                    text: t("hallOps.nav.operations"),
                    icon: "event",
                    link: "/hall-operations.html"
                },
                {
                    id: "nav-hall-reports-group",
                    text: t("hallOps.nav.reports"),
                    icon: "chart",
                    expanded: false,
                    items: [
                        {
                            id: "nav-hall-report-daily-journal",
                            text: t("hallReports.nav.dailyJournal"),
                            icon: "doc",
                            link: "/hall-report-daily-journal.html"
                        },
                        {
                            id: "nav-hall-report-cash-ledger",
                            text: t("hallReports.nav.cashLedger"),
                            icon: "chart",
                            link: "/hall-report-cash-ledger.html"
                        },
                        {
                            id: "nav-hall-report-network-cash",
                            text: t("hallReports.nav.networkCashPayments"),
                            icon: "money",
                            link: "/hall-report-network-cash.html"
                        },
                        {
                            id: "nav-hall-report-bookings",
                            text: t("hallReports.nav.bookings"),
                            icon: "event",
                            link: "/hall-report-bookings.html"
                        },
                        {
                            id: "nav-hall-report-receipts",
                            text: t("hallReports.nav.receipts"),
                            icon: "money",
                            link: "/hall-report-receipts.html"
                        },
                        {
                            id: "nav-hall-report-disbursements",
                            text: t("hallReports.nav.disbursements"),
                            icon: "undo",
                            link: "/hall-report-disbursements.html"
                        },
                        {
                            id: "nav-hall-report-deposits",
                            text: t("hallReports.nav.deposits"),
                            icon: "box",
                            link: "/hall-report-deposits.html"
                        },
                        {
                            id: "nav-hall-report-expenses",
                            text: t("hallReports.nav.expenses"),
                            icon: "money",
                            link: "/hall-report-expenses.html"
                        },
                        {
                            id: "nav-hall-report-invoices",
                            text: t("hallReports.nav.invoices"),
                            icon: "doc",
                            link: "/hall-report-invoices.html"
                        },
                        {
                            id: "nav-hall-report-credit-notes",
                            text: t("hallReports.nav.creditNotes"),
                            icon: "clearformat",
                            link: "/hall-report-credit-notes.html"
                        }
                    ]
                }
            ]
        };
    }

    function lodgingReportsNavGroupLabel(mode) {
        if (mode && mode.isResort) {
            return t("hotelReports.nav.groupResort");
        }
        if (mode && mode.isHotel) {
            return t("hotelReports.nav.groupHotel");
        }
        return t("hotelReports.nav.group");
    }

    function createHotelReportsNavGroup(modeOrLodging) {
        const mode = typeof modeOrLodging === "object"
            ? modeOrLodging
            : { isHotel: !!modeOrLodging, isResort: false, isHall: false };
        const catalog = window.Zaaer && window.Zaaer.PmsLodgingReportsCatalog;
        const hubLink = (catalog && catalog.hubLink) || "/hotel-reports-hub.html";

        return {
            id: "nav-hotel-reports-group",
            text: lodgingReportsNavGroupLabel(mode),
            icon: "chart",
            link: hubLink,
            lodgingOnly: true
        };
    }

    function createResortTicketsNavGroup(isResort) {
        return {
            id: "nav-resort-tickets-group",
            text: t("resortTickets.nav.title"),
            icon: "ticket",
            expanded: false,
            resortOnly: true,
            items: [
                {
                    id: "nav-resort-tickets",
                    text: t("resortTickets.nav.cashier"),
                    icon: "cart",
                    link: "/resort-tickets.html"
                },
                {
                    id: "nav-resort-ticket-scanner",
                    text: t("resortTickets.nav.scanner"),
                    icon: "check",
                    link: "/resort-ticket-scanner.html"
                },
                {
                    id: "nav-resort-ticket-gate",
                    text: t("resortTickets.nav.gate"),
                    icon: "fullscreen",
                    link: "/resort-ticket-gate.html"
                },
                {
                    id: "nav-resort-ticket-settings",
                    text: t("resortTickets.nav.settings"),
                    icon: "preferences",
                    link: "/resort-ticket-settings.html"
                },
                {
                    id: "nav-resort-ticket-finance-group",
                    text: t("resortTickets.nav.financeGroup"),
                    icon: "money",
                    expanded: false,
                    items: [
                        {
                            id: "nav-resort-ticket-finance",
                            text: t("resortTickets.nav.finance"),
                            icon: "money",
                            link: "/resort-ticket-finance.html"
                        },
                        {
                            id: "nav-resort-ticket-receipts",
                            text: t("resortTickets.nav.receipts"),
                            icon: "doc",
                            link: "/resort-ticket-receipts.html"
                        },
                        {
                            id: "nav-resort-ticket-invoices",
                            text: t("resortTickets.nav.invoices"),
                            icon: "doc",
                            link: "/resort-ticket-invoices.html"
                        }
                    ]
                }
            ]
        };
    }

    function fetchPropertyMode() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        const prop = window.Zaaer && window.Zaaer.PropertySettingsService;
        const tickets = window.Zaaer && window.Zaaer.ResortTicketService;
        const hall = window.Zaaer && window.Zaaer.HallEventsService;

        const defaultMode = { propertyType: "hotel", isResort: false, isHall: false, isHotel: true };

        const hotelCode = api && typeof api.getHotelCode === "function" ? api.getHotelCode() : "";
        if (!api || !hotelCode) {
            return $.Deferred().resolve(defaultMode).promise();
        }

        if (propertyModeCacheKey === hotelCode && propertyModeCachePromise) {
            return propertyModeCachePromise;
        }

        function fromHallLookups() {
            if (!hall || typeof hall.getLookups !== "function") {
                return $.Deferred().resolve(defaultMode).promise();
            }
            return hall.getLookups()
                .then((data) => readPropertyMode({ isHall: true, isResort: false, isHotel: false, propertyType: "hall", ...(data || {}) }))
                .catch(() => defaultMode);
        }

        function fromTicketLookups() {
            if (!tickets || typeof tickets.getLookups !== "function") {
                return fromHallLookups();
            }
            return tickets.getLookups()
                .then((data) => readPropertyMode(data))
                .catch(() => fromHallLookups());
        }

        if (prop && typeof prop.getMode === "function") {
            propertyModeCacheKey = hotelCode;
            propertyModeCachePromise = prop.getMode().then(readPropertyMode).catch(() => fromTicketLookups());
            return propertyModeCachePromise;
        }

        if (prop && typeof prop.getLookups === "function") {
            propertyModeCacheKey = hotelCode;
            propertyModeCachePromise = prop.getLookups().then(readPropertyMode).catch(() => fromTicketLookups());
            return propertyModeCachePromise;
        }

        propertyModeCacheKey = hotelCode;
        propertyModeCachePromise = fromTicketLookups();
        return propertyModeCachePromise;
    }

    function fetchIsResortProperty() {
        return fetchPropertyMode().then((mode) => !!mode.isResort);
    }

    function buildNavItems(modeInput) {
        const mode = typeof modeInput === "object"
            ? modeInput
            : { isResort: !!modeInput, isHall: false, isHotel: !modeInput };
        const isResort = !!mode.isResort;
        const isHall = !!mode.isHall;
        const isHotel = !!mode.isHotel;
        const items = [{
            id: "nav-root",
            text: t("app.title"),
            expanded: false,
            items: [
                {
                    id: "nav-board",
                    text: boardHomeNavLabel(mode),
                    icon: "home",
                    link: isHall ? "/hall-operations.html" : "/room-board.html"
                },
                {
                    id: "nav-property",
                    text: propertyNavLabel(mode, "group"),
                    icon: "box",
                    expanded: false,
                    items: [
                        {
                            id: "nav-property-settings",
                            text: propertyNavLabel(mode, "settings"),
                            icon: "preferences",
                            link: "/unit-settings.html"
                        },
                        {
                            id: "nav-property-rates",
                            text: propertyNavLabel(mode, "rates"),
                            icon: "money",
                            link: "/unit-rates.html"
                        }
                    ]
                },
                {
                    id: "nav-booking-engine",
                    text: t("bookingEngine.nav.title"),
                    icon: "globe",
                    link: "/booking-engine-settings.html",
                    expanded: false,
                    items: [
                        {
                            id: "nav-booking-engine-settings",
                            text: t("bookingEngine.nav.settings"),
                            icon: "preferences",
                            link: "/booking-engine-settings.html"
                        },
                        {
                            id: "nav-booking-engine-open",
                            text: t("bookingEngine.nav.openPage"),
                            icon: "link",
                            action: "open-booking-page"
                        }
                    ]
                },
                {
                    id: "nav-pos",
                    text: t("pos.nav.title"),
                    icon: "cart",
                    expanded: false,
                    items: [
                        { id: "nav-pos-terminal", text: t("pos.nav.terminal"), icon: "product", link: "/pos.html" },
                        { id: "nav-pos-orders", text: t("pos.nav.orders"), icon: "orderedlist", link: "/pos-orders.html" },
                        { id: "nav-pos-settings", text: t("pos.nav.settings"), icon: "preferences", link: "/pos-settings.html" }
                    ]
                },
                createResortTicketsNavGroup(!!isResort),
                createHallOperationsNavGroup(!!isHall),
                createHotelReportsNavGroup(mode),
                {
                    id: "nav-finance-cash",
                    text: t("financeCash.nav.title"),
                    icon: "money",
                    expanded: false,
                    items: [
                        { id: "nav-finance-expenses", text: t("financeCash.nav.expenses"), icon: "money", link: "/expenses.html" },
                        { id: "nav-finance-deposits", text: t("financeCash.nav.deposits"), icon: "box", link: "/deposits.html" }
                    ]
                },
                {
                    id: "nav-integrations",
                    text: t("integrations.nav.title"),
                    icon: "globe",
                    expanded: false,
                    items: [
                        {
                            id: "nav-integrations-ntmp",
                            text: t("integrations.nav.ntmp"),
                            icon: "image",
                            navIconSrc: "/logo/ntmp.svg",
                            link: "/integration-ntmp-settings.html"
                        },
                        {
                            id: "nav-integrations-shomoos",
                            text: t("integrations.nav.shomoos"),
                            icon: "image",
                            navIconSrc: "/logo/shomoos.svg",
                            link: "/integration-shomoos-settings.html"
                        },
                        {
                            id: "nav-integrations-zatca",
                            text: t("integrations.nav.zatca"),
                            icon: "image",
                            navIconSrc: "/logo/zatca.svg",
                            link: "/integration-zatca-settings.html"
                        },
                        {
                            id: "nav-integrations-balady",
                            text: t("integrations.nav.balady"),
                            icon: "image",
                            navIconSrc: "/logo/balady_logo.svg",
                            link: "/integration-balady-report.html"
                        },
                        { id: "nav-integrations-responses", text: t("integrations.nav.responses"), icon: "orderedlist", link: "/integration-responses.html" }
                    ]
                },
                {
                    id: "nav-system",
                    text: t("rbac.nav.systemSettings"),
                    icon: "preferences",
                    expanded: false,
                    items: [
                        { id: "nav-rbac-users", text: t("rbac.nav.users"), icon: "user", link: "/users.html" },
                        { id: "nav-rbac-roles", text: t("rbac.nav.roles"), icon: "group", link: "/roles.html" },
                        { id: "nav-rbac-permissions", text: t("rbac.nav.permissions"), icon: "key", link: "/permissions.html" },
                        { id: "nav-numbering-admin", text: t("numberingAdmin.navTitle"), icon: "preferences", link: "/numbering-admin.html" }
                    ]
                }
            ]
        }];

        const propertyFiltered = filterPropertyNavItems(items, mode);

        if (window.Zaaer.PmsRbacNav && window.Zaaer.PmsRbacNav.filterNavItems) {
            return window.Zaaer.PmsRbacNav.filterNavItems(propertyFiltered, mode);
        }

        return propertyFiltered;
    }

    function initSidebarToggle() {
        const $shell = $(".room-board-shell");
        const $btn = $("#roomBoardNavToggle");
        if (!$shell.length || !$btn.length) {
            return;
        }

        const collapsed = window.localStorage.getItem(NAV_COLLAPSED_KEY) === "1";
        if (collapsed) {
            $shell.addClass("room-board-shell--nav-collapsed");
            $btn.attr("aria-expanded", "false");
        }

        $btn.on("click", () => {
            const isCollapsed = $shell.toggleClass("room-board-shell--nav-collapsed")
                .hasClass("room-board-shell--nav-collapsed");
            window.localStorage.setItem(NAV_COLLAPSED_KEY, isCollapsed ? "1" : "0");
            $btn.attr("aria-expanded", isCollapsed ? "false" : "true");
        });
    }

    function ensureBreadcrumbHotelPickerMarkup() {
        $(".room-board-breadcrumb").each(function () {
            const $bc = $(this);
            let $host = $bc.find("#pmsHeaderHotelPicker").first();
            const $navToggle = $bc.find("#roomBoardNavToggle, .room-board-nav-toggle").first();
            let $sep = $bc.find(".room-board-bc-sep:not([hidden])").first();

            if (!$sep.length) {
                $sep = $("<span/>", {
                    class: "dx-icon dx-icon-chevronright room-board-bc-sep",
                    "aria-hidden": "true"
                });
                if ($navToggle.length) {
                    $sep.insertAfter($navToggle);
                } else {
                    $bc.prepend($sep);
                }
            }

            if (!$host.length) {
                $host = $("<div/>", {
                    id: "pmsHeaderHotelPicker",
                    class: "pms-header-hotel-picker pms-header-hotel-picker--breadcrumb"
                });
            }

            $host
                .addClass("pms-header-hotel-picker--breadcrumb")
                .removeClass("pms-header-hotel-picker--hidden");

            if (!$host.parent().is($bc) || !$host.prev().is($sep)) {
                $host.insertAfter($sep);
            }
        });
    }

    function refreshResortNav(applyNav, selectedKey) {
        return fetchIsResortProperty().then((isResortFromApi) => {
            applyNav(resolveResortNavMode(!!isResortFromApi, selectedKey));
        });
    }

    function initTopChrome(options, onReady) {
        const chrome = window.Zaaer.PmsTopChrome;
        let pickerPromise = $.Deferred().resolve().promise();
        if (chrome && chrome.initHeaderHotelPicker) {
            pickerPromise = $.when(
                chrome.initHeaderHotelPicker({
                    onHotelChanged: (options && options.onHotelChanged) || (() => window.location.reload())
                })
            );
        }
        if (chrome && chrome.initUserAccountMenu) {
            chrome.initUserAccountMenu("#userAccountMenu");
        }
        if (typeof onReady === "function") {
            pickerPromise.always(onReady);
        }
    }

    function resetNavTreeSearch() {
        const $tree = $("#roomBoardNavTree");
        if (!$tree.length || !$tree.data("dxTreeView")) {
            return;
        }

        const instance = $tree.dxTreeView("instance");
        instance.option("searchValue", "");
    }

    function applyNavTreeItemIcon(e) {
        const src = e.node && e.node.itemData && e.node.itemData.navIconSrc;
        if (!src) {
            return;
        }
        const $icon = $(e.itemElement).find(".dx-icon").first();
        if ($icon.length && !$icon.hasClass("pms-nav-tree-icon-wrap")) {
            $icon
                .empty()
                .addClass("pms-nav-tree-icon-wrap")
                .append(
                    $("<img/>", {
                        src: src,
                        alt: "",
                        class: "pms-nav-tree-icon",
                        width: 16,
                        height: 16
                    })
                );
        }
    }

    function openBookingEnginePage() {
        const api = window.Zaaer && window.Zaaer.ApiService;
        const hotelCode = api && typeof api.getHotelCode === "function" ? api.getHotelCode() : "";
        const url = hotelCode
            ? `/booking-engine.html?hotel=${encodeURIComponent(hotelCode)}`
            : "/booking-engine.html";
        window.open(url, "_blank", "noopener,noreferrer");
    }

    function createNavTreeItemTemplate() {
        return function itemTemplate(itemData, _itemIndex, element) {
            const $content = $("<div/>").addClass("dx-item-content dx-treeview-item-content").appendTo(element);
            if (itemData && itemData.icon) {
                const $icon = $("<span/>").addClass(`dx-icon dx-icon-${itemData.icon}`).appendTo($content);
                if (itemData.navIconSrc) {
                    $icon
                        .empty()
                        .addClass("pms-nav-tree-icon-wrap")
                        .append(
                            $("<img/>", {
                                src: itemData.navIconSrc,
                                alt: "",
                                class: "pms-nav-tree-icon",
                                width: 16,
                                height: 16
                            })
                        );
                }
            }
            $("<span/>")
                .addClass("dx-treeview-item-text")
                .text((itemData && itemData.text) || "")
                .appendTo($content);
        };
    }

    function initNavTree(selectedKey, modeInput) {
        const mode = typeof modeInput === "object"
            ? modeInput
            : { isResort: !!modeInput, isHall: false, isHotel: !modeInput };
        lastNavTreeKey = selectedKey || lastNavTreeKey;
        lastNavTreeMode = mode;
        const $tree = $("#roomBoardNavTree");
        const items = buildNavItems(mode);
        const activeNavKey = selectedKey || "nav-rbac-users";
        const expandedKeys = resolveExpandedNavKeys(activeNavKey, items);
        persistNavExpandedKeys(expandedKeys);
        const selectedKeys = [activeNavKey];
        const navAccordionHandler = createNavTreeAccordionHandler();

        if ($tree.data("dxTreeView")) {
            const instance = $tree.dxTreeView("instance");
            instance.option("items", items);
            instance.option("selectedItemKeys", selectedKeys);
            applyNavTreeExpandedKeys(instance, expandedKeys);
            return;
        }

        $tree.dxTreeView({
            items: items,
            keyExpr: "id",
            width: "100%",
            animationEnabled: true,
            focusStateEnabled: false,
            selectNodesRecursive: false,
            selectionMode: "single",
            selectByClick: true,
            searchEnabled: true,
            itemTemplate: createNavTreeItemTemplate(),
            searchEditorOptions: {
                placeholder: t("roomBoard.navSearchPlaceholder"),
                mode: "text",
                stylingMode: "filled"
            },
            expandedItemKeys: expandedKeys,
            selectedItemKeys: selectedKeys,
            onItemRendered: applyNavTreeItemIcon,
            onItemClick(e) {
                const nav = window.Zaaer.PmsRbacNav;
                if (nav && nav.handleNavItemClick) {
                    if (nav.handleNavItemClick(e, () => window.Zaaer.ApiService.getHotelCode())) {
                        return;
                    }
                }

                if (handleNavTreeItemNavigate(e, items, activeNavKey)) {
                    return;
                }
            },
            onItemExpanded: navAccordionHandler,
            onContentReady(e) {
                applyNavTreeExpandedKeys(e.component, expandedKeys);
            }
        });
    }

    function navigateBack() {
        window.location.href = backNavUrl;
    }

    function isResortCashierPage() {
        return /\/resort-tickets\.html$/i.test(String(window.location.pathname || ""));
    }

    function syncResortTicketBackChrome(showResortCashierBack) {
        const $bc = $(".room-board-breadcrumb");
        if (!$bc.length) {
            return;
        }

        const $genericBack = $bc.find(".room-board-back-link:not(.room-board-resort-cashier-back)");
        let $resortBack = $bc.find(".room-board-resort-cashier-back");

        if (!showResortCashierBack) {
            $resortBack.remove();
            $genericBack.show();
            return;
        }

        const label = t("resortTickets.backToCashier");
        if (!$resortBack.length) {
            $resortBack = $("<a/>", {
                href: RESORT_CASHIER_URL,
                class: "room-board-back-link room-board-resort-cashier-back"
            });
            $resortBack.append(
                $("<span/>", { class: "dx-icon dx-icon-arrowleft", "aria-hidden": "true" }),
                $("<span/>", { class: "room-board-resort-cashier-back__label" }).text(label)
            );
            $bc.prepend($resortBack);
        }

        $resortBack.attr({
            href: RESORT_CASHIER_URL,
            title: label,
            "aria-label": label
        });
        $resortBack.find(".room-board-resort-cashier-back__label").text(label);
        $genericBack.hide();
    }

    function setBackNavigation(isResort, selectedKey) {
        const showResortCashierBack = shouldShowResortCashierBack(isResort, selectedKey);

        if (showResortCashierBack) {
            backNavUrl = RESORT_CASHIER_URL;
            backNavLabel = t("resortTickets.backToCashier");
        } else {
            backNavUrl = ROOM_BOARD_URL;
            backNavLabel = t("integrations.common.backToRoomBoard");
        }

        syncResortTicketBackChrome(showResortCashierBack);

        $(".room-board-back-link:not(.room-board-resort-cashier-back)")
            .attr({
                href: backNavUrl,
                title: backNavLabel,
                "aria-label": backNavLabel
            })
            .toggle(!showResortCashierBack);

        $(".room-board-brand, .room-board-hotel-card, #adminHotelBadge").attr("title", backNavLabel);
    }

    function initRoomBoardNavigation() {
        const label = t("integrations.common.backToRoomBoard");
        backNavUrl = ROOM_BOARD_URL;
        backNavLabel = label;

        $(".room-board-brand, .room-board-hotel-card")
            .addClass("room-board-nav-home-link")
            .attr({ role: "link", tabindex: "0", title: label })
            .off("click.pmsBackNav keydown.pmsBackNav")
            .on("click.pmsBackNav", navigateBack)
            .on("keydown.pmsBackNav", (ev) => {
                if (ev.key === "Enter" || ev.key === " ") {
                    ev.preventDefault();
                    navigateBack();
                }
            });

        const $breadcrumb = $(".room-board-breadcrumb");
        if ($breadcrumb.length && !$breadcrumb.find(".room-board-back-link").length) {
            $breadcrumb.prepend(
                $("<a/>", {
                    href: ROOM_BOARD_URL,
                    class: "room-board-back-link",
                    title: label,
                    "aria-label": label
                }).append($("<span/>", { class: "dx-icon dx-icon-arrowleft", "aria-hidden": "true" }))
            );
        }

        const $badge = $("#adminHotelBadge");
        if ($badge.length) {
            $badge
                .addClass("pms-admin-hotel-badge--link")
                .attr({ role: "link", tabindex: "0", title: label })
                .off("click.pmsBackNav keydown.pmsBackNav")
                .on("click.pmsBackNav", navigateBack)
                .on("keydown.pmsBackNav", (ev) => {
                    if (ev.key === "Enter" || ev.key === " ") {
                        ev.preventDefault();
                        navigateBack();
                    }
                });
        }
    }

    function initHeader(options) {
        const titleKey = options.titleKey || "rbac.adminTitle";
        const subtitleKey = options.subtitleKey || "";
        $("#pageTitle").text(t(titleKey));
        const $subtitle = $("#pageSubtitle");
        const subtitleText = subtitleKey ? t(subtitleKey) : "";
        if ($subtitle.length) {
            if (subtitleText) {
                $subtitle.text(subtitleText).show();
            } else {
                $subtitle.text("").hide();
            }
        }

        const $badge = $("#adminHotelBadge");
        if ($(".room-board-breadcrumb").length && $badge.length) {
            $badge.prop("hidden", true);
        }

        $("#businessDateText").text(new Intl.DateTimeFormat("en-GB").format(new Date()));
    }

    function initRefreshButton(onRefresh) {
        $("#adminRefreshButton").dxButton({
            icon: "refresh",
            text: t("common.refresh"),
            stylingMode: "contained",
            type: "default",
            onClick() {
                if (typeof onRefresh === "function") {
                    onRefresh();
                } else if (window.__rbacGrid) {
                    window.__rbacGrid.refresh();
                }
            }
        });
    }

    function init(options) {
        window.Zaaer = window.Zaaer || {};
        const body = document.body;
        const selectedKey =
            (options && (options.navKey || options.selectedNavKey)) ||
            body.getAttribute("data-rbac-nav-key") ||
            "nav-rbac-users";
        const titleKey = (options && options.titleKey) || body.getAttribute("data-rbac-title-key") || "rbac.adminTitle";
        const subtitleKey = (options && options.subtitleKey) || body.getAttribute("data-rbac-subtitle-key") || "";

        window.Zaaer.LocalizationService.init();
        ensureBreadcrumbHotelPickerMarkup();
        if (window.Zaaer.PmsTopChrome && typeof window.Zaaer.PmsTopChrome.ensureHeaderHotelHost === "function") {
            window.Zaaer.PmsTopChrome.ensureHeaderHotelHost();
        }
        initHeader({ titleKey, subtitleKey });
        initSidebarToggle();
        initRefreshButton(options && options.onRefresh);

        const applyNav = (mode) => {
            const resolved = typeof mode === "object"
                ? mode
                : { isResort: !!mode, isHall: false, isHotel: !mode };
            initNavTree(selectedKey, resolved);
            setBackNavigation(!!resolved.isResort, selectedKey);
        };

        initRoomBoardNavigation();

        const api = window.Zaaer && window.Zaaer.ApiService;
        const bootNav = () =>
            $.when(
                options && options.propertyMode ? options.propertyMode : fetchPropertyMode(),
                options && options.permissionsReady
                    ? $.when()
                    : api && typeof api.ensurePermissionsReady === "function"
                    ? api.ensurePermissionsReady()
                    : $.when()
            ).then((modeFromApi) => {
                const mode = modeFromApi || { isResort: false, isHall: false, isHotel: true };
                if (resolveResortNavMode(!!mode.isResort, selectedKey)) {
                    applyNav({ isResort: true, isHall: false, isHotel: false });
                } else {
                    applyNav(mode);
                }
            });

        const chromeOptions = Object.assign({}, options || {}, {
            onHotelChanged:
                (options && options.onHotelChanged) ||
                function onResortNavHotelChanged() {
                    bootNav();
                }
        });

        bootNav();
        initTopChrome(chromeOptions, () => bootNav());

        if (!window.__zaaerPmsNavCultureListener) {
            window.__zaaerPmsNavCultureListener = true;
            window.addEventListener("zaaer:culture-changed", () => {
                initNavTree(lastNavTreeKey, lastNavTreeMode);
                const titleKey = (options && options.titleKey) || body.getAttribute("data-rbac-title-key") || "rbac.adminTitle";
                const subtitleKey = (options && options.subtitleKey) || body.getAttribute("data-rbac-subtitle-key") || "";
                initHeader({ titleKey, subtitleKey });
            });
        }
    }

    window.Zaaer.PmsAdminShell = {
        init,
        rebuildNavTree: initNavTree,
        resetNavTreeSearch,
        ensureBreadcrumbHotelPickerMarkup,
        filterResortNavItems,
        createResortTicketsNavGroup,
        boardHomeNavLabel,
        fetchIsResortProperty,
        fetchPropertyMode,
        readPropertyMode,
        filterPropertyNavItems,
        createHallOperationsNavGroup,
        createHotelReportsNavGroup,
        lodgingReportsNavGroupLabel,
        resolveExpandedNavKeys,
        createNavTreeAccordionHandler,
        applyNavTreeExpandedKeys,
        persistNavExpandedKeys,
        handleNavTreeItemNavigate,
        refreshResortNav
    };
})(window, jQuery);
