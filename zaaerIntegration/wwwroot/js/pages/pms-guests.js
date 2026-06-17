(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer.ApiService;
    const policy = () => window.Zaaer && window.Zaaer.PmsRbacPolicy;

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    function canList() {
        const p = policy();
        return p && typeof p.has === "function" && (p.has("guests.list") || p.has("guests.view"));
    }

    function canCreate() {
        const p = policy();
        return p && typeof p.canGuestCreate === "function" && p.canGuestCreate();
    }

    function canUpdate() {
        const p = policy();
        return p && typeof p.canGuestUpdate === "function" && p.canGuestUpdate();
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

    function openGuestForm(mode, customerId) {
        if (mode === "create" && !canCreate()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (mode === "edit" && !canUpdate()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        if (!window.Zaaer.GuestVisitorForm || typeof window.Zaaer.GuestVisitorForm.open !== "function") {
            return;
        }

        window.Zaaer.GuestVisitorForm.open({
            mode,
            customerId,
            hotelCode: api.getHotelCode(),
            t,
            isArabic: isAr,
            pageCtx: {},
            onDone() {
                if (window.__pmsGuestsGrid) {
                    window.__pmsGuestsGrid.refresh();
                }
            }
        });
    }

    function guestsStore() {
        return new DevExpress.data.CustomStore({
            key: "customerId",
            load(loadOptions) {
                if (!canList()) {
                    return { data: [], totalCount: 0 };
                }

                const skip = loadOptions.skip || 0;
                const take = loadOptions.take || 50;
                const pageNumber = Math.floor(skip / take) + 1;
                const searchValue =
                    loadOptions.searchValue != null && `${loadOptions.searchValue}`.trim() !== ""
                        ? `${loadOptions.searchValue}`.trim()
                        : undefined;

                return api
                    .get("/api/v1/pms/customers", {
                        pageNumber,
                        pageSize: take,
                        searchTerm: searchValue,
                        searchMode: searchValue ? "contains" : undefined
                    })
                    .then((res) => {
                        const normalized = normalizePagedCustomers(res);
                        return { data: normalized.rows, totalCount: normalized.totalCount };
                    });
            }
        });
    }

    function buildGrid() {
        const po = window.Zaaer.PmsGridOptions;
        const grid = $("#guestsGrid").dxDataGrid(
            po.merge(po.adminBaseline(), {
            dataSource: guestsStore(),
            keyExpr: "customerId",
            height: "calc(100vh - 248px)",
            remoteOperations: { paging: true, filtering: true, sorting: false },
            paging: { pageSize: 50 },
            pager: po.adminPager(),
            columns: [
                { dataField: "customerId", caption: "ID", width: 64, allowEditing: false },
                { dataField: "customerName", caption: t("reservationDetail.guest.name"), minWidth: 160 },
                { dataField: "mobileNo", caption: t("reservationDetail.guest.phone"), minWidth: 120 },
                { dataField: "email", caption: t("rbac.users.email"), minWidth: 140 },
                {
                    type: "buttons",
                    caption: t("rbac.actions"),
                    width: 56,
                    fixed: true,
                    fixedPosition: document.documentElement.dir === "rtl" ? "left" : "right",
                    buttons: [
                        {
                            hint: t("common.edit"),
                            icon: "edit",
                            visible: canUpdate(),
                            onClick(e) {
                                const id = e.row.data.customerId ?? e.row.data.CustomerId;
                                openGuestForm("edit", id);
                            }
                        }
                    ]
                }
            ],
            onToolbarPreparing(e) {
                e.toolbarOptions.visible = false;
            }
            })
        ).dxDataGrid("instance");

        window.__pmsGuestsGrid = grid;
        return grid;
    }

    function initFab(grid) {
        const $fab = $("#guestsFabAdd");
        if (!canCreate()) {
            $fab.prop("hidden", true);
            return;
        }

        $fab.prop("hidden", false).empty().dxButton({
            icon: "add",
            text: t("pms.guests.add"),
            type: "default",
            stylingMode: "contained",
            elementAttr: { class: "pms-admin-fab-btn" },
            onClick() {
                openGuestForm("create");
            }
        });
    }

    $(function () {
        loc.init();
        if (!api.requireToken()) {
            return;
        }

        if (!canList()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 4000);
            return;
        }

        window.Zaaer.PmsAdminShell.init({
            selectedNavKey: "nav-guests",
            onRefresh() {
                if (window.__pmsGuestsGrid) {
                    window.__pmsGuestsGrid.refresh();
                }
            }
        });

        $("[data-i18n]").each(function () {
            $(this).text(t($(this).attr("data-i18n")));
        });

        $("#guestsLoadPanel").dxLoadPanel({
            shadingColor: "rgba(255,255,255,0.45)",
            position: { of: ".room-board-shell" },
            visible: false
        });

        $("#guestsGridRefresh").dxButton({
            icon: "refresh",
            hint: t("common.refresh"),
            stylingMode: "text",
            type: "default",
            elementAttr: { class: "pms-admin-grid-refresh-btn" },
            onClick() {
                if (window.__pmsGuestsGrid) {
                    window.__pmsGuestsGrid.refresh();
                }
            }
        });

        $("#adminRefreshButton").hide();

        const grid = buildGrid();
        initFab(grid);

        window.addEventListener("zaaer:permissions-refreshed", () => {
            initFab(grid);
            grid.repaint();
        });
    });
})(window, jQuery);
