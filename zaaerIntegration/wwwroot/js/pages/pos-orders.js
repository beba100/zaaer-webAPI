(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const pos = window.Zaaer.PosService;
    const api = window.Zaaer.ApiService;

    let gridInstance;
    let loadPanel;
    let paymentMethods = [];

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    /** Date-only for API (KSA local calendar day — no UTC shift). */
    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return null;
        }

        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
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

    function isCashPaymentMethodId(paymentMethodId, cashMethodId) {
        if (paymentMethodId == null) {
            return true;
        }

        return Number(paymentMethodId) === Number(cashMethodId);
    }

    let banks = [];
    let cashMethodId = null;

    function fmtMoney(n) {
        return DevExpress.localization.formatNumber(Number(n) || 0, "#,##0.00");
    }

    function formatOrderDateTime(row) {
        if (!row) {
            return "";
        }

        const d = row.orderDate || row.createdAt;
        if (!d) {
            return "";
        }

        const date = new Date(d);
        const time = row.orderTime || "";
        const datePart = new Intl.DateTimeFormat("en-GB", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric"
        }).format(date);
        return time ? `${time} ${datePart}` : datePart;
    }

    function normalizeOrderStatus(status) {
        return (status || "").trim().toLowerCase().replace(/[\s-]+/g, "_");
    }

    function isTransferredOrder(row) {
        const s = normalizeOrderStatus(row && (row.orderStatus || row.paymentStatus));
        return s === "transferred_to_reservation";
    }

    function statusLabel(status) {
        const s = normalizeOrderStatus(status);
        if (s === "paid" || s === "completed") {
            return t("pos.orders.statusPaid");
        }

        if (s === "cancelled") {
            return t("pos.orders.statusCancelled");
        }

        if (s === "transferred_to_reservation") {
            return t("pos.orders.statusTransferred");
        }

        return status || "";
    }

    function statusBadgeClass(status) {
        const s = normalizeOrderStatus(status);
        if (s === "paid" || s === "completed") {
            return "pos-status-badge pos-status-badge--paid";
        }

        if (s === "cancelled") {
            return "pos-status-badge pos-status-badge--cancelled";
        }

        if (s === "transferred_to_reservation") {
            return "pos-status-badge pos-status-badge--transferred";
        }

        return "pos-status-badge";
    }

    function reservationDetailUrl(reservationId) {
        if (!reservationId) {
            return "";
        }

        const params = new URLSearchParams();
        params.set("id", String(reservationId));
        const hc = api.getHotelCode();
        if (hc) {
            params.set("hotelCode", hc);
        }

        return `/reservation-detail.html?${params.toString()}`;
    }

    function resolveOrderUser(row) {
        if (!row) {
            return "";
        }

        if (isAr()) {
            const first = row.createdByFirstName || "";
            const last = row.createdByLastName || "";
            const full = `${first} ${last}`.trim();
            if (full) {
                return full;
            }

            return row.createdByName || row.createdByUsername || "";
        }

        const username = (row.createdByUsername || "").trim();
        if (username) {
            return username;
        }

        return row.createdByName || "";
    }

    function formatPaymentMethod(row) {
        if (!row) {
            return "";
        }

        if (isTransferredOrder(row)) {
            return "—";
        }

        const raw = (row.paymentMethod || "").trim();
        const code = raw.toLowerCase();

        if (code === "cash") {
            return t("pos.orders.payCash");
        }

        if (code === "mada") {
            return t("pos.orders.payMada");
        }

        if (isAr() && row.paymentMethodAr) {
            return row.paymentMethodAr;
        }

        return raw;
    }

    function loadGrid() {
        return pos.listOrders({ take: 200 }).then((rows) => {
            gridInstance.option("dataSource", rows || []);
        });
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

    function syncPosReceiptBankFields(formInstance, paymentMethodId) {
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

    function openReceiptEditor(row) {
        if (!row || !row.receiptId) {
            return;
        }

        if (!api.hasPermission("pos.orders.receipt_edit")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        hideLoadPanel();

        const $popup = $("#posReceiptEditPopup");
        disposePosPopup($popup);

        const $formHost = $("<div>").attr("id", "posReceiptEditForm");
        let formInstance;
        let popupInstance;

        const initialDate = row.orderDate ? new Date(row.orderDate) : new Date(row.createdAt);
        cashMethodId = resolveCashPaymentMethodId(paymentMethods);
        const initialPmId = row.paymentMethodId || cashMethodId;

        popupInstance = $popup
            .dxPopup(
                posPopupBaseOptions(t("pos.orders.editReceipt"), {
                    contentTemplate() {
                        return $formHost;
                    },
                    onShown() {
                        formInstance = $formHost
                            .dxForm({
                                formData: {
                                    receiptDate: initialDate,
                                    paymentMethodId: initialPmId,
                                    bankId: row.receiptBankId || (banks[0] && banks[0].id) || null,
                                    transactionNo: row.receiptTransactionNo || ""
                                },
                                labelLocation: "top",
                                colCount: 1,
                                items: [
                                    {
                                        dataField: "receiptDate",
                                        label: { text: t("pos.orders.receiptDate") },
                                        editorType: "dxDateBox",
                                        editorOptions: {
                                            type: "date",
                                            openOnFieldClick: true,
                                            displayFormat: "dd/MM/yyyy"
                                        }
                                    },
                                    {
                                        dataField: "paymentMethodId",
                                        label: { text: t("pos.terminal.paymentMethod") },
                                        editorType: "dxSelectBox",
                                        editorOptions: {
                                            items: paymentMethods,
                                            valueExpr: "id",
                                            displayExpr: paymentMethodLabel,
                                            onValueChanged(e) {
                                                syncPosReceiptBankFields(formInstance, e.value);
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
                                ]
                            })
                            .dxForm("instance");

                        syncPosReceiptBankFields(formInstance, initialPmId);
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
                                text: t("common.save"),
                                type: "default",
                                stylingMode: "contained",
                                onClick() {
                                    const data = formInstance.option("formData");
                                    const isCash = isCashPaymentMethodId(data.paymentMethodId, cashMethodId);
                                    if (!isCash && !data.bankId) {
                                        DevExpress.ui.notify(
                                            t("pos.terminal.bankRequired"),
                                            "warning",
                                            2800
                                        );
                                        return;
                                    }

                                    hideLoadPanel();
                                    withLoad(
                                        pos
                                            .updateOrderReceipt(row.orderId, {
                                                receiptDate: formatLocalDateParam(data.receiptDate),
                                                paymentMethodId: data.paymentMethodId,
                                                bankId: isCash ? null : data.bankId,
                                                transactionNo: isCash
                                                    ? ""
                                                    : (data.transactionNo || "").trim()
                                            })
                                            .then(() => {
                                                popupInstance.hide();
                                                DevExpress.ui.notify(t("pos.settings.saved"), "success", 2200);
                                                return loadGrid();
                                            })
                                            .catch((xhr) => {
                                                const msg =
                                                    (xhr &&
                                                        xhr.responseJSON &&
                                                        xhr.responseJSON.message) ||
                                                    t("common.error");
                                                DevExpress.ui.notify(msg, "error", 3200);
                                            })
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
    }

    function localizePosOrdersError(message) {
        const s = (message || "").trim();
        if (!s) {
            return t("common.error");
        }

        if (s === "pos.orders.reservationCheckedOut" || s.includes("reservationCheckedOut")) {
            return t("pos.orders.reservationCheckedOut");
        }

        const tr = t(s);
        if (tr && tr !== s) {
            return tr;
        }

        return s;
    }

    function buildTransferredOrderEditUrl(row) {
        const params = new URLSearchParams({
            outletId: String(row.outletId),
            orderId: String(row.orderId),
            orderNo: row.orderNo || "",
            embedded: "edit-transferred"
        });

        if (row.reservationId) {
            params.set("reservationId", String(row.reservationId));
        }

        if (row.reservationNo) {
            params.set("reservationNo", String(row.reservationNo));
        }

        const hotelCode = api.getHotelCode();
        if (hotelCode) {
            params.set("hotelCode", hotelCode);
        }

        return `/pos.html?${params.toString()}`;
    }

    function hideTransferredOrderEditPopup() {
        const $popup = $("#posOrderEditPopup");
        try {
            const inst = $popup.dxPopup("instance");
            if (inst) {
                inst.hide();
            }
        } catch {
            /* not open */
        }
    }

    function openTransferredOrderEditor(row) {
        if (!row || !row.canEditTransferred) {
            return;
        }

        if (!api.hasPermission("pos.orders.create")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (!row.outletId) {
            DevExpress.ui.notify(t("common.error"), "error", 3200);
            return;
        }

        let $popupHost = $("#posOrderEditPopup");
        if (!$popupHost.length) {
            $popupHost = $("<div>").attr("id", "posOrderEditPopup").appendTo("body");
        }

        const title = t("pos.orders.editTransferredTitle").replace("{0}", row.orderNo || "");
        const resNo = (row.reservationNo || "").trim();
        const fullTitle = resNo ? `${title} — ${resNo}` : title;

        $popupHost.dxPopup({
            fullScreen: true,
            title: fullTitle,
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: false,
            dragEnabled: false,
            wrapperAttr: { class: "res-pos-embed-popup" },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "top",
                    location: "before",
                    options: {
                        icon: "back",
                        stylingMode: "text",
                        type: "default",
                        hint: t("pos.orders.backToOrders"),
                        text: t("pos.orders.backToOrders"),
                        onClick() {
                            hideTransferredOrderEditPopup();
                        }
                    }
                }
            ],
            contentTemplate(contentElem) {
                const $shell = $("<div>").addClass("res-pos-embed-shell").appendTo($(contentElem).empty());
                $("<div>")
                    .addClass("res-pos-embed-loading")
                    .append($("<div>").addClass("res-pos-embed-loading__spinner"))
                    .appendTo($shell);

                const $frame = $("<iframe>")
                    .addClass("res-pos-embed-frame res-pos-embed-frame--loading")
                    .attr({ title: title })
                    .on("load", function onPosEditFrameLoad() {
                        $shell.find(".res-pos-embed-loading").remove();
                        $(this).removeClass("res-pos-embed-frame--loading");
                    })
                    .appendTo($shell);

                window.setTimeout(() => {
                    $frame.attr("src", buildTransferredOrderEditUrl(row));
                }, 0);
            },
            onHidden() {
                try {
                    $popupHost.dxPopup("instance").dispose();
                } catch {
                    /* not initialized */
                }

                $popupHost.remove();
            }
        });
    }

    function initPosOrdersMessageListener() {
        if (window.__posOrdersMsgBound) {
            return;
        }

        window.__posOrdersMsgBound = true;
        window.addEventListener("message", (ev) => {
            if (!ev.data || ev.origin !== window.location.origin) {
                return;
            }

            if (ev.data.type === "pos-order-edit-close") {
                hideTransferredOrderEditPopup();
                return;
            }

            if (ev.data.type === "pos-transferred-order-updated") {
                hideTransferredOrderEditPopup();
                loadGrid().then(() => {
                    const orderNo = ev.data.orderNo || "";
                    DevExpress.ui.notify(
                        t("pos.orders.transferredUpdated").replace("{0}", orderNo),
                        "success",
                        2800
                    );
                });
            }
        });
    }

    function confirmCancel(row) {
        if (!row || !row.canCancel) {
            if (row && isTransferredOrder(row)) {
                DevExpress.ui.notify(t("pos.orders.reservationCheckedOut"), "warning", 3200);
            }

            return;
        }

        if (!api.hasPermission("pos.orders.cancel")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        const confirmKey = isTransferredOrder(row)
            ? "pos.orders.cancelTransferredConfirm"
            : "pos.orders.cancelConfirm";
        DevExpress.ui.dialog.confirm(t(confirmKey), t("pos.orders.cancelTitle")).done((ok) => {
            if (!ok) {
                return;
            }

            withLoad(
                pos
                    .cancelOrder(row.orderId)
                    .then(() => {
                        DevExpress.ui.notify(t("pos.orders.cancelled"), "success", 2600);
                        return loadGrid();
                    })
                    .catch((xhr) => {
                        const msg = localizePosOrdersError(
                            xhr && xhr.responseJSON && xhr.responseJSON.message
                        );
                        DevExpress.ui.notify(msg, "error", 3200);
                    })
            );
        });
    }

    function initGrid() {
        const po = window.Zaaer.PmsGridOptions;
        gridInstance = $("#posOrdersGrid")
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: [],
                searchPanel: po.searchPanelOptions({ width: 280 }),
                paging: { pageSize: 50 },
                pager: {
                    visible: true,
                    showPageSizeSelector: true,
                    allowedPageSizes: [10, 20, 50, 100],
                    showInfo: true,
                    showNavigationButtons: true
                },
                columns: [
                    {
                        dataField: "orderNo",
                        caption: t("pos.orders.colOrderNo"),
                        width: 110
                    },
                    {
                        dataField: "displayAmount",
                        caption: t("pos.orders.colAmount"),
                        width: 100,
                        alignment: "right",
                        cellTemplate(container, info) {
                            const v = Number(info.value) || 0;
                            const $cell = $("<span>").text(fmtMoney(Math.abs(v)));
                            if (v < 0) {
                                $cell.addClass("pos-orders-amount--negative").text(`-${fmtMoney(Math.abs(v))}`);
                            }

                            $cell.appendTo(container);
                        }
                    },
                    {
                        caption: t("pos.settings.outlet"),
                        minWidth: 120,
                        calculateCellValue: (r) => label(r, "outletName", "outletNameAr")
                    },
                    {
                        caption: t("pos.orders.colUser"),
                        width: 96,
                        calculateCellValue: resolveOrderUser
                    },
                    {
                        caption: t("pos.orders.colDateTime"),
                        width: 118,
                        calculateCellValue: formatOrderDateTime
                    },
                    {
                        dataField: "orderStatus",
                        caption: t("pos.orders.colStatus"),
                        minWidth: 128,
                        alignment: "center",
                        cellTemplate(container, info) {
                            const status = info.data && info.data.orderStatus;
                            $("<span>")
                                .addClass(statusBadgeClass(status))
                                .text(statusLabel(status))
                                .appendTo(container);
                        }
                    },
                    {
                        dataField: "reservationNo",
                        caption: t("pos.orders.colReservationNo"),
                        width: 108,
                        cellTemplate(container, info) {
                            const row = info.data || {};
                            const resNo = (row.reservationNo || "").trim();
                            const resId = row.reservationId;
                            if (!resNo || !resId) {
                                $("<span>").text("—").appendTo(container);
                                return;
                            }

                            const href = reservationDetailUrl(resId);
                            $("<a>")
                                .addClass("pos-orders-reservation-link")
                                .attr("href", href)
                                .text(resNo)
                                .appendTo(container);
                        }
                    },
                    {
                        caption: t("pos.terminal.paymentMethod"),
                        width: 78,
                        calculateCellValue: formatPaymentMethod
                    },
                    {
                        type: "buttons",
                        caption: t("pos.orders.colActions"),
                        width: 148,
                        buttons: [
                            {
                                hint: t("pos.orders.editTransferred"),
                                icon: "edit",
                                visible: (e) => !!e.row.data.canEditTransferred,
                                onClick(e) {
                                    window.setTimeout(() => openTransferredOrderEditor(e.row.data), 0);
                                }
                            },
                            {
                                hint: t("pos.orders.editReceipt"),
                                icon: "money",
                                visible: (e) => !!e.row.data.canEditReceipt,
                                onClick(e) {
                                    window.setTimeout(() => openReceiptEditor(e.row.data), 0);
                                }
                            },
                            {
                                hint: t("pos.orders.cancelOrder"),
                                icon: "trash",
                                visible: (e) => !!e.row.data.canCancel,
                                onClick(e) {
                                    confirmCancel(e.row.data);
                                }
                            }
                        ]
                    }
                ]
                })
            )
            .dxDataGrid("instance");

        window.__posOrdersGrid = gridInstance;
    }

    function init() {
        if (!api.hasPermission("pos.view")) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        loadPanel = $("#posOrdersLoadPanel")
            .dxLoadPanel({
                shading: false,
                showIndicator: true,
                showPane: true,
                visible: false,
                position: { of: ".room-board-workspace" }
            })
            .dxLoadPanel("instance");

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-pos-orders",
            onRefresh() {
                withLoad(loadGrid());
            }
        });

        initGrid();
        initPosOrdersMessageListener();
        withLoad(
            Promise.all([
                pos.getPaymentMethods(),
                api.get("/api/v1/pms/lookups/banks").then((r) => (r && r.data !== undefined ? r.data : r)),
                loadGrid()
            ]).then(([methods, bankRows]) => {
                paymentMethods = methods || [];
                banks = bankRows || [];
                cashMethodId = resolveCashPaymentMethodId(paymentMethods);
            })
        );
    }

    $(init);
})(window, jQuery);
