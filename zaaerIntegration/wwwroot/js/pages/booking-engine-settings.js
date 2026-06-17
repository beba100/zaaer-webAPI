(function (window, $, DevExpress) {
    "use strict";

    const svc = window.Zaaer.BookingEngineService;
    const DEFAULT_COLORS = {
        buttonColor: "#3f6f9f",
        borderColor: "#d9dee7",
        backgroundColor: "#eceff3"
    };

    let formData = {};
    let loadPanel;
    let forms = {};
    let roomTypeOptions = [{ id: null, name: "" }];
    let selectedMediaRoomTypeId = null;
    /** @type {{ localId: string, file: File, roomTypeId: number|null, previewUrl: string, isPrimary: boolean }[]} */
    let pendingMedia = [];
    /** @type {number[]} */
    let deletedMediaIds = [];
    /** @type {{ logo: { file: File, previewUrl: string }|null, banner: { file: File, previewUrl: string }|null }} */
    let pendingBranding = { logo: null, banner: null };
    /** @type {{ file: File, previewUrl: string }|null} */
    let pendingPromoBanner = null;
    let promoBannerPainter = null;
    let brandingPainters = {};
    let preferredTabIndex = 0;
    let pendingAvailabilityBatch = null;

    function t(key) {
        return window.Zaaer.LocalizationService.t(key);
    }

    function isAr() {
        const loc = window.Zaaer.LocalizationService;
        return loc && loc.currentCulture && loc.currentCulture() === "ar";
    }

    function canView() {
        const api = window.Zaaer.ApiService;
        return api && api.hasPermission && api.hasPermission("booking_engine.settings.view");
    }

    function canManage() {
        const api = window.Zaaer.ApiService;
        return api && api.hasPermission && api.hasPermission("booking_engine.settings.manage");
    }

    function normalizeSettings(raw) {
        const d = raw && typeof raw === "object" ? raw : {};
        return {
            settingsId: d.settingsId ?? d.SettingsId ?? null,
            hotelId: d.hotelId ?? d.HotelId ?? null,
            isEnabled: d.isEnabled ?? d.IsEnabled ?? true,
            publicSlug: d.publicSlug ?? d.PublicSlug ?? "",
            logoUrl: d.logoUrl ?? d.LogoUrl ?? "",
            faviconUrl: d.faviconUrl ?? d.FaviconUrl ?? "",
            bannerUrl: d.bannerUrl ?? d.BannerUrl ?? "",
            showHotelPicker: d.showHotelPicker ?? d.ShowHotelPicker ?? false,
            showCurrentBranchOnly: d.showCurrentBranchOnly ?? d.ShowCurrentBranchOnly ?? true,
            minimumStayNights: d.minimumStayNights ?? d.MinimumStayNights ?? 1,
            buttonColor: d.buttonColor ?? d.ButtonColor ?? DEFAULT_COLORS.buttonColor,
            borderColor: d.borderColor ?? d.BorderColor ?? DEFAULT_COLORS.borderColor,
            backgroundColor: d.backgroundColor ?? d.BackgroundColor ?? DEFAULT_COLORS.backgroundColor,
            topFilterHtml: d.topFilterHtml ?? d.TopFilterHtml ?? "",
            downFilterHtml: d.downFilterHtml ?? d.DownFilterHtml ?? "",
            contactEmail: d.contactEmail ?? d.ContactEmail ?? "",
            contactPhone: d.contactPhone ?? d.ContactPhone ?? "",
            contactDescription: d.contactDescription ?? d.ContactDescription ?? "",
            depositMode: d.depositMode ?? d.DepositMode ?? "optional",
            depositAmount: d.depositAmount ?? d.DepositAmount ?? null,
            depositPercent: d.depositPercent ?? d.DepositPercent ?? null,
            onlineDepositEnabled: d.onlineDepositEnabled ?? d.OnlineDepositEnabled ?? false,
            salesClosed: d.salesClosed ?? d.SalesClosed ?? false,
            salesClosedMessage: d.salesClosedMessage ?? d.SalesClosedMessage ?? "",
            rentalTypeMode: d.rentalTypeMode ?? d.RentalTypeMode ?? "both",
            promoBannerEnabled: d.promoBannerEnabled ?? d.PromoBannerEnabled ?? false,
            promoBannerImageUrl: d.promoBannerImageUrl ?? d.PromoBannerImageUrl ?? "",
            promoBannerHtml: d.promoBannerHtml ?? d.PromoBannerHtml ?? "",
            promoBannerEndsAt: d.promoBannerEndsAt ?? d.PromoBannerEndsAt ?? null,
            availabilityMode: d.availabilityMode ?? d.AvailabilityMode ?? "actual",
            rateFallbackMode: d.rateFallbackMode ?? d.RateFallbackMode ?? "standard",
            rateFallbackMin: d.rateFallbackMin ?? d.RateFallbackMin ?? null,
            rateFallbackMax: d.rateFallbackMax ?? d.RateFallbackMax ?? null,
            availabilityOverrides: d.availabilityOverrides ?? d.AvailabilityOverrides ?? [],
            publicBookingUrl: d.publicBookingUrl ?? d.PublicBookingUrl ?? "",
            hotelCode: d.hotelCode ?? d.HotelCode ?? "",
            media: d.media ?? d.Media ?? [],
            coupons: d.coupons ?? d.Coupons ?? []
        };
    }

    function compactFormOptions(extra) {
        return $.extend(true, {
            formData: formData,
            readOnly: !canManage(),
            colCount: 2,
            labelLocation: "top",
            showColonAfterLabel: false,
            minColWidth: 128,
            colCountByScreen: { xs: 1, sm: 1, md: 2 },
            elementAttr: { class: "be-compact-form" }
        }, extra || {});
    }

    function colorBoxOptions() {
        return {
            editAlphaChannel: false,
            applyValueMode: "instantly",
            openOnFieldClick: true
        };
    }

    function renderUrlCard() {
        const $card = $("#beUrlCard").empty();
        $("<h4/>").text(t("bookingEngine.publicUrl")).appendTo($card);
        const $row = $("<div/>").addClass("be-settings-url-row").appendTo($card);
        $("<code/>").attr("id", "bePublicUrl").text(formData.publicBookingUrl || "").appendTo($row);
        $("<div/>").appendTo($row).dxButton({
            text: t("bookingEngine.openPreview"),
            icon: "link",
            type: "default",
            stylingMode: "contained",
            onClick: () => {
                const url = formData.publicBookingUrl || "/booking-engine.html";
                window.open(url, "_blank", "noopener,noreferrer");
            }
        });
    }

    function renderColorPreview($host) {
        const $preview = $("<div/>").addClass("be-color-preview").appendTo($host);
        const fields = [
            { key: "buttonColor", label: t("bookingEngine.buttonColor") },
            { key: "borderColor", label: t("bookingEngine.borderColor") },
            { key: "backgroundColor", label: t("bookingEngine.backgroundColor") }
        ];

        fields.forEach((f) => {
            const $chip = $("<div/>").addClass("be-color-preview-chip").appendTo($preview);
            $("<span/>").addClass("be-color-preview-swatch").css("background", formData[f.key] || DEFAULT_COLORS[f.key]).appendTo($chip);
            $("<span/>").addClass("be-color-preview-label").text(f.label).appendTo($chip);
        });
    }

    function mediaChanged() {
        return pendingMedia.length > 0 || deletedMediaIds.length > 0;
    }

    function brandingChanged() {
        return !!(pendingBranding.logo || pendingBranding.banner);
    }

    function clearPendingBranding(type) {
        const item = pendingBranding[type];
        if (item && item.previewUrl) {
            URL.revokeObjectURL(item.previewUrl);
        }
        pendingBranding[type] = null;
        if (brandingPainters[type]) {
            brandingPainters[type]();
        }
    }

    function buildBrandingPicker($host, type, labelKey, urlField) {
        const $wrap = $("<div/>").addClass("be-branding-field").appendTo($host);
        $("<div/>").addClass("be-branding-label").text(t(labelKey)).appendTo($wrap);
        const $row = $("<div/>").addClass("be-branding-row").appendTo($wrap);
        const $preview = $("<div/>").addClass("be-branding-preview").appendTo($row);
        const $actions = $("<div/>").addClass("be-branding-actions").appendTo($row);

        function paint() {
            $preview.empty();
            const url = (pendingBranding[type] && pendingBranding[type].previewUrl) || formData[urlField] || "";
            if (url) {
                $("<img/>", { src: url, alt: "" }).appendTo($preview);
            } else {
                $("<span/>").addClass("be-branding-placeholder").text(t("bookingEngine.noImage")).appendTo($preview);
            }
        }

        brandingPainters[type] = paint;
        paint();

        $("<div/>")
            .appendTo($actions)
            .dxButton({
                text: t("bookingEngine.chooseImage"),
                icon: "image",
                type: "default",
                stylingMode: "outlined",
                disabled: !canManage(),
                onClick: () => {
                    const input = document.createElement("input");
                    input.type = "file";
                    input.accept = "image/*";
                    input.onchange = () => {
                        const file = input.files && input.files[0];
                        if (!file) {
                            return;
                        }
                        clearPendingBranding(type);
                        pendingBranding[type] = {
                            file,
                            previewUrl: URL.createObjectURL(file)
                        };
                        paint();
                        DevExpress.ui.notify(t("bookingEngine.brandingStaged"), "info", 2200);
                    };
                    input.click();
                }
            });

        $("<div/>")
            .appendTo($actions)
            .dxButton({
                text: t("bookingEngine.clearImage"),
                icon: "clear",
                stylingMode: "text",
                visible: canManage(),
                onClick: () => {
                    clearPendingBranding(type);
                    formData[urlField] = "";
                    paint();
                }
            });
    }

    async function syncBrandingChanges() {
        if (!brandingChanged()) {
            return;
        }

        if (pendingBranding.logo && pendingBranding.logo.file) {
            const uploaded = await svc.uploadImage(pendingBranding.logo.file);
            formData.logoUrl =
                (uploaded && uploaded.imageUrl) || (typeof uploaded === "string" ? uploaded : formData.logoUrl);
            URL.revokeObjectURL(pendingBranding.logo.previewUrl);
            pendingBranding.logo = null;
        }

        if (pendingBranding.banner && pendingBranding.banner.file) {
            const uploaded = await svc.uploadImage(pendingBranding.banner.file);
            formData.bannerUrl =
                (uploaded && uploaded.imageUrl) || (typeof uploaded === "string" ? uploaded : formData.bannerUrl);
            URL.revokeObjectURL(pendingBranding.banner.previewUrl);
            pendingBranding.banner = null;
        }
    }

    function removePendingMedia(localId) {
        const item = pendingMedia.find((p) => p.localId === localId);
        if (item && item.previewUrl) {
            URL.revokeObjectURL(item.previewUrl);
        }
        pendingMedia = pendingMedia.filter((p) => p.localId !== localId);
    }

    function removeSavedMedia(mediaId) {
        if (mediaId) {
            deletedMediaIds.push(mediaId);
        }
        formData.media = (formData.media || []).filter((m) => (m.mediaId || m.MediaId) !== mediaId);
    }

    function renderMediaGrid($host) {
        const $grid = $("<div/>").addClass("be-media-grid").appendTo($host.empty());

        (formData.media || []).forEach((m) => {
            const mediaId = m.mediaId || m.MediaId;
            const $thumb = $("<div/>").addClass("be-media-thumb").appendTo($grid);
            $("<img/>", { src: m.imageUrl || m.ImageUrl, alt: "" }).appendTo($thumb);
            if (canManage()) {
                $("<div/>")
                    .addClass("be-media-del")
                    .appendTo($thumb)
                    .dxButton({
                        icon: "trash",
                        stylingMode: "text",
                        type: "danger",
                        onClick: () => {
                            removeSavedMedia(mediaId);
                            renderMediaGrid($host);
                        }
                    });
            }
        });

        pendingMedia.forEach((p) => {
            const $thumb = $("<div/>").addClass("be-media-thumb be-media-thumb--pending").appendTo($grid);
            $("<img/>", { src: p.previewUrl, alt: "" }).appendTo($thumb);
            $("<span/>").addClass("be-media-pending-badge").text(t("bookingEngine.mediaPending")).appendTo($thumb);
            if (canManage()) {
                $("<div/>")
                    .addClass("be-media-del")
                    .appendTo($thumb)
                    .dxButton({
                        icon: "trash",
                        stylingMode: "text",
                        type: "danger",
                        onClick: () => {
                            removePendingMedia(p.localId);
                            renderMediaGrid($host);
                        }
                    });
            }
        });
    }

    async function syncMediaChanges() {
        if (!mediaChanged()) {
            return;
        }

        const ids = [...deletedMediaIds];
        for (let i = 0; i < ids.length; i += 1) {
            await svc.deleteMedia(ids[i]);
        }
        deletedMediaIds = [];

        const staged = [...pendingMedia];
        const existingCount = (formData.media || []).length;
        for (let i = 0; i < staged.length; i += 1) {
            const p = staged[i];
            const uploaded = await svc.uploadImage(p.file);
            const imageUrl =
                (uploaded && uploaded.imageUrl) || (typeof uploaded === "string" ? uploaded : "");
            await svc.addMedia({
                imageUrl,
                roomTypeId: p.roomTypeId,
                isPrimary: p.isPrimary && existingCount === 0 && i === 0,
                sortOrder: existingCount + i + 1
            });
            URL.revokeObjectURL(p.previewUrl);
        }
        pendingMedia = [];
    }

    function buildGeneralTab() {
        brandingPainters = {};
        const $body = $("<div/>").addClass("be-settings-tab-body");
        const $formHost = $("<div/>").appendTo($body);
        forms.general = $formHost
            .dxForm(
                compactFormOptions({
                    items: [
                        { dataField: "isEnabled", editorType: "dxCheckBox", label: { text: t("bookingEngine.enabled") } },
                        {
                            dataField: "salesClosed",
                            editorType: "dxCheckBox",
                            label: { text: t("bookingEngine.salesClosed") },
                            helpText: t("bookingEngine.salesClosedHelp")
                        },
                        {
                            dataField: "salesClosedMessage",
                            editorType: "dxTextArea",
                            label: { text: t("bookingEngine.salesClosedMessage") },
                            colSpan: 2,
                            editorOptions: { height: 72, placeholder: t("bookingEngine.salesClosedPlaceholder") }
                        },
                        { dataField: "publicSlug", label: { text: "Slug" } },
                        { dataField: "minimumStayNights", editorType: "dxNumberBox", label: { text: t("bookingEngine.minimumStay") } },
                        {
                            dataField: "rentalTypeMode",
                            editorType: "dxSelectBox",
                            label: { text: t("bookingEngine.rentalTypeMode") },
                            helpText: t("bookingEngine.rentalTypeModeHelp"),
                            editorOptions: {
                                dataSource: [
                                    { value: "both", text: t("bookingEngine.rentalTypeMode.both") },
                                    { value: "daily_only", text: t("bookingEngine.rentalTypeMode.dailyOnly") },
                                    { value: "monthly_only", text: t("bookingEngine.rentalTypeMode.monthlyOnly") },
                                    { value: "hidden", text: t("bookingEngine.rentalTypeMode.hidden") }
                                ],
                                valueExpr: "value",
                                displayExpr: "text"
                            }
                        },
                        { dataField: "showHotelPicker", editorType: "dxCheckBox", label: { text: t("bookingEngine.showHotelPicker") } },
                        { dataField: "showCurrentBranchOnly", editorType: "dxCheckBox", label: { text: t("bookingEngine.showCurrentBranchOnly") } },
                        { dataField: "faviconUrl", label: { text: t("bookingEngine.favicon") } }
                    ]
                })
            )
            .dxForm("instance");

        const $branding = $("<div/>").addClass("be-branding-block").appendTo($body);
        $("<h4/>").addClass("be-branding-block-title").text(t("bookingEngine.branding")).appendTo($branding);
        const $grid = $("<div/>").addClass("be-branding-grid").appendTo($branding);
        buildBrandingPicker($grid, "logo", "bookingEngine.logo", "logoUrl");
        buildBrandingPicker($grid, "banner", "bookingEngine.banner", "bannerUrl");
        return $body;
    }

    function buildAppearanceTab() {
        const $body = $("<div/>").addClass("be-settings-tab-body");
        renderColorPreview($body);

        forms.colors = $("<div/>")
            .appendTo($body)
            .dxForm(
                compactFormOptions({
                    colCount: 1,
                    items: [
                        {
                            dataField: "buttonColor",
                            editorType: "dxColorBox",
                            label: { text: t("bookingEngine.buttonColor") },
                            editorOptions: colorBoxOptions()
                        },
                        {
                            dataField: "borderColor",
                            editorType: "dxColorBox",
                            label: { text: t("bookingEngine.borderColor") },
                            editorOptions: colorBoxOptions()
                        },
                        {
                            dataField: "backgroundColor",
                            editorType: "dxColorBox",
                            label: { text: t("bookingEngine.backgroundColor") },
                            editorOptions: colorBoxOptions()
                        }
                    ]
                })
            )
            .dxForm("instance");

        return $body;
    }

    function buildContentTab() {
        const $body = $("<div/>").addClass("be-settings-tab-body");
        forms.content = $("<div/>")
            .appendTo($body)
            .dxForm(
                compactFormOptions({
                    colCount: 1,
                    items: [
                        {
                            dataField: "topFilterHtml",
                            editorType: "dxTextArea",
                            label: { text: t("bookingEngine.topFilterHtml") },
                            editorOptions: { height: 72 }
                        },
                        {
                            dataField: "downFilterHtml",
                            editorType: "dxTextArea",
                            label: { text: t("bookingEngine.downFilterHtml") },
                            editorOptions: { height: 72 }
                        },
                        { dataField: "contactEmail", label: { text: t("bookingEngine.contactEmail") } },
                        { dataField: "contactPhone", label: { text: t("bookingEngine.contactPhone") } },
                        {
                            dataField: "contactDescription",
                            editorType: "dxTextArea",
                            label: { text: t("bookingEngine.contactDescription") },
                            editorOptions: { height: 64 }
                        }
                    ]
                })
            )
            .dxForm("instance");
        return $body;
    }

    function roomTypesForCouponPicker() {
        return roomTypeOptions.filter((r) => r.id != null && r.id !== "");
    }

    function parseRoomTypeIds(csv) {
        if (!csv) {
            return [];
        }
        return String(csv)
            .split(",")
            .map((s) => s.trim())
            .filter(Boolean)
            .map((s) => Number(s))
            .filter((n) => Number.isFinite(n) && n > 0);
    }

    function serializeRoomTypeIds(ids) {
        if (!Array.isArray(ids) || !ids.length) {
            return "";
        }
        return ids.join(",");
    }

    function validateCouponForm(fd) {
        const promo = String(fd.promoCode || "").trim();
        if (!promo) {
            return t("bookingEngine.couponValidationPromoRequired");
        }

        const val = Number(fd.discountValue);
        if (!Number.isFinite(val) || val <= 0) {
            return t("bookingEngine.couponValidationDiscount");
        }

        const type = (fd.discountType || "percent").toLowerCase();
        if (type === "percent" && val > 100) {
            return t("bookingEngine.couponValidationPercentMax");
        }

        if (fd.validFrom && fd.validTo) {
            const from = fd.validFrom instanceof Date ? fd.validFrom : new Date(fd.validFrom);
            const to = fd.validTo instanceof Date ? fd.validTo : new Date(fd.validTo);
            if (!Number.isNaN(from.getTime()) && !Number.isNaN(to.getTime()) && to < from) {
                return t("bookingEngine.couponValidationDateRange");
            }
        }

        const maxUses = fd.maxRedemptions != null ? Number(fd.maxRedemptions) : null;
        if (maxUses != null && Number.isFinite(maxUses) && maxUses <= 0) {
            return t("bookingEngine.couponValidationMaxUses");
        }

        return null;
    }

    function normalizeCouponRow(c) {
        const x = c || {};
        return {
            couponId: x.couponId ?? x.CouponId,
            couponNo: x.couponNo ?? x.CouponNo ?? "",
            promoCode: x.promoCode ?? x.PromoCode ?? "",
            title: x.title ?? x.Title ?? "",
            discountType: x.discountType ?? x.DiscountType ?? "percent",
            discountValue: x.discountValue ?? x.DiscountValue ?? 0,
            minStayNights: x.minStayNights ?? x.MinStayNights ?? null,
            minBookingAmount: x.minBookingAmount ?? x.MinBookingAmount ?? null,
            maxRedemptions: x.maxRedemptions ?? x.MaxRedemptions ?? null,
            redemptionCount: x.redemptionCount ?? x.RedemptionCount ?? 0,
            validFrom: x.validFrom ?? x.ValidFrom ?? null,
            validTo: x.validTo ?? x.ValidTo ?? null,
            roomTypeIds: x.roomTypeIds ?? x.RoomTypeIds ?? "",
            isActive: x.isActive ?? x.IsActive ?? true,
            notes: x.notes ?? x.Notes ?? ""
        };
    }

    function openCouponEditor(row, onSaved) {
        const isEdit = !!(row && row.couponId);
        const data = normalizeCouponRow(row || { discountType: "percent", isActive: true });
        let selectedRoomTypeIds = parseRoomTypeIds(data.roomTypeIds);

        const $popup = $("<div/>").appendTo("body");
        $popup.dxPopup({
            title: isEdit ? t("bookingEngine.couponEdit") : t("bookingEngine.couponAdd"),
            width: Math.min(760, window.innerWidth - 24),
            height: "auto",
            maxHeight: "80vh",
            showCloseButton: true,
            wrapperAttr: { class: "be-coupon-editor-popup" },
            contentTemplate() {
                const $wrap = $("<div/>").addClass("be-coupon-editor-body");
                const $formHost = $("<div/>").appendTo($wrap);
                const form = $formHost
                    .dxForm({
                        formData: data,
                        colCount: 2,
                        labelLocation: "top",
                        readOnly: !canManage(),
                        items: [
                            { dataField: "promoCode", label: { text: t("bookingEngine.couponPromoCode") }, colSpan: 2, isRequired: true },
                            { dataField: "title", label: { text: t("bookingEngine.couponTitle") }, colSpan: 2 },
                            {
                                dataField: "discountType",
                                editorType: "dxSelectBox",
                                label: { text: t("bookingEngine.couponDiscountType") },
                                editorOptions: {
                                    items: [
                                        { value: "percent", text: t("bookingEngine.couponPercent") },
                                        { value: "fixed", text: t("bookingEngine.couponFixed") }
                                    ],
                                    valueExpr: "value",
                                    displayExpr: "text"
                                }
                            },
                            {
                                dataField: "discountValue",
                                editorType: "dxNumberBox",
                                label: { text: t("bookingEngine.couponDiscountValue") },
                                isRequired: true,
                                editorOptions: { min: 0.01, showSpinButtons: true }
                            },
                            { dataField: "minStayNights", editorType: "dxNumberBox", label: { text: t("bookingEngine.couponMinNights") }, editorOptions: { min: 0 } },
                            {
                                dataField: "minBookingAmount",
                                editorType: "dxNumberBox",
                                label: { text: t("bookingEngine.couponMinAmount") },
                                editorOptions: { min: 0 }
                            },
                            {
                                dataField: "maxRedemptions",
                                editorType: "dxNumberBox",
                                colSpan: 2,
                                label: { text: t("bookingEngine.couponMaxUses") },
                                helpText: t("bookingEngine.couponMaxUsesHelp"),
                                editorOptions: { min: 0, showClearButton: true }
                            },
                            {
                                dataField: "validFrom",
                                editorType: "dxDateBox",
                                label: { text: t("bookingEngine.couponValidFrom") },
                                editorOptions: { type: "date", openOnFieldClick: true, showClearButton: true }
                            },
                            {
                                dataField: "validTo",
                                editorType: "dxDateBox",
                                label: { text: t("bookingEngine.couponValidTo") },
                                editorOptions: { type: "date", openOnFieldClick: true, showClearButton: true }
                            },
                            { dataField: "isActive", editorType: "dxCheckBox", label: { text: t("bookingEngine.couponActive") }, colSpan: 2 },
                            {
                                dataField: "notes",
                                editorType: "dxTextArea",
                                label: { text: t("bookingEngine.notes") },
                                colSpan: 2,
                                editorOptions: { height: 64 }
                            }
                        ]
                    })
                    .dxForm("instance");

                const $rtLabel = $("<div/>").addClass("be-coupon-room-types-label").text(t("bookingEngine.couponRoomTypesPicker")).appendTo($wrap);
                const $tagHost = $("<div/>").appendTo($wrap);
                const tagBox = $tagHost
                    .dxTagBox({
                        dataSource: roomTypesForCouponPicker(),
                        value: selectedRoomTypeIds,
                        valueExpr: "id",
                        displayExpr: "name",
                        showSelectionControls: true,
                        searchEnabled: true,
                        showClearButton: true,
                        multiline: true,
                        readOnly: !canManage(),
                        placeholder: t("bookingEngine.couponRoomTypesAll"),
                        onValueChanged(e) {
                            selectedRoomTypeIds = e.value || [];
                        }
                    })
                    .dxTagBox("instance");

                const $footer = $("<div/>").addClass("be-coupon-editor-footer").appendTo($wrap);
                $("<div/>")
                    .appendTo($footer)
                    .dxButton({
                        text: t("common.close"),
                        stylingMode: "outlined",
                        onClick: () => $popup.dxPopup("instance").hide()
                    });
                if (canManage()) {
                    $("<div/>")
                        .appendTo($footer)
                        .dxButton({
                            text: t("common.save"),
                            type: "default",
                            icon: "save",
                            onClick: async () => {
                                const fd = form.option("formData");
                                const err = validateCouponForm(fd);
                                if (err) {
                                    DevExpress.ui.notify(err, "warning", 3500);
                                    return;
                                }

                                const body = {
                                    promoCode: String(fd.promoCode || "").trim(),
                                    title: fd.title,
                                    discountType: fd.discountType,
                                    discountValue: fd.discountValue,
                                    minStayNights: fd.minStayNights || null,
                                    minBookingAmount: fd.minBookingAmount || null,
                                    maxRedemptions: fd.maxRedemptions || null,
                                    validFrom: fd.validFrom || null,
                                    validTo: fd.validTo || null,
                                    roomTypeIds: serializeRoomTypeIds(tagBox.option("value")),
                                    isActive: !!fd.isActive,
                                    notes: fd.notes
                                };
                                try {
                                    if (isEdit) {
                                        await svc.updateCoupon(data.couponId, body);
                                    } else {
                                        await svc.createCoupon(body);
                                    }
                                    DevExpress.ui.notify(t("bookingEngine.saved"), "success", 2000);
                                    $popup.dxPopup("instance").hide();
                                    if (onSaved) {
                                        onSaved();
                                    }
                                } catch (saveErr) {
                                    DevExpress.ui.notify(saveErr.message || String(saveErr), "error", 3500);
                                }
                            }
                        });
                }
                return $wrap;
            },
            onHidden() {
                $popup.remove();
            }
        });
        $popup.dxPopup("instance").show();
    }

    function renderCouponsGrid($host) {
        const rows = (formData.coupons || []).map(normalizeCouponRow);
        const po = window.Zaaer.PmsGridOptions;
        const $grid = $("<div/>").appendTo($host.empty());
        $grid.dxDataGrid(
            po.merge(po.adminBaseline(), {
            dataSource: rows,
            keyExpr: "couponId",
            wordWrapEnabled: true,
            height: 280,
            columns: [
                { dataField: "couponNo", caption: t("bookingEngine.couponNo"), minWidth: 88 },
                { dataField: "promoCode", caption: t("bookingEngine.couponPromoCode"), minWidth: 90 },
                { dataField: "title", caption: t("bookingEngine.couponTitle"), minWidth: 100 },
                {
                    dataField: "discountType",
                    caption: t("bookingEngine.couponDiscountType"),
                    minWidth: 72,
                    customizeText(e) {
                        return e.value === "fixed" ? t("bookingEngine.couponFixed") : t("bookingEngine.couponPercent");
                    }
                },
                { dataField: "discountValue", caption: t("bookingEngine.couponDiscountValue"), format: "#,##0.##", minWidth: 70 },
                {
                    caption: t("bookingEngine.couponUses"),
                    minWidth: 90,
                    calculateCellValue(row) {
                        const used = row.redemptionCount || 0;
                        const max = row.maxRedemptions;
                        if (max != null && max > 0) {
                            return `${used} / ${max}`;
                        }
                        return String(used);
                    }
                },
                { dataField: "validFrom", caption: t("bookingEngine.couponValidFrom"), dataType: "date", minWidth: 88 },
                { dataField: "validTo", caption: t("bookingEngine.couponValidTo"), dataType: "date", minWidth: 88 },
                { dataField: "isActive", caption: t("bookingEngine.couponActive"), dataType: "boolean", width: 64 },
                {
                    type: "buttons",
                    width: 110,
                    visible: canManage(),
                    buttons: [
                        {
                            hint: t("common.edit"),
                            icon: "edit",
                            onClick(e) {
                                openCouponEditor(e.row.data, async () => {
                                    formData.coupons = await svc.listCoupons();
                                    renderCouponsGrid($host);
                                });
                            }
                        },
                        {
                            hint: t("common.delete"),
                            icon: "trash",
                            onClick: async (e) => {
                                const ok = await DevExpress.ui.dialog.confirm(
                                    t("bookingEngine.couponDeleteConfirm"),
                                    t("common.confirm")
                                );
                                if (!ok) {
                                    return;
                                }
                                await svc.deleteCoupon(e.row.data.couponId);
                                formData.coupons = await svc.listCoupons();
                                renderCouponsGrid($host);
                            }
                        }
                    ]
                }
            ]
            })
        );
    }

    function clearPendingPromoBanner() {
        if (pendingPromoBanner && pendingPromoBanner.previewUrl) {
            URL.revokeObjectURL(pendingPromoBanner.previewUrl);
        }
        pendingPromoBanner = null;
        if (promoBannerPainter) {
            promoBannerPainter();
        }
    }

    function buildPromoBannerImagePicker($host) {
        const $wrap = $("<div/>").addClass("be-promo-banner-upload").appendTo($host);
        $("<div/>").addClass("be-branding-label").text(t("bookingEngine.promoBannerImage")).appendTo($wrap);
        const $row = $("<div/>").addClass("be-branding-row").appendTo($wrap);
        const $preview = $("<div/>").addClass("be-branding-preview be-branding-preview--banner").appendTo($row);
        const $actions = $("<div/>").addClass("be-branding-actions").appendTo($row);

        function paint() {
            $preview.empty();
            const url =
                (pendingPromoBanner && pendingPromoBanner.previewUrl) || formData.promoBannerImageUrl || "";
            if (url) {
                $("<img/>", { src: url, alt: "" }).appendTo($preview);
            } else {
                $("<span/>").addClass("be-branding-placeholder").text(t("bookingEngine.noImage")).appendTo($preview);
            }
            const $urlLine = $("#bePromoBannerUrlLine");
            if ($urlLine.length) {
                $urlLine.text(url ? url : "");
                $urlLine.toggle(!!url);
            }
        }

        promoBannerPainter = paint;
        paint();

        if (canManage()) {
            $("<div/>")
                .appendTo($actions)
                .dxButton({
                    text: t("bookingEngine.chooseImage"),
                    icon: "image",
                    stylingMode: "outlined",
                    type: "default",
                    onClick: () => {
                        const input = document.createElement("input");
                        input.type = "file";
                        input.accept = "image/*";
                        input.onchange = () => {
                            const file = input.files && input.files[0];
                            if (!file) {
                                return;
                            }
                            clearPendingPromoBanner();
                            pendingPromoBanner = {
                                file,
                                previewUrl: URL.createObjectURL(file)
                            };
                            paint();
                            DevExpress.ui.notify(t("bookingEngine.promoBannerStaged"), "info", 2200);
                        };
                        input.click();
                    }
                });
            $("<div/>")
                .appendTo($actions)
                .dxButton({
                    text: t("bookingEngine.clearImage"),
                    icon: "clear",
                    stylingMode: "text",
                    onClick: () => {
                        clearPendingPromoBanner();
                        formData.promoBannerImageUrl = "";
                        paint();
                    }
                });
        }

        $("<div/>")
            .attr("id", "bePromoBannerUrlLine")
            .addClass("be-promo-banner-url")
            .toggle(!!formData.promoBannerImageUrl)
            .text(formData.promoBannerImageUrl || "")
            .appendTo($wrap);
    }

    function buildPromoCouponsTab() {
        const $body = $("<div/>").addClass("be-settings-tab-body");
        $("<h4/>").text(t("bookingEngine.promoBannerTitle")).appendTo($body);
        $("<p/>").addClass("be-settings-help").text(t("bookingEngine.promoBannerHelp")).appendTo($body);

        const $promoFormHost = $("<div/>").appendTo($body);
        buildPromoBannerImagePicker($promoFormHost);

        forms.promo = $("<div/>")
            .appendTo($body)
            .dxForm(
                compactFormOptions({
                    colCount: 1,
                    items: [
                        {
                            dataField: "promoBannerEnabled",
                            editorType: "dxCheckBox",
                            label: { text: t("bookingEngine.promoBannerEnabled") }
                        },
                        {
                            dataField: "promoBannerHtml",
                            editorType: "dxTextArea",
                            label: { text: t("bookingEngine.promoBannerMessage") },
                            editorOptions: { height: 96 }
                        },
                        {
                            dataField: "promoBannerEndsAt",
                            editorType: "dxDateBox",
                            label: { text: t("bookingEngine.promoBannerEnds") },
                            editorOptions: { type: "datetime", openOnFieldClick: true, showClearButton: true }
                        }
                    ]
                })
            )
            .dxForm("instance");

        const $couponBlock = $("<div/>").addClass("be-settings-coupons-block").appendTo($body);
        const $toolbar = $("<div/>").addClass("be-settings-coupons-toolbar").appendTo($couponBlock);
        $("<h4/>").text(t("bookingEngine.couponsTitle")).appendTo($toolbar);
        if (canManage()) {
            $("<div/>")
                .appendTo($toolbar)
                .dxButton({
                    text: t("bookingEngine.couponAdd"),
                    icon: "add",
                    type: "default",
                    stylingMode: "contained",
                    onClick: () => {
                        openCouponEditor(null, async () => {
                            formData.coupons = await svc.listCoupons();
                            renderCouponsGrid($("#beCouponsGridHost"));
                        });
                    }
                });
        }
        renderCouponsGrid($("<div/>").attr("id", "beCouponsGridHost").appendTo($couponBlock));
        return $body;
    }

    function buildDepositTab() {
        const $body = $("<div/>").addClass("be-settings-tab-body");
        forms.deposit = $("<div/>")
            .appendTo($body)
            .dxForm(
                compactFormOptions({
                    items: [
                        {
                            dataField: "depositMode",
                            editorType: "dxSelectBox",
                            label: { text: t("bookingEngine.depositMode") },
                            editorOptions: {
                                items: ["none", "optional", "required"],
                                value: formData.depositMode || "optional"
                            }
                        },
                        { dataField: "depositAmount", editorType: "dxNumberBox", label: { text: t("bookingEngine.depositAmount") } },
                        { dataField: "depositPercent", editorType: "dxNumberBox", label: { text: t("bookingEngine.depositPercent") } },
                        { dataField: "onlineDepositEnabled", editorType: "dxCheckBox", label: { text: t("bookingEngine.onlineDeposit") } }
                    ]
                })
            )
            .dxForm("instance");

        const $mediaBlock = $("<div/>").addClass("be-settings-media-block").appendTo($body);
        $("<h4/>").text(t("bookingEngine.media")).appendTo($mediaBlock);

        if (canManage()) {
            const $toolbar = $("<div/>").addClass("be-settings-media-toolbar").appendTo($mediaBlock);
            $("<div/>")
                .appendTo($toolbar)
                .dxSelectBox({
                    label: t("bookingEngine.mediaRoomType"),
                    dataSource: roomTypeOptions,
                    displayExpr: "name",
                    valueExpr: "id",
                    value: selectedMediaRoomTypeId,
                    showClearButton: true,
                    searchEnabled: true,
                    width: 280,
                    onValueChanged: (e) => {
                        selectedMediaRoomTypeId = e.value == null ? null : e.value;
                    }
                });
            $("<div/>")
                .appendTo($toolbar)
                .dxButton({
                    text: t("bookingEngine.uploadImage"),
                    icon: "image",
                    stylingMode: "outlined",
                    type: "default",
                    onClick: () => {
                        const input = document.createElement("input");
                        input.type = "file";
                        input.accept = "image/*";
                        input.onchange = () => {
                            const file = input.files && input.files[0];
                            if (!file) {
                                return;
                            }
                            const totalCount = (formData.media || []).length + pendingMedia.length;
                            pendingMedia.push({
                                localId: `pending_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
                                file,
                                roomTypeId: selectedMediaRoomTypeId,
                                previewUrl: URL.createObjectURL(file),
                                isPrimary: totalCount === 0
                            });
                            const $gridHost = $("#beMediaGrid");
                            if ($gridHost.length) {
                                renderMediaGrid($gridHost);
                            }
                            DevExpress.ui.notify(t("bookingEngine.mediaStaged"), "info", 2200);
                        };
                        input.click();
                    }
                });
        }

        renderMediaGrid($("<div/>").attr("id", "beMediaGrid").appendTo($mediaBlock));
        return $body;
    }

    function canManageAvailability() {
        const api = window.Zaaer.ApiService;
        return api && api.hasPermission && api.hasPermission("booking_engine.availability.manage");
    }

    function buildPricingAvailabilityTab() {
        const $body = $("<div/>").addClass("be-settings-tab-body be-pricing-tab");
        $("<p/>").addClass("be-settings-hint").text(t("bookingEngine.pricingTabHint")).appendTo($body);

        forms.pricing = $("<div/>")
            .appendTo($body)
            .dxForm(
                compactFormOptions({
                    items: [
                        {
                            dataField: "availabilityMode",
                            editorType: "dxSelectBox",
                            label: { text: t("bookingEngine.availabilityMode") },
                            editorOptions: {
                                dataSource: [
                                    { value: "actual", text: t("bookingEngine.availabilityMode.actual") },
                                    { value: "override", text: t("bookingEngine.availabilityMode.override") },
                                    { value: "min", text: t("bookingEngine.availabilityMode.min") }
                                ],
                                valueExpr: "value",
                                displayExpr: "text"
                            }
                        },
                        {
                            dataField: "rateFallbackMode",
                            editorType: "dxSelectBox",
                            label: { text: t("bookingEngine.rateFallbackMode") },
                            editorOptions: {
                                dataSource: [
                                    { value: "standard", text: t("bookingEngine.rateFallbackMode.standard") },
                                    { value: "programmatic", text: t("bookingEngine.rateFallbackMode.programmatic") }
                                ],
                                valueExpr: "value",
                                displayExpr: "text"
                            }
                        },
                        {
                            dataField: "rateFallbackMin",
                            editorType: "dxNumberBox",
                            label: { text: t("bookingEngine.rateFallbackMin") },
                            editorOptions: { min: 0, format: "#,##0.##" }
                        },
                        {
                            dataField: "rateFallbackMax",
                            editorType: "dxNumberBox",
                            label: { text: t("bookingEngine.rateFallbackMax") },
                            editorOptions: { min: 0, format: "#,##0.##" }
                        }
                    ]
                })
            )
            .dxForm("instance");

        const $availBlock = $("<div/>").addClass("be-avail-block").appendTo($body);
        $("<h4/>").text(t("bookingEngine.availabilityOverridesTitle")).appendTo($availBlock);
        $("<p/>").addClass("be-settings-hint").text(t("bookingEngine.availabilityOverridesHint")).appendTo($availBlock);

        const $toolbar = $("<div/>").addClass("be-avail-toolbar").appendTo($availBlock);
        $("<div/>")
            .appendTo($toolbar)
            .dxButton({
                text: t("bookingEngine.availabilityAdd"),
                icon: "add",
                type: "default",
                stylingMode: "outlined",
                visible: canManageAvailability(),
                onClick() {
                    const grid = $("#beAvailGrid").dxDataGrid("instance");
                    if (!grid) {
                        return;
                    }
                    const ds = grid.option("dataSource") || [];
                    ds.push({
                        roomTypeId: roomTypeOptions[1] ? roomTypeOptions[1].id : null,
                        dateFrom: svc.formatLocalDateParam(new Date()),
                        dateTo: svc.formatLocalDateParam(new Date()),
                        displayUnits: 1
                    });
                    grid.option("dataSource", ds);
                    pendingAvailabilityBatch = collectAvailabilityBatch(ds);
                }
            });

        const po = window.Zaaer.PmsGridOptions;
        const overrideRows = (formData.availabilityOverrides || []).map((r) => ({
            overrideId: r.overrideId ?? r.OverrideId,
            roomTypeId: r.roomTypeId ?? r.RoomTypeId,
            roomTypeName: r.roomTypeName ?? r.RoomTypeName,
            rateDate: r.rateDate ?? r.RateDate,
            displayUnits: r.displayUnits ?? r.DisplayUnits
        }));

        $("<div/>")
            .attr("id", "beAvailGrid")
            .appendTo($availBlock)
            .dxDataGrid(
                po.merge(po.baseline(), {
                    dataSource: overrideRows,
                    height: 280,
                    editing: {
                        mode: "cell",
                        allowUpdating: canManageAvailability(),
                        allowDeleting: canManageAvailability()
                    },
                    columns: [
                        {
                            dataField: "roomTypeId",
                            caption: t("bookingEngine.availabilityColRoomType"),
                            lookup: {
                                dataSource: roomTypeOptions.filter((x) => x.id != null),
                                valueExpr: "id",
                                displayExpr: "name"
                            },
                            width: 200
                        },
                        { dataField: "rateDate", caption: t("bookingEngine.availabilityColDate"), width: 120 },
                        {
                            dataField: "displayUnits",
                            caption: t("bookingEngine.availabilityColUnits"),
                            dataType: "number",
                            width: 100
                        },
                        { dataField: "roomTypeName", caption: t("bookingEngine.availabilityColName"), allowEditing: false }
                    ],
                    onRowRemoved() {
                        pendingAvailabilityBatch = collectAvailabilityBatchFromGrid();
                    },
                    onSaved() {
                        pendingAvailabilityBatch = collectAvailabilityBatchFromGrid();
                    }
                })
            );

        const $addRange = $("<div/>").addClass("be-avail-add-range").appendTo($availBlock);
        const rangeState = {
            roomTypeId: roomTypeOptions[1] ? roomTypeOptions[1].id : null,
            dateFrom: new Date(),
            dateTo: new Date(),
            displayUnits: 2
        };
        $("<div/>")
            .appendTo($addRange)
            .dxForm({
                formData: rangeState,
                colCount: 4,
                labelLocation: "top",
                readOnly: !canManageAvailability(),
                items: [
                    {
                        dataField: "roomTypeId",
                        editorType: "dxSelectBox",
                        label: { text: t("bookingEngine.availabilityColRoomType") },
                        editorOptions: {
                            dataSource: roomTypeOptions.filter((x) => x.id != null),
                            valueExpr: "id",
                            displayExpr: "name"
                        }
                    },
                    {
                        dataField: "dateFrom",
                        editorType: "dxDateBox",
                        label: { text: t("bookingEngine.availabilityFrom") },
                        editorOptions: { type: "date", openOnFieldClick: true }
                    },
                    {
                        dataField: "dateTo",
                        editorType: "dxDateBox",
                        label: { text: t("bookingEngine.availabilityTo") },
                        editorOptions: { type: "date", openOnFieldClick: true }
                    },
                    {
                        dataField: "displayUnits",
                        editorType: "dxNumberBox",
                        label: { text: t("bookingEngine.availabilityColUnits") },
                        editorOptions: { min: 0, showSpinButtons: true }
                    }
                ]
            });
        $("<div/>")
            .appendTo($addRange)
            .dxButton({
                text: t("bookingEngine.availabilityApplyRange"),
                type: "default",
                stylingMode: "contained",
                visible: canManageAvailability(),
                onClick() {
                    if (!rangeState.roomTypeId) {
                        DevExpress.ui.notify(t("bookingEngine.availabilityRoomRequired"), "warning", 2800);
                        return;
                    }
                    pendingAvailabilityBatch = {
                        items: [
                            {
                                roomTypeId: rangeState.roomTypeId,
                                dateFrom: svc.formatLocalDateParam(rangeState.dateFrom),
                                dateTo: svc.formatLocalDateParam(rangeState.dateTo),
                                displayUnits: rangeState.displayUnits
                            }
                        ]
                    };
                    DevExpress.ui.notify(t("bookingEngine.availabilityStaged"), "info", 2200);
                }
            });

        return $body;
    }

    function collectAvailabilityBatchFromGrid() {
        const grid = $("#beAvailGrid").dxDataGrid("instance");
        if (!grid) {
            return null;
        }
        return collectAvailabilityBatch(grid.option("dataSource") || []);
    }

    function collectAvailabilityBatch(rows) {
        const items = (rows || [])
            .filter((r) => r.roomTypeId != null && r.rateDate)
            .map((r) => ({
                roomTypeId: r.roomTypeId,
                dateFrom: r.rateDate,
                dateTo: r.rateDate,
                displayUnits: Math.max(0, Number(r.displayUnits) || 0)
            }));
        return items.length ? { items } : null;
    }

    function buildTabs() {
        forms = {};
        const $host = $("#beSettingsTabs");
        const keepIndex = preferredTabIndex;

        if ($host.data("dxTabPanel")) {
            $host.dxTabPanel("dispose");
        }
        $host.empty();

        const panel = $host
            .dxTabPanel({
                rtlEnabled: isAr(),
                animationEnabled: true,
                swipeEnabled: false,
                deferRendering: false,
                stylingMode: "secondary",
                iconPosition: "top",
                height: "auto",
                elementAttr: { class: "be-settings-tabs-panel" },
                items: [
                    { title: t("bookingEngine.tabs.general"), template: buildGeneralTab },
                    { title: t("bookingEngine.tabs.appearance"), template: buildAppearanceTab },
                    { title: t("bookingEngine.tabs.content"), template: buildContentTab },
                    { title: t("bookingEngine.tabs.deposit"), template: buildDepositTab },
                    { title: t("bookingEngine.tabs.promoCoupons"), template: buildPromoCouponsTab },
                    { title: t("bookingEngine.tabs.pricingAvailability"), template: buildPricingAvailabilityTab }
                ],
                onSelectionChanged(e) {
                    if (e.component && typeof e.component.option === "function") {
                        preferredTabIndex = e.component.option("selectedIndex");
                    }
                }
            })
            .dxTabPanel("instance");

        if (keepIndex > 0) {
            panel.option("selectedIndex", Math.min(keepIndex, panel.option("items").length - 1));
        }

        renderUrlCard();
    }

    function collectFormData() {
        Object.keys(forms).forEach((key) => {
            const instance = forms[key];
            if (instance && instance.option) {
                $.extend(formData, instance.option("formData"));
            }
        });
    }

    async function loadRoomTypeOptions() {
        try {
            const list = await svc.loadAdminRoomTypes();
            const rows = Array.isArray(list) ? list : [];
            roomTypeOptions = [
                { id: null, name: t("bookingEngine.mediaRoomTypeAll") },
                ...rows.map((r) => ({
                    id: r.id != null ? r.id : r.Id,
                    name: r.name || r.Name || String(r.id != null ? r.id : r.Id || "")
                }))
            ];
        } catch {
            roomTypeOptions = [{ id: null, name: t("bookingEngine.mediaRoomTypeAll") }];
        }
    }

    async function loadSettings() {
        pendingMedia.forEach((p) => {
            if (p.previewUrl) {
                URL.revokeObjectURL(p.previewUrl);
            }
        });
        pendingMedia = [];
        deletedMediaIds = [];
        clearPendingBranding("logo");
        clearPendingBranding("banner");
        pendingBranding = { logo: null, banner: null };
        clearPendingPromoBanner();

        const raw = await svc.loadAdminSettings();
        formData = normalizeSettings(raw);
        try {
            formData.coupons = await svc.listCoupons();
        } catch {
            formData.coupons = formData.coupons || [];
        }
        buildTabs();
    }

    async function syncPromoBannerImage() {
        if (!pendingPromoBanner || !pendingPromoBanner.file) {
            return;
        }
        const uploaded = await svc.uploadImage(pendingPromoBanner.file);
        formData.promoBannerImageUrl =
            (uploaded && uploaded.imageUrl) || (typeof uploaded === "string" ? uploaded : formData.promoBannerImageUrl);
        URL.revokeObjectURL(pendingPromoBanner.previewUrl);
        pendingPromoBanner = null;
    }

    async function saveSettings() {
        collectFormData();
        const tabPanel = $("#beSettingsTabs").dxTabPanel("instance");
        if (tabPanel) {
            preferredTabIndex = tabPanel.option("selectedIndex");
        }
        try {
            loadPanel.show();
            if (brandingChanged()) {
                await syncBrandingChanges();
            }
            if (pendingPromoBanner && pendingPromoBanner.file) {
                await syncPromoBannerImage();
            }
            if (mediaChanged()) {
                await syncMediaChanges();
            }
            const saved = await svc.saveAdminSettings(formData);
            if (pendingAvailabilityBatch && canManageAvailability()) {
                const overrides = await svc.saveAvailabilityOverrides(pendingAvailabilityBatch);
                saved.availabilityOverrides = overrides;
                pendingAvailabilityBatch = null;
            }
            formData = normalizeSettings(saved);
            try {
                formData.coupons = await svc.listCoupons();
            } catch {
                formData.coupons = formData.coupons || [];
            }
            DevExpress.ui.notify(t("bookingEngine.saved"), "success", 2500);
            buildTabs();
        } catch (err) {
            DevExpress.ui.notify(err.message || String(err), "error", 3500);
        } finally {
            loadPanel.hide();
        }
    }

    async function bootstrapPage() {
        loadPanel = $("#beSettingsLoadPanel")
            .dxLoadPanel({ shadingColor: "rgba(15,23,42,0.12)", visible: false })
            .dxLoadPanel("instance");

        const $saveHost = $("#beSaveBtn");
        if ($saveHost.data("dxButton")) {
            $saveHost.dxButton("dispose");
        }
        $saveHost.dxButton({
            text: t("bookingEngine.save"),
            type: "default",
            icon: "save",
            stylingMode: "contained",
            visible: canManage(),
            onClick: saveSettings
        });

        try {
            loadPanel.show();
            await loadRoomTypeOptions();
            await loadSettings();
        } catch (err) {
            DevExpress.ui.notify(err.message || String(err), "error", 4000);
        } finally {
            loadPanel.hide();
        }
    }

    function init() {
        if (!window.Zaaer.ApiService.requireToken()) {
            return;
        }

        if (!canView()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-booking-engine-settings",
            onRefresh() {
                bootstrapPage();
            }
        });

        bootstrapPage();
    }

    $(init);
})(window, jQuery, DevExpress);
