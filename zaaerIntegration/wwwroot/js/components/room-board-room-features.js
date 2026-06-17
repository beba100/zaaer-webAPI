(function (window, $, DevExpress) {
    "use strict";

    const prop = window.Zaaer && window.Zaaer.PropertySettingsService;
    const api = window.Zaaer && window.Zaaer.ApiService;
    const SG = window.Zaaer && window.Zaaer.SaveGuard;
    const loc = window.Zaaer && window.Zaaer.LocalizationService;

    function isAr() {
        return loc && typeof loc.isArabic === "function" && loc.isArabic();
    }

    function popupWidth() {
        return Math.min(1080, Math.max(720, window.innerWidth - 32));
    }

    function resolveApartmentId(room) {
        if (!room) {
            return null;
        }
        if (room.internalApartmentId != null && !Number.isNaN(Number(room.internalApartmentId))) {
            return Number(room.internalApartmentId);
        }
        if (room.apartmentId != null && !Number.isNaN(Number(room.apartmentId))) {
            return Number(room.apartmentId);
        }
        return null;
    }

    function kitchenLabel(code, t) {
        return t(`property.kitchen.${code}`) || code;
    }

    function hallLabel(code, t) {
        return t(`property.hall.${code}`) || code;
    }

    function serviceLabel(code, t) {
        return t(`property.service.${code}`) || code;
    }

    function facilityLabel(f) {
        if (!f) {
            return "";
        }
        if (!isAr() && f.facilityNameEn) {
            return f.facilityNameEn;
        }
        return f.facilityName || f.facilityNameEn || "";
    }

    function renderChipGroup($host, options, labels, selected, onPick, readOnly) {
        $host.empty().addClass("pms-chip-group");
        options.forEach((code) => {
            const $chip = $("<button/>", {
                type: "button",
                class: `pms-chip${selected === code ? " pms-chip--active" : ""}`,
                text: labels(code),
                disabled: !!readOnly
            });
            if (!readOnly) {
                $chip.on("click", () => {
                    onPick(code);
                    $host.find(".pms-chip").removeClass("pms-chip--active");
                    $chip.addClass("pms-chip--active");
                });
            }
            $host.append($chip);
        });
    }

    function renderServiceTiles($host, codes, selected, labelFn, readOnly, onChange) {
        $host.empty().addClass("room-features-service-grid");
        const set = new Set(selected || []);
        codes.forEach((code) => {
            const $tile = $("<button/>", {
                type: "button",
                class: `room-features-service-tile${set.has(code) ? " is-on" : ""}`,
                text: labelFn(code),
                disabled: !!readOnly
            });
            if (!readOnly) {
                $tile.on("click", () => {
                    if (set.has(code)) {
                        set.delete(code);
                        $tile.removeClass("is-on");
                    } else {
                        set.add(code);
                        $tile.addClass("is-on");
                    }
                    onChange(Array.from(set));
                });
            }
            $host.append($tile);
        });
    }

    function mapCatalogFacility(f) {
        return {
            facilityId: f.facilityId,
            id: f.zaaerId,
            name: facilityLabel(f),
            description: f.description || ""
        };
    }

    function renderFacilityChecklist($host, facilities, selectedIds, readOnly, canDelete, t, onChange, onDelete) {
        $host.empty().addClass("room-features-facility-grid");
        const selected = new Set(selectedIds || []);

        if (!facilities.length) {
            return;
        }

        facilities.forEach((f) => {
            const id = f.id;
            const checked = selected.has(id);
            const $wrap = $("<div/>", {
                class: `room-features-facility-row${checked ? " is-checked" : ""}`
            });
            const $card = $("<label/>", {
                class: `room-features-facility-card${readOnly ? " is-disabled" : ""}`
            });
            const $cb = $("<input/>", { type: "checkbox", checked }).prop("disabled", !!readOnly);
            const $body = $("<div/>", { class: "room-features-facility-card-body" });
            $body.append($("<strong/>").text(f.name));
            if (f.description) {
                $body.append($("<small/>").text(f.description));
            }
            $card.append($cb, $body);

            if (!readOnly) {
                $card.on("click", (ev) => {
                    if (ev.target === $cb[0]) {
                        return;
                    }
                    $cb.prop("checked", !$cb.prop("checked")).trigger("change");
                });
                $cb.on("change", () => {
                    const isOn = $cb.prop("checked");
                    if (isOn) {
                        selected.add(id);
                        $wrap.addClass("is-checked");
                    } else {
                        selected.delete(id);
                        $wrap.removeClass("is-checked");
                    }
                    onChange(Array.from(selected));
                });
            }

            $wrap.append($card);

            if (canDelete && typeof onDelete === "function") {
                const $del = $("<button/>", {
                    type: "button",
                    class: "room-features-facility-del",
                    title: "",
                    "aria-label": "delete"
                });
                $del.append($("<span/>", { class: "dx-icon dx-icon-trash" }));
                $del.on("click", (ev) => {
                    ev.preventDefault();
                    ev.stopPropagation();
                    const msg = t("roomBoard.roomFeatures.deleteFacilityConfirm").replace("{0}", f.name);
                    DevExpress.ui.dialog
                        .confirm(msg, t("roomBoard.roomFeatures.deleteFacilityTitle"))
                        .done((ok) => {
                            if (ok) {
                                onDelete(f);
                            }
                        });
                });
                $wrap.append($del);
            }

            $host.append($wrap);
        });
    }

    function openQuickAddFacilityPopup(t, onCreated) {
        const draft = { facilityName: "", facilityNameEn: "", description: "" };
        const $content = $("<div/>").addClass("room-features-quick-add");
        const $form = $("<div/>").appendTo($content);
        const $popup = $("<div>").appendTo("body");

        $popup.dxPopup({
            title: t("property.facilities.add"),
            visible: true,
            showCloseButton: true,
            width: Math.min(520, Math.max(320, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "70vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate() {
                return $content;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    location: "after",
                    toolbar: "bottom",
                    options: {
                        text: t("common.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $popup.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    location: "after",
                    toolbar: "bottom",
                    options: {
                        text: t("common.save"),
                        type: "default",
                        stylingMode: "contained",
                        onClick() {
                            const inst = $form.dxForm("instance");
                            const valid = inst.validate();
                            if (!valid.isValid) {
                                return;
                            }
                            const data = inst.option("formData") || draft;
                            const body = {
                                facilityName: (data.facilityName || "").trim(),
                                facilityNameEn: (data.facilityNameEn || "").trim() || null,
                                description: (data.description || "").trim() || null,
                                isActive: true,
                                imageUrls: []
                            };
                            prop
                                .createFacility(body)
                                .then((created) => {
                                    DevExpress.ui.notify(t("roomBoard.roomFeatures.facilityAdded"), "success", 2000);
                                    $popup.dxPopup("instance").hide();
                                    if (typeof onCreated === "function") {
                                        onCreated(created);
                                    }
                                })
                                .fail(() => {
                                    DevExpress.ui.notify(t("roomBoard.roomFeatures.facilityAddFailed"), "error", 2800);
                                });
                        }
                    }
                }
            ],
            onHidden() {
                $popup.remove();
            }
        });

        $form.dxForm({
            formData: draft,
            labelLocation: "top",
            items: [
                {
                    dataField: "facilityName",
                    isRequired: true,
                    label: { text: t("property.facilities.name") }
                },
                {
                    dataField: "facilityNameEn",
                    label: { text: t("property.facilities.nameEn") }
                },
                {
                    dataField: "description",
                    label: { text: t("property.facilities.description") },
                    editorType: "dxTextArea",
                    editorOptions: { height: 72 }
                }
            ]
        });
    }

    function buildSavePayload(loaded, state) {
        return {
            apartmentCode: loaded.apartmentCode,
            apartmentName: loaded.apartmentName || loaded.apartmentCode,
            buildingZaaerId: loaded.buildingZaaerId,
            floorZaaerId: loaded.floorZaaerId,
            roomTypeZaaerId: loaded.roomTypeZaaerId,
            status: loaded.status || "available",
            isActive: loaded.isActive !== false,
            telephoneExtension: state.telephoneExtension || "",
            bathroomsCount: state.bathroomsCount != null ? state.bathroomsCount : 0,
            kitchenType: state.kitchenType || "none",
            hallType: state.hallType || "none",
            singleBedsCount: state.singleBedsCount != null ? state.singleBedsCount : 0,
            doubleBedsCount: state.doubleBedsCount != null ? state.doubleBedsCount : 0,
            area: state.area,
            description: state.description || "",
            services: state.services ? state.services.slice() : [],
            facilityZaaerIds: state.facilityZaaerIds ? state.facilityZaaerIds.slice() : []
        };
    }

    function facilityCatalogFromLookups(lookups) {
        return (lookups.facilities || [])
            .filter((f) => f.isActive !== false && f.zaaerId != null)
            .map((f) => mapCatalogFacility(f));
    }

    function facilityFromApiRow(row) {
        return mapCatalogFacility({
            facilityId: row.facilityId,
            zaaerId: row.zaaerId,
            facilityName: row.facilityName,
            facilityNameEn: row.facilityNameEn,
            description: row.description
        });
    }

    function open(room, t, onBoardRefresh) {
        if (!prop || typeof prop.getApartment !== "function") {
            DevExpress.ui.notify(t("roomBoard.roomFeatures.missingModule"), "error", 3200);
            return;
        }

        const apartmentId = resolveApartmentId(room);
        if (!apartmentId) {
            DevExpress.ui.notify(t("roomBoard.roomFeatures.loadError"), "error", 3200);
            return;
        }

        const canView = api.hasPermission("property.units.view");
        const canEdit = api.hasPermission("property.units.update");
        if (!canView && !canEdit) {
            DevExpress.ui.notify(t("roomBoard.roomFeatures.forbidden"), "error", 2800);
            return;
        }

        const readOnly = !canEdit;
        const $loadHost = $("<div>").appendTo("body");
        const loadPanel = $loadHost
            .dxLoadPanel({
                visible: true,
                message: t("common.loading"),
                showIndicator: true,
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.12)",
                position: { of: window }
            })
            .dxLoadPanel("instance");

        function dismissLoadPanel() {
            try {
                if (loadPanel) {
                    loadPanel.option("visible", false);
                    loadPanel.dispose();
                }
            } catch {
                /* already disposed */
            }
            $loadHost.remove();
        }

        $.when(prop.getLookups(), prop.getApartment(apartmentId))
            .done((lookups, apartment) => {
                dismissLoadPanel();
                showPopup(room, t, onBoardRefresh, lookups || {}, apartment || {}, apartmentId, readOnly);
            })
            .fail(() => {
                dismissLoadPanel();
                DevExpress.ui.notify(t("roomBoard.roomFeatures.loadError"), "error", 3200);
            });
    }

    function showPopup(room, t, onBoardRefresh, lookups, loaded, apartmentId, readOnly) {
        const canCreateFacility = api.hasPermission("property.facilities.create");
        const canDeleteFacility = api.hasPermission("property.facilities.delete");

        const roomLabel = room.apartmentCode || room.apartmentName || String(apartmentId);
        const title = t("roomBoard.roomFeatures.title").replace("{0}", roomLabel);

        const state = {
            telephoneExtension: loaded.telephoneExtension || "",
            bathroomsCount: loaded.bathroomsCount != null ? loaded.bathroomsCount : 0,
            kitchenType: loaded.kitchenType || "none",
            hallType: loaded.hallType || "none",
            singleBedsCount: loaded.singleBedsCount != null ? loaded.singleBedsCount : 0,
            doubleBedsCount: loaded.doubleBedsCount != null ? loaded.doubleBedsCount : 0,
            area: loaded.area,
            description: loaded.description || "",
            services: loaded.services ? loaded.services.slice() : [],
            facilityZaaerIds: loaded.facilityZaaerIds ? loaded.facilityZaaerIds.slice() : []
        };

        const building = loaded.buildingName || room.buildingName;
        const floor = loaded.floorName || room.floorName;
        const roomType = loaded.roomTypeName || room.roomTypeName;
        let catalogFacilities = facilityCatalogFromLookups(lookups);

        const $shell = $("<div/>").addClass("room-features-shell");

        const $head = $("<div/>").addClass("room-features-head").appendTo($shell);
        $("<span/>")
            .addClass("room-features-head-room")
            .text(loaded.apartmentName || room.apartmentName || roomLabel)
            .appendTo($head);

        function headPill(icon, text) {
            if (!text) {
                return;
            }
            $("<span/>")
                .addClass("room-features-head-pill")
                .append($("<span/>").addClass(`dx-icon dx-icon-${icon}`), document.createTextNode(text))
                .appendTo($head);
        }

        headPill("home", building);
        headPill("columnfield", floor);
        headPill("tags", roomType);

        if (readOnly) {
            $("<p/>")
                .addClass("room-features-readonly-banner")
                .text(t("roomBoard.roomFeatures.readOnly"))
                .appendTo($shell);
        }

        const $body = $("<div/>").addClass("room-features-body").appendTo($shell);
        const $panel = $("<div/>").addClass("room-features-panel").appendTo($body);
        const $layout = $("<div/>").addClass("room-features-layout").appendTo($panel);

        const $main = $("<div/>").addClass("room-features-main").appendTo($layout);
        const $formHost = $("<div/>").addClass("room-features-form-host").appendTo($main);

        const $servicesBlock = $("<div/>").addClass("room-features-block").appendTo($main);
        $("<h4/>")
            .addClass("room-features-block-title")
            .text(t("property.units.services"))
            .appendTo($servicesBlock);
        $("<p/>")
            .addClass("room-features-block-hint")
            .text(t("roomBoard.roomFeatures.servicesHint"))
            .appendTo($servicesBlock);
        const $serviceTiles = $("<div/>").appendTo($servicesBlock);

        const $amenitiesRow = $("<div/>").addClass("room-features-amenities-row").appendTo($main);

        const $kitchenBlock = $("<div/>").addClass("room-features-amenity-block").appendTo($amenitiesRow);
        $("<label/>", { class: "pms-field-label", text: t("property.units.kitchen") }).appendTo($kitchenBlock);
        const $kitchenChips = $("<div/>").appendTo($kitchenBlock);

        const $hallBlock = $("<div/>").addClass("room-features-amenity-block").appendTo($amenitiesRow);
        $("<label/>", { class: "pms-field-label", text: t("property.units.hall") }).appendTo($hallBlock);
        const $hallChips = $("<div/>").appendTo($hallBlock);

        const $aside = $("<div/>").addClass("room-features-aside").appendTo($layout);
        const $asideHead = $("<div/>").addClass("room-features-aside-head").appendTo($aside);
        $("<h4/>")
            .addClass("room-features-block-title")
            .text(t("roomBoard.roomFeatures.sectionFacilities"))
            .appendTo($asideHead);
        const $addFacilityHost = $("<div/>").appendTo($asideHead);
        $("<p/>")
            .addClass("room-features-block-hint")
            .text(t("roomBoard.roomFeatures.facilitiesHint"))
            .appendTo($aside);
        const $facilityScroll = $("<div/>").addClass("room-features-facility-scroll").appendTo($aside);
        const $facilityGrid = $("<div/>").appendTo($facilityScroll);

        function paintFacilityList() {
            $facilityGrid.empty();
            if (!catalogFacilities.length) {
                $facilityGrid.append(
                    $("<div/>", {
                        class: "room-features-facility-empty",
                        text: t("roomBoard.roomFeatures.noFacilities")
                    })
                );
                return;
            }
            renderFacilityChecklist(
                $facilityGrid,
                catalogFacilities,
                state.facilityZaaerIds,
                readOnly,
                canDeleteFacility && !readOnly,
                t,
                (next) => {
                    state.facilityZaaerIds = next;
                },
                (f) => {
                    prop.deleteFacility(f.facilityId)
                        .then(() => {
                            catalogFacilities = catalogFacilities.filter((x) => x.facilityId !== f.facilityId);
                            state.facilityZaaerIds = (state.facilityZaaerIds || []).filter((id) => id !== f.id);
                            DevExpress.ui.notify(t("roomBoard.roomFeatures.facilityDeleted"), "success", 2000);
                            paintFacilityList();
                        })
                        .fail(() => {
                            DevExpress.ui.notify(t("roomBoard.roomFeatures.facilityDeleteFailed"), "error", 2800);
                        });
                }
            );
        }

        renderServiceTiles(
            $serviceTiles,
            lookups.serviceOptions || [],
            state.services,
            (code) => serviceLabel(code, t),
            readOnly,
            (next) => {
                state.services = next;
            }
        );

        renderChipGroup(
            $kitchenChips,
            lookups.kitchenTypes || [],
            (code) => kitchenLabel(code, t),
            state.kitchenType,
            (code) => {
                state.kitchenType = code;
            },
            readOnly
        );
        renderChipGroup(
            $hallChips,
            lookups.hallTypes || [],
            (code) => hallLabel(code, t),
            state.hallType,
            (code) => {
                state.hallType = code;
            },
            readOnly
        );

        paintFacilityList();

        if (canCreateFacility && !readOnly) {
            $addFacilityHost.dxButton({
                text: t("property.facilities.add"),
                icon: "add",
                type: "default",
                stylingMode: "contained",
                onClick() {
                    openQuickAddFacilityPopup(t, (created) => {
                        const item = facilityFromApiRow(created);
                        if (item.id == null) {
                            paintFacilityList();
                            return;
                        }
                        const exists = catalogFacilities.some((x) => x.facilityId === item.facilityId);
                        if (!exists) {
                            catalogFacilities = catalogFacilities.concat([item]);
                        }
                        if (!state.facilityZaaerIds.includes(item.id)) {
                            state.facilityZaaerIds.push(item.id);
                        }
                        paintFacilityList();
                    });
                }
            });
        }

        const $popup = $("<div>").appendTo("body");
        let saveButton;
        const popupSaveGuard = SG ? SG.create() : null;

        $popup.dxPopup({
            title,
            visible: true,
            showCloseButton: true,
            width: popupWidth(),
            height: "auto",
            maxHeight: "90vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup room-features-popup" },
            contentTemplate() {
                return $shell;
            },
            toolbarItems: [
                {
                    widget: "dxButton",
                    location: "after",
                    toolbar: "bottom",
                    options: {
                        text: t("common.cancel"),
                        stylingMode: "outlined",
                        onClick() {
                            $popup.dxPopup("instance").hide();
                        }
                    }
                },
                {
                    widget: "dxButton",
                    location: "after",
                    toolbar: "bottom",
                    options: {
                        text: t("common.save"),
                        type: "default",
                        stylingMode: "contained",
                        visible: !readOnly,
                        onInitialized(e) {
                            saveButton = e.component;
                        },
                        onClick() {
                            const work = () =>
                                Promise.resolve(
                                    prop.updateApartment(apartmentId, buildSavePayload(loaded, state))
                                ).then(() => {
                                    DevExpress.ui.notify(t("roomBoard.roomFeatures.saved"), "success", 2000);
                                    $popup.dxPopup("instance").hide();
                                    if (typeof onBoardRefresh === "function") {
                                        onBoardRefresh();
                                    }
                                });

                            if (SG && popupSaveGuard) {
                                SG.run(popupSaveGuard, work, { button: saveButton });
                                return;
                            }
                            work();
                        }
                    }
                }
            ],
            onHidden() {
                $popup.remove();
            }
        });

        $formHost.dxForm({
            formData: state,
            readOnly,
            labelLocation: "top",
            colCount: 4,
            items: [
                {
                    dataField: "telephoneExtension",
                    label: { text: t("property.units.phoneExt") },
                    colSpan: 1
                },
                {
                    dataField: "bathroomsCount",
                    label: { text: t("property.units.bathrooms") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, showSpinButtons: true, width: "100%" }
                },
                {
                    dataField: "singleBedsCount",
                    label: { text: t("property.units.singleBeds") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, showSpinButtons: true, width: "100%" }
                },
                {
                    dataField: "doubleBedsCount",
                    label: { text: t("property.units.doubleBeds") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, showSpinButtons: true, width: "100%" }
                },
                {
                    dataField: "area",
                    label: { text: t("property.units.area") },
                    cssClass: "room-features-field-area",
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, format: "#0.##", width: "100%" }
                },
                {
                    dataField: "description",
                    label: { text: t("property.units.unitDescription") },
                    cssClass: "room-features-field-desc",
                    editorType: "dxTextArea",
                    colSpan: 3,
                    editorOptions: {
                        height: 36,
                        autoResizeEnabled: false,
                        width: "100%"
                    }
                }
            ],
            onFieldDataChanged(e) {
                state[e.dataField] = e.value;
            }
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.RoomBoardRoomFeaturesPopup = { open };
})(window, jQuery, DevExpress);
