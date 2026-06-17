(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    loc.init();
    const t = loc.t;

    const formData = {
        username: "",
        password: ""
    };

    function returnUrl() {
        return new URLSearchParams(window.location.search).get("returnUrl") || "";
    }

    function navigateAfterAuth() {
        const api = window.Zaaer.ApiService;
        const requested = returnUrl();
        const target = api.resolveLandingUrl
            ? api.resolveLandingUrl(requested)
            : requested || "/room-board.html";
        window.location.href = target;
    }

    $(function () {
        if (window.Zaaer.ApiService.getToken()) {
            const pending = window.Zaaer.ApiService.ensurePermissionsReady
                ? window.Zaaer.ApiService.ensurePermissionsReady()
                : $.when();
            pending.always(navigateAfterAuth);
            return;
        }

        if (window.Zaaer.PmsTopChrome && window.Zaaer.PmsTopChrome.initLoginTopBar) {
            window.Zaaer.PmsTopChrome.initLoginTopBar();
        }

        $("#loginHeroTitle").text(t("auth.heroTitle"));
        $("#loginHeroSubtitle").text(t("auth.heroSubtitle"));
        $("#loginTitle").text(t("auth.loginTitle"));

        const loadPanel = $("#loginLoadPanel").dxLoadPanel({
            shading: true,
            visible: false,
            showIndicator: true,
            message: t("auth.signingIn")
        }).dxLoadPanel("instance");

        const form = $("#loginForm").dxForm({
            formData,
            labelLocation: "top",
            rtlEnabled: document.documentElement.dir === "rtl",
            items: [
                {
                    dataField: "username",
                    label: { text: t("auth.username") },
                    isRequired: true,
                    editorOptions: {
                        stylingMode: "outlined",
                        inputAttr: { autocomplete: "username" }
                    }
                },
                {
                    dataField: "password",
                    label: { text: t("auth.password") },
                    isRequired: true,
                    editorOptions: {
                        mode: "password",
                        stylingMode: "outlined",
                        inputAttr: { autocomplete: "current-password" },
                        onEnterKey: submitLogin
                    }
                }
            ]
        }).dxForm("instance");

        $("#loginButton").dxButton({
            text: t("auth.loginButton"),
            type: "default",
            stylingMode: "contained",
            width: "100%",
            height: 44,
            onClick: submitLogin
        });

        function submitLogin() {
            const result = form.validate();
            if (!result.isValid) {
                return;
            }

            loadPanel.show();
            const data = form.option("formData");

            window.Zaaer.ApiService.login({
                username: data.username,
                password: data.password
            })
                .done((response) => {
                    if (response && response.tenantCode) {
                        window.Zaaer.ApiService.setHotelCode(response.tenantCode);
                    }
                    navigateAfterAuth();
                })
                .fail((xhr) => {
                    const body = xhr.responseJSON || {};
                    const error = body.error || body.message;
                    DevExpress.ui.notify(error || t("auth.loginFailed"), "error", 3200);
                })
                .always(() => loadPanel.hide());
        }
    });
})(window, jQuery);
