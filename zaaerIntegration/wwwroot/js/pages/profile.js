(function (window, $) {
    "use strict";

    const api = window.Zaaer.ApiService;
    const countryCodes = [
        { code: "+966", label: "🇸🇦 +966" },
        { code: "+971", label: "🇦🇪 +971" },
        { code: "+20", label: "🇪🇬 +20" },
        { code: "+965", label: "🇰🇼 +965" },
        { code: "+973", label: "🇧🇭 +973" },
        { code: "+974", label: "🇶🇦 +974" },
        { code: "+968", label: "🇴🇲 +968" }
    ];

    let profileForm;
    let passwordForm;
    let profileData = {};

    function t(key) {
        return window.Zaaer.LocalizationService.t(key);
    }

    function notifyError(xhr) {
        const body = xhr && xhr.responseJSON;
        const msg = (body && (body.error || body.message)) || t("common.error");
        DevExpress.ui.notify(msg, "error", 4000);
    }

    function setAvatarDisplay(profile, imageUrl) {
        const $avatar = $("#profileAvatar");
        $avatar.empty();
        if (imageUrl) {
            $("<img />").attr({ src: imageUrl, alt: "" }).appendTo($avatar);
            return;
        }

        const initials = profile.initials || "?";
        $avatar.text(initials);
    }

    function renderReadonlyKv(profile) {
        const $kv = $("#profileReadonlyKv").empty();
        const rows = [
            [t("rbac.users.username"), profile.username],
            [t("rbac.users.employeeNumber"), profile.employeeNumber || "—"],
            [t("rbac.users.department"), profile.department || "—"]
        ];

        rows.forEach(([label, value]) => {
            $("<div />").appendTo($kv)
                .append($("<span />").text(label))
                .append($("<strong />").text(value || "—"));
        });
    }

    function syncDisplayName(profile) {
        const fullName = `${profile.firstName || ""} ${profile.lastName || ""}`.trim();
        if (fullName) {
            try {
                window.localStorage.setItem(api.storageKeys.userDisplayName, fullName);
            } catch {
                /* ignore */
            }
        }
    }

    function loadProfile() {
        return api.get("/api/rbac/profile").then((data) => {
            profileData = data || {};
            renderReadonlyKv(profileData);
            setAvatarDisplay(profileData);

            profileForm.option("formData", {
                firstName: profileData.firstName || "",
                lastName: profileData.lastName || "",
                email: profileData.email || "",
                phoneCountryCode: profileData.phoneCountryCode || "+966",
                phoneLocal: profileData.phoneLocal || ""
            });
        });
    }

    function saveProfile() {
        const result = profileForm.validate();
        if (!result.isValid) {
            return;
        }

        const data = profileForm.option("formData");
        api.put("/api/rbac/profile", {
            firstName: data.firstName,
            lastName: data.lastName,
            email: data.email,
            phoneCountryCode: data.phoneCountryCode,
            phoneLocal: data.phoneLocal
        })
            .then((updated) => {
                profileData = updated || profileData;
                syncDisplayName(profileData);
                renderReadonlyKv(profileData);
                DevExpress.ui.notify(t("common.saved"), "success", 2200);
            })
            .catch(notifyError);
    }

    function savePassword() {
        const result = passwordForm.validate();
        if (!result.isValid) {
            return;
        }

        const data = passwordForm.option("formData");
        api.put("/api/rbac/profile/password", data)
            .then(() => {
                passwordForm.option("formData", {
                    currentPassword: "",
                    newPassword: "",
                    confirmPassword: ""
                });
                DevExpress.ui.notify(t("profile.passwordSaved"), "success", 2200);
            })
            .catch(notifyError);
    }

    function initProfileForm() {
        profileForm = $("#profileForm").dxForm({
            formData: {},
            labelLocation: "top",
            colCount: 2,
            items: [
                {
                    dataField: "firstName",
                    label: { text: t("rbac.users.firstName") },
                    isRequired: true,
                    editorOptions: { stylingMode: "outlined" }
                },
                {
                    dataField: "lastName",
                    label: { text: t("rbac.users.lastName") },
                    isRequired: true,
                    editorOptions: { stylingMode: "outlined" }
                },
                {
                    dataField: "email",
                    label: { text: t("rbac.users.email") },
                    isRequired: true,
                    colSpan: 2,
                    editorOptions: {
                        stylingMode: "outlined",
                        mode: "email",
                        inputAttr: { autocomplete: "email" }
                    }
                },
                {
                    dataField: "phoneCountryCode",
                    label: { text: t("profile.phoneCountry") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        stylingMode: "outlined",
                        dataSource: countryCodes,
                        valueExpr: "code",
                        displayExpr: "label",
                        searchEnabled: true
                    }
                },
                {
                    dataField: "phoneLocal",
                    label: { text: t("rbac.users.phone") },
                    isRequired: false,
                    editorOptions: {
                        stylingMode: "outlined",
                        mode: "tel",
                        inputAttr: { autocomplete: "tel" }
                    }
                }
            ]
        }).dxForm("instance");
    }

    function initPasswordForm() {
        passwordForm = $("#passwordForm").dxForm({
            formData: {
                currentPassword: "",
                newPassword: "",
                confirmPassword: ""
            },
            labelLocation: "top",
            colCount: 1,
            items: [
                {
                    dataField: "currentPassword",
                    label: { text: t("profile.currentPassword") },
                    isRequired: true,
                    editorType: "dxTextBox",
                    editorOptions: { mode: "password", stylingMode: "outlined" }
                },
                {
                    dataField: "newPassword",
                    label: { text: t("profile.newPassword") },
                    isRequired: true,
                    editorType: "dxTextBox",
                    editorOptions: { mode: "password", stylingMode: "outlined" }
                },
                {
                    dataField: "confirmPassword",
                    label: { text: t("profile.confirmPassword") },
                    isRequired: true,
                    editorType: "dxTextBox",
                    editorOptions: { mode: "password", stylingMode: "outlined" }
                }
            ]
        }).dxForm("instance");
    }

    function initPhotoPicker() {
        $("#profileChangePhoto").dxButton({
            text: t("profile.changePhoto"),
            stylingMode: "outlined",
            width: "100%",
            onClick() {
                $("#profilePhotoInput").trigger("click");
            }
        });

        $("#profilePhotoInput").on("change", function (e) {
            const file = e.target.files && e.target.files[0];
            if (!file) {
                return;
            }

            const reader = new FileReader();
            reader.onload = (ev) => {
                setAvatarDisplay(profileData, ev.target.result);
                DevExpress.ui.notify(t("profile.photoPreviewOnly"), "info", 3200);
            };
            reader.readAsDataURL(file);
        });
    }

    function initVerifyPhone() {
        $("#profileVerifyPhone").dxButton({
            text: t("profile.verifyPhone"),
            stylingMode: "text",
            type: "default",
            onClick() {
                DevExpress.ui.notify(t("profile.verifyPhoneSoon"), "info", 3000);
            }
        });
    }

    $(function () {
        if (!api.requireToken()) {
            return;
        }

        window.Zaaer.LocalizationService.init();
        window.Zaaer.PmsAdminShell.init({
            onRefresh: () => loadProfile()
        });

        $("[data-i18n]").each(function () {
            const key = $(this).attr("data-i18n");
            if (key) {
                $(this).text(t(key));
            }
        });

        initProfileForm();
        initPasswordForm();
        initPhotoPicker();
        initVerifyPhone();

        $("#profileSaveButton").dxButton({
            text: t("common.save"),
            type: "default",
            stylingMode: "contained",
            onClick: saveProfile
        });

        $("#passwordSaveButton").dxButton({
            text: t("common.save"),
            type: "default",
            stylingMode: "contained",
            onClick: savePassword
        });

        loadProfile().catch(notifyError);
    });
})(window, jQuery);
