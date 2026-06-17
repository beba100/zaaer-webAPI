(function (window, $) {
    "use strict";

    function t(key) {
        return window.Zaaer.LocalizationService.t(key);
    }

    function displayUserName() {
        const api = window.Zaaer.ApiService;
        if (api.getLocalizedUserDisplayName) {
            const label = api.getLocalizedUserDisplayName();
            if (label) {
                return label;
            }
        }

        const payload = api.decodeTokenPayload();
        if (!payload) {
            return t("userMenu.guest");
        }

        return payload.username || payload.Username || t("userMenu.account");
    }

    function logout() {
        const api = window.Zaaer.ApiService;
        const finish = function () {
            api.clearToken();
            try {
                window.localStorage.removeItem(api.storageKeys.hotelCode);
                window.localStorage.removeItem(api.storageKeys.hotelName);
                window.localStorage.removeItem(api.storageKeys.hotelNameEn);
                window.localStorage.removeItem(api.storageKeys.userDisplayName);
                window.localStorage.removeItem(api.storageKeys.userDisplayNameEn);
            } catch {
                /* ignore */
            }
            window.location.href = "/login.html";
        };

        if (api.logout) {
            api.logout().always(finish);
            return;
        }

        finish();
    }

    function setCulture(cultureId) {
        if (!cultureId) {
            return;
        }
        window.Zaaer.LocalizationService.setCulture(cultureId);
        window.location.reload();
    }

    function initUserAccountMenu(hostSelector) {
        const $host = $(hostSelector || "#userAccountMenu");
        if (!$host.length) {
            return;
        }

        $host.empty();
        const name = displayUserName();

        $host.dxDropDownButton({
            text: name,
            icon: "user",
            stylingMode: "outlined",
            type: "default",
            showArrowIcon: true,
            dropDownOptions: {
                width: 220,
                wrapperAttr: { class: "pms-user-menu-popup" }
            },
            items: [
                { id: "profile", text: t("userMenu.profile"), icon: "card" },
                { id: "themes", text: t("userMenu.themes"), icon: "palette" },
                { id: "lang", text: t("userMenu.language"), icon: "globe", disabled: true },
                { id: "lang-ar", text: "العربية", icon: "isblank" },
                { id: "lang-en", text: "English", icon: "isblank" },
                { id: "logout", text: t("userMenu.logout"), icon: "runner" }
            ],
            onItemClick(e) {
                const id = e.itemData && e.itemData.id;
                if (id === "profile") {
                    window.location.href = "/profile.html";
                    return;
                }
                if (id === "themes") {
                    if (window.Zaaer.PmsThemeSwitcher && typeof window.Zaaer.PmsThemeSwitcher.showThemePickerPopup === "function") {
                        window.Zaaer.PmsThemeSwitcher.showThemePickerPopup();
                    }
                    return;
                }
                if (id === "lang-ar") {
                    setCulture("ar");
                    return;
                }
                if (id === "lang-en") {
                    setCulture("en");
                    return;
                }
                if (id === "logout") {
                    logout();
                }
            }
        });
    }

    function showProfilePopup() {
        const api = window.Zaaer.ApiService;
        const $host = $("<div />").appendTo("body");

        function renderBody(contentElement, data) {
            const lines = [
                [t("rbac.users.username"), data.username || "—"],
                [t("rbac.users.employeeNumber"), data.employeeNumber || "—"],
                [t("rbac.users.email"), data.email || "—"],
                [t("rbac.users.phone"), data.phoneNumber || "—"],
                [t("rbac.users.department"), data.department || "—"]
            ];

            const $wrap = $("<div class=\"pms-profile-popup-body\" />").appendTo(contentElement);
            lines.forEach(([label, value]) => {
                $("<div class=\"pms-profile-row\" />").appendTo($wrap)
                    .append($("<span />").text(label))
                    .append($("<strong />").text(value));
            });

            const hotelName = api.getHotelName();
            const hotelCode = api.getHotelCode();
            if (hotelCode) {
                $("<div class=\"pms-profile-row\" />").appendTo($wrap)
                    .append($("<span />").text(t("roomBoard.hotelCode")))
                    .append($("<strong />").text(hotelName || hotelCode));
            }
        }

        const popupOpts = {
            title: t("userMenu.profile"),
            visible: true,
            width: Math.min(420, window.innerWidth - 24),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup pms-profile-popup" },
            onHidden: () => $host.remove(),
            contentTemplate(contentElement) {
                $("<div class=\"pms-profile-loading\" />").text(t("common.loading")).appendTo(contentElement);
            }
        };

        $host.dxPopup(popupOpts);

        api.get("/api/rbac/me").then((data) => {
            const popup = $host.dxPopup("instance");
            popup.option("contentTemplate", (el) => renderBody(el, data || {}));
            popup.repaint();
        }).catch(() => {
            const payload = api.decodeTokenPayload() || {};
            const popup = $host.dxPopup("instance");
            popup.option("contentTemplate", (el) => renderBody(el, {
                username: payload.username || payload.Username
            }));
            popup.repaint();
        });
    }

    function initLoginTopBar() {
        const $bar = $(".pms-auth-topbar");
        if (!$bar.length) {
            return;
        }

        $("[data-i18n-auth]").each(function () {
            const key = $(this).attr("data-i18n-auth");
            if (key) {
                $(this).text(t(key));
            }
        });

        const $cultureHost = $("#loginCultureSelector");
        if ($cultureHost.length && !$cultureHost.children().length) {
            $cultureHost.dxSelectBox({
                dataSource: [
                    { id: "ar", text: "العربية" },
                    { id: "en", text: "English" }
                ],
                valueExpr: "id",
                displayExpr: "text",
                value: window.Zaaer.LocalizationService.currentCulture(),
                width: 128,
                stylingMode: "outlined",
                onValueChanged(e) {
                    if (e.value) {
                        setCulture(e.value);
                    }
                }
            });
        }
    }

    const MOBILE_HOTEL_PICKER_MQ = "(max-width: 900px)";

    function headerPickerFieldWidth($host) {
        if (!$host || !$host.length) {
            return 220;
        }

        if (!$host.hasClass("pms-header-hotel-picker--breadcrumb")) {
            return 220;
        }

        if (window.matchMedia(MOBILE_HOTEL_PICKER_MQ).matches) {
            return "100%";
        }

        return 200;
    }

    function bindHeaderPickerFieldResize($host) {
        if (!$host || !$host.length) {
            return;
        }

        const sync = () => {
            try {
                const inst = $host.dxDropDownBox("instance");
                if (inst) {
                    inst.option("width", headerPickerFieldWidth($host));
                }
            } catch {
                /* widget not ready */
            }
        };

        $(window).on("resize.pmsHeaderHotelPicker orientationchange.pmsHeaderHotelPicker", sync);
        const mq = window.matchMedia(MOBILE_HOTEL_PICKER_MQ);
        if (typeof mq.addEventListener === "function") {
            mq.addEventListener("change", sync);
        } else if (typeof mq.addListener === "function") {
            mq.addListener(sync);
        }
    }

    function ensureHeaderHotelHost() {
        let $host = $("#pmsHeaderHotelPicker");
        const $bc = $(".room-board-breadcrumb").first();
        const $resHeader = $(".res-header").first();

        if ($resHeader.length) {
            if (!$host.length) {
                $host = $("<div/>", {
                    id: "pmsHeaderHotelPicker",
                    class: "pms-header-hotel-picker pms-header-hotel-picker--res-header"
                });
                const $back = $resHeader.find("#backToBoard").first();
                if ($back.length) {
                    $host.insertAfter($back);
                } else {
                    $resHeader.prepend($host);
                }
            } else if (!$host.closest(".res-header").length) {
                const $back = $resHeader.find("#backToBoard").first();
                if ($back.length) {
                    $host.insertAfter($back);
                } else {
                    $resHeader.prepend($host);
                }
            }
            return $host;
        }

        if ($bc.length) {
            let $sep = $bc.find(".room-board-bc-sep:not([hidden])").first();
            const $navToggle = $bc.find("#roomBoardNavToggle, .room-board-nav-toggle").first();

            if (!$sep.length) {
                $sep = $("<span/>", {
                    class: "dx-icon dx-icon-chevronright room-board-bc-sep",
                    "aria-hidden": "true"
                });
                if ($navToggle.length) {
                    $sep.insertAfter($navToggle);
                } else {
                    $bc.prepend($sep);
                }
            }

            if (!$host.length) {
                $host = $("<div/>", {
                    id: "pmsHeaderHotelPicker",
                    class: "pms-header-hotel-picker pms-header-hotel-picker--breadcrumb"
                });
            }

            $host
                .addClass("pms-header-hotel-picker--breadcrumb")
                .removeClass("pms-header-hotel-picker--hidden");

            if (!$host.parent().is($bc) || !$host.prev().is($sep)) {
                $host.insertAfter($sep);
            }

            return $host;
        }

        if ($host.length) {
            return $host;
        }

        const $actions = $(".room-board-top-actions").first();
        if (!$actions.length) {
            return $();
        }

        $host = $("<div/>", { id: "pmsHeaderHotelPicker", class: "pms-header-hotel-picker" });
        const $user = $actions.find("#userAccountMenu").first();
        if ($user.length) {
            $host.insertBefore($user);
        } else {
            $actions.prepend($host);
        }

        return $host;
    }

    function disposeHeaderHotelPicker($host) {
        if (!$host || !$host.length) {
            return;
        }

        try {
            const inst = $host.dxDropDownBox("instance");
            if (inst) {
                inst.dispose();
            }
        } catch {
            /* not initialized */
        }

        $host.empty();
    }

    function initHeaderHotelPicker(options) {
        const api = window.Zaaer.ApiService;
        const lookup = window.Zaaer.PmsHotelLookup;
        if (!api || !lookup || typeof lookup.createSingleHotelLookup !== "function") {
            return $.Deferred().resolve().promise();
        }

        const $host = ensureHeaderHotelHost();
        if (!$host.length) {
            return $.Deferred().resolve().promise();
        }

        const loadHotels =
            typeof options.loadHotels === "function"
                ? options.loadHotels
                : () =>
                      api.get("/api/v1/pms/room-board/hotel-codes").then((res) => {
                          const data = res && (res.data !== undefined ? res.data : res);
                          return Array.isArray(data) ? data : [];
                      });

        return $.when(loadHotels()).then((hotels) => {
            const list = hotels || [];
            const stored = api.getHotelCode() || "";
            const codes = new Set(list.map((h) => h.code || h.Code));
            let initial = stored && codes.has(stored) ? stored : list[0]?.code || list[0]?.Code || "";

            if (initial && initial !== stored) {
                api.setHotelCode(initial);
            }

            if (list.length <= 1) {
                const only = list[0];
                const name = only && (only.name || only.Name);
                if (name && `${name}`.trim()) {
                    window.localStorage.setItem(api.storageKeys.hotelName, `${name}`.trim());
                }
                const isArabic = () =>
                    window.Zaaer.LocalizationService &&
                    typeof window.Zaaer.LocalizationService.currentCulture === "function" &&
                    window.Zaaer.LocalizationService.currentCulture() === "ar";
                const label = lookup.tenantDisplayName(only, isArabic());
                if ($host.hasClass("pms-header-hotel-picker--breadcrumb")) {
                    $host.removeClass("pms-header-hotel-picker--hidden")
                        .empty()
                        .append($("<strong/>", { class: "room-board-bc-current" }).text(label));
                } else {
                    $host.addClass("pms-header-hotel-picker--hidden").empty();
                }
                $("#adminHotelBadge").prop("hidden", true);
                return;
            }

            $host.removeClass("pms-header-hotel-picker--hidden");
            disposeHeaderHotelPicker($host);

            const isArabic = () =>
                window.Zaaer.LocalizationService &&
                typeof window.Zaaer.LocalizationService.currentCulture === "function" &&
                window.Zaaer.LocalizationService.currentCulture() === "ar";

            lookup.createSingleHotelLookup($host, {
                dataSource: list,
                value: initial || null,
                isArabic,
                t,
                headerMode: true,
                width: headerPickerFieldWidth($host),
                rtlEnabled: isArabic(),
                onValueChanged(e) {
                    const v = e.value || "";
                    if (!v) {
                        return;
                    }

                    api.setHotelCode(v);
                    const row = list.find((h) => (h.code || h.Code) === v);
                    if (row) {
                        const name = (row.name || row.Name || "").trim();
                        const nameEn = (row.nameEn || row.NameEn || "").trim();
                        if (isArabic() && name) {
                            window.localStorage.setItem(api.storageKeys.hotelName, name);
                        } else if (!isArabic() && nameEn) {
                            window.localStorage.setItem(api.storageKeys.hotelNameEn, nameEn);
                        }
                    }

                    const rollbackHotelSelection = () => {
                        const prev = e.previousValue || "";
                        if (prev) {
                            api.setHotelCode(prev);
                        }
                        const picker = $host.dxDropDownBox("instance");
                        if (picker) {
                            picker.option("value", prev || null);
                        }
                    };

                    const apply = () => {
                        if (typeof options.onHotelChanged === "function") {
                            options.onHotelChanged(v, e.previousValue);
                        } else {
                            window.location.reload();
                        }
                    };

                    if (typeof api.switchHotel === "function") {
                        api.switchHotel({ hotelCode: v }).done(apply).fail(rollbackHotelSelection);
                    } else {
                        apply();
                    }
                }
            });

            bindHeaderPickerFieldResize($host);
            $("#adminHotelBadge").prop("hidden", true);
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsTopChrome = {
        initUserAccountMenu,
        initHeaderHotelPicker,
        ensureHeaderHotelHost,
        initLoginTopBar,
        logout,
        displayUserName
    };
})(window, jQuery);
