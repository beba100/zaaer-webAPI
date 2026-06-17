(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const prop = window.Zaaer.PropertySettingsService;

    let panel;
    let menuToggleButton;

    function t(key) {
        return loc.t(key);
    }

    function isSidebarCollapsed() {
        return $(".room-board-shell").hasClass("room-board-shell--nav-collapsed");
    }

    function syncMenuToggleButton() {
        if (!menuToggleButton) {
            return;
        }
        const collapsed = isSidebarCollapsed();
        menuToggleButton.option({
            icon: collapsed ? "menu" : "hidepanel",
            hint: collapsed ? t("resortTickets.quickNav.showMenu") : t("resortTickets.quickNav.hideMenu")
        });
    }

    function toggleCashierSidebar() {
        const $shell = $(".room-board-shell");
        const collapsed = $shell.toggleClass("room-board-shell--nav-collapsed").hasClass(
            "room-board-shell--nav-collapsed"
        );
        $("#roomBoardNavToggle").attr("aria-expanded", collapsed ? "false" : "true");
        syncMenuToggleButton();
    }

    function applyCashierWorkspace() {
        document.body.classList.add("resort-ticket-cashier-mode");
        $(".room-board-shell").addClass("room-board-shell--nav-collapsed");
        $("#roomBoardNavToggle").attr("aria-expanded", "false");
        syncMenuToggleButton();
    }

    function initQuickNav() {
        const $host = $("#resortTicketCashierQuickNav");
        if (!$host.length) {
            return;
        }

        $("<div/>")
            .appendTo($host)
            .dxButton({
                icon: "home",
                stylingMode: "outlined",
                type: "default",
                hint: t("resortTickets.quickNav.roomBoard"),
                onClick() {
                    window.location.href = "room-board.html";
                }
            });

        $("<div/>")
            .appendTo($host)
            .dxButton({
                icon: "menu",
                stylingMode: "outlined",
                type: "default",
                hint: t("resortTickets.quickNav.showMenu"),
                onInitialized(e) {
                    menuToggleButton = e.component;
                    syncMenuToggleButton();
                },
                onClick: toggleCashierSidebar
            });
    }

    function refreshPanel() {
        if (panel && panel.reload) {
            return panel.reload();
        }
        return $.Deferred().resolve().promise();
    }

    $(function () {
        window.Zaaer.PmsAdminShell.init({ onRefresh: refreshPanel });
        initQuickNav();
        applyCashierWorkspace();

        if (prop && prop.getLookups) {
            prop.getLookups().then((data) => {
                if (!data || !data.isResort) {
                    DevExpress.ui.notify(t("resortTickets.resortOnly"), "warning", 4000);
                }
            });
        }

        panel = window.Zaaer.ResortTicketSalesPanel.mount("#resortTicketCashierHost", {
            t,
            showOrders: true,
            posLayout: true
        });
    });
})(window, jQuery);
