(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer && window.Zaaer.ApiService;
    const ticketService = window.Zaaer && window.Zaaer.ResortTicketService;
    const stationUtil = window.Zaaer && window.Zaaer.ResortTicketGateStation;

    function t(key) {
        return loc.t(key);
    }

    function can(code) {
        return api && api.hasPermission(code);
    }

    $(function () {
        window.Zaaer.PmsAdminShell.init();

        const ensure =
            api && api.ensurePermissionsReady ? api.ensurePermissionsReady() : $.when();
        ensure.always(() => {
            if (!can("resort_tickets.validate")) {
                DevExpress.ui.notify(t("common.forbidden") || t("common.error"), "error", 4000);
                return;
            }

            const stationCode = stationUtil ? stationUtil.readStationCode() : "";
            const mountOptions = { mode: "staff", t };

            if (!stationCode) {
                window.Zaaer.ResortTicketScannerPanel.mount("#resortTicketScannerHost", mountOptions);
                return;
            }

            const finish = (stationLabel) => {
                mountOptions.stationCode = stationCode;
                mountOptions.stationLabel = stationLabel || stationCode;
                window.Zaaer.ResortTicketScannerPanel.mount("#resortTicketScannerHost", mountOptions);
            };

            if (ticketService && ticketService.listTypes) {
                ticketService
                    .listTypes()
                    .then((types) => {
                        finish(
                            stationUtil.resolveStationLabel(
                                stationCode,
                                types,
                                t,
                                loc.currentCulture && loc.currentCulture() === "ar"
                            )
                        );
                    })
                    .catch(() => finish(stationCode));
                return;
            }

            finish(stationCode);
        });
    });
})(window, jQuery);
