(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer.ApiService;

    function t(key) {
        return loc.t(key);
    }

    function unwrap(res) {
        if (res && res.data !== undefined) return res.data;
        return res;
    }

    const SG = window.Zaaer && window.Zaaer.SaveGuard;

    function runIntegrationPopupSave(guard, saveButton, $popupHost, saveWork) {
        const work = () =>
            Promise.resolve(saveWork()).then((result) => {
                if (SG) {
                    SG.closePopupThenRun($popupHost);
                } else {
                    $popupHost.dxPopup("instance").hide();
                }
                return result;
            });

        if (!SG || !guard) {
            return work();
        }

        const ran = SG.run(guard, work, { button: saveButton });
        return ran === false ? false : ran;
    }

    function renderDetails(data) {
        const active = data && data.isActive;
        const rows = [
            {
                label: t("integrations.ntmp.status"),
                html: `<span class="pms-integration-status-badge ${active ? "pms-integration-status-badge--active" : "pms-integration-status-badge--inactive"}">${active ? t("integrations.ntmp.active") : t("integrations.ntmp.inactive")}</span>`
            },
            { label: t("integrations.shomoos.userId"), value: data?.userId || "—" },
            { label: t("integrations.shomoos.branchCode"), value: data?.branchCode || "—" },
            { label: t("integrations.shomoos.branchSecret"), value: data?.branchSecret ? "******" : "—" },
            { label: t("integrations.shomoos.language"), value: data?.languageCode || "—" }
        ];
        const $host = $("#shomoosDetailsView").empty();
        rows.forEach((r) => {
            const $cell = $("<div/>");
            $cell.append($("<span/>", { class: "pms-integration-kv-label", text: r.label }));
            if (r.html) {
                $cell.append($("<div/>", { class: "pms-integration-kv-value" }).html(r.html));
            } else {
                $cell.append($("<div/>", { class: "pms-integration-kv-value", text: r.value }));
            }
            $host.append($cell);
        });
    }

    function openEditPopup(current) {
        const $host = $("#shomoosEditPopupHost").empty();
        const $popup = $("<div/>").appendTo($host);
        let formInstance;
        let saveButton;
        const editSaveGuard = SG ? SG.create() : null;

        $popup.dxPopup({
            title: t("integrations.ntmp.edit"),
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            visible: true,
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate() {
                const $form = $("<div/>");
                $form.dxForm({
                    formData: {
                        isActive: current?.isActive !== false,
                        userId: current?.userId || "",
                        branchCode: current?.branchCode || "",
                        branchSecret: current?.branchSecret || "",
                        languageCode: current?.languageCode || "ar"
                    },
                    labelLocation: "top",
                    items: [
                        { dataField: "isActive", editorType: "dxSwitch", label: { text: t("integrations.ntmp.status") } },
                        { dataField: "userId", label: { text: t("integrations.shomoos.userId") } },
                        { dataField: "branchCode", label: { text: t("integrations.shomoos.branchCode") } },
                        { dataField: "branchSecret", label: { text: t("integrations.shomoos.branchSecret") } },
                        { dataField: "languageCode", label: { text: t("integrations.shomoos.language") } }
                    ],
                    onInitialized(e) {
                        formInstance = e.component;
                    }
                });
                return $form;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("common.cancel"),
                        onClick() {
                            $popup.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    toolbar: "bottom",
                    location: "after",
                    options: {
                        text: t("integrations.common.save"),
                        type: "default",
                        onInitialized(e) {
                            saveButton = e.component;
                        },
                        onClick() {
                            const ran = runIntegrationPopupSave(editSaveGuard, saveButton, $popup, () =>
                                api
                                    .put("/api/v1/pms/integrations/shomoos", formInstance.option("formData"))
                                    .then((res) => {
                                        const data = unwrap(res);
                                        window.__shomoosSettings = data;
                                        renderDetails(data);
                                        DevExpress.ui.notify(t("integrations.ntmp.saved"), "success", 2500);
                                        return data;
                                    })
                            );
                            if (ran === false) {
                                return;
                            }
                            if (ran && typeof ran.catch === "function") {
                                ran.catch((err) => {
                                    const msg =
                                        (err &&
                                            err.responseJSON &&
                                            (err.responseJSON.message || err.responseJSON.error)) ||
                                        err?.message ||
                                        t("common.error");
                                    DevExpress.ui.notify(msg, "error", 4000);
                                });
                            }
                        }
                    }
                }
            ]
        });
    }

    function loadSettings() {
        return api.get("/api/v1/pms/integrations/shomoos").then((res) => {
            const data = unwrap(res);
            window.__shomoosSettings = data;
            renderDetails(data);
        });
    }

    $(function () {
        loc.init();
        window.Zaaer.PmsAdminShell.init({ navKey: "nav-integrations-shomoos", onRefresh: loadSettings });
        $("#btnShomoosEdit").dxButton({
            text: t("integrations.ntmp.edit"),
            type: "default",
            stylingMode: "contained",
            onClick() {
                openEditPopup(window.__shomoosSettings);
            }
        });
        loadSettings();
    });
})(window, jQuery);
