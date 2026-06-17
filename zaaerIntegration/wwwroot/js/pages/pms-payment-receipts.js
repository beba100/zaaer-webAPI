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

    function canList() {
        const p = window.Zaaer && window.Zaaer.PmsRbacPolicy;
        return p && typeof p.has === "function" && p.has("payments.list");
    }

    function canCreate() {
        const p = window.Zaaer && window.Zaaer.PmsRbacPolicy;
        return p && typeof p.canPaymentCreate === "function" && p.canPaymentCreate();
    }

    function loadReceipts(reservationId) {
        return window.Zaaer.ReservationDetailService.loadPaymentRows({
            reservationId,
            kind: "receipts"
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

        window.Zaaer.PmsAdminShell.init({ navKey: "nav-payment-receipts" });
        $("[data-i18n]").each(function () {
            $(this).text(t($(this).attr("data-i18n")));
        });
        $("#adminRefreshButton").hide();

        let reservationIdInst;
        let grid;

        $("#paymentsReservationId").dxNumberBox({
            label: t("pms.payments.reservationId"),
            labelMode: "floating",
            min: 1,
            showSpinButtons: true,
            width: 220,
            value: Number(new URLSearchParams(window.location.search).get("reservationId")) || null
        });
        reservationIdInst = $("#paymentsReservationId").dxNumberBox("instance");

        function refreshGrid() {
            const rid = Number(reservationIdInst.option("value"));
            if (!Number.isFinite(rid) || rid <= 0) {
                DevExpress.ui.notify(t("pms.payments.reservationRequired"), "warning", 2800);
                return;
            }

            grid.beginCustomLoading("");
            loadReceipts(rid)
                .then((rows) => {
                    grid.option("dataSource", rows);
                })
                .catch(() => DevExpress.ui.notify(t("common.error"), "error", 3200))
                .always(() => grid.endCustomLoading());
        }

        $("#paymentsLoadBtn").dxButton({
            text: t("pms.payments.load"),
            type: "default",
            stylingMode: "contained",
            icon: "find",
            onClick: refreshGrid
        });

        $("#paymentsGridRefresh").dxButton({
            icon: "refresh",
            hint: t("common.refresh"),
            stylingMode: "text",
            type: "default",
            elementAttr: { class: "pms-admin-grid-refresh-btn" },
            onClick: refreshGrid
        });

        const po = window.Zaaer.PmsGridOptions;
        grid = $("#paymentsGrid")
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: [],
                keyExpr: "id",
                height: "calc(100vh - 300px)",
                columns: [
                    { dataField: "zaaerId", caption: "ID", width: 72 },
                    { dataField: "receiptNo", caption: t("pms.payments.receiptNo"), minWidth: 120 },
                    { dataField: "receiptType", caption: t("pms.payments.type"), width: 100 },
                    { dataField: "amount", caption: t("pms.payments.amount"), dataType: "number", format: "#,##0.00" },
                    { dataField: "receiptStatus", caption: t("pms.payments.status"), width: 100 },
                    { dataField: "paymentMethod", caption: t("pms.payments.method"), minWidth: 100 }
                ],
                onToolbarPreparing(e) {
                    e.toolbarOptions.visible = false;
                }
                })
            )
            .dxDataGrid("instance");

        window.__pmsPaymentsGrid = grid;

        if (reservationIdInst.option("value")) {
            refreshGrid();
        }

        if (canCreate()) {
            /* future: open create receipt wizard */
        }
    });
})(window, jQuery);
