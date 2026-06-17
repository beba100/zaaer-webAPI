(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const pos = window.Zaaer.PosService;
    const api = window.Zaaer.ApiService;

    const pricingTax = window.Zaaer.PmsPricingTax;
    let catalog = null;
    let taxConfig = pricingTax.defaultConfig();
    let cart = [];
    let payments = [];
    let orderDiscountAmount = 0;
    let selectedCategoryId = null;
    let searchText = "";
    let loadPanel;
    let banks = [];
    let cashMethodId = null;
    const SG = window.Zaaer && window.Zaaer.SaveGuard;
    const orderSubmitGuard = SG ? SG.create() : null;

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    function fmtMoney(n) {
        return `${DevExpress.localization.formatNumber(Number(n) || 0, "#,##0.00")} SAR`;
    }

    /** Full URL or site-relative path e.g. /images/pos/pepsi.png */
    function resolveItemImageUrl(item) {
        const raw = item && (item.imageUrl || item.image_url);
        if (!raw || `${raw}`.trim() === "") {
            return null;
        }

        const url = `${raw}`.trim();
        if (/^https?:\/\//i.test(url) || url.startsWith("data:")) {
            return url;
        }

        if (url.startsWith("//")) {
            return `${window.location.protocol}${url}`;
        }

        if (url.startsWith("/")) {
            return `${window.location.origin}${url}`;
        }

        return `${window.location.origin}/${url.replace(/^\.\//, "")}`;
    }

    function appendItemImage($card, item) {
        const $media = $("<div>").addClass("pos-item-card__media").appendTo($card);
        const src = resolveItemImageUrl(item);
        if (!src) {
            $("<span>").addClass("pos-item-card__ph dx-icon dx-icon-image").attr("aria-hidden", "true").appendTo($media);
            return;
        }

        $("<img>")
            .attr({ src, alt: "", loading: "lazy", decoding: "async" })
            .on("error", function handleImgError() {
                $(this).replaceWith(
                    $("<span>").addClass("pos-item-card__ph dx-icon dx-icon-image").attr("aria-hidden", "true")
                );
            })
            .appendTo($media);
    }

    function label(row, enField, arField) {
        if (!row) {
            return "";
        }

        if (isAr()) {
            return row[arField] || row[enField] || "";
        }

        return row[enField] || row[arField] || "";
    }

    function isOutletOpen(outlet) {
        const status = `${(outlet && (outlet.status || outlet.Status)) || ""}`.trim().toLowerCase();
        return status === "open";
    }

    function notifyOutletClosed() {
        DevExpress.ui.notify(t("pos.terminal.outletClosed"), "warning", 2800);
    }

    function getOutletIdFromUrl() {
        const p = new URLSearchParams(window.location.search);
        const id = Number(p.get("outletId"));
        return Number.isFinite(id) && id > 0 ? id : null;
    }

    function getEmbeddedPosContext() {
        const p = new URLSearchParams(window.location.search);
        const embedded = `${p.get("embedded") || ""}`.trim();

        if (embedded === "edit-transferred") {
            const orderId = Number(p.get("orderId"));
            if (!Number.isFinite(orderId) || orderId <= 0) {
                return null;
            }

            const reservationId = Number(p.get("reservationId"));
            return {
                mode: "edit-transferred",
                orderId,
                orderNo: `${p.get("orderNo") || ""}`.trim(),
                reservationId: Number.isFinite(reservationId) && reservationId > 0 ? reservationId : null,
                reservationNo: `${p.get("reservationNo") || ""}`.trim()
            };
        }

        if (embedded === "reservation") {
            const reservationId = Number(p.get("reservationId"));
            if (!Number.isFinite(reservationId) || reservationId <= 0) {
                return null;
            }

            return {
                mode: "reservation",
                reservationId,
                reservationNo: `${p.get("reservationNo") || ""}`.trim()
            };
        }

        return null;
    }

    function getEmbeddedReservationContext() {
        const ctx = getEmbeddedPosContext();
        return ctx && ctx.mode === "reservation" ? ctx : null;
    }

    function getEditTransferredContext() {
        const ctx = getEmbeddedPosContext();
        return ctx && ctx.mode === "edit-transferred" ? ctx : null;
    }

    function isEmbeddedPosShellMode() {
        return !!getEmbeddedPosContext();
    }

    function isEmbeddedReservationMode() {
        return !!getEmbeddedReservationContext();
    }

    function closeEmbeddedPosShell() {
        if (window.parent === window) {
            return;
        }

        const ctx = getEmbeddedPosContext();
        const type =
            ctx && ctx.mode === "edit-transferred"
                ? "pos-order-edit-close"
                : "pos-reservation-embed-close";
        window.parent.postMessage({ type }, window.location.origin);
    }

    function closeEmbeddedReservationPos() {
        closeEmbeddedPosShell();
    }

    function hydrateCartFromExistingOrder(order) {
        const items = (catalog && catalog.items) || [];
        orderDiscountAmount = Number(order.discountAmount) || 0;
        cart = (order.lines || []).map((line) => {
            const catalogItem = items.find((i) => Number(i.itemId) === Number(line.itemId));
            let unitPrice = catalogItem ? Number(catalogItem.price) : 0;
            if (!unitPrice) {
                const gross = Number(line.totalLineGross);
                const qty = Number(line.quantity) || 1;
                unitPrice =
                    gross > 0 ? Math.round((gross / qty) * 100) / 100 : Number(line.unitPrice) || 0;
            }

            return {
                itemId: line.itemId,
                itemName: line.itemName,
                unitPrice,
                quantity: Number(line.quantity) || 1,
                discount: Number(line.discount) || 0,
                includesTax: catalogItem ? !!catalogItem.includesTax : true
            };
        });
        renderCart();
        renderItemGrid();
    }

    function mountEmbeddedReservationBackButton() {
        if ($("#posEmbeddedBackBtn").length) {
            return;
        }

        const $host = $("#posToolbarHost > div").first();
        if (!$host.length) {
            return;
        }

        const $wrap = $("<div>").attr("id", "posEmbeddedBackBtn").addClass("pos-embedded-back-host").prependTo($host);
        const editCtx = getEditTransferredContext();
        $wrap.dxButton({
            text: editCtx ? t("pos.orders.backToOrders") : t("pos.terminal.backToReservation"),
            icon: "back",
            stylingMode: "text",
            type: "default",
            elementAttr: { class: "pos-embedded-back-btn" },
            onClick: closeEmbeddedPosShell
        });
    }

    function setOutletInUrl(outletId) {
        const url = new URL(window.location.href);
        if (outletId) {
            url.searchParams.set("outletId", String(outletId));
        } else {
            url.searchParams.delete("outletId");
        }
        window.history.replaceState({}, "", url.toString());
    }

    function withLoad(promise) {
        loadPanel.show();
        return $.when(promise).always(() => loadPanel.hide());
    }

    function hideLoadPanel() {
        if (loadPanel) {
            loadPanel.hide();
        }
    }

    function disposePosPopup($host) {
        if (!$host || !$host.length) {
            return;
        }

        try {
            const inst = $host.dxPopup("instance");
            if (inst) {
                inst.dispose();
            }
        } catch {
            /* not initialized */
        }

        $host.empty();
    }

    function posPopupBaseOptions(title, extra) {
        return Object.assign(
            {
                title,
                visible: false,
                showCloseButton: true,
                showTitle: true,
                dragEnabled: false,
                hideOnOutsideClick: true,
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                container: document.body,
                position: {
                    my: "center",
                    at: "center",
                    of: window,
                    offset: "0 0"
                },
                width: Math.min(520, Math.max(300, window.innerWidth - 20)),
                height: "auto",
                maxHeight: Math.min(520, Math.max(280, window.innerHeight - 48)),
                wrapperAttr: { class: "res-extra-popup res-extra-select-popup pos-modal-popup" }
            },
            extra || {}
        );
    }

    function paymentMethodLabel(m) {
        if (!m) {
            return "";
        }

        if (isAr()) {
            return m.nameAr || m.name || m.code || "";
        }

        return m.name || m.nameAr || m.code || "";
    }

    function resolveCashPaymentMethodId(methods) {
        const list = methods || [];
        const cash = list.find((m) => {
            const n = paymentMethodLabel(m).toLowerCase();
            return n.includes("cash") || n.includes("نقد");
        });
        return cash ? cash.id : (list[0] && list[0].id);
    }

    function isCashPaymentMethodId(paymentMethodId, cashId) {
        if (paymentMethodId == null) {
            return true;
        }

        return Number(paymentMethodId) === Number(cashId);
    }

    function syncCheckoutNonCashFields(formInstance, paymentMethodId) {
        if (!formInstance) {
            return;
        }

        const show = !isCashPaymentMethodId(paymentMethodId, cashMethodId);
        formInstance.itemOption("nonCashGroup", "visible", show);
        const fd = formInstance.option("formData") || {};
        if (!show) {
            formInstance.updateData({ bankId: null, transactionNo: "" });
        } else if (!fd.bankId && banks[0]) {
            formInstance.updateData({ bankId: banks[0].id });
        }
    }

    function posReceiptPaymentFormItems(paymentMethods, getFormInstance) {
        return [
            {
                dataField: "paymentMethodId",
                label: { text: t("pos.terminal.paymentMethod") },
                editorType: "dxSelectBox",
                isRequired: true,
                validationRules: [{ type: "required", message: t("pos.terminal.paymentMethodRequired") }],
                editorOptions: {
                    items: paymentMethods,
                    valueExpr: "id",
                    displayExpr: paymentMethodLabel,
                    onValueChanged(e) {
                        syncCheckoutNonCashFields(getFormInstance(), e.value);
                    }
                }
            },
            {
                itemType: "group",
                name: "nonCashGroup",
                colCount: 2,
                visible: false,
                items: [
                    {
                        dataField: "bankId",
                        label: { text: t("pos.orders.bank") },
                        editorType: "dxSelectBox",
                        editorOptions: {
                            items: banks,
                            valueExpr: "id",
                            displayExpr: (b) => label(b, "name", "nameAr")
                        }
                    },
                    {
                        dataField: "transactionNo",
                        label: { text: t("pos.orders.transactionNo") },
                        editorOptions: { maxLength: 100 }
                    }
                ]
            }
        ];
    }

    function buildPosPaymentLinesFromForm(data, totalAmount) {
        const paymentMethodId = Number(data.paymentMethodId);
        if (!Number.isFinite(paymentMethodId) || paymentMethodId <= 0) {
            return null;
        }

        const isCash = isCashPaymentMethodId(paymentMethodId, cashMethodId);
        if (!isCash && !data.bankId) {
            return null;
        }

        return [
            {
                paymentMethodId,
                amount: totalAmount,
                bankId: isCash ? null : data.bankId,
                transactionNo: isCash ? "" : (data.transactionNo || "").trim()
            }
        ];
    }

    function cartKey(item) {
        return item.itemId || `name:${item.itemName}`;
    }

    function canApplyOrderDiscount() {
        return api.hasPermission("pos.orders.discount");
    }

    function cartLineGrossAmounts() {
        return cart.map((line) =>
            Math.round(line.unitPrice * line.quantity - (line.discount || 0), 2)
        );
    }

    function cartGrossSum() {
        return cartLineGrossAmounts().reduce((s, g) => s + g, 0);
    }

    function computeTotals() {
        const grossLines = cartLineGrossAmounts();
        const orderTotals =
            typeof pricingTax.computePosOrderTotals === "function"
                ? pricingTax.computePosOrderTotals(grossLines, orderDiscountAmount, taxConfig)
                : (function fallbackOrderTotals() {
                      const grossSum = grossLines.reduce((s, g) => s + g, 0);
                      const discount = Math.min(Math.max(0, orderDiscountAmount), grossSum);
                      const adjusted = Math.round((grossSum - discount) * 100) / 100;
                      if (adjusted <= 0) {
                          return { subtotal: 0, tax: 0, discount, total: 0, grossSum };
                      }

                      const calc = pricingTax.calculateAmounts(adjusted, taxConfig);
                      return {
                          subtotal: calc.net,
                          tax: calc.ewa + calc.vat,
                          discount,
                          total: calc.total,
                          grossSum
                      };
                  })();
        const paid = Math.round(
            payments.reduce((s, p) => s + (Number(p.amount) || 0), 0),
            2
        );
        const balance = Math.round(Math.max(0, orderTotals.total - paid), 2);

        return {
            subtotal: orderTotals.subtotal,
            tax: orderTotals.tax,
            discount: orderTotals.discount,
            total: orderTotals.total,
            grossSum: orderTotals.grossSum,
            paid,
            balance
        };
    }

    function filteredItems() {
        if (!catalog || !catalog.items) {
            return [];
        }

        let items = catalog.items.slice();
        if (selectedCategoryId != null) {
            items = items.filter((i) => i.categoryId === selectedCategoryId);
        }

        if (searchText) {
            const q = searchText.toLowerCase();
            items = items.filter((i) => {
                const n1 = (i.itemName || "").toLowerCase();
                const n2 = (i.itemNameAr || "").toLowerCase();
                const code = (i.itemCode || "").toLowerCase();
                return n1.includes(q) || n2.includes(q) || code.includes(q);
            });
        }

        return items;
    }

    function addToCart(item) {
        const key = cartKey(item);
        const existing = cart.find((c) => cartKey(c) === key);
        if (existing) {
            existing.quantity += 1;
        } else {
            cart.push({
                itemId: item.itemId,
                itemName: label(item, "itemName", "itemNameAr") || item.itemName,
                unitPrice: Number(item.price) || 0,
                quantity: 1,
                discount: 0,
                includesTax: !!item.includesTax
            });
        }

        renderCart();
        renderItemGrid();
    }

    function changeQty(key, delta) {
        const line = cart.find((c) => cartKey(c) === key);
        if (!line) {
            return;
        }

        if (delta < 0 && line.quantity <= 1) {
            cart = cart.filter((c) => cartKey(c) !== key);
        } else {
            line.quantity += delta;
        }

        renderCart();
        renderItemGrid();
    }

    function removeFromCart(key) {
        cart = cart.filter((c) => cartKey(c) !== key);
        renderCart();
        renderItemGrid();
    }

    function renderItemGrid() {
        const $grid = $("#posItemGrid");
        $grid.empty();
        const items = filteredItems();

        if (!items.length) {
            $("<p>").addClass("pos-empty-hint").text(t("pos.terminal.cartEmpty")).appendTo($grid);
            return;
        }

        items.forEach((item) => {
            const key = cartKey(item);
            const inCart = cart.find((c) => cartKey(c) === key);
            const qty = inCart ? inCart.quantity : 0;
            const $card = $("<article>")
                .addClass(`pos-item-card${qty > 0 ? " pos-item-card--in-cart" : ""}`)
                .appendTo($grid);

            appendItemImage($card, item);
            $("<div>").addClass("pos-item-card__name").text(label(item, "itemName", "itemNameAr")).appendTo($card);
            $("<div>").addClass("pos-item-card__price").text(fmtMoney(item.price)).appendTo($card);

            const $qty = $("<div>").addClass("pos-item-card__qty").appendTo($card);
            $("<div>")
                .dxButton({
                    icon: "minus",
                    stylingMode: "outlined",
                    type: "normal",
                    elementAttr: { class: "pos-qty-btn" },
                    onClick: () => changeQty(key, -1)
                })
                .appendTo($qty);
            $("<span>").addClass("pos-item-card__qty-val").text(String(qty)).appendTo($qty);
            $("<div>")
                .dxButton({
                    icon: "add",
                    stylingMode: "outlined",
                    type: "default",
                    elementAttr: { class: "pos-qty-btn" },
                    onClick: () => addToCart(item)
                })
                .appendTo($qty);

            $card.on("click", (e) => {
                if ($(e.target).closest(".dx-button").length) {
                    return;
                }

                addToCart(item);
            });
        });
    }

    function resolvePosCategoryIcon(chip) {
        if (chip.id == null) {
            return "menu";
        }

        const n = String(chip.name || "").toLowerCase();
        if (/شاي|قهوة|coffee|tea/.test(n)) {
            return "food";
        }
        if (/ساخن|hot/.test(n)) {
            return "sun";
        }
        if (/بارد|cold|عصير|juice|مياه|water|بيبسي|pepsi/.test(n)) {
            return "refresh";
        }
        if (/شوكولات|chocolate|حلو|snack|كيت/.test(n)) {
            return "favorites";
        }
        if (/إيراد|revenue|أخرى|other/.test(n)) {
            return "money";
        }
        if (/مشروب|drink|beverage/.test(n)) {
            return "cart";
        }

        return "box";
    }

    function renderCategoryBar() {
        const $bar = $("#posCategoryBar");
        $bar.empty();

        const chips = [{ id: null, name: t("pos.terminal.allCategories") }].concat(
            (catalog.categories || []).map((c) => ({
                id: c.categoryId,
                name: label(c, "categoryName", "categoryNameAr")
            }))
        );

        chips.forEach((chip) => {
            const active = selectedCategoryId === chip.id;
            $("<div>")
                .addClass(`pos-category-chip${active ? " pos-category-chip--active" : ""}`)
                .dxButton({
                    text: chip.name,
                    icon: resolvePosCategoryIcon(chip),
                    stylingMode: "outlined",
                    type: active ? "default" : "normal",
                    elementAttr: { class: "pos-category-btn" },
                    onClick() {
                        selectedCategoryId = chip.id;
                        renderCategoryBar();
                        renderItemGrid();
                    }
                })
                .appendTo($bar);
        });
    }

    function clampOrderDiscount() {
        const max = Math.round(cartGrossSum() * 100) / 100;
        if (orderDiscountAmount > max) {
            orderDiscountAmount = max;
        }
    }

    function renderCart() {
        clampOrderDiscount();
        const totals = computeTotals();
        const $lines = $("#posCartLines");
        $lines.empty();

        if (!cart.length) {
            $("<p>").addClass("pos-empty-hint").text(t("pos.terminal.cartEmpty")).appendTo($lines);
        } else {
            cart.forEach((line) => {
                const key = cartKey(line);
                const gross = Math.round(line.unitPrice * line.quantity - (line.discount || 0), 2);
                const breakdown = pricingTax.computePosLineTax(gross, line.includesTax, taxConfig);
                const $row = $("<div>").addClass("pos-cart-line").appendTo($lines);
                $("<div>").addClass("pos-cart-line__name").text(line.itemName).appendTo($row);
                $("<div>")
                    .addClass("pos-cart-line__meta")
                    .text(`${fmtMoney(line.unitPrice)} × ${line.quantity}`)
                    .appendTo($row);
                $("<div>").addClass("pos-cart-line__total").text(fmtMoney(breakdown.total)).appendTo($row);

                const $actions = $("<div>").addClass("pos-cart-line__actions").appendTo($row);
                $("<div>")
                    .dxButton({
                        icon: "minus",
                        stylingMode: "outlined",
                        type: "normal",
                        elementAttr: { class: "pos-qty-btn pos-qty-btn--sm" },
                        onClick: () => changeQty(key, -1)
                    })
                    .appendTo($actions);
                $("<span>").addClass("pos-cart-line__qty-val").text(String(line.quantity)).appendTo($actions);
                $("<div>")
                    .dxButton({
                        icon: "add",
                        stylingMode: "outlined",
                        type: "default",
                        elementAttr: { class: "pos-qty-btn pos-qty-btn--sm" },
                        onClick: () => changeQty(key, 1)
                    })
                    .appendTo($actions);
                $("<div>")
                    .dxButton({
                        icon: "trash",
                        stylingMode: "text",
                        type: "danger",
                        hint: t("pos.terminal.removeLine"),
                        elementAttr: { class: "pos-cart-line__remove" },
                        onClick: () => removeFromCart(key)
                    })
                    .appendTo($actions);
            });
        }

        $("#posTotalSub").text(fmtMoney(totals.subtotal));
        $("#posTotalTax").text(fmtMoney(totals.tax));
        $("#posTotalDiscount").text(fmtMoney(totals.discount));
        $("#posTotalAmount").text(fmtMoney(totals.total));
        $("#posTotalPaid").text(fmtMoney(totals.paid));
        $("#posTotalBalance").text(fmtMoney(totals.balance));
        renderDiscountActionButtons();
    }

    function renderDiscountActionButtons() {
        const $host = $("#posDiscountActions");
        if (!$host.length) {
            return;
        }

        $host.empty();

        $("<div>")
            .dxButton({
                icon: "edit",
                stylingMode: "text",
                type: "default",
                hint: t("pos.terminal.editDiscount"),
                elementAttr: { class: "pos-discount-edit-btn" },
                onClick() {
                    window.setTimeout(() => openOrderDiscountPopup(), 0);
                }
            })
            .appendTo($host);

        if (orderDiscountAmount > 0) {
            $("<div>")
                .dxButton({
                    icon: "trash",
                    stylingMode: "text",
                    type: "danger",
                    hint: t("pos.terminal.clearDiscount"),
                    elementAttr: { class: "pos-discount-clear-btn" },
                    onClick() {
                        orderDiscountAmount = 0;
                        renderCart();
                    }
                })
                .appendTo($host);
        }
    }

    function renderOutletPicker(outlets) {
        const $host = $("#posWorkspaceHost");
        $host.empty();
        $("#posSearchHost").prop("hidden", true);
        $("#posPageSubtitle").text(t("pos.terminal.pickOutlet"));
        try {
            $("#posBackBtn").dxButton("instance").option("visible", false);
        } catch {
            /* not initialized */
        }

        const $list = $("<div>").addClass("pos-outlet-list").appendTo($host);
        (outlets || []).forEach((o) => {
            if (!o.isActive) {
                return;
            }

            const open = isOutletOpen(o);
            const $row = (open ? $("<button>", { type: "button" }) : $("<div>", { role: "button", tabindex: "0" }))
                .addClass("pos-outlet-row")
                .toggleClass("pos-outlet-row--closed", !open)
                .appendTo($list);
            const $text = $("<div>").appendTo($row);
            $("<div>").addClass("pos-outlet-row__name").text(label(o, "outletName", "outletNameAr")).appendTo($text);
            if (o.location) {
                $("<div>").addClass("pos-outlet-row__meta").text(o.location).appendTo($text);
            }
            $("<span>")
                .addClass(
                    open ? "pos-outlet-row__badge pos-outlet-row__badge--open" : "pos-outlet-row__badge pos-outlet-row__badge--closed"
                )
                .text(open ? t("pos.settings.open") : t("pos.settings.closed"))
                .appendTo($row);

            if (open) {
                $row.on("click", () => {
                    setOutletInUrl(o.outletId);
                    loadTerminal(o.outletId);
                    $("#posBackBtn").dxButton("instance").option("visible", true);
                });
            } else {
                $row.on("click keydown", (e) => {
                    if (e.type === "keydown" && e.key !== "Enter" && e.key !== " ") {
                        return;
                    }

                    e.preventDefault();
                    notifyOutletClosed();
                });
            }
        });

        if (!outlets || !outlets.length) {
            const $actions = $("<div>").css({ marginTop: 12 }).appendTo($host);
            $("<div>")
                .dxButton({
                    text: t("pos.terminal.settingsLink"),
                    icon: "preferences",
                    stylingMode: "outlined",
                    onClick() {
                        window.location.href = "/pos-settings.html";
                    }
                })
                .appendTo($actions);
        }
    }

    function renderTerminalShell() {
        const $host = $("#posWorkspaceHost");
        $host.empty();
        $("#posSearchHost").prop("hidden", false);

        const outletName = label(catalog.outlet, "outletName", "outletNameAr");
        $("#posPageSubtitle").text(t("pos.terminal.outletLabel").replace("{0}", outletName));
        try {
            $("#posBackBtn").dxButton("instance").option("visible", true);
        } catch {
            /* not initialized */
        }

        const $layout = $("<div>").addClass("pos-terminal-layout").appendTo($host);

        const $main = $("<div>").addClass("pos-terminal-main").appendTo($layout);
        $("<div>").attr("id", "posCategoryBar").addClass("pos-category-bar").appendTo($main);
        $("<div>").attr("id", "posItemGrid").addClass("pos-item-grid").appendTo($main);

        const embeddedCtx = getEmbeddedReservationContext();
        const editCtx = getEditTransferredContext();

        const $cart = $("<aside>").addClass("pos-cart-panel").appendTo($layout);
        if (editCtx) {
            const bannerLabel = editCtx.orderNo
                ? t("pos.terminal.editTransferredBanner").replace("{0}", editCtx.orderNo)
                : String(editCtx.orderId);
            $("<div>").addClass("pos-embedded-res-banner").text(bannerLabel).appendTo($cart);
            if (editCtx.reservationNo || editCtx.reservationId) {
                const resLabel = editCtx.reservationNo || String(editCtx.reservationId);
                $("<div>")
                    .addClass("pos-embedded-res-subbanner")
                    .text(t("pos.terminal.embeddedReservationBanner").replace("{0}", resLabel))
                    .appendTo($cart);
            }
        } else if (embeddedCtx) {
            const bannerLabel = embeddedCtx.reservationNo || String(embeddedCtx.reservationId);
            $("<div>")
                .addClass("pos-embedded-res-banner")
                .text(t("pos.terminal.embeddedReservationBanner").replace("{0}", bannerLabel))
                .appendTo($cart);
        }

        $("<h2>").addClass("pos-cart-panel__title").text(t("pos.terminal.cartTitle")).appendTo($cart);
        if (!embeddedCtx && !editCtx) {
            $("<p>").addClass("pos-tax-invoice-hint").text(t("pos.terminal.taxInvoiceHint")).appendTo($cart);
        } else if (editCtx) {
            $("<p>").addClass("pos-tax-invoice-hint").text(t("pos.terminal.editTransferredHint")).appendTo($cart);
        }
        $("<div>").attr("id", "posCartLines").addClass("pos-cart-lines").appendTo($cart);

        const $totals = $("<div>").addClass("pos-cart-totals").appendTo($cart);
        function totalRow(key, id) {
            const $row = $("<div>").addClass("pos-cart-totals__row").appendTo($totals);
            $("<span>").text(key).appendTo($row);
            $("<span>").attr("id", id).appendTo($row);
        }

        totalRow(t("pos.terminal.subtotal"), "posTotalSub");
        totalRow(t("pos.terminal.tax"), "posTotalTax");

        const $discountRow = $("<div>")
            .addClass("pos-cart-totals__row pos-cart-totals__row--discount")
            .appendTo($totals);
        $("<span>").text(t("pos.terminal.generalDiscount")).appendTo($discountRow);
        const $discountVal = $("<div>").addClass("pos-cart-totals__discount-val").appendTo($discountRow);
        $("<span>").attr("id", "posTotalDiscount").appendTo($discountVal);
        if (canApplyOrderDiscount()) {
            $("<div>").attr("id", "posDiscountActions").addClass("pos-cart-totals__discount-actions").appendTo($discountVal);
        }

        const $totalRow = $("<div>").addClass("pos-cart-totals__row pos-cart-totals__row--total").appendTo($totals);
        $("<span>").text(t("pos.terminal.grandTotal")).appendTo($totalRow);
        $("<span>").attr("id", "posTotalAmount").appendTo($totalRow);
        totalRow(t("pos.terminal.paid"), "posTotalPaid");
        totalRow(t("pos.terminal.balance"), "posTotalBalance");

        const $actions = $("<div>").addClass("pos-cart-actions").appendTo($cart);
        if (editCtx) {
            $cart.addClass("pos-cart-panel--embedded-reservation");
            $("<div>")
                .dxButton({
                    text: t("pos.terminal.saveTransferredEdit"),
                    icon: "save",
                    type: "default",
                    stylingMode: "contained",
                    onClick: submitEditTransferredOrder
                })
                .appendTo($actions);
            $("<div>")
                .dxButton({
                    text: t("pos.orders.backToOrders"),
                    icon: "back",
                    stylingMode: "outlined",
                    onClick: closeEmbeddedPosShell
                })
                .appendTo($actions);
        } else if (embeddedCtx) {
            $cart.addClass("pos-cart-panel--embedded-reservation");
            $("<div>")
                .dxButton({
                    text: t("pos.terminal.chargeReservation"),
                    icon: "check",
                    type: "default",
                    stylingMode: "contained",
                    onClick: submitEmbeddedReservationOrder
                })
                .appendTo($actions);
            $("<div>")
                .dxButton({
                    text: t("pos.terminal.backToReservation"),
                    icon: "back",
                    stylingMode: "outlined",
                    onClick: closeEmbeddedReservationPos
                })
                .appendTo($actions);
        } else {
            $("<div>")
                .dxButton({
                    text: t("pos.terminal.checkout"),
                    icon: "check",
                    type: "default",
                    stylingMode: "contained",
                    onClick: openCheckoutPopup
                })
                .appendTo($actions);
            $("<div>")
                .dxButton({
                    text: t("pos.terminal.transferToReservation"),
                    icon: "group",
                    stylingMode: "outlined",
                    onClick: openTransferToReservationPopup
                })
                .appendTo($actions);
        }

        $("<div>")
            .dxButton({
                text: t("pos.terminal.clearCart"),
                icon: "trash",
                stylingMode: "outlined",
                onClick() {
                    cart = [];
                    payments = [];
                    orderDiscountAmount = 0;
                    renderCart();
                    renderItemGrid();
                }
            })
            .appendTo($actions);

        if (!$("#posSearchBox").length) {
            $("#posSearchHost").empty().append($("<div>").attr("id", "posSearchBox"));
            $("#posSearchBox").dxTextBox({
                placeholder: t("pos.terminal.search"),
                mode: "search",
                valueChangeEvent: "input",
                onValueChanged(e) {
                    searchText = (e.value || "").trim();
                    renderItemGrid();
                }
            });
        }

        renderCategoryBar();
        renderItemGrid();
        renderCart();
    }

    function openOrderDiscountPopup() {
        if (!canApplyOrderDiscount()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (!cart.length) {
            DevExpress.ui.notify(t("pos.terminal.cartEmpty"), "warning", 2600);
            return;
        }

        hideLoadPanel();

        const maxDiscount = Math.round(cartGrossSum() * 100) / 100;
        const $popup = $("#posDiscountPopup");
        disposePosPopup($popup);
        let formInstance;

        const popup = $popup
            .dxPopup(
                posPopupBaseOptions(t("pos.terminal.discountTitle"), {
                    width: Math.min(380, Math.max(300, window.innerWidth - 20)),
                    wrapperAttr: {
                        class: "res-extra-popup res-extra-select-popup pos-modal-popup pos-discount-popup"
                    },
                    contentTemplate() {
                        return $("<div>").attr("id", "posDiscountForm");
                    },
                    onShown() {
                        formInstance = $("#posDiscountForm")
                            .dxForm({
                                formData: { discountAmount: orderDiscountAmount },
                                labelLocation: "top",
                                items: [
                                    {
                                        dataField: "discountAmount",
                                        label: { text: t("pos.terminal.discountAmount") },
                                        editorType: "dxNumberBox",
                                        editorOptions: {
                                            format: "#,##0.00",
                                            min: 0,
                                            max: maxDiscount,
                                            showSpinButtons: true
                                        }
                                    }
                                ]
                            })
                            .dxForm("instance");
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
                                    popup.hide();
                                }
                            }
                        },
                        {
                            widget: "dxButton",
                            location: "after",
                            toolbar: "bottom",
                            options: {
                                text: t("common.save"),
                                type: "default",
                                stylingMode: "contained",
                                onClick() {
                                    const data = formInstance.option("formData");
                                    let amount = Math.round((Number(data.discountAmount) || 0) * 100) / 100;
                                    if (amount < 0) {
                                        amount = 0;
                                    }

                                    if (amount > maxDiscount) {
                                        DevExpress.ui.notify(t("pos.terminal.discountTooHigh"), "warning", 2800);
                                        return;
                                    }

                                    orderDiscountAmount = amount;
                                    popup.hide();
                                    renderCart();
                                }
                            }
                        }
                    ],
                    onHidden() {
                        disposePosPopup($popup);
                    }
                })
            )
            .dxPopup("instance");

        popup.show();
    }

    function openCheckoutPopup() {
        if (!cart.length) {
            DevExpress.ui.notify(t("pos.terminal.cartEmpty"), "warning", 2600);
            return;
        }

        if (!api.hasPermission("pos.orders.create")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        hideLoadPanel();

        const totals = computeTotals();
        const $popup = $("#posCheckoutPopup");
        disposePosPopup($popup);

        const $formHost = $("<div>").attr("id", "posCheckoutForm");
        let formInstance;
        let paymentMethods = [];
        let popupInstance;
        let checkoutButton;

        Promise.all([
            pos.getPaymentMethods(),
            api.get("/api/v1/pms/lookups/banks").then((r) => (r && r.data !== undefined ? r.data : r))
        ])
            .then(([methods, bankRows]) => {
                paymentMethods = methods || [];
                banks = bankRows || [];
                cashMethodId = resolveCashPaymentMethodId(paymentMethods);
                const defaultPmId = cashMethodId || (paymentMethods[0] && paymentMethods[0].id);

                popupInstance = $popup
                    .dxPopup(
                        posPopupBaseOptions(t("pos.terminal.checkout"), {
                            contentTemplate() {
                                const $wrap = $("<div>");
                                $("<p>")
                                    .addClass("pos-checkout-balance")
                                    .text(`${t("pos.terminal.balance")}: ${fmtMoney(totals.balance)}`)
                                    .appendTo($wrap);
                                $formHost.appendTo($wrap);
                                return $wrap;
                            },
                            onShown() {
                                formInstance = $formHost
                                    .dxForm({
                                        formData: {
                                            notes: "",
                                            paymentMethodId: defaultPmId,
                                            payAmount: totals.total,
                                            bankId: banks[0] && banks[0].id,
                                            transactionNo: ""
                                        },
                                        labelLocation: "top",
                                        colCount: 1,
                                        items: [
                                            {
                                                dataField: "payAmount",
                                                label: { text: t("pos.terminal.amount") },
                                                editorType: "dxNumberBox",
                                                editorOptions: {
                                                    format: "#,##0.00",
                                                    min: 0,
                                                    readOnly: true
                                                }
                                            },
                                            ...posReceiptPaymentFormItems(paymentMethods, () => formInstance),
                                            {
                                                dataField: "notes",
                                                label: { text: t("pos.terminal.notes") },
                                                editorType: "dxTextArea",
                                                editorOptions: { height: 72 }
                                            }
                                        ]
                                    })
                                    .dxForm("instance");

                                syncCheckoutNonCashFields(formInstance, defaultPmId);
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
                                            popupInstance.hide();
                                        }
                                    }
                                },
                                {
                                    widget: "dxButton",
                                    location: "after",
                                    toolbar: "bottom",
                                    options: {
                                        text: t("pos.terminal.checkout"),
                                        type: "default",
                                        stylingMode: "contained",
                                        onInitialized(e) {
                                            checkoutButton = e.component;
                                        },
                                        onClick() {
                                            submitOrder(popupInstance, formInstance, totals, checkoutButton);
                                        }
                                    }
                                }
                            ],
                            onHidden() {
                                disposePosPopup($popup);
                            }
                        })
                    )
                    .dxPopup("instance");

                popupInstance.show();
            })
            .catch(() => DevExpress.ui.notify(t("common.error"), "error", 3200));
    }

    function localizePosError(raw) {
        const s = raw == null ? "" : String(raw).trim();
        if (!s) {
            return t("common.error");
        }
        if (/^pos\./.test(s)) {
            const tr = t(s);
            if (tr && tr !== s) {
                return tr;
            }
        }
        return s;
    }

    function buildOrderLinesPayload() {
        return cart.map((c) => ({
            itemId: c.itemId,
            itemName: c.itemName,
            quantity: c.quantity,
            unitPrice: c.unitPrice,
            discount: c.discount || 0,
            includesTax: c.includesTax
        }));
    }

    function posReservationPickerText(value) {
        const s = value == null ? "" : String(value).trim();
        return s || "—";
    }

    function appendPosReservationPickerHeader($popupContent) {
        if (!$popupContent || !$popupContent.length) {
            return;
        }

        if ($popupContent.find(".pos-res-picker-table-head").length) {
            return;
        }

        const $head = $("<div>").addClass("pos-res-picker-table-head pos-res-picker-row");
        $head.append(
            $("<span>")
                .addClass("pos-res-picker-cell pos-res-picker-cell--head pos-res-picker-cell--room")
                .text(t("pos.terminal.resPickerColRoom")),
            $("<span>")
                .addClass("pos-res-picker-cell pos-res-picker-cell--head pos-res-picker-cell--no")
                .text(t("pos.terminal.resPickerColReservation")),
            $("<span>")
                .addClass("pos-res-picker-cell pos-res-picker-cell--head pos-res-picker-cell--guest")
                .text(t("pos.terminal.resPickerColGuest"))
        );
        $popupContent.prepend($head);
    }

    function buildPosReservationPickerRow(row, options) {
        options = options || {};
        const $row = $("<div>").addClass("pos-res-picker-row");
        if (options.compact) {
            $row.addClass("pos-res-picker-row--field");
        }

        $row.append(
            $("<span>")
                .addClass("pos-res-picker-cell pos-res-picker-cell--room")
                .text(posReservationPickerText(row && row.roomLabels)),
            $("<span>")
                .addClass("pos-res-picker-cell pos-res-picker-cell--no")
                .text(posReservationPickerText(row && row.reservationNo)),
            $("<span>")
                .addClass("pos-res-picker-cell pos-res-picker-cell--guest")
                .text(posReservationPickerText(row && row.customerName))
        );
        return $row;
    }

    function posReservationPickerDisplayExpr(item) {
        if (!item) {
            return "";
        }

        return [
            posReservationPickerText(item.roomLabels),
            posReservationPickerText(item.reservationNo),
            posReservationPickerText(item.customerName)
        ].join("  ·  ");
    }

    function posReservationPickerSelectOptions(rows) {
        return {
            items: rows,
            valueExpr: "reservationId",
            displayExpr: posReservationPickerDisplayExpr,
            searchEnabled: true,
            searchMode: "contains",
            searchExpr: ["reservationNo", "customerName", "roomLabels", "displayLabel"],
            placeholder: t("pos.terminal.pickReservationPlaceholder"),
            showClearButton: true,
            elementAttr: { class: "pos-res-picker-select" },
            itemTemplate(itemData, _itemIndex, itemElement) {
                buildPosReservationPickerRow(itemData).appendTo($(itemElement));
            },
            dropDownOptions: {
                maxHeight: 360,
                minWidth: Math.min(520, Math.max(300, window.innerWidth - 32)),
                wrapperAttr: { class: "pos-reservation-picker-dropdown" },
                onShown(e) {
                    appendPosReservationPickerHeader($(e.component.content()));
                }
            }
        };
    }

    function openTransferToReservationPopup() {
        if (!cart.length) {
            DevExpress.ui.notify(t("pos.terminal.cartEmpty"), "warning", 2600);
            return;
        }

        if (!catalog || !catalog.outlet) {
            DevExpress.ui.notify(t("common.error"), "error", 2800);
            return;
        }

        hideLoadPanel();
        const totals = computeTotals();

        const $popup = $("#posReservationPickerPopup");
        disposePosPopup($popup);

        let popupInstance;
        let formInstance;
        let inHouseRows = [];
        let submitButton;

        pos.listInHouseReservations()
            .then((rows) => {
                inHouseRows = Array.isArray(rows) ? rows : [];
                if (!inHouseRows.length) {
                    DevExpress.ui.notify(t("pos.terminal.noInHouseReservations"), "warning", 3600);
                    return null;
                }

                popupInstance = $popup
                    .dxPopup(
                        posPopupBaseOptions(t("pos.terminal.transferTitle"), {
                            width: Math.min(640, Math.max(320, window.innerWidth - 24)),
                            maxHeight: "72vh",
                            wrapperAttr: {
                                class: "res-extra-popup res-extra-select-popup pos-modal-popup pos-reservation-picker-popup"
                            },
                            contentTemplate() {
                                const $wrap = $("<div>").addClass("pos-reservation-picker");
                                $("<p>")
                                    .addClass("pos-checkout-balance")
                                    .text(`${t("pos.terminal.grandTotal")}: ${fmtMoney(totals.total)}`)
                                    .appendTo($wrap);
                                $("<div>").attr("id", "posReservationPickerForm").appendTo($wrap);
                                return $wrap;
                            },
                            onShown() {
                                formInstance = $("#posReservationPickerForm")
                                    .dxForm({
                                        formData: {
                                            reservationId: null,
                                            notes: ""
                                        },
                                        labelLocation: "top",
                                        colCount: 1,
                                        items: [
                                            {
                                                dataField: "reservationId",
                                                label: { text: t("pos.terminal.pickReservation") },
                                                editorType: "dxSelectBox",
                                                isRequired: true,
                                                validationRules: [
                                                    { type: "required", message: t("pos.terminal.reservationRequired") }
                                                ],
                                                editorOptions: posReservationPickerSelectOptions(inHouseRows)
                                            },
                                            {
                                                dataField: "notes",
                                                label: { text: t("pos.terminal.notes") },
                                                editorType: "dxTextArea",
                                                editorOptions: { height: 72 }
                                            }
                                        ]
                                    })
                                    .dxForm("instance");
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
                                                popupInstance.hide();
                                            }
                                        }
                                    },
                                    {
                                        widget: "dxButton",
                                        location: "after",
                                        toolbar: "bottom",
                                        options: {
                                            text: t("pos.terminal.transferToReservation"),
                                            type: "default",
                                            stylingMode: "contained",
                                            onInitialized(e) {
                                                submitButton = e.component;
                                            },
                                            onClick() {
                                                submitReservationOrder(
                                                    popupInstance,
                                                    formInstance,
                                                    totals,
                                                    submitButton
                                                );
                                            }
                                        }
                                    }
                                ],
                                onHidden() {
                                    disposePosPopup($popup);
                                }
                            })
                        )
                        .dxPopup("instance");

                    popupInstance.show();
                    return popupInstance;
                })
            .catch(() => DevExpress.ui.notify(t("common.error"), "error", 3200));
    }

    function submitEditTransferredOrder() {
        if (!cart.length) {
            DevExpress.ui.notify(t("pos.terminal.cartEmpty"), "warning", 2600);
            return;
        }

        const ctx = getEditTransferredContext();
        if (!ctx) {
            return;
        }

        const useOrderGuard = SG && orderSubmitGuard;
        if (useOrderGuard && !orderSubmitGuard.begin()) {
            return;
        }

        const totalsPreview = computeTotals();
        const body = {
            discountAmount: totalsPreview.discount,
            lines: buildOrderLinesPayload()
        };

        hideLoadPanel();
        withLoad(
            $.when(pos.updateTransferredOrder(ctx.orderId, body))
                .done((order) => {
                    DevExpress.ui.notify(
                        t("pos.terminal.transferredOrderUpdated").replace("{0}", order.orderNo || ""),
                        "success",
                        2600
                    );
                    if (window.parent !== window) {
                        window.parent.postMessage(
                            {
                                type: "pos-transferred-order-updated",
                                orderNo: order.orderNo || "",
                                orderId: order.orderId != null ? order.orderId : null
                            },
                            window.location.origin
                        );
                    }
                })
                .fail((xhr) => {
                    const msg = localizePosError(xhr && xhr.responseJSON && xhr.responseJSON.message);
                    DevExpress.ui.notify(msg, "error", 3400);
                })
                .always(() => {
                    if (useOrderGuard) {
                        orderSubmitGuard.end();
                    }
                })
        );
    }

    function submitEmbeddedReservationOrder() {
        if (!cart.length) {
            DevExpress.ui.notify(t("pos.terminal.cartEmpty"), "warning", 2600);
            return;
        }

        const ctx = getEmbeddedReservationContext();
        if (!ctx) {
            return;
        }

        const useOrderGuard = SG && orderSubmitGuard;
        if (useOrderGuard && !orderSubmitGuard.begin()) {
            return;
        }

        const totalsPreview = computeTotals();
        const body = {
            outletId: catalog.outlet.outletId,
            reservationId: ctx.reservationId,
            discountAmount: totalsPreview.discount,
            lines: buildOrderLinesPayload()
        };

        hideLoadPanel();
        withLoad(
            $.when(pos.createOrder(body))
                .done((order) => {
                    DevExpress.ui.notify(
                        t("pos.terminal.orderTransferred").replace("{0}", order.orderNo || ""),
                        "success",
                        2600
                    );
                    cart = [];
                    payments = [];
                    orderDiscountAmount = 0;
                    renderCart();
                    renderItemGrid();
                    if (window.parent !== window) {
                        window.parent.postMessage(
                            {
                                type: "pos-reservation-order-complete",
                                orderNo: order.orderNo || "",
                                orderId: order.orderId != null ? order.orderId : null
                            },
                            window.location.origin
                        );
                    }
                })
                .fail((xhr) => {
                    const msg = localizePosError(xhr && xhr.responseJSON && xhr.responseJSON.message);
                    DevExpress.ui.notify(msg, "error", 3400);
                })
                .always(() => {
                    if (useOrderGuard) {
                        orderSubmitGuard.end();
                    }
                })
        );
    }

    function submitReservationOrder(popupInstance, formInstance, totalsPreview, submitButton) {
        const validation = formInstance.validate();
        if (!validation.isValid) {
            return;
        }

        const data = formInstance.option("formData");
        const reservationId = Number(data.reservationId);
        if (!Number.isFinite(reservationId) || reservationId <= 0) {
            DevExpress.ui.notify(t("pos.terminal.reservationRequired"), "warning", 2800);
            return;
        }

        const useOrderGuard = SG && orderSubmitGuard;
        if (useOrderGuard && !orderSubmitGuard.begin()) {
            return;
        }
        if (useOrderGuard) {
            SG.setButtonDisabled(submitButton, true);
        }

        const body = {
            outletId: catalog.outlet.outletId,
            reservationId,
            notes: data.notes,
            discountAmount: totalsPreview.discount,
            lines: buildOrderLinesPayload()
        };

        hideLoadPanel();
        try {
            popupInstance.hide();
        } catch {
            /* popup may already be closing */
        }

        withLoad(
            $.when(pos.createOrder(body))
                .done((order) => {
                    DevExpress.ui.notify(
                        t("pos.terminal.orderTransferred").replace("{0}", order.orderNo || ""),
                        "success",
                        2600
                    );
                    cart = [];
                    payments = [];
                    orderDiscountAmount = 0;
                    renderCart();
                    renderItemGrid();
                    window.location.href = "/pos-orders.html";
                })
                .fail((xhr) => {
                    const msg = localizePosError(xhr && xhr.responseJSON && xhr.responseJSON.message);
                    DevExpress.ui.notify(msg, "error", 3400);
                })
                .always(() => {
                    if (useOrderGuard) {
                        orderSubmitGuard.end();
                        SG.setButtonDisabled(submitButton, false);
                    }
                })
        );
    }

    function submitOrder(popupInstance, formInstance, totalsPreview, checkoutButton) {
        const data = formInstance.option("formData");
        const payAmount = Number(data.payAmount) || 0;
        if (payAmount < totalsPreview.total) {
            DevExpress.ui.notify(t("pos.terminal.payFullRequired"), "warning", 3200);
            return;
        }

        const isCash = isCashPaymentMethodId(data.paymentMethodId, cashMethodId);
        if (!isCash && !data.bankId) {
            DevExpress.ui.notify(t("pos.terminal.bankRequired"), "warning", 2800);
            return;
        }

        const useOrderGuard = SG && orderSubmitGuard;
        if (useOrderGuard && !orderSubmitGuard.begin()) {
            return;
        }
        if (useOrderGuard) {
            SG.setButtonDisabled(checkoutButton, true);
        }

        const paymentLines = buildPosPaymentLinesFromForm(data, totalsPreview.total) || [];

        const body = {
            outletId: catalog.outlet.outletId,
            notes: data.notes,
            discountAmount: totalsPreview.discount,
            payments: paymentLines.length ? paymentLines : null,
            lines: buildOrderLinesPayload()
        };

        hideLoadPanel();
        try {
            popupInstance.hide();
        } catch {
            /* popup may already be closing */
        }

        withLoad(
            $.when(pos.createOrder(body))
                .done((order) => {
                    DevExpress.ui.notify(
                        t("pos.terminal.orderCreated").replace("{0}", order.orderNo || ""),
                        "success",
                        2200
                    );
                    window.location.href = "/pos-orders.html";
                })
                .fail((xhr) => {
                    const msg = localizePosError(xhr && xhr.responseJSON && xhr.responseJSON.message);
                    DevExpress.ui.notify(msg, "error", 3200);
                })
                .always(() => {
                    if (useOrderGuard) {
                        orderSubmitGuard.end();
                        SG.setButtonDisabled(checkoutButton, false);
                    }
                })
        );
    }

    function loadTerminal(outletId) {
        return withLoad(
            pos.getPosMenu(outletId).then((data) => {
                if (!isOutletOpen(data && data.outlet)) {
                    setOutletInUrl(null);
                    notifyOutletClosed();
                    try {
                        $("#posBackBtn").dxButton("instance").option("visible", false);
                    } catch {
                        /* not initialized */
                    }
                    return pos.listOutlets().then(renderOutletPicker);
                }

                catalog = data;
                taxConfig = pricingTax.normalizeConfig(data && data.pricingTax);
                cart = [];
                payments = [];
                selectedCategoryId = null;
                searchText = "";
                renderTerminalShell();
                if (isEmbeddedPosShellMode()) {
                    mountEmbeddedReservationBackButton();
                    try {
                        $("#posBackBtn").dxButton("instance").option("visible", false);
                    } catch {
                        /* not initialized */
                    }
                }

                const editCtx = getEditTransferredContext();
                if (editCtx) {
                    return pos.getOrder(editCtx.orderId).then((order) => {
                        if (order) {
                            hydrateCartFromExistingOrder(order);
                        }
                    });
                }

                return null;
            })
        );
    }

    function initEmbeddedPosShell() {
        document.body.classList.add("pms-pos-embedded", "pms-pos-embedded-reservation");

        const hotelCode = new URLSearchParams(window.location.search).get("hotelCode");
        if (hotelCode) {
            api.setHotelCode(hotelCode);
        }

        loadPanel = $("#posLoadPanel")
            .dxLoadPanel({
                shading: false,
                showIndicator: true,
                showPane: true,
                visible: false,
                position: { of: ".room-board-workspace" }
            })
            .dxLoadPanel("instance");

        mountEmbeddedReservationBackButton();

        const outletId = getOutletIdFromUrl();
        if (outletId) {
            loadTerminal(outletId);
        }
    }

    function init() {
        if (!api.hasPermission("pos.view")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (isEmbeddedPosShellMode()) {
            initEmbeddedPosShell();
            return;
        }

        loadPanel = $("#posLoadPanel")
            .dxLoadPanel({
                shading: false,
                showIndicator: true,
                showPane: true,
                visible: false,
                position: { of: ".room-board-workspace" }
            })
            .dxLoadPanel("instance");

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-pos-terminal",
            onRefresh() {
                const id = getOutletIdFromUrl();
                if (id) {
                    loadTerminal(id);
                } else {
                    withLoad(pos.listOutlets().then(renderOutletPicker));
                }
            }
        });

        $("#posBackBtn").dxButton({
            text: t("pos.terminal.backOutlets"),
            icon: "back",
            stylingMode: "text",
            type: "default",
            visible: !!getOutletIdFromUrl(),
            elementAttr: { class: "pos-back-outlet-btn" },
            onClick() {
                setOutletInUrl(null);
                catalog = null;
                cart = [];
                withLoad(pos.listOutlets().then(renderOutletPicker));
                $("#posBackBtn").dxButton("instance").option("visible", false);
            }
        });

        const outletId = getOutletIdFromUrl();
        if (outletId) {
            $("#posBackBtn").dxButton("instance").option("visible", true);
            loadTerminal(outletId);
        } else {
            withLoad(pos.listOutlets().then(renderOutletPicker));
        }
    }

    $(init);
})(window, jQuery);
