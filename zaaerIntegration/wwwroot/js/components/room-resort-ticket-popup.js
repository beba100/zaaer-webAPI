(function (window, $, DevExpress) {
    "use strict";

    function open(context, t, onRefresh) {
        const panelApi = window.Zaaer && window.Zaaer.ResortTicketSalesPanel;
        if (!panelApi) {
            DevExpress.ui.notify(t("roomBoard.resortTickets.missingModule"), "error", 3000);
            return;
        }

        const ctx = context || {};
        const $popup = $("<div/>").appendTo("body");
        const $host = $("<div/>").addClass("resort-ticket-popup").appendTo($popup);
        let panel;

        $popup.dxPopup({
            title: t("roomBoard.resortTickets.title"),
            visible: true,
            showCloseButton: true,
            width: Math.min(980, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "72vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "guest-picker-popup res-extra-popup resort-ticket-popup-wrapper" },
            contentTemplate() {
                return $host;
            },
            onShown() {
                panel = panelApi.mount($host, {
                    t,
                    context: {
                        reservationId: ctx.reservationId || (ctx.currentStay && ctx.currentStay.reservationId) || null,
                        unitId: ctx.apartmentId || ctx.unitId || null,
                        customerId: ctx.customerId || null,
                        lockReservation: !!(ctx.reservationId || (ctx.currentStay && ctx.currentStay.reservationId)),
                        lockUnit: !!(ctx.apartmentId || ctx.unitId),
                        lockCustomer: !!ctx.customerId
                    },
                    showOrders: true,
                    onRefresh
                });
            },
            onHidden() {
                if (panel && panel.destroy) {
                    panel.destroy();
                }
                $popup.remove();
            }
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.RoomResortTicketPopup = { open };
})(window, jQuery, DevExpress);
