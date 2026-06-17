(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer.ApiService;

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    function canView() {
        const p = window.Zaaer && window.Zaaer.PmsRbacPolicy;
        return p && typeof p.canReservationView === "function" && p.canReservationView();
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

    function loadRows() {
        const hc = api.getHotelCode();
        const params = {};
        if (hc) {
            params.hotelCode = hc;
        }
        return api.get("/api/v1/pms/corporate-customers/for-picker", params).then((raw) => {
            const body = unwrapPayload(raw);
            if (Array.isArray(body)) {
                return body;
            }
            if (body && Array.isArray(body.items)) {
                return body.items;
            }
            if (body && Array.isArray(body.Items)) {
                return body.Items;
            }
            return [];
        });
    }

    $(function () {
        loc.init();
        if (!api.requireToken()) {
            return;
        }

        if (!canView()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 4000);
            return;
        }

        window.Zaaer.PmsAdminShell.init({ navKey: "nav-corporate" });
        $("[data-i18n]").each(function () {
            $(this).text(t($(this).attr("data-i18n")));
        });
        $("#adminRefreshButton").hide();

        $("#corpGridRefresh").dxButton({
            icon: "refresh",
            hint: t("common.refresh"),
            stylingMode: "text",
            type: "default",
            elementAttr: { class: "pms-admin-grid-refresh-btn" },
            onClick() {
                if (window.__pmsCorporateGrid) {
                    window.__pmsCorporateGrid.refresh();
                }
            }
        });

        const po = window.Zaaer.PmsGridOptions;
        window.__pmsCorporateGrid = $("#corporateGrid")
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: new DevExpress.data.CustomStore({
                    key: "corporateId",
                    load() {
                        return loadRows();
                    }
                }),
                keyExpr: "corporateId",
                height: "calc(100vh - 248px)",
                paging: { pageSize: 50 },
                pager: po.adminPager(),
                columns: [
                    { dataField: "corporateId", caption: "ID", width: 64 },
                    { dataField: "corporateName", caption: t("reservationDetail.company.name"), minWidth: 180 },
                    { dataField: "email", caption: t("rbac.users.email"), minWidth: 140 },
                    { dataField: "corporatePhone", caption: t("rbac.users.phone"), minWidth: 120 },
                    { dataField: "contactPersonName", caption: t("reservationDetail.company.contact"), minWidth: 120 }
                ],
                onToolbarPreparing(e) {
                    e.toolbarOptions.visible = false;
                }
                })
            )
            .dxDataGrid("instance");
    });
})(window, jQuery);
