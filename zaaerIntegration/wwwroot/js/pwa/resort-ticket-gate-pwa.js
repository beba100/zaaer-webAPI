(function (window) {
    "use strict";

    let deferredInstallPrompt = null;

    function isStandalone() {
        return (
            window.matchMedia("(display-mode: standalone)").matches ||
            window.navigator.standalone === true
        );
    }

    function isIos() {
        return /iphone|ipad|ipod/i.test(window.navigator.userAgent);
    }

    function registerServiceWorker() {
        if (!("serviceWorker" in navigator)) {
            return Promise.resolve();
        }

        return navigator.serviceWorker
            .register("/pwa/gate/sw.js", { scope: "/" })
            .catch(() => {});
    }

    function showInstallBanner(t, stationCode) {
        if (isStandalone() || document.getElementById("gatePwaInstallBanner")) {
            return;
        }

        const dismissKey = stationCode
            ? `zaaer.gate.pwaInstallDismissed.${stationCode}`
            : "zaaer.gate.pwaInstallDismissed";
        const installQuery = new URLSearchParams(window.location.search).get("install");
        const forceShow = installQuery === "1";
        const dismissed = window.localStorage.getItem(dismissKey) === "1";
        if (dismissed && !forceShow && !deferredInstallPrompt) {
            return;
        }

        const $banner = $("<div/>")
            .attr("id", "gatePwaInstallBanner")
            .addClass("resort-ticket-gate__pwa-banner")
            .appendTo("body");

        const $text = $("<div/>").addClass("resort-ticket-gate__pwa-banner-text").appendTo($banner);

        if (deferredInstallPrompt) {
            $text.text(t("resortTickets.gate.pwa.installAndroid"));
            $("<button/>")
                .attr("type", "button")
                .addClass("resort-ticket-gate__pwa-banner-btn")
                .text(t("resortTickets.gate.pwa.installAction"))
                .on("click", () => {
                    deferredInstallPrompt.prompt();
                    deferredInstallPrompt.userChoice.finally(() => {
                        deferredInstallPrompt = null;
                        $banner.remove();
                    });
                })
                .appendTo($banner);
        } else if (isIos()) {
            $text.html(t("resortTickets.gate.pwa.installIos"));
        } else {
            $text.text(t("resortTickets.gate.pwa.installGeneric"));
        }

        $("<button/>")
            .attr("type", "button")
            .addClass("resort-ticket-gate__pwa-banner-dismiss")
            .text("×")
            .on("click", () => {
                window.localStorage.setItem(dismissKey, "1");
                $banner.remove();
            })
            .appendTo($banner);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ResortTicketGatePwa = {
        init(t, options) {
            const stationCode = (options && options.stationCode) || "";
            window.addEventListener("beforeinstallprompt", (event) => {
                event.preventDefault();
                deferredInstallPrompt = event;
                showInstallBanner(t, stationCode);
            });

            registerServiceWorker().then(() => {
                if (!isStandalone()) {
                    showInstallBanner(t, stationCode);
                }
            });
        }
    };
})(window);
