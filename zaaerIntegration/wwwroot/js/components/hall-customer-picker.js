/* global jQuery, DevExpress */
(function (window, $) {
    "use strict";

    const loc = () => window.Zaaer && window.Zaaer.LocalizationService;
    const api = () => window.Zaaer && window.Zaaer.ApiService;

    function t(key) {
        return loc() ? loc().t(key) : key;
    }

    function isArabic() {
        const L = loc();
        if (L && typeof L.isArabic === "function") {
            return L.isArabic();
        }
        return document.documentElement.dir === "rtl";
    }

    function canListGuests() {
        const p = window.Zaaer && window.Zaaer.PmsRbacPolicy;
        return p && typeof p.has === "function" && (p.has("guests.list") || p.has("guests.view"));
    }

    function canCreateGuest() {
        const p = window.Zaaer && window.Zaaer.PmsRbacPolicy;
        return p && typeof p.canGuestCreate === "function" && p.canGuestCreate();
    }

    function buildPmsPickerSelectColumn(onPick, hint) {
        return {
            type: "buttons",
            width: 50,
            minWidth: 50,
            fixed: true,
            fixedPosition: isArabic() ? "right" : "left",
            cssClass: "guest-picker-col-select",
            caption: "",
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            buttons: [
                {
                    hint: hint || t("reservationDetail.guest.pickVisitorHint"),
                    icon: "check",
                    cssClass: "guest-picker-select-btn",
                    onClick(e) {
                        if (e.event && typeof e.event.stopPropagation === "function") {
                            e.event.stopPropagation();
                        }
                        if (e.row && e.row.data && typeof onPick === "function") {
                            onPick(e.row.data);
                        }
                    }
                }
            ]
        };
    }

    function pmsPickerGridPagingOptions() {
        return { pageSize: 50 };
    }

    function pmsPickerGridPagerOptions() {
        return {
            visible: true,
            showPageSizeSelector: true,
            allowedPageSizes: [10, 20, 50],
            showInfo: true,
            showNavigationButtons: true,
            displayMode: "full"
        };
    }

    function unwrapPayload(raw) {
        if (!raw || typeof raw !== "object") {
            return raw;
        }
        if (raw.data !== undefined) {
            return raw.data;
        }
        if (raw.Data !== undefined) {
            return raw.Data;
        }
        return raw;
    }

    function normalizePagedCustomers(raw) {
        const body = unwrapPayload(raw) || {};
        const rows = body.customers || body.Customers || [];
        const total = body.totalCount ?? body.TotalCount ?? rows.length;
        return {
            rows: Array.isArray(rows) ? rows : [],
            totalCount: Number(total) || 0
        };
    }

    function customerStore(getSearchMode, getSearchTerm) {
        return new DevExpress.data.CustomStore({
            key: "customerId",
            load(loadOptions) {
                if (!canListGuests()) {
                    return { data: [], totalCount: 0 };
                }

                const skip = loadOptions.skip || 0;
                const take = loadOptions.take || 50;
                const pageNumber = Math.floor(skip / take) + 1;
                const term = typeof getSearchTerm === "function" ? (`${getSearchTerm() || ""}`).trim() : "";
                const mode = typeof getSearchMode === "function" ? getSearchMode() : "name";

                return api()
                    .get("/api/v1/pms/customers", {
                        pageNumber,
                        pageSize: take,
                        searchTerm: term || undefined,
                        searchMode: term ? mode : undefined
                    })
                    .then((res) => {
                        const normalized = normalizePagedCustomers(res);
                        return { data: normalized.rows, totalCount: normalized.totalCount };
                    });
            }
        });
    }

    function openCreateCustomer(onPick, onClosePicker, hotelId) {
        if (!canCreateGuest()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (!hotelId) {
            DevExpress.ui.notify(t("reservationDetail.missingHotel"), "warning", 2600);
            return;
        }

        if (!window.Zaaer.GuestVisitorForm || typeof window.Zaaer.GuestVisitorForm.open !== "function") {
            DevExpress.ui.notify(t("common.error"), "error", 3200);
            return;
        }

        window.Zaaer.GuestVisitorForm.open({
            mode: "create",
            hotelCode: api() && api().getHotelCode(),
            t,
            isArabic,
            pageCtx: { detail: { hotelId: Number(hotelId) } },
            assignGuest(id, cust) {
                const row = cust || { customerId: id };
                if (typeof onPick === "function") {
                    onPick(row);
                }
            },
            onDone() {
                if (typeof onClosePicker === "function") {
                    onClosePicker();
                }
            }
        });
    }

    function open(options) {
        const onPick = options && typeof options.onPick === "function" ? options.onPick : null;
        if (!onPick) {
            return;
        }

        if (!canListGuests()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        const $host = $("<div>").appendTo("body");

        $host.dxPopup({
            width: Math.min(1040, Math.max(360, window.innerWidth - 24)),
            height: "90vh",
            maxHeight: "90vh",
            title: t("hallOps.customer.pickTitle"),
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            wrapperAttr: { class: "guest-picker-popup guest-picker-popup--wide" },
            onShowing(e) {
                const popupInstance = e.component;
                const $content = $(popupInstance.content()).empty().addClass("guest-picker-body guest-picker-body--picker");

                let searchModeState = "name";
                const modeOrder = ["name", "id", "mobile"];
                let gridInst = null;

                function pickRow(data) {
                    if (!data) {
                        return;
                    }
                    onPick(data);
                    popupInstance.hide();
                }

                const $toolbar = $("<div>").addClass("guest-picker-toolbar").appendTo($content);

                const $rowModes = $("<div>").addClass("guest-picker-toolbar-row guest-picker-toolbar-row--modes").appendTo($toolbar);
                $("<span>").addClass("guest-picker-label").text(t("reservationDetail.guest.pickVisitorSearchBy")).appendTo($rowModes);
                const $modes = $("<div>").addClass("guest-picker-mode-buttons").appendTo($rowModes);
                const modeBtnRefs = {};

                function syncModeButtons() {
                    modeOrder.forEach((m) => {
                        modeBtnRefs[m].option({
                            stylingMode: searchModeState === m ? "contained" : "outlined",
                            type: searchModeState === m ? "default" : "normal"
                        });
                    });
                }

                modeOrder.forEach((mode) => {
                    const $b = $("<div>").appendTo($modes);
                    $b.dxButton({
                        stylingMode: searchModeState === mode ? "contained" : "outlined",
                        type: searchModeState === mode ? "default" : "normal",
                        text:
                            mode === "id"
                                ? t("reservationDetail.guest.pickVisitorModeId")
                                : mode === "mobile"
                                  ? t("reservationDetail.guest.pickVisitorModeMobile")
                                  : t("reservationDetail.guest.pickVisitorModeName"),
                        onClick() {
                            searchModeState = mode;
                            syncModeButtons();
                        }
                    });
                    modeBtnRefs[mode] = $b.dxButton("instance");
                });

                const $addCustHost = $("<div>").addClass("guest-picker-toolbar-end").appendTo($rowModes);
                $addCustHost.dxButton({
                    text: t("hallOps.customer.create"),
                    type: "default",
                    icon: "plus",
                    stylingMode: "contained",
                    visible: canCreateGuest(),
                    onClick() {
                        const hid = options && options.hotelId;
                        openCreateCustomer(onPick, () => popupInstance.hide(), hid);
                    }
                });

                const $rowFilters = $("<div>").addClass("guest-picker-toolbar-row guest-picker-toolbar-row--filters").appendTo($toolbar);
                const $txtHost = $("<div>").addClass("guest-picker-field").appendTo($rowFilters);
                $txtHost.dxTextBox({
                    width: "100%",
                    showClearButton: true,
                    placeholder: t("reservationDetail.guest.pickVisitorSearch"),
                    inputAttr: { "aria-label": t("reservationDetail.guest.pickVisitorSearch") },
                    onEnterKey() {
                        if (gridInst) {
                            gridInst.refresh();
                        }
                    }
                });
                const txtInst = $txtHost.dxTextBox("instance");

                const $actionsHost = $("<div>").addClass("guest-picker-toolbar-actions").appendTo($rowFilters);
                $actionsHost.dxButton({
                    text: t("reservationDetail.guest.pickVisitorSearch"),
                    type: "default",
                    icon: "search",
                    stylingMode: "contained",
                    onClick() {
                        if (gridInst) {
                            gridInst.refresh();
                        }
                    }
                });

                const $gridHost = $("<div>")
                    .addClass("guest-picker-grid guest-picker-grid--pl pms-grid-compact")
                    .appendTo($content);

                const po = window.Zaaer.PmsGridOptions;
                $gridHost.dxDataGrid(
                    po.merge(po.baseline(), {
                        keyExpr: "customerId",
                        height: "calc(90vh - 280px)",
                        dataSource: customerStore(() => searchModeState, () => txtInst.option("value")),
                        remoteOperations: { paging: true, filtering: false, sorting: false },
                        rowAlternationEnabled: false,
                        paging: pmsPickerGridPagingOptions(),
                        pager: pmsPickerGridPagerOptions(),
                        searchPanel: { visible: false },
                        headerFilter: { visible: true, search: { enabled: true } },
                        filterRow: { visible: false },
                        elementAttr: { class: "guest-picker-grid guest-picker-grid--pl pms-grid-compact" },
                        columns: [
                            buildPmsPickerSelectColumn(pickRow, t("hallOps.customer.pickHint")),
                            {
                                dataField: "customerName",
                                caption: t("reservationDetail.guest.name"),
                                width: 220,
                                minWidth: 180
                            },
                            {
                                dataField: "mobileNo",
                                caption: t("reservationDetail.guest.phone"),
                                minWidth: 130
                            },
                            {
                                dataField: "email",
                                caption: t("rbac.users.email"),
                                minWidth: 140
                            }
                        ]
                    })
                );

                gridInst = $gridHost.dxDataGrid("instance");
            },
            onHidden() {
                $host.remove();
            }
        });
    }

    function applyToForm(formInstance, customer) {
        const pmsCust = window.Zaaer.PmsCustomerService;
        if (!pmsCust || !formInstance) {
            return;
        }

        const id = pmsCust.reservationCustomerId(customer);
        const name = customer.customerName || customer.CustomerName || "";
        const mobile = customer.mobileNo || customer.MobileNo || "";
        const current = formInstance.option("formData") || {};
        formInstance.updateData({
            customerId: id,
            customerName: name,
            customerMobile: mobile,
            occasionOwner: name
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.HallCustomerPicker = {
        open,
        openCreateCustomer,
        applyToForm
    };
})(window, jQuery);
