(function (window, $, DevExpress) {
    "use strict";

    const CATEGORIES = ["entry", "games", "pool", "other"];
    const pricingTax = window.Zaaer && window.Zaaer.PmsPricingTax;
    /** Set true to show customer / reservation / unit link row on cashier. */
    const SHOW_OPTIONAL_RESERVATION_LINK = false;

    function can(code) {
        const api = window.Zaaer && window.Zaaer.ApiService;
        return !api || typeof api.hasPermission !== "function" || api.hasPermission(code);
    }

    function today() {
        const d = new Date();
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function ksaNow() {
        const parts = new Intl.DateTimeFormat("en-GB", {
            timeZone: "Asia/Riyadh",
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
            hourCycle: "h23"
        }).formatToParts(new Date());
        const get = (type) => Number(parts.find((p) => p.type === type).value);
        return new Date(get("year"), get("month") - 1, get("day"), get("hour"), get("minute"), get("second"));
    }

    function parseTimeOfDayMinutes(value) {
        const m = String(value || "").match(/^(\d{1,2}):(\d{2})/);
        if (!m) {
            return 0;
        }
        return Number(m[1]) * 60 + Number(m[2]);
    }

    /** Mirrors ResortTicketBusinessHours.IsWithinIssueWindow (KSA local clock). */
    function isWithinIssueWindowKsa(nowKsa, issueStartTime, dailyCloseTime) {
        const issueStart = parseTimeOfDayMinutes(issueStartTime);
        const dailyClose = parseTimeOfDayMinutes(dailyCloseTime);
        const nowMins = nowKsa.getHours() * 60 + nowKsa.getMinutes();

        if (issueStart === 0 && dailyClose === 0) {
            return true;
        }
        if (nowMins >= issueStart) {
            return true;
        }
        if (nowMins < dailyClose) {
            return true;
        }
        return false;
    }

    /** Mirrors ResortTicketBusinessHours.ResolveCurrentBusinessServiceDate. */
    function resolveCurrentBusinessServiceDateKsa(nowKsa, issueStartTime, dailyCloseTime) {
        const issueStart = parseTimeOfDayMinutes(issueStartTime);
        const dailyClose = parseTimeOfDayMinutes(dailyCloseTime);
        const nowMins = nowKsa.getHours() * 60 + nowKsa.getMinutes();
        const day = new Date(nowKsa.getFullYear(), nowKsa.getMonth(), nowKsa.getDate());

        if (issueStart === 0 && dailyClose === 0) {
            return day;
        }
        if (nowMins >= issueStart) {
            return day;
        }
        if (nowMins < dailyClose) {
            const prev = new Date(day);
            prev.setDate(prev.getDate() - 1);
            return prev;
        }
        return null;
    }

    function typeLabel(type, t, isAr) {
        if (!type) {
            return "";
        }
        if (isAr) {
            return type.nameAr || type.nameEn || type.code;
        }
        return type.nameEn || type.nameAr || type.code;
    }

    function paymentMethodLabel(m, isAr) {
        if (!m) {
            return "";
        }
        if (isAr) {
            return m.nameAr || m.NameAr || m.name || m.Name || m.code || m.Code || "";
        }
        return m.name || m.Name || m.nameAr || m.NameAr || m.code || m.Code || "";
    }

    function resolveNetworkPaymentMethodId(methods, cashId) {
        const list = Array.isArray(methods) ? methods.slice() : [];
        const network = list.find((m) => {
            const id = Number(m.id);
            if (cashId != null && id === Number(cashId)) {
                return false;
            }
            const code = String(m.code || m.Code || "").toLowerCase();
            const name = paymentMethodLabel(m, true).toLowerCase();
            const en = paymentMethodLabel(m, false).toLowerCase();
            return (
                code.includes("mada")
                || code.includes("card")
                || code.includes("network")
                || code.includes("visa")
                || name.includes("شبكة")
                || name.includes("بطاق")
                || en.includes("card")
                || en.includes("network")
            );
        });
        if (network) {
            return network.id;
        }
        const fallback = list.find((m) => cashId == null || Number(m.id) !== Number(cashId));
        return fallback ? fallback.id : null;
    }

    function resolveCashPaymentMethodId(methods) {
        const list = Array.isArray(methods) ? methods.slice() : [];
        if (!list.length) {
            return null;
        }

        list.sort((a, b) => {
            const ao = Number(a.sortOrder);
            const bo = Number(b.sortOrder);
            const aSort = Number.isFinite(ao) ? ao : 9999;
            const bSort = Number.isFinite(bo) ? bo : 9999;
            if (aSort !== bSort) {
                return aSort - bSort;
            }
            return paymentMethodLabel(a, isArCulture()).localeCompare(
                paymentMethodLabel(b, isArCulture()),
                isArCulture() ? "ar" : "en"
            );
        });

        const cash = list.find((m) => {
            const code = String(m.code || m.Code || "").toLowerCase();
            const name = paymentMethodLabel(m, true).toLowerCase();
            const en = paymentMethodLabel(m, false).toLowerCase();
            return code === "cash" || name.includes("نقد") || en.includes("cash");
        });
        return cash ? cash.id : list[0].id;
    }

    function isArCulture() {
        const loc = window.Zaaer && window.Zaaer.LocalizationService;
        return loc && loc.currentCulture && loc.currentCulture() === "ar";
    }

    function isCashPaymentMethodId(paymentMethodId, cashMethodId, paymentMethods) {
        if (paymentMethodId == null) {
            return true;
        }
        if (cashMethodId != null && Number(paymentMethodId) === Number(cashMethodId)) {
            return true;
        }
        const pm = (paymentMethods || []).find((m) => Number(m.id) === Number(paymentMethodId));
        if (!pm) {
            return false;
        }
        const code = String(pm.code || pm.Code || "").toLowerCase();
        const name = paymentMethodLabel(pm, true).toLowerCase();
        const en = paymentMethodLabel(pm, false).toLowerCase();
        return code === "cash" || name.includes("نقد") || en.includes("cash");
    }

    function categoryIcon(code) {
        const icons = {
            entry: "event",
            games: "gift",
            pool: "fields",
            other: "box"
        };
        return icons[code] || "box";
    }

    function ticketTypeIcon(type) {
        const code = String((type && type.code) || "").toLowerCase();
        const category = String((type && type.ticketCategory) || "").toLowerCase();

        if (code.includes("bumper") || code.includes("car") || code.includes("cart")) {
            return "car";
        }
        if (code.includes("slide") || code.includes("swing")) {
            return "runner";
        }
        if (code.includes("pool") || code.includes("swim") || category === "pool") {
            return "fields";
        }
        if (code.includes("adult") || code.includes("child") || code.includes("entry")) {
            return "card";
        }
        if (category === "games") {
            return "gift";
        }
        if (category === "entry") {
            return "event";
        }
        if (category === "other") {
            return "product";
        }
        return "tags";
    }

    function bankLabel(b, isAr) {
        if (!b) {
            return "";
        }
        return isAr
            ? b.nameAr || b.NameAr || b.name || b.Name
            : b.name || b.Name || b.nameAr || b.NameAr;
    }

    function normalizePaymentMethods(raw) {
        return (raw || [])
            .map((m) => ({
                id: m.id ?? m.Id ?? m.paymentMethodId ?? m.PaymentMethodId,
                name: m.name ?? m.Name ?? "",
                nameAr: m.nameAr ?? m.NameAr ?? "",
                code: m.code ?? m.Code ?? "",
                sortOrder: m.sortOrder ?? m.SortOrder ?? 9999
            }))
            .filter((m) => m.id !== undefined && m.id !== null);
    }

    function normalizeBanks(raw) {
        return (raw || [])
            .map((b) => ({
                id: b.id ?? b.Id ?? b.zaaerId ?? b.ZaaerId ?? b.bankId ?? b.BankId,
                bankId: b.bankId ?? b.BankId,
                zaaerId: b.zaaerId ?? b.ZaaerId ?? b.id ?? b.Id,
                name: b.name ?? b.Name ?? "",
                nameAr: b.nameAr ?? b.NameAr ?? "",
                code: b.code ?? b.Code ?? "",
                isDefault: !!(b.isDefault ?? b.IsDefault)
            }))
            .filter((b) => b.id !== undefined && b.id !== null);
    }

    function resolveDefaultBankId(bankList) {
        const list = Array.isArray(bankList) ? bankList : [];
        const preferred = list.find((b) => b.isDefault);
        if (preferred) {
            return preferred.id;
        }
        return list.length ? list[0].id : null;
    }

    function formatLocalDate(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return value;
        }

        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function openPrint(service, orderId) {
        const hotelCode = window.Zaaer.ApiService.getHotelCode();
        const url = service.printOrderUrl(orderId, "thermal");
        window.open(
            `resort-ticket-viewer.html?src=${encodeURIComponent(url)}&hotelCode=${encodeURIComponent(hotelCode || "")}`,
            "_blank",
            "noopener"
        );
    }

    function mount(host, options) {
        const service = window.Zaaer && window.Zaaer.ResortTicketService;
        const t = (options && options.t) || function (key) {
            return window.Zaaer.LocalizationService.t(key);
        };
        const loc = window.Zaaer.LocalizationService;
        const isAr = loc && loc.currentCulture && loc.currentCulture() === "ar";
        const ctx = (options && options.context) || {};
        const showOrders = options && options.showOrders !== false;
        const posLayout = !!(options && options.posLayout);
        const onRefresh = options && options.onRefresh;

        if (!service) {
            DevExpress.ui.notify(t("roomBoard.resortTickets.missingModule"), "error", 3000);
            return { destroy() {} };
        }

        let taxConfig = pricingTax ? pricingTax.defaultConfig() : { vatRate: 15, ewaRate: 0, vatIncluded: true, ewaIncluded: true };
        let cashMethodId = null;
        let banks = [];
        let paymentMethods = [];
        let uiReady = false;

        const $root = $(host).addClass("resort-ticket-sales");
        const state = {
            lookups: { paymentMethods: [], banks: [] },
            ticketTypes: [],
            activeCategory: "entry",
            cart: [],
            orders: [],
            ordersRefundFilter: false,
            ordersFilter: {
                fromDate: today(),
                toDate: today()
            },
            form: {
                serviceDate: today(),
                reservationId: ctx.reservationId || null,
                unitId: ctx.unitId || null,
                customerId: ctx.customerId || null,
                payNow: can("resort_tickets.pay_now"),
                paymentMethodId: null,
                bankId: null,
                transactionNo: "",
                notes: ""
            }
        };

        let $hero;
        let $header;
        let $posTopbar;
        let $posTopbarFields;
        let $posModeHost;
        let $posSellPanel;
        let $posOrdersPanel;
        let posModeTabs;
        let posMode = "sell";
        let $bodyParent = $root;

        if (posLayout) {
            $root.addClass("resort-ticket-sales--pos resort-ticket-pos-terminal");
            $posTopbar = $("<div/>").addClass("resort-ticket-pos-topbar").appendTo($root);
            const $posTopbarMain = $("<div/>").addClass("resort-ticket-pos-topbar__main").appendTo($posTopbar);
            $("<div/>")
                .addClass("resort-ticket-pos-topbar__title")
                .text(t("resortTickets.cashier.title"))
                .appendTo($posTopbarMain);
            $("<div/>")
                .addClass("resort-ticket-pos-topbar__sub")
                .text(t("resortTickets.cashier.heroSubtitle"))
                .appendTo($posTopbarMain);
            $posTopbarFields = $("<div/>").addClass("resort-ticket-pos-topbar__fields").appendTo($posTopbar);
            const $posTopbarActions = $("<div/>")
                .addClass("resort-ticket-pos-topbar__actions")
                .appendTo($posTopbar);
            $("<button/>", { type: "button" })
                .addClass("resort-ticket-pos-hotkeys-btn")
                .attr("title", t("resortTickets.hotkeys"))
                .attr("aria-label", t("resortTickets.hotkeys"))
                .append($("<span/>").addClass("dx-icon dx-icon-help"))
                .appendTo($posTopbarActions);
            $posTopbarActions.attr("data-pos-topbar-actions", "1");

            if (showOrders) {
                $posModeHost = $("<div/>").addClass("resort-ticket-pos-mode-tabs").appendTo($root);
                $posSellPanel = $("<div/>").addClass("resort-ticket-pos-panel").appendTo($root);
                $posOrdersPanel = $("<div/>")
                    .addClass("resort-ticket-pos-panel resort-ticket-pos-panel--hidden")
                    .appendTo($root);
                $bodyParent = $posSellPanel;
            }

            $header = $("<div/>").addClass("resort-ticket-sales-header").hide().appendTo($root);
        } else {
            $hero = $("<div/>").addClass("resort-ticket-sales-hero").appendTo($root);
            $("<div/>")
                .append($("<div/>").addClass("resort-ticket-sales-hero__title").text(t("resortTickets.cashier.title")))
                .append($("<div/>").addClass("resort-ticket-sales-hero__sub").text(t("resortTickets.cashier.heroSubtitle")))
                .appendTo($hero);
            $("<div/>")
                .addClass("resort-ticket-sales-hotkeys")
                .attr("title", t("resortTickets.hotkeys"))
                .text(t("resortTickets.hotkeysShort"))
                .appendTo($hero);
            $header = $("<div/>").addClass("resort-ticket-sales-header").appendTo($root);
        }

        const $body = $("<div/>").addClass("resort-ticket-sales-body").appendTo($bodyParent);
        if (posLayout) {
            $body.addClass("pos-terminal-layout");
        }

        const $catalog = $("<div/>")
            .addClass(posLayout ? "pos-terminal-main" : "resort-ticket-sales-catalog")
            .appendTo($body);
        const $tabsHost = $("<div/>")
            .addClass(posLayout ? "pos-category-bar" : "resort-ticket-category-tabs")
            .appendTo($catalog);
        const $tiles = $("<div/>")
            .addClass(posLayout ? "pos-item-grid" : "resort-ticket-sales-tiles")
            .appendTo($catalog);

        const $cartPanel = $("<div/>").appendTo($body);
        let $cartHead;
        let $cartBody;
        let $cartEmpty;
        let $cartGrid;
        let $cartLines;
        let $summary;
        let $cartFooter;
        let $actions;
        let $cartCountEl;
        let $payNowStrip;
        let $totalItemsEl;
        let $totalSubEl;
        let $totalTaxRow;
        let $totalTaxEl;
        let $totalAmountEl;

        if (posLayout) {
            $cartPanel.addClass("pos-cart-panel resort-ticket-pos-cart");
            $("<h2/>").addClass("pos-cart-panel__title").text(t("resortTickets.posCartTitle")).appendTo($cartPanel);
            $payNowStrip = $("<div/>").addClass("resort-ticket-pay-now-strip").appendTo($cartPanel);
            $cartLines = $("<div/>").addClass("pos-cart-lines").appendTo($cartPanel);
            $summary = $("<div/>").addClass("pos-cart-totals").appendTo($cartPanel);

            function appendPosTotalRow(label, id, extraClass) {
                const $row = $("<div/>").addClass("pos-cart-totals__row").appendTo($summary);
                if (extraClass) {
                    $row.addClass(extraClass);
                }
                $("<span/>").text(label).appendTo($row);
                return $("<span/>").attr("id", id).appendTo($row);
            }

            $totalItemsEl = appendPosTotalRow(t("resortTickets.cartLineItems"), "resortTicketPosTotalItems");
            $totalSubEl = appendPosTotalRow(t("resortTickets.subtotal"), "resortTicketPosTotalSub");
            $totalTaxRow = $("<div/>")
                .addClass("pos-cart-totals__row")
                .appendTo($summary);
            $("<span/>").text(t("resortTickets.taxAmount")).appendTo($totalTaxRow);
            $totalTaxEl = $("<span/>").attr("id", "resortTicketPosTotalTax").appendTo($totalTaxRow);
            $totalAmountEl = appendPosTotalRow(
                t("resortTickets.cartTotal"),
                "resortTicketPosTotalAmount",
                "pos-cart-totals__row--total"
            );

            $cartFooter = $("<div/>").addClass("pos-cart-actions").appendTo($cartPanel);
            $actions = $cartFooter;
            $cartCountEl = $();
        } else {
            $cartPanel.addClass("resort-ticket-sales-cart resort-ticket-sales-cart--pos");
            $cartHead = $("<div/>").addClass("resort-ticket-cart-head").appendTo($cartPanel);
            $("<div/>")
                .addClass("resort-ticket-cart-head__main")
                .append($("<h3/>").addClass("resort-ticket-cart-title").text(t("resortTickets.cartTitle")))
                .append($("<span/>").addClass("resort-ticket-cart-count resort-ticket-cart-count--empty").text("0"))
                .appendTo($cartHead);
            $payNowStrip = $("<div/>").addClass("resort-ticket-pay-now-strip").appendTo($cartHead);

            $cartBody = $("<div/>").addClass("resort-ticket-cart-body resort-ticket-cart-body--empty").appendTo($cartPanel);
            $cartEmpty = $("<div/>").addClass("resort-ticket-cart-empty").appendTo($cartBody);
            $("<span/>").addClass("dx-icon dx-icon-cart resort-ticket-cart-empty__icon").appendTo($cartEmpty);
            $("<p/>").addClass("resort-ticket-cart-empty__title").text(t("resortTickets.cartEmpty")).appendTo($cartEmpty);
            $cartGrid = $("<div/>").addClass("resort-ticket-cart-lines pms-grid-compact").appendTo($cartBody);
            $summary = $("<div/>").addClass("resort-ticket-cart-summary").appendTo($cartPanel);
            $cartFooter = $("<div/>").addClass("resort-ticket-cart-footer").appendTo($cartPanel);
            $actions = $("<div/>").addClass("resort-ticket-sales-actions").appendTo($cartFooter);
            $cartCountEl = $cartHead.find(".resort-ticket-cart-count");
        }
        let $ordersBody;
        let ordersCollapsed = true;
        let $ordersSection;
        let $ordersGrid;
        let cartGrid;
        let tabs;
        let headerForm;
        let payNowStripBuilt = false;
        let issueButton;
        let businessHoursTimer = null;
        let $ordersSummaryOrderEl;
        let $ordersSummaryTicketsEl;
        let $ordersSummaryTotalEl;
        let ordersFilterFromBox;
        let ordersFilterToBox;
        let ordersRefundFilterSwitch;

        function ordersGridActionsAlignment() {
            const rtl =
                document.documentElement.dir === "rtl"
                || (document.body && document.body.classList.contains("dx-rtl"));
            return rtl ? "left" : "right";
        }

        function normalizeDateOnly(value) {
            const d = value instanceof Date ? new Date(value) : new Date(value);
            if (Number.isNaN(d.getTime())) {
                return today();
            }
            d.setHours(0, 0, 0, 0);
            return d;
        }

        function syncOrdersFilterFromServiceDate(reload) {
            const svc = normalizeDateOnly(state.form.serviceDate || today());
            state.ordersFilter.fromDate = new Date(svc.getTime());
            state.ordersFilter.toDate = new Date(svc.getTime());
            if (ordersFilterFromBox) {
                ordersFilterFromBox.option("value", state.ordersFilter.fromDate);
            }
            if (ordersFilterToBox) {
                ordersFilterToBox.option("value", state.ordersFilter.toDate);
            }
            if (reload) {
                return reloadOrders();
            }
            return $.Deferred().resolve().promise();
        }

        function clearOrdersGridUiFilters() {
            if (!$ordersGrid) {
                return;
            }
            const grid = $ordersGrid.dxDataGrid("instance");
            if (!grid) {
                return;
            }
            grid.clearFilter();
            grid.searchByText("");
        }

        if (showOrders) {
            const ordersHost = posLayout && $posOrdersPanel ? $posOrdersPanel : $root;
            $ordersSection = $("<div/>")
                .addClass("resort-ticket-orders-section")
                .toggleClass("resort-ticket-orders-section--collapsed", !posLayout)
                .appendTo(ordersHost);
            const $ordersToolbar = $("<div/>").addClass("resort-ticket-orders-toolbar").appendTo($ordersSection);
            if (!posLayout) {
                $("<button/>", { type: "button" })
                    .addClass("resort-ticket-orders-toggle")
                    .attr("aria-expanded", "false")
                    .append($("<span/>").addClass("dx-icon dx-icon-chevrondown resort-ticket-orders-toggle__icon"))
                    .append(
                        $("<span/>")
                            .addClass("resort-ticket-orders-toggle__text")
                            .text(t("resortTickets.ordersExpand"))
                    )
                    .on("click", toggleOrdersSection)
                    .appendTo($ordersToolbar);
            }
            $("<div/>")
                .addClass("resort-ticket-section-title")
                .text(t("resortTickets.todayOrders"))
                .appendTo($ordersToolbar);
            const $refundFilterHost = $("<div/>")
                .addClass("resort-ticket-orders-filter")
                .appendTo($ordersToolbar);
            $refundFilterHost.dxSwitch({
                value: state.ordersRefundFilter,
                switchedOnText: t("resortTickets.filterRefundRequired"),
                switchedOffText: t("resortTickets.filterRefundRequired"),
                onInitialized(e) {
                    ordersRefundFilterSwitch = e.component;
                },
                onValueChanged(e) {
                    state.ordersRefundFilter = !!e.value;
                    reloadOrders();
                }
            });

            $ordersBody = $("<div/>").addClass("resort-ticket-orders-body").appendTo($ordersSection);
            const $ordersDashboard = $("<div/>").addClass("resort-ticket-orders-dashboard").appendTo($ordersBody);
            const $kpiRow = $("<div/>").addClass("resort-ticket-orders-kpi-row").appendTo($ordersDashboard);

            function appendKpiCard(labelKey, extraClass) {
                const $card = $("<div/>")
                    .addClass("resort-ticket-orders-kpi")
                    .appendTo($kpiRow);
                if (extraClass) {
                    $card.addClass(extraClass);
                }
                $("<div/>").addClass("resort-ticket-orders-kpi__label").text(t(labelKey)).appendTo($card);
                const $value = $("<div/>").addClass("resort-ticket-orders-kpi__value").text("0").appendTo($card);
                return $value;
            }

            $ordersSummaryOrderEl = appendKpiCard("resortTickets.ordersSummary.orders");
            $ordersSummaryTicketsEl = appendKpiCard("resortTickets.ordersSummary.tickets");
            $ordersSummaryTotalEl = appendKpiCard("resortTickets.ordersSummary.total", "resort-ticket-orders-kpi--total");

            const $filters = $("<div/>").addClass("resort-ticket-orders-filters").appendTo($ordersDashboard);

            $("<div/>")
                .addClass("resort-ticket-orders-filters__field")
                .appendTo($filters)
                .dxDateBox({
                    label: t("resortTickets.ordersFilter.from"),
                    labelMode: "static",
                    type: "date",
                    openOnFieldClick: true,
                    value: state.ordersFilter.fromDate,
                    onInitialized(e) {
                        ordersFilterFromBox = e.component;
                    }
                });

            $("<div/>")
                .addClass("resort-ticket-orders-filters__field")
                .appendTo($filters)
                .dxDateBox({
                    label: t("resortTickets.ordersFilter.to"),
                    labelMode: "static",
                    type: "date",
                    openOnFieldClick: true,
                    value: state.ordersFilter.toDate,
                    onInitialized(e) {
                        ordersFilterToBox = e.component;
                    }
                });

            $("<div/>")
                .appendTo($filters)
                .dxButton({
                    text: t("resortTickets.ordersFilter.apply"),
                    icon: "filter",
                    type: "default",
                    stylingMode: "contained",
                    onClick: applyOrdersFilter
                });

            $("<div/>")
                .appendTo($filters)
                .dxButton({
                    text: t("resortTickets.ordersFilter.reset"),
                    icon: "revert",
                    stylingMode: "outlined",
                    onClick: resetOrdersFilter
                });

            $ordersGrid = $("<div/>").addClass("pms-grid-compact").appendTo($ordersBody);
        }

        function toggleOrdersSection() {
            if (!$ordersSection) {
                return;
            }
            ordersCollapsed = !ordersCollapsed;
            $ordersSection.toggleClass("resort-ticket-orders-section--collapsed", ordersCollapsed);
            const $toggle = $ordersSection.find(".resort-ticket-orders-toggle");
            $toggle.attr("aria-expanded", ordersCollapsed ? "false" : "true");
            $toggle
                .find(".resort-ticket-orders-toggle__text")
                .text(t(ordersCollapsed ? "resortTickets.ordersExpand" : "resortTickets.ordersCollapse"));
            $toggle
                .find(".resort-ticket-orders-toggle__icon")
                .toggleClass("dx-icon-chevrondown", ordersCollapsed)
                .toggleClass("dx-icon-chevronup", !ordersCollapsed);
            if (!ordersCollapsed) {
                reloadOrders();
            }
        }

        function syncCategoryChrome() {
            $catalog.attr("data-active-category", state.activeCategory);
            $tabsHost.attr("data-active-category", state.activeCategory);
        }

        function categoryLabel(code) {
            return t(`resortTickets.category.${code}`) || code;
        }

        function typesForCategory(category) {
            return (state.ticketTypes || []).filter((x) => x.ticketCategory === category && x.isActive !== false);
        }

        function lineGross(line) {
            const qty = line.quantity || 0;
            const price = line.unitPrice || 0;
            return Math.round(price * qty * 100) / 100;
        }

        function lineTotal(line) {
            if (!pricingTax) {
                return lineGross(line);
            }
            return pricingTax.computePosLineTax(lineGross(line), true, taxConfig).total;
        }

        function cartTotals(lines) {
            if (!pricingTax) {
                const total = (lines || []).reduce((sum, line) => sum + lineGross(line), 0);
                return { subtotal: total, tax: 0, total };
            }

            const grossLines = (lines || []).map((line) => lineGross(line));
            const totals = pricingTax.computePosOrderTotals(grossLines, 0, taxConfig);
            return { subtotal: totals.subtotal, tax: totals.tax, total: totals.total };
        }

        function syncNonCashFields(paymentMethodId) {
            if (!headerForm) {
                return;
            }

            const show =
                state.form.payNow &&
                !isCashPaymentMethodId(paymentMethodId, cashMethodId, paymentMethods);
            const groupPaths = ["nonCashGroup", "paymentDetailsRow.nonCashGroup"];
            const pmPaths = ["paymentMethodId", "paymentDetailsRow.paymentMethodId"];

            groupPaths.forEach((path) => {
                headerForm.itemOption(path, "visible", show);
            });
            pmPaths.forEach((path) => {
                headerForm.itemOption(path, "colSpan", show ? 1 : 3);
            });

            try {
                headerForm.element().toggleClass("resort-ticket-form--non-cash", show);
            } catch {
                /* optional */
            }

            if (!show) {
                headerForm.updateData({ bankId: null, transactionNo: "" });
                state.form.bankId = null;
                state.form.transactionNo = "";
            } else if (!state.form.bankId && banks[0]) {
                headerForm.updateData({ bankId: banks[0].id });
                state.form.bankId = banks[0].id;
            }
        }

        function applyTaxLookups(lookups) {
            const payload = (lookups && (lookups.data || lookups.Data)) || lookups || {};
            state.lookups = Object.assign({}, state.lookups, payload);
            taxConfig = pricingTax
                ? pricingTax.normalizeConfig(payload.pricingTax || payload.PricingTax)
                : taxConfig;
            const cfg = payload.businessConfig || payload.BusinessConfig;
            if (
                cfg &&
                cfg.currentBusinessServiceDate &&
                (!can("resort_tickets.service_date") || posLayout)
            ) {
                state.form.serviceDate = new Date(cfg.currentBusinessServiceDate);
            }
            syncOrdersFilterFromServiceDate(false);
            refreshBusinessHoursLive(false);
        }

        function getBusinessConfig() {
            return state.lookups && (state.lookups.businessConfig || state.lookups.BusinessConfig);
        }

        function evaluateBusinessConfigLive(cfg) {
            if (!cfg) {
                return { canIssueNow: true, currentBusinessServiceDate: null };
            }
            const nowKsa = ksaNow();
            const issueStart = cfg.issueStartTime || cfg.IssueStartTime || "16:00";
            const dailyClose = cfg.dailyCloseTime || cfg.DailyCloseTime || "04:00";
            return {
                canIssueNow: isWithinIssueWindowKsa(nowKsa, issueStart, dailyClose),
                currentBusinessServiceDate: resolveCurrentBusinessServiceDateKsa(nowKsa, issueStart, dailyClose)
            };
        }

        function syncIssueButtonState() {
            if (!issueButton) {
                return;
            }
            const cfg = getBusinessConfig();
            const canIssue = !cfg || cfg.canIssueNow !== false;
            issueButton.option("disabled", !canIssue || !state.cart.length);
        }

        function refreshBusinessHoursLive(notifyWhenOpened) {
            const cfg = getBusinessConfig();
            if (!cfg) {
                syncIssueButtonState();
                return;
            }

            const prevCanIssue = cfg.canIssueNow !== false;
            const live = evaluateBusinessConfigLive(cfg);
            cfg.canIssueNow = live.canIssueNow;
            if (live.currentBusinessServiceDate) {
                cfg.currentBusinessServiceDate = formatLocalDate(live.currentBusinessServiceDate);
            } else {
                cfg.currentBusinessServiceDate = null;
            }

            if (
                live.currentBusinessServiceDate &&
                (!can("resort_tickets.service_date") || posLayout)
            ) {
                state.form.serviceDate = new Date(
                    live.currentBusinessServiceDate.getFullYear(),
                    live.currentBusinessServiceDate.getMonth(),
                    live.currentBusinessServiceDate.getDate()
                );
                if (headerForm) {
                    headerForm.updateData({ serviceDate: state.form.serviceDate });
                    const editor = headerForm.getEditor("serviceDate");
                    if (editor) {
                        editor.option("readOnly", true);
                        editor.option("disabled", true);
                    }
                }
                syncOrdersFilterFromServiceDate(false);
            }

            syncIssueButtonState();

            if (notifyWhenOpened !== false && !prevCanIssue && live.canIssueNow && state.cart.length) {
                DevExpress.ui.notify(t("resortTickets.issueNowAvailable"), "success", 3000);
            }
        }

        function startBusinessHoursWatcher() {
            stopBusinessHoursWatcher();
            refreshBusinessHoursLive(false);
            businessHoursTimer = window.setInterval(() => refreshBusinessHoursLive(true), 15000);
            $(document).on("visibilitychange.resortTicketBusinessHours", () => {
                if (!document.hidden) {
                    refreshBusinessHoursLive(true);
                }
            });
        }

        function stopBusinessHoursWatcher() {
            if (businessHoursTimer) {
                window.clearInterval(businessHoursTimer);
                businessHoursTimer = null;
            }
            $(document).off("visibilitychange.resortTicketBusinessHours");
        }

        function showPaymentTypePopup(tFn) {
            const deferred = $.Deferred();
            let resolved = false;
            const $host = $("<div/>").appendTo("body");
            const totals = cartTotals(state.cart);
            let popupInst;

            function pickPayment(kind) {
                if (resolved) {
                    return;
                }
                const paymentMethodId =
                    kind === "cash"
                        ? cashMethodId || resolveCashPaymentMethodId(paymentMethods)
                        : resolveNetworkPaymentMethodId(paymentMethods, cashMethodId);
                if (!paymentMethodId) {
                    DevExpress.ui.notify(tFn("resortTickets.noPaymentMethods"), "warning", 3000);
                    return;
                }
                resolved = true;
                popupInst.hide();
                deferred.resolve({ kind, paymentMethodId });
            }

            $host.dxPopup({
                title: tFn("resortTickets.paymentType.title"),
                width: Math.min(560, Math.max(340, window.innerWidth - 24)),
                height: "auto",
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.28)",
                wrapperAttr: {
                    class: "res-extra-popup res-extra-select-popup resort-ticket-payment-popup"
                },
                showCloseButton: true,
                onHiding() {
                    if (!resolved) {
                        deferred.reject({ cancelled: true });
                    }
                },
                onHidden() {
                    $host.remove();
                },
                contentTemplate(contentElement) {
                    const $wrap = $("<div/>").addClass(
                        "resort-payment-type-popup resort-payment-type-popup--issue"
                    );
                    $("<p/>")
                        .addClass("resort-payment-type-popup__prompt")
                        .text(tFn("resortTickets.paymentType.prompt"))
                        .appendTo($wrap);
                    $("<div/>")
                        .addClass("resort-payment-type-popup__total")
                        .append(
                            $("<span/>").text(tFn("resortTickets.cartTotal")),
                            $("<strong/>").text(
                                `${totals.total.toFixed(2)} ${tFn("resortTickets.currency")}`
                            )
                        )
                        .appendTo($wrap);

                    const $actions = $("<div/>")
                        .addClass("resort-payment-type-popup__actions")
                        .appendTo($wrap);

                    $("<button/>", { type: "button" })
                        .addClass("resort-payment-type-option resort-payment-type-option--cash")
                        .append(
                            $("<span/>").addClass(
                                "dx-icon dx-icon-money resort-payment-type-option__icon"
                            ),
                            $("<span/>")
                                .addClass("resort-payment-type-option__label")
                                .text(tFn("resortTickets.paymentType.cash"))
                        )
                        .on("click", () => pickPayment("cash"))
                        .appendTo($actions);

                    $("<button/>", { type: "button" })
                        .addClass("resort-payment-type-option resort-payment-type-option--network")
                        .append(
                            $("<span/>").addClass(
                                "dx-icon dx-icon-card resort-payment-type-option__icon"
                            ),
                            $("<span/>")
                                .addClass("resort-payment-type-option__label")
                                .text(tFn("resortTickets.paymentType.network"))
                        )
                        .on("click", () => pickPayment("network"))
                        .appendTo($actions);

                    $("<div/>")
                        .addClass("resort-payment-type-popup__footer")
                        .append(
                            $("<button/>", { type: "button" })
                                .addClass("resort-payment-type-popup__close")
                                .text(tFn("common.cancel"))
                                .on("click", () => popupInst && popupInst.hide())
                        )
                        .appendTo($wrap);

                    $(contentElement).empty().append($wrap);
                },
                onInitialized(e) {
                    popupInst = e.component;
                }
            });

            popupInst = $host.dxPopup("instance");
            popupInst.show();
            return deferred.promise();
        }

        function applyPaymentSources(methods, bankList) {
            paymentMethods = normalizePaymentMethods(methods);
            banks = normalizeBanks(bankList);
            state.lookups.paymentMethods = paymentMethods;
            state.lookups.banks = banks;
            cashMethodId = resolveCashPaymentMethodId(paymentMethods);

            if (headerForm && !posLayout) {
                headerForm.itemOption(
                    "paymentDetailsRow.paymentMethodId",
                    "editorOptions.dataSource",
                    paymentMethods.slice()
                );
                headerForm.itemOption(
                    "paymentDetailsRow.nonCashGroup.bankId",
                    "editorOptions.dataSource",
                    banks.slice()
                );
                if (!state.form.paymentMethodId && cashMethodId) {
                    headerForm.updateData({ paymentMethodId: cashMethodId });
                    state.form.paymentMethodId = cashMethodId;
                }
                syncNonCashFields(state.form.paymentMethodId);
            } else if (!state.form.paymentMethodId && cashMethodId) {
                state.form.paymentMethodId = cashMethodId;
            }

            if (paymentMethods.length === 0) {
                DevExpress.ui.notify(t("resortTickets.noPaymentMethods"), "warning", 3500);
            }
        }

        function addToCart(type) {
            const existing = state.cart.find((x) => x.ticketTypeId === type.ticketTypeId);
            if (existing) {
                existing.quantity += 1;
            } else {
                state.cart.push({
                    ticketTypeId: type.ticketTypeId,
                    name: typeLabel(type, t, isAr),
                    unitPrice: type.unitPrice,
                    quantity: 1
                });
            }
            refreshCart();
        }

        function changeCartQty(ticketTypeId, delta) {
            const line = state.cart.find((x) => x.ticketTypeId === ticketTypeId);
            if (!line) {
                return;
            }

            if (delta < 0 && (line.quantity || 1) <= 1) {
                removeFromCart(ticketTypeId);
                return;
            }

            line.quantity = Math.max(1, Math.min(500, (line.quantity || 1) + delta));
            refreshCart();
        }

        function fmtTicketMoney(amount) {
            const n = Number(amount) || 0;
            return `${DevExpress.localization.formatNumber(n, "#,##0.00")} ${t("resortTickets.currency")}`;
        }

        function renderCategoryBar() {
            if (!posLayout) {
                return;
            }

            $tabsHost.empty();
            CATEGORIES.forEach((id) => {
                const active = state.activeCategory === id;
                $("<div/>")
                    .addClass(`pos-category-chip${active ? " pos-category-chip--active" : ""}`)
                    .dxButton({
                        text: categoryLabel(id),
                        icon: categoryIcon(id),
                        stylingMode: "outlined",
                        type: active ? "default" : "normal",
                        elementAttr: { class: "pos-category-btn" },
                        onClick() {
                            state.activeCategory = id;
                            renderCategoryBar();
                            refreshTiles();
                        }
                    })
                    .appendTo($tabsHost);
            });
            syncCategoryChrome();
        }

        function renderPosCartLines() {
            if (!$cartLines) {
                return;
            }

            $cartLines.empty();
            if (!state.cart.length) {
                $("<p/>").addClass("pos-empty-hint").text(t("resortTickets.cartEmpty")).appendTo($cartLines);
                return;
            }

            state.cart.forEach((line) => {
                const total = lineTotal(line);
                const $row = $("<div/>").addClass("pos-cart-line").appendTo($cartLines);
                $("<div/>").addClass("pos-cart-line__name").text(line.name).appendTo($row);
                $("<div/>")
                    .addClass("pos-cart-line__meta")
                    .text(`${fmtTicketMoney(line.unitPrice)} × ${line.quantity}`)
                    .appendTo($row);
                $("<div/>").addClass("pos-cart-line__total").text(fmtTicketMoney(total)).appendTo($row);

                const $lineActions = $("<div/>").addClass("pos-cart-line__actions").appendTo($row);
                $("<div/>")
                    .dxButton({
                        icon: "minus",
                        stylingMode: "outlined",
                        type: "normal",
                        elementAttr: { class: "pos-qty-btn pos-qty-btn--sm" },
                        onClick: () => changeCartQty(line.ticketTypeId, -1)
                    })
                    .appendTo($lineActions);
                $("<span/>").addClass("pos-cart-line__qty-val").text(String(line.quantity)).appendTo($lineActions);
                $("<div/>")
                    .dxButton({
                        icon: "add",
                        stylingMode: "outlined",
                        type: "default",
                        elementAttr: { class: "pos-qty-btn pos-qty-btn--sm" },
                        onClick: () => changeCartQty(line.ticketTypeId, 1)
                    })
                    .appendTo($lineActions);
                $("<div/>")
                    .dxButton({
                        icon: "trash",
                        stylingMode: "text",
                        type: "danger",
                        hint: t("common.delete"),
                        elementAttr: { class: "pos-cart-line__remove" },
                        onClick: () => removeFromCart(line.ticketTypeId)
                    })
                    .appendTo($lineActions);
            });
        }

        function refreshPosTotals() {
            const totals = cartTotals(state.cart);
            const itemCount = (state.cart || []).reduce(
                (sum, line) => sum + (Number(line.quantity) || 0),
                0
            );

            if ($totalItemsEl) {
                $totalItemsEl.text(String(itemCount));
            }
            if ($totalSubEl) {
                $totalSubEl.text(fmtTicketMoney(totals.subtotal));
            }
            if ($totalTaxRow && $totalTaxEl) {
                $totalTaxRow.toggle(totals.tax > 0);
                $totalTaxEl.text(fmtTicketMoney(totals.tax));
            }
            if ($totalAmountEl) {
                $totalAmountEl.text(fmtTicketMoney(totals.total));
            }
            syncIssueButtonState();
        }

        function removeFromCart(ticketTypeId) {
            state.cart = state.cart.filter((x) => x.ticketTypeId !== ticketTypeId);
            refreshCart();
        }

        function selectCategoryByIndex(index) {
            const category = CATEGORIES[index];
            if (!category) {
                return;
            }
            state.activeCategory = category;
            if (posLayout) {
                renderCategoryBar();
            } else if (tabs) {
                tabs.option("selectedItemKeys", [category]);
            }
            syncCategoryChrome();
            refreshTiles();
        }

        function addTypeByIndex(index) {
            const type = typesForCategory(state.activeCategory)[index];
            if (type) {
                addToCart(type);
            }
        }

        function refreshTiles() {
            $tiles.empty();
            syncCategoryChrome();
            if (posLayout) {
                renderCategoryBar();
            }

            const types = typesForCategory(state.activeCategory);
            if (!types.length) {
                $tiles.append($("<p/>").addClass("pos-empty-hint").text(t("resortTickets.noTypesInCategory")));
                return;
            }

            types.forEach((type, index) => {
                const shelfPrice = Number(type.unitPrice || 0);
                const displayPrice = pricingTax
                    ? pricingTax.computePosLineTax(shelfPrice, true, taxConfig).total
                    : shelfPrice;
                const cartLine = state.cart.find((x) => x.ticketTypeId === type.ticketTypeId);
                const qty = cartLine ? cartLine.quantity : 0;

                if (posLayout) {
                    const $card = $("<article/>")
                        .addClass("pos-item-card resort-ticket-pos-card")
                        .addClass(`resort-ticket-pos-card--cat-${state.activeCategory}`)
                        .toggleClass("pos-item-card--in-cart", qty > 0)
                        .toggleClass("resort-ticket-tile--generic", !!type.isGeneric)
                        .appendTo($tiles);

                    const $media = $("<div/>").addClass("pos-item-card__media resort-ticket-pos-card__media").appendTo($card);
                    $("<span/>")
                        .addClass(`dx-icon dx-icon-${ticketTypeIcon(type)} resort-ticket-pos-card__icon`)
                        .attr("aria-hidden", "true")
                        .appendTo($media);

                    $("<div/>").addClass("pos-item-card__name").text(typeLabel(type, t, isAr)).appendTo($card);
                    $("<div/>").addClass("pos-item-card__price").text(fmtTicketMoney(displayPrice)).appendTo($card);

                    const $qty = $("<div/>").addClass("pos-item-card__qty").appendTo($card);
                    $("<div/>")
                        .dxButton({
                            icon: "minus",
                            stylingMode: "outlined",
                            type: "normal",
                            elementAttr: { class: "pos-qty-btn" },
                            onClick: () => {
                                if (qty > 0) {
                                    changeCartQty(type.ticketTypeId, -1);
                                }
                            }
                        })
                        .appendTo($qty);
                    $("<span/>").addClass("pos-item-card__qty-val").text(String(qty)).appendTo($qty);
                    $("<div/>")
                        .dxButton({
                            icon: "add",
                            stylingMode: "outlined",
                            type: "default",
                            elementAttr: { class: "pos-qty-btn" },
                            onClick: () => addToCart(type)
                        })
                        .appendTo($qty);

                    $card.on("click", (e) => {
                        if ($(e.target).closest(".dx-button").length) {
                            return;
                        }
                        addToCart(type);
                    });
                    return;
                }

                const $tile = $("<button/>", { type: "button" })
                    .addClass("resort-ticket-tile")
                    .addClass(`resort-ticket-tile--cat-${state.activeCategory}`)
                    .attr("data-hotkey", index < 9 ? String(index + 1) : "")
                    .toggleClass("resort-ticket-tile--generic", !!type.isGeneric)
                    .appendTo($tiles);
                $("<span/>")
                    .addClass(`dx-icon dx-icon-${ticketTypeIcon(type)} resort-ticket-tile__icon`)
                    .appendTo($tile);
                $tile.append($("<div/>").addClass("resort-ticket-tile__name").text(typeLabel(type, t, isAr)));
                $tile.append(
                    $("<div/>")
                        .addClass("resort-ticket-tile__price")
                        .text(`${displayPrice.toFixed(2)} ${t("resortTickets.currency")}`)
                );
                if (index < 9) {
                    $tile.append($("<span/>").addClass("resort-ticket-tile__hotkey").text(index + 1));
                }
                $tile.on("click", () => addToCart(type));
            });
        }

        function refreshSummary() {
            if (posLayout) {
                refreshPosTotals();
                return;
            }

            const totals = cartTotals(state.cart);
            const itemCount = (state.cart || []).reduce(
                (sum, line) => sum + (Number(line.quantity) || 0),
                0
            );

            $cartCountEl.text(String(itemCount));
            $cartCountEl.toggleClass("resort-ticket-cart-count--empty", itemCount === 0);
            $cartBody.toggleClass("resort-ticket-cart-body--empty", !state.cart.length);

            $summary.empty();
            const rows = [
                {
                    label: t("resortTickets.cartLineItems"),
                    value: String(itemCount),
                    muted: true
                },
                {
                    label: t("resortTickets.subtotal"),
                    value: `${totals.subtotal.toFixed(2)} ${t("resortTickets.currency")}`,
                    muted: true
                }
            ];
            if (totals.tax > 0) {
                rows.push({
                    label: t("resortTickets.taxAmount"),
                    value: `${totals.tax.toFixed(2)} ${t("resortTickets.currency")}`,
                    muted: true
                });
            }
            rows.push({
                label: t("resortTickets.cartTotal"),
                value: `${totals.total.toFixed(2)} ${t("resortTickets.currency")}`,
                total: true
            });
            rows.forEach((row) => {
                const $row = $("<div/>").addClass("resort-ticket-cart-receipt__row");
                if (row.muted) {
                    $row.addClass("resort-ticket-cart-receipt__row--muted");
                }
                if (row.total) {
                    $row.addClass("resort-ticket-cart-receipt__row--total");
                }
                $row.append($("<span/>").text(row.label));
                $row.append($("<span/>").text(row.value));
                $summary.append($row);
            });
            syncIssueButtonState();
        }

        function isPosMobileViewport() {
            return !!(
                posLayout &&
                typeof window.matchMedia === "function" &&
                window.matchMedia("(max-width: 768px)").matches
            );
        }

        function buildCartColumns() {
            const mobile = isPosMobileViewport();
            return [
                {
                    dataField: "name",
                    caption: t("roomBoard.resortTickets.type"),
                    allowEditing: false,
                    minWidth: mobile ? 56 : posLayout ? 72 : undefined
                },
                {
                    dataField: "quantity",
                    caption: mobile ? "#" : t("roomBoard.resortTickets.quantity"),
                    dataType: "number",
                    width: mobile ? 42 : posLayout ? 56 : 90,
                    editorOptions: { min: 1, max: 500 }
                },
                {
                    dataField: "unitPrice",
                    caption: t("resortTickets.unitPrice"),
                    dataType: "number",
                    format: "#,##0.00",
                    allowEditing: false,
                    visible: !mobile,
                    width: posLayout ? 68 : 90
                },
                {
                    caption: t("roomBoard.resortTickets.total"),
                    width: mobile ? 56 : posLayout ? 72 : 100,
                    allowEditing: false,
                    calculateCellValue(row) {
                        return lineTotal(row);
                    },
                    format: "#,##0.00"
                },
                {
                    type: "buttons",
                    width: mobile ? 104 : posLayout ? 136 : 132,
                    buttons: [
                        {
                            text: "-",
                            cssClass: "resort-ticket-cart-action resort-ticket-cart-action--minus",
                            hint: t("resortTickets.decreaseQty"),
                            onClick(e) {
                                changeCartQty(e.row.data.ticketTypeId, -1);
                            }
                        },
                        {
                            text: "+",
                            cssClass: "resort-ticket-cart-action resort-ticket-cart-action--plus",
                            hint: t("resortTickets.increaseQty"),
                            onClick(e) {
                                changeCartQty(e.row.data.ticketTypeId, 1);
                            }
                        },
                        {
                            icon: "trash",
                            cssClass: "resort-ticket-cart-action resort-ticket-cart-action--delete",
                            hint: t("common.delete"),
                            onClick(e) {
                                removeFromCart(e.row.data.ticketTypeId);
                            }
                        }
                    ]
                }
            ];
        }

        function getCartGridHeight() {
            if (!posLayout) {
                return 220;
            }
            if (isPosMobileViewport()) {
                const rows = Math.max(state.cart.length, 1);
                return Math.min(40 + rows * 38, 142);
            }
            return 172;
        }

        function syncPosCartLayout() {
            if (!cartGrid || !posLayout) {
                return;
            }
            const mobile = isPosMobileViewport();
            $cartGrid.toggleClass("resort-ticket-cart-lines--mobile", mobile);
            cartGrid.option({
                columns: buildCartColumns(),
                height: getCartGridHeight(),
                columnAutoWidth: posLayout && !mobile,
                wordWrapEnabled: true
            });
        }

        function refreshCart() {
            if (posLayout) {
                renderPosCartLines();
                refreshPosTotals();
                refreshTiles();
                return;
            }

            if (cartGrid) {
                cartGrid.option("dataSource", state.cart.slice());
            }
            refreshSummary();
        }

        function clearCart() {
            state.cart = [];
            refreshCart();
        }

        function computeOrdersSummary(orders) {
            const rows = orders || [];
            let tickets = 0;
            let total = 0;
            rows.forEach((row) => {
                tickets += row && row.tickets ? row.tickets.length : 0;
                total += Number(row && row.totalAmount) || 0;
            });
            return { orders: rows.length, tickets, total };
        }

        function refreshOrdersSummary() {
            if (!$ordersSummaryOrderEl) {
                return;
            }
            const summary = computeOrdersSummary(state.orders);
            $ordersSummaryOrderEl.text(summary.orders);
            $ordersSummaryTicketsEl.text(summary.tickets);
            $ordersSummaryTotalEl.text(`${summary.total.toFixed(2)} ${t("resortTickets.currency")}`);
        }

        function applyOrdersFilter() {
            if (ordersFilterFromBox) {
                state.ordersFilter.fromDate = ordersFilterFromBox.option("value") || today();
            }
            if (ordersFilterToBox) {
                state.ordersFilter.toDate = ordersFilterToBox.option("value") || today();
            }
            return reloadOrders();
        }

        function resetOrdersFilter() {
            state.ordersRefundFilter = false;
            if (ordersRefundFilterSwitch) {
                ordersRefundFilterSwitch.option("value", false);
            }
            clearOrdersGridUiFilters();
            return syncOrdersFilterFromServiceDate(true);
        }

        function reloadOrders() {
            if (!showOrders || !$ordersGrid) {
                return $.Deferred().resolve().promise();
            }
            const listParams = {
                fromDate: formatLocalDate(state.ordersFilter.fromDate),
                toDate: formatLocalDate(state.ordersFilter.toDate),
                reservationId: state.form.reservationId || undefined
            };
            if (state.ordersRefundFilter) {
                listParams.paymentStatus = "refund_required";
            }
            return service
                .listOrders(listParams)
                .then((rows) => {
                    state.orders = rows || [];
                    const grid = $ordersGrid.dxDataGrid("instance");
                    if (grid) {
                        grid.option("dataSource", state.orders);
                    }
                    refreshOrdersSummary();
                });
        }

        function syncPayNowChrome() {
            const payNowOn = !!state.form.payNow;
            if (posLayout) {
                return;
            }
            $header.toggle(!payNowOn);
        }

        function buildPayNowStrip() {
            const hasPayNowPerm = can("resort_tickets.pay_now");
            if (hasPayNowPerm) {
                state.form.payNow = true;
            }

            if (payNowStripBuilt) {
                const $sw = $payNowStrip.find(".resort-ticket-pay-now-strip__switch");
                const swInst = $sw.length ? $sw.dxSwitch("instance") : null;
                if (swInst) {
                    swInst.option("value", !!state.form.payNow);
                    swInst.option("disabled", true);
                }
                syncPayNowChrome();
                return;
            }
            $payNowStrip.empty();
            const $label = $("<span/>")
                .addClass("resort-ticket-pay-now-strip__label")
                .text(t("roomBoard.resortTickets.payNow"))
                .appendTo($payNowStrip);
            if (!hasPayNowPerm) {
                $label.attr("title", t("resortTickets.payNowNoPermission"));
            } else {
                $label.attr("title", t("resortTickets.payNowLocked"));
            }
            $("<div/>")
                .addClass("resort-ticket-pay-now-strip__switch")
                .appendTo($payNowStrip)
                .dxSwitch({
                    value: !!state.form.payNow,
                    disabled: true,
                    onValueChanged(e) {
                        if (hasPayNowPerm) {
                            return;
                        }
                        state.form.payNow = !!e.value;
                        syncPayNowChrome();
                    }
                });
            payNowStripBuilt = true;
            syncPayNowChrome();
        }

        function buildHeaderForm() {
            const $target = posLayout ? $posTopbarFields : $header;
            $target.empty();
            const saleInfoItems = posLayout
                ? [
                      {
                          dataField: "serviceDate",
                          label: { text: t("roomBoard.resortTickets.serviceDate") },
                          editorType: "dxDateBox",
                          editorOptions: {
                              type: "date",
                              readOnly: true,
                              disabled: true,
                              openOnFieldClick: false
                          }
                      },
                      {
                          dataField: "notes",
                          label: { text: t("common.notes") },
                          editorType: "dxTextBox",
                          editorOptions: { placeholder: t("resortTickets.notesPlaceholder") }
                      }
                  ]
                : [
                      {
                          dataField: "serviceDate",
                          label: { text: t("roomBoard.resortTickets.serviceDate") },
                          editorType: "dxDateBox",
                          visible: can("resort_tickets.service_date"),
                          editorOptions: {
                              type: "date",
                              openOnFieldClick: true,
                              onValueChanged() {
                                  syncOrdersFilterFromServiceDate(true);
                              }
                          }
                      },
                      {
                          itemType: "group",
                          name: "paymentDetailsRow",
                          colCount: 3,
                          visible: false,
                          cssClass: "resort-ticket-payment-details-row",
                          items: [
                              {
                                  dataField: "paymentMethodId",
                                  colSpan: 3,
                                  label: { text: t("resortTickets.paymentMethod") },
                                  editorType: "dxSelectBox",
                                  isRequired: true,
                                  validationRules: [
                                      {
                                          type: "custom",
                                          message: t("resortTickets.pickPaymentMethod"),
                                          validationCallback(e) {
                                              if (!state.form.payNow) {
                                                  return true;
                                              }
                                              return e.value != null && Number(e.value) > 0;
                                          }
                                      }
                                  ],
                                  editorOptions: {
                                      dataSource: paymentMethods.slice(),
                                      valueExpr: "id",
                                      displayExpr(item) {
                                          return paymentMethodLabel(item, isAr);
                                      },
                                      searchEnabled: true,
                                      placeholder: t(
                                          "reservationDetail.payments.receipt.paymentMethodPlaceholder"
                                      ),
                                      onValueChanged(e) {
                                          syncNonCashFields(e.value);
                                      }
                                  }
                              },
                              {
                                  itemType: "group",
                                  name: "nonCashGroup",
                                  colSpan: 2,
                                  colCount: 2,
                                  visible: false,
                                  cssClass: "resort-ticket-non-cash-group",
                                  items: [
                                      {
                                          dataField: "bankId",
                                          label: { text: t("pos.orders.bank") },
                                          editorType: "dxSelectBox",
                                          editorOptions: {
                                              dataSource: banks.slice(),
                                              valueExpr: "id",
                                              displayExpr(item) {
                                                  return bankLabel(item, isAr);
                                              },
                                              searchEnabled: true,
                                              placeholder: t(
                                                  "reservationDetail.payments.receipt.bankPlaceholder"
                                              )
                                          }
                                      },
                                      {
                                          dataField: "transactionNo",
                                          label: { text: t("pos.orders.transactionNo") },
                                          editorOptions: { maxLength: 100 }
                                      }
                                  ]
                              }
                          ]
                      },
                      {
                          dataField: "notes",
                          label: { text: t("common.notes") },
                          editorType: "dxTextBox",
                          editorOptions: { placeholder: t("resortTickets.notesPlaceholder") }
                      }
                  ];

            headerForm = $target
                .dxForm({
                    formData: state.form,
                    labelLocation: "top",
                    colCount: posLayout ? 2 : 6,
                    cssClass: posLayout ? "resort-ticket-form resort-ticket-form--pos-topbar" : "resort-ticket-form",
                    items: [
                        {
                            itemType: "group",
                            name: "saleInfoGroup",
                            colSpan: posLayout ? 2 : 6,
                            colCount: posLayout ? 2 : 4,
                            caption: posLayout ? undefined : t("resortTickets.saleInfo"),
                            items: saleInfoItems
                        },
                        {
                            itemType: "group",
                            name: "optionalReservationLinkGroup",
                            colSpan: 6,
                            colCount: 3,
                            visible: SHOW_OPTIONAL_RESERVATION_LINK && !posLayout,
                            caption: t("resortTickets.optionalReservationLink"),
                            items: [
                                {
                                    dataField: "customerId",
                                    label: { text: t("resortTickets.customer") },
                                    editorType: "dxNumberBox",
                                    editorOptions: { min: 0, showSpinButtons: true },
                                    visible: !ctx.lockCustomer
                                },
                                {
                                    dataField: "reservationId",
                                    label: { text: t("resortTickets.reservation") },
                                    editorType: "dxNumberBox",
                                    editorOptions: { min: 0, showSpinButtons: true },
                                    visible: !ctx.lockReservation
                                },
                                {
                                    dataField: "unitId",
                                    label: { text: t("resortTickets.unit") },
                                    editorType: "dxNumberBox",
                                    editorOptions: { min: 0, showSpinButtons: true },
                                    visible: !ctx.lockUnit
                                }
                            ]
                        }
                    ],
                    onFieldDataChanged(e) {
                        if (posLayout && e.dataField === "serviceDate") {
                            refreshBusinessHoursLive(false);
                            return;
                        }
                        state.form[e.dataField] = e.value;
                        if (e.dataField === "serviceDate") {
                            syncOrdersFilterFromServiceDate(true);
                        }
                        if (e.dataField === "paymentMethodId") {
                            syncNonCashFields(e.value);
                        }
                    }
                })
                .dxForm("instance");

            syncNonCashFields(state.form.paymentMethodId);
            syncPayNowChrome();
        }

        function buildSalesUi() {
            if (uiReady) {
                return;
            }
            uiReady = true;

            if (posLayout && showOrders && $posModeHost) {
                posModeTabs = $posModeHost
                    .dxTabs({
                        dataSource: [
                            {
                                id: "sell",
                                text: t("resortTickets.posMode.sell"),
                                icon: "cart"
                            },
                            {
                                id: "orders",
                                text: t("resortTickets.posMode.orders"),
                                icon: "orderedlist"
                            }
                        ],
                        keyExpr: "id",
                        width: "auto",
                        scrollByContent: false,
                        showNavButtons: false,
                        stylingMode: "primary",
                        selectedItemKeys: ["sell"],
                        itemTemplate(itemData, _index, element) {
                            const $item = $("<span/>").addClass("resort-ticket-pos-mode-item");
                            $("<span/>")
                                .addClass(`dx-icon dx-icon-${itemData.icon} resort-ticket-pos-mode-item__icon`)
                                .appendTo($item);
                            $("<span/>").text(itemData.text).appendTo($item);
                            element.append($item);
                        },
                        onSelectionChanged(e) {
                            const item = e.addedItems && e.addedItems[0];
                            if (!item) {
                                return;
                            }
                            posMode = item.id;
                            if ($posSellPanel) {
                                $posSellPanel.toggleClass(
                                    "resort-ticket-pos-panel--hidden",
                                    posMode !== "sell"
                                );
                            }
                            if ($posOrdersPanel) {
                                $posOrdersPanel.toggleClass(
                                    "resort-ticket-pos-panel--hidden",
                                    posMode !== "orders"
                                );
                            }
                            if (posMode === "orders") {
                                reloadOrders();
                            }
                        }
                    })
                    .dxTabs("instance");
            }

            if (posLayout) {
                renderCategoryBar();
            } else {
                tabs = $tabsHost
                    .dxTabs({
                        dataSource: CATEGORIES.map((id) => ({
                            id,
                            text: categoryLabel(id),
                            icon: categoryIcon(id)
                        })),
                        keyExpr: "id",
                        selectedItemKeys: [state.activeCategory],
                        itemTemplate(itemData, _index, element) {
                            const $item = $("<span/>").addClass("resort-ticket-tab-item");
                            $("<span/>")
                                .addClass(`dx-icon dx-icon-${itemData.icon} resort-ticket-tab-item__icon`)
                                .appendTo($item);
                            $("<span/>").addClass("resort-ticket-tab-item__text").text(itemData.text).appendTo($item);
                            element.append($item);
                        },
                        onSelectionChanged(e) {
                            const item = e.addedItems && e.addedItems[0];
                            if (item) {
                                state.activeCategory = item.id;
                                syncCategoryChrome();
                                refreshTiles();
                            }
                        }
                    })
                    .dxTabs("instance");
            }

            const po = window.Zaaer.PmsGridOptions;
            if (!posLayout && $cartGrid) {
                cartGrid = $cartGrid
                    .dxDataGrid(
                        po.merge(po.adminBaseline ? po.adminBaseline() : {}, {
                            dataSource: [],
                            keyExpr: "ticketTypeId",
                            height: 220,
                            showBorders: true,
                            editing: { allowUpdating: true, mode: "cell" },
                            focusedRowEnabled: true,
                            keyboardNavigation: {
                                enabled: true,
                                enterKeyAction: "startEdit",
                                editOnKeyPress: true
                            },
                            columns: buildCartColumns(),
                            onRowUpdated(e) {
                                const idx = state.cart.findIndex((x) => x.ticketTypeId === e.key);
                                if (idx >= 0) {
                                    state.cart[idx] = { ...state.cart[idx], ...e.data };
                                }
                                refreshSummary();
                            }
                        })
                    )
                    .dxDataGrid("instance");
            }

            const $issueHost = $("<div/>").addClass("resort-ticket-issue-btn-host");
            $issueHost.dxButton({
                text: t("roomBoard.resortTickets.issue"),
                icon: "check",
                type: "default",
                stylingMode: "contained",
                visible: can("resort_tickets.issue"),
                elementAttr: { class: "resort-ticket-issue-btn" },
                onInitialized(e) {
                    issueButton = e.component;
                    syncIssueButtonState();
                },
                onClick: issueTickets
            });
            $actions.append($issueHost);
            $actions.append(
                $("<div/>").dxButton({
                    text: t("resortTickets.clearCart"),
                    stylingMode: "outlined",
                    onClick: clearCart
                })
            );

            if (showOrders && $ordersGrid) {
                $ordersGrid.dxDataGrid(
                    po.merge(po.adminBaseline ? po.adminBaseline() : {}, {
                        dataSource: [],
                        keyExpr: "ticketOrderId",
                        height: 280,
                        noDataText: t("resortTickets.noOrdersInRange"),
                        headerFilter: { visible: true, search: { enabled: true } },
                        searchPanel: { visible: true, width: 260 },
                        elementAttr: { class: "pms-grid-compact resort-ticket-orders-grid" },
                        columns: [
                            { dataField: "orderNo", caption: t("roomBoard.resortTickets.orderNo"), width: 120 },
                            {
                                dataField: "serviceDate",
                                caption: t("roomBoard.resortTickets.serviceDate"),
                                dataType: "date",
                                width: 120
                            },
                            {
                                dataField: "totalAmount",
                                caption: t("roomBoard.resortTickets.total"),
                                dataType: "number",
                                format: "#,##0.00",
                                width: 110
                            },
                            {
                                dataField: "paymentStatus",
                                caption: t("roomBoard.resortTickets.paymentStatus"),
                                width: 120,
                                customizeText(e) {
                                    const key = `resortTickets.paymentStatus.${e.value}`;
                                    return t(key) || e.value;
                                }
                            },
                            {
                                dataField: "orderStatus",
                                caption: t("roomBoard.resortTickets.status"),
                                width: 110,
                                customizeText(e) {
                                    return e.value === "cancelled"
                                        ? t("resortTickets.ticketStatus.cancelled")
                                        : t("resortTickets.ticketStatus.active");
                                }
                            },
                            {
                                caption: t("roomBoard.resortTickets.count"),
                                width: 80,
                                calculateCellValue(row) {
                                    return row && row.tickets ? row.tickets.length : 0;
                                }
                            },
                            {
                                type: "buttons",
                                width: 156,
                                alignment: ordersGridActionsAlignment(),
                                cssClass: "resort-ticket-orders-actions-col",
                                buttons: [
                                    {
                                        icon: "info",
                                        hint: t("resortTickets.orderDetail.view"),
                                        visible: can("resort_tickets.view"),
                                        onClick(e) {
                                            openOrderDetailDialog(e.row.data);
                                        }
                                    },
                                    {
                                        icon: "print",
                                        hint: t("roomBoard.resortTickets.print"),
                                        visible: can("resort_tickets.print"),
                                        onClick(e) {
                                            openPrint(service, e.row.data.ticketOrderId);
                                        }
                                    },
                                    {
                                        icon: "clear",
                                        hint: t("roomBoard.resortTickets.cancel"),
                                        visible(e) {
                                            return can("resort_tickets.cancel") && e.row.data.orderStatus !== "cancelled";
                                        },
                                        onClick(e) {
                                            cancelOrder(e.row.data);
                                        }
                                    }
                                ]
                            }
                        ]
                    })
                );
            }

            $(document)
                .off("keydown.resortTicketSales")
                .on("keydown.resortTicketSales", (ev) => {
                    const tag = String((ev.target && ev.target.tagName) || "").toLowerCase();
                    if (tag === "input" || tag === "textarea") {
                        return;
                    }

                    if (ev.key === "F8") {
                        ev.preventDefault();
                        issueTickets();
                        return;
                    }

                    if (ev.altKey && ["1", "2", "3", "4"].includes(ev.key)) {
                        ev.preventDefault();
                        selectCategoryByIndex(Number(ev.key) - 1);
                        return;
                    }

                    if (!ev.altKey && /^[1-9]$/.test(ev.key)) {
                        ev.preventDefault();
                        addTypeByIndex(Number(ev.key) - 1);
                    }
                });

            if (posLayout) {
                refreshCart();
            }
        }

        (posLayout ? $posTopbarFields : $header).append(
            $("<div/>")
                .addClass("resort-ticket-sales-header__loading")
                .text(t("common.loading"))
        );

        function reloadAll() {
            return $.when(
                service.getLookups(),
                service.getPaymentMethods(),
                service.getBanks(),
                service.listTypes()
            ).then(function (lookups, methods, bankList, types) {
                applyTaxLookups(lookups);
                applyPaymentSources(methods, bankList);
                state.ticketTypes = types || [];
                buildPayNowStrip();
                if (!headerForm) {
                    buildHeaderForm();
                }
                if (!uiReady) {
                    buildSalesUi();
                }
                if (posLayout) {
                    renderCategoryBar();
                } else if (tabs) {
                    tabs.option("selectedItemKeys", [state.activeCategory]);
                }
                refreshTiles();
                startBusinessHoursWatcher();
                return reloadOrders();
            });
        }

        function submitIssueOrder(paymentChoice) {
            const paymentMethodId = paymentChoice && paymentChoice.paymentMethodId;
            const isCash = isCashPaymentMethodId(paymentMethodId, cashMethodId, paymentMethods);
            let bankId = null;
            if (state.form.payNow && !isCash) {
                bankId = resolveDefaultBankId(banks);
                if (!bankId) {
                    DevExpress.ui.notify(t("resortTickets.noDefaultBank"), "warning", 3000);
                    return $.Deferred().reject().promise();
                }
            }
            const body = {
                reservationId: state.form.reservationId || null,
                unitId: state.form.unitId || null,
                customerId: state.form.customerId || null,
                serviceDate: formatLocalDate(state.form.serviceDate),
                payNow: !!state.form.payNow,
                paymentMethodId: state.form.payNow ? paymentMethodId : null,
                bankId: isCash ? null : bankId,
                transactionNo: "",
                notes: state.form.notes || "",
                lines: state.cart.map((line) => ({
                    ticketTypeId: line.ticketTypeId,
                    quantity: line.quantity
                }))
            };

            return service.createOrder(body).then((created) => {
                DevExpress.ui.notify(t("roomBoard.resortTickets.issued"), "success", 2200);
                if (created && created.ticketOrderId) {
                    openPrint(service, created.ticketOrderId);
                }
                clearCart();
                if (typeof onRefresh === "function") {
                    onRefresh();
                }
                return reloadOrders();
            });
        }

        function issueTickets() {
            if (!can("resort_tickets.issue")) {
                return;
            }
            if (!state.cart.length) {
                DevExpress.ui.notify(t("resortTickets.cartEmpty"), "warning", 2500);
                return;
            }

            refreshBusinessHoursLive(false);
            const cfg = getBusinessConfig();
            if (cfg && cfg.canIssueNow === false) {
                DevExpress.ui.notify(t("resortTickets.issueOutsideHours"), "warning", 3500);
                return;
            }

            if (state.form.payNow) {
                if (!can("resort_tickets.pay_now")) {
                    DevExpress.ui.notify(t("resortTickets.payNowNoPermission"), "warning", 3000);
                    return;
                }
                if (paymentMethods.length === 0) {
                    DevExpress.ui.notify(t("resortTickets.noPaymentMethods"), "error", 3500);
                    return;
                }
                return showPaymentTypePopup(t)
                    .then((choice) => {
                        if (!choice || !choice.paymentMethodId) {
                            return $.Deferred().reject({ cancelled: true }).promise();
                        }
                        return submitIssueOrder(choice);
                    })
                    .fail(() => {
                        /* user cancelled payment popup — sale not completed */
                    });
            }

            return submitIssueOrder(null);
        }

        function ticketStatusLabel(status) {
            const key = `resortTickets.ticketStatus.${status}`;
            return t(key) || status || "";
        }

        function paymentStatusLabel(status) {
            const key = `resortTickets.paymentStatus.${status}`;
            return t(key) || status || "";
        }

        function formatDateTime(value) {
            if (!value) {
                return "—";
            }
            const d = value instanceof Date ? value : new Date(value);
            if (Number.isNaN(d.getTime())) {
                return String(value);
            }
            return d.toLocaleString();
        }

        function cancelKvRow(label, value) {
            return $("<div/>")
                .addClass("resort-ticket-cancel-kv")
                .append($("<span/>").text(label))
                .append($("<strong/>").text(value || "—"));
        }

        function openOrderDetailDialog(row) {
            service.getOrder(row.ticketOrderId).then((order) => {
                if (!order) {
                    DevExpress.ui.notify(t("common.error"), "error", 3000);
                    return;
                }

                const fin = order.financial || order.Financial || {};
                const total = Number(order.totalAmount || 0);
                const $popup = $("<div/>").appendTo("body");
                const $content = $("<div/>").addClass("resort-ticket-order-detail");
                const $summary = $("<div/>").addClass("resort-ticket-cancel-summary").appendTo($content);

                $("<h4/>")
                    .addClass("resort-ticket-order-detail__section-title")
                    .text(t("resortTickets.orderDetail.title"))
                    .appendTo($content);

                cancelKvRow(t("roomBoard.resortTickets.orderNo"), order.orderNo).appendTo($summary);
                cancelKvRow(
                    t("roomBoard.resortTickets.serviceDate"),
                    formatLocalDate(order.serviceDate)
                ).appendTo($summary);
                cancelKvRow(
                    t("roomBoard.resortTickets.total"),
                    `${total.toFixed(2)} ${t("resortTickets.currency")}`
                ).appendTo($summary);
                cancelKvRow(
                    t("roomBoard.resortTickets.paymentStatus"),
                    paymentStatusLabel(order.paymentStatus)
                ).appendTo($summary);
                cancelKvRow(
                    t("roomBoard.resortTickets.status"),
                    order.orderStatus === "cancelled"
                        ? t("resortTickets.ticketStatus.cancelled")
                        : t("resortTickets.ticketStatus.active")
                ).appendTo($summary);

                $("<h4/>")
                    .addClass("resort-ticket-order-detail__section-title")
                    .text(t("resortTickets.orderDetail.financial"))
                    .appendTo($content);
                const $fin = $("<div/>").addClass("resort-ticket-cancel-summary").appendTo($content);
                cancelKvRow(t("resortTickets.cancel.invoice"), fin.invoiceNo || fin.InvoiceNo).appendTo($fin);
                cancelKvRow(t("resortTickets.cancel.receipt"), fin.receiptNo || fin.ReceiptNo).appendTo($fin);
                cancelKvRow(
                    t("resortTickets.cancel.refundReceipt"),
                    fin.refundReceiptNo || fin.RefundReceiptNo
                ).appendTo($fin);
                cancelKvRow(
                    t("resortTickets.cancel.creditNote"),
                    fin.creditNoteNo || fin.CreditNoteNo
                ).appendTo($fin);

                $("<h4/>")
                    .addClass("resort-ticket-order-detail__section-title")
                    .text(t("resortTickets.orderDetail.ticketsGrid"))
                    .appendTo($content);
                const $gridHost = $("<div/>").appendTo($content);

                const toolbarItems = [
                    {
                        widget: "dxButton",
                        location: "after",
                        toolbar: "bottom",
                        options: {
                            text: t("common.close"),
                            onClick() {
                                $popup.dxPopup("instance").hide();
                            }
                        }
                    }
                ];

                if (can("resort_tickets.print")) {
                    toolbarItems.unshift({
                        widget: "dxButton",
                        location: "before",
                        toolbar: "bottom",
                        options: {
                            text: t("roomBoard.resortTickets.print"),
                            icon: "print",
                            onClick() {
                                openPrint(service, order.ticketOrderId);
                            }
                        }
                    });
                }

                if (can("resort_tickets.cancel") && order.orderStatus !== "cancelled") {
                    const finCanCancel = fin.canCancel !== false && fin.CanCancel !== false;
                    if (finCanCancel) {
                        toolbarItems.unshift({
                            widget: "dxButton",
                            location: "before",
                            toolbar: "bottom",
                            options: {
                                text: t("roomBoard.resortTickets.cancel"),
                                icon: "clear",
                                type: "danger",
                                stylingMode: "outlined",
                                onClick() {
                                    $popup.dxPopup("instance").hide();
                                    openCancelOrderDialog(row);
                                }
                            }
                        });
                    }
                }

                $popup.dxPopup({
                    title: t("resortTickets.orderDetail.title"),
                    visible: true,
                    showCloseButton: true,
                    width: Math.min(720, Math.max(360, window.innerWidth - 24)),
                    height: "auto",
                    maxHeight: "72vh",
                    shading: true,
                    shadingColor: "rgba(15, 23, 42, 0.24)",
                    wrapperAttr: { class: "res-extra-popup resort-ticket-order-detail-popup" },
                    contentTemplate() {
                        return $content;
                    },
                    onShown() {
                        $gridHost.dxDataGrid({
                            dataSource: order.tickets || [],
                            showBorders: true,
                            rowAlternationEnabled: true,
                            columnAutoWidth: true,
                            height: Math.min(280, 56 + (order.tickets || []).length * 32),
                            noDataText: t("common.noData"),
                            elementAttr: { class: "pms-grid-compact" },
                            columns: [
                                {
                                    dataField: "ticketNo",
                                    caption: t("resortTickets.orderDetail.ticketNo"),
                                    width: 120
                                },
                                {
                                    dataField: "ticketTypeName",
                                    caption: t("resortTickets.orderDetail.ticketType"),
                                    minWidth: 140
                                },
                                {
                                    dataField: "ticketStatus",
                                    caption: t("resortTickets.orderDetail.ticketStatus"),
                                    width: 110,
                                    customizeText(e) {
                                        return ticketStatusLabel(e.value);
                                    }
                                },
                                {
                                    dataField: "validFrom",
                                    caption: t("resortTickets.orderDetail.validFrom"),
                                    width: 150,
                                    customizeText(e) {
                                        return formatDateTime(e.value);
                                    }
                                },
                                {
                                    dataField: "validTo",
                                    caption: t("resortTickets.orderDetail.validTo"),
                                    width: 150,
                                    customizeText(e) {
                                        return formatDateTime(e.value);
                                    }
                                },
                                {
                                    dataField: "printedAt",
                                    caption: t("resortTickets.orderDetail.printedAt"),
                                    width: 150,
                                    customizeText(e) {
                                        return formatDateTime(e.value);
                                    }
                                },
                                {
                                    dataField: "usedAt",
                                    caption: t("resortTickets.orderDetail.usedAt"),
                                    width: 150,
                                    customizeText(e) {
                                        return formatDateTime(e.value);
                                    }
                                }
                            ]
                        });
                    },
                    toolbarItems,
                    onHidden() {
                        $popup.remove();
                    }
                });
            });
        }

        function openCancelOrderDialog(row) {
            service.getOrder(row.ticketOrderId).then((order) => {
                if (!order) {
                    DevExpress.ui.notify(t("common.error"), "error", 3000);
                    return;
                }

                const fin = order.financial || order.Financial || {};
                const canCancel = fin.canCancel !== false && fin.CanCancel !== false;
                const blockKey = fin.cancelBlockReason || fin.CancelBlockReason;
                if (!canCancel) {
                    DevExpress.ui.notify(
                        t(`resortTickets.cancel.block.${blockKey}`) || t("common.error"),
                        "warning",
                        4000
                    );
                    return;
                }

                const isPaid = String(order.paymentStatus || "").toLowerCase() === "paid";
                const total = Number(order.totalAmount || 0);
                const $popup = $("<div/>").appendTo("body");
                const $content = $("<div/>").addClass("resort-ticket-cancel-review");
                const $summary = $("<div/>").addClass("resort-ticket-cancel-summary").appendTo($content);

                cancelKvRow(t("roomBoard.resortTickets.orderNo"), order.orderNo).appendTo($summary);
                cancelKvRow(
                    t("roomBoard.resortTickets.total"),
                    `${total.toFixed(2)} ${t("resortTickets.currency")}`
                ).appendTo($summary);
                cancelKvRow(
                    t("roomBoard.resortTickets.paymentStatus"),
                    paymentStatusLabel(order.paymentStatus)
                ).appendTo($summary);
                cancelKvRow(t("resortTickets.cancel.invoice"), fin.invoiceNo || fin.InvoiceNo).appendTo(
                    $summary
                );
                cancelKvRow(t("resortTickets.cancel.receipt"), fin.receiptNo || fin.ReceiptNo).appendTo(
                    $summary
                );

                const $effects = $("<ul/>").addClass("resort-ticket-cancel-effects").appendTo($content);
                if (fin.willCreateCreditNote || fin.WillCreateCreditNote) {
                    $("<li/>").text(t("resortTickets.cancel.willCreditNote")).appendTo($effects);
                } else if (fin.willReverseInvoiceOnly || fin.WillReverseInvoiceOnly) {
                    $("<li/>").text(t("resortTickets.cancel.willReverseInvoice")).appendTo($effects);
                }

                $("<div/>")
                    .addClass("resort-ticket-cancel-tickets-title")
                    .text(t("resortTickets.cancel.tickets"))
                    .appendTo($content);
                const $ticketList = $("<div/>").addClass("resort-ticket-cancel-tickets").appendTo($content);
                (order.tickets || []).forEach((tk) => {
                    $("<div/>")
                        .addClass("resort-ticket-cancel-ticket-row")
                        .text(
                            `${tk.ticketNo || tk.TicketNo} · ${tk.ticketTypeName || tk.TicketTypeName} · ${ticketStatusLabel(tk.ticketStatus || tk.TicketStatus)}`
                        )
                        .appendTo($ticketList);
                });

                const $reasonHost = $("<div/>").appendTo($content);
                let reasonBox;
                let confirmRefundCheck;
                let confirmRefundForm;

                if (isPaid) {
                    const $confirmHost = $("<div/>").appendTo($content);
                    confirmRefundForm = $confirmHost
                        .dxForm({
                            items: [
                                {
                                    dataField: "confirmRefund",
                                    label: { text: t("resortTickets.cancel.confirmRefundCheck") },
                                    editorType: "dxCheckBox"
                                }
                            ]
                        })
                        .dxForm("instance");
                }

                $popup.dxPopup({
                    title: t("resortTickets.cancel.reviewTitle"),
                    visible: true,
                    showCloseButton: true,
                    width: Math.min(560, Math.max(360, window.innerWidth - 24)),
                    height: "auto",
                    maxHeight: "72vh",
                    shading: true,
                    shadingColor: "rgba(15, 23, 42, 0.24)",
                    wrapperAttr: { class: "res-extra-popup resort-ticket-cancel-popup" },
                    contentTemplate() {
                        return $content;
                    },
                    onShown() {
                        reasonBox = $reasonHost
                            .dxTextArea({
                                label: t("resortTickets.cancel.reason"),
                                height: 72,
                                maxLength: 500
                            })
                            .dxTextArea("instance");
                    },
                    toolbarItems: [
                        {
                            widget: "dxButton",
                            location: "after",
                            toolbar: "bottom",
                            options: {
                                text: t("common.cancel"),
                                stylingMode: "outlined",
                                onClick() {
                                    $popup.dxPopup("instance").hide();
                                }
                            }
                        },
                        {
                            widget: "dxButton",
                            location: "after",
                            toolbar: "bottom",
                            options: {
                                text: t("roomBoard.resortTickets.cancel"),
                                type: "danger",
                                onClick() {
                                    const reason = (reasonBox && reasonBox.option("value")) || "";
                                    if (!String(reason).trim()) {
                                        DevExpress.ui.notify(
                                            t("resortTickets.cancel.reasonRequired"),
                                            "warning",
                                            2500
                                        );
                                        return;
                                    }
                                    if (isPaid) {
                                        const fd =
                                            (confirmRefundForm && confirmRefundForm.option("formData")) || {};
                                        if (!fd.confirmRefund) {
                                            DevExpress.ui.notify(
                                                t("resortTickets.cancel.confirmRefundCheck"),
                                                "warning",
                                                3000
                                            );
                                            return;
                                        }
                                    }

                                    const proceed = () => {
                                        service
                                            .cancelOrder(order.ticketOrderId, {
                                                reason: String(reason).trim(),
                                                confirmPaidRefund: !!isPaid
                                            })
                                            .then((result) => {
                                                const finResult =
                                                    (result && (result.financial || result.Financial)) || {};
                                                const refunded =
                                                    String(
                                                        (result && result.paymentStatus) || ""
                                                    ).toLowerCase() === "refunded";
                                                DevExpress.ui.notify(
                                                    refunded
                                                        ? t("resortTickets.cancel.successRefund")
                                                        : t("resortTickets.cancel.success"),
                                                    "success",
                                                    3200
                                                );
                                                $popup.dxPopup("instance").hide();
                                                reloadOrders();
                                            })
                                            .catch((err) => {
                                                const msg =
                                                    (err && err.responseJSON && err.responseJSON.message) ||
                                                    t("common.error");
                                                DevExpress.ui.notify(msg, "error", 4000);
                                            });
                                    };

                                    if (!isPaid) {
                                        proceed();
                                        return;
                                    }

                                    const amountText = total.toFixed(2);
                                    const currency = t("resortTickets.currency");
                                    const firstMsg = t("resortTickets.cancel.paidConfirm")
                                        .replace("{amount}", amountText)
                                        .replace("{currency}", currency);
                                    DevExpress.ui.dialog.confirm(firstMsg, t("resortTickets.cancel.confirmTitle")).done(
                                        (ok1) => {
                                            if (!ok1) {
                                                return;
                                            }
                                            const secondMsg = t("resortTickets.cancel.paidConfirmSecond")
                                                .replace("{amount}", amountText)
                                                .replace("{currency}", currency);
                                            DevExpress.ui.dialog
                                                .confirm(secondMsg, t("resortTickets.cancel.confirmTitle"))
                                                .done((ok2) => {
                                                    if (ok2) {
                                                        proceed();
                                                    }
                                                });
                                        }
                                    );
                                }
                            }
                        }
                    ],
                    onHidden() {
                        $popup.remove();
                    }
                });
            });
        }

        function cancelOrder(row) {
            openCancelOrderDialog(row);
        }

        reloadAll().catch((err) => {
            const msg = (err && err.responseJSON && err.responseJSON.message) || t("common.error");
            DevExpress.ui.notify(msg, "error", 3500);
        });

        return {
            reload: reloadAll,
            destroy() {
                stopBusinessHoursWatcher();
                $(document).off("keydown.resortTicketSales");
                $(window).off("resize.resortTicketCart");
                $root.remove();
            }
        };
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ResortTicketSalesPanel = { mount, CATEGORIES };
})(window, jQuery, DevExpress);
