(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const prop = window.Zaaer.PropertySettingsService;
    const api = window.Zaaer.ApiService;
    const SG = window.Zaaer && window.Zaaer.SaveGuard;

    let loadPanel;
    let lookups = {
        buildings: [],
        floors: [],
        roomTypes: [],
        kitchenTypes: [],
        hallTypes: [],
        propertyTypes: [],
        propertyType: "hotel",
        isResort: false,
        isHall: false,
        roomCategories: [],
        resortAreaTypes: [],
        serviceOptions: [],
        facilities: []
    };
    let buildingsCache = [];

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    function canView() {
        return api.hasPermission("property.settings.view");
    }

    function canManageUnits() {
        return api.hasPermission("property.units.create") || api.hasPermission("property.units.update");
    }

    function canManageBuildings() {
        return api.hasPermission("property.buildings.create") || api.hasPermission("property.buildings.update");
    }

    function canManageRoomTypes() {
        return api.hasPermission("property.room_types.create") || api.hasPermission("property.room_types.update");
    }

    function canManageFacilities() {
        return api.hasPermission("property.facilities.create") || api.hasPermission("property.facilities.update");
    }

    function canUpload() {
        return api.hasPermission("property.settings.manage");
    }

    function withLoad(promise) {
        loadPanel.show();
        return $.when(promise).always(() => loadPanel.hide());
    }

    function kitchenLabel(code) {
        return t(`property.kitchen.${code}`) || code;
    }

    function hallLabel(code) {
        return t(`property.hall.${code}`) || code;
    }

    function modeText(hallKey, resortKey, defaultKey) {
        if (lookups.isHall) {
            return t(hallKey);
        }
        if (lookups.isResort) {
            return t(resortKey);
        }
        return t(defaultKey);
    }

    function applyPropertyModeLabels() {
        const titleKey = lookups.isHall
            ? "property.venue.settings.title"
            : lookups.isResort
                ? "property.resort.settings.title"
                : "property.settings.title";
        const subtitleKey = lookups.isHall
            ? "property.venue.settings.subtitle"
            : lookups.isResort
                ? "property.resort.settings.subtitle"
                : "property.settings.subtitle";
        $(".pms-admin-toolbar-title").text(t(titleKey));
        $(".pms-admin-toolbar-sub").text(t(subtitleKey));
        document.title = t(titleKey) || document.title;
    }

    function serviceLabel(code) {
        return t(`property.service.${code}`) || code;
    }

    function roomCategoryLabel(code) {
        return t(`property.roomTypes.category.${code}`) || code;
    }

    function resortAreaLabel(code) {
        return t(`property.resort.area.${code}`) || code;
    }

    function roomCategoryOptions() {
        const codes = lookups.roomCategories && lookups.roomCategories.length
            ? lookups.roomCategories
            : ["other", "room", "suite", "apartment", "villa", "chalet"];
        return codes.map((code) => ({ id: code, name: roomCategoryLabel(code) }));
    }

    function resortAreaOptions() {
        return (lookups.resortAreaTypes || []).map((code) => ({ id: code, name: resortAreaLabel(code) }));
    }

    function serviceOptionsDataSource() {
        return (lookups.serviceOptions || []).map((code) => ({
            id: code,
            name: serviceLabel(code)
        }));
    }

    function apartmentPayloadFromRow(row, isActive) {
        return {
            apartmentCode: row.apartmentCode,
            apartmentName: row.apartmentName,
            buildingZaaerId: row.buildingZaaerId,
            floorZaaerId: row.floorZaaerId,
            roomTypeZaaerId: row.roomTypeZaaerId,
            parentApartmentZaaerId: row.parentApartmentZaaerId || null,
            status: row.status || "vacant",
            isActive: typeof isActive === "boolean" ? isActive : row.isActive !== false,
            telephoneExtension: row.telephoneExtension || "",
            bathroomsCount: row.bathroomsCount != null ? row.bathroomsCount : 1,
            kitchenType: row.kitchenType || "none",
            hallType: row.hallType || "none",
            resortAreaType: row.resortAreaType || null,
            singleBedsCount: row.singleBedsCount != null ? row.singleBedsCount : 0,
            doubleBedsCount: row.doubleBedsCount != null ? row.doubleBedsCount : 0,
            area: row.area,
            description: row.description || "",
            services: row.services ? row.services.slice() : [],
            facilityZaaerIds: row.facilityZaaerIds ? row.facilityZaaerIds.slice() : []
        };
    }

    function facilityOptionsDataSource() {
        return (lookups.facilities || [])
            .filter((f) => f.isActive !== false && f.zaaerId != null)
            .map((f) => ({
                id: f.zaaerId,
                name: !isAr() && f.facilityNameEn ? f.facilityNameEn : f.facilityName
            }));
    }

    function setApartmentsActive(rows, isActive) {
        const updates = (rows || []).map((row) =>
            prop.updateApartment(row.apartmentId, apartmentPayloadFromRow(row, isActive))
        );
        return $.when.apply($, updates);
    }

    function buildingOptions() {
        return (lookups.buildings || []).map((b) => ({
            id: b.zaaerId || b.buildingId,
            name: b.buildingName
        }));
    }

    function floorOptionsForBuilding(buildingZaaerId) {
        if (!buildingZaaerId) {
            return [];
        }
        return (lookups.floors || [])
            .filter((f) => f.buildingZaaerId === buildingZaaerId)
            .map((f) => ({
                id: f.zaaerId || f.floorId,
                name: f.floorName || String(f.floorNumber)
            }));
    }

    function roomTypeOptions() {
        return (lookups.roomTypes || []).map((rt) => ({
            id: rt.zaaerId || rt.roomTypeId,
            name: rt.roomTypeName
        }));
    }

    function roomTypeOptionsForChildRoom() {
        const nonChalet = (lookups.roomTypes || []).filter(
            (rt) => `${rt.roomCategory || ""}`.toLowerCase() !== "chalet"
        );
        const source = nonChalet.length ? nonChalet : lookups.roomTypes || [];
        return source.map((rt) => ({
            id: rt.zaaerId || rt.roomTypeId,
            name: rt.roomTypeName
        }));
    }

    function chaletParentLinkId(row) {
        return row ? row.zaaerId || row.apartmentId : null;
    }

    function parentChaletOptions(currentApartmentId) {
        const chaletTypeIds = new Set(
            (lookups.roomTypes || [])
                .filter((rt) => `${rt.roomCategory || ""}`.toLowerCase() === "chalet")
                .map((rt) => rt.zaaerId || rt.roomTypeId)
                .filter((id) => id !== undefined && id !== null)
        );

        return ((window.__propertyApartmentsGrid && window.__propertyApartmentsGrid.option("dataSource")) || [])
            .filter((row) => {
                if (!row || row.apartmentId === currentApartmentId) {
                    return false;
                }
                const typeId = row.roomTypeZaaerId;
                return chaletTypeIds.has(typeId) || `${row.roomCategory || ""}`.toLowerCase() === "chalet";
            })
            .map((row) => ({
                id: row.zaaerId || row.apartmentId,
                name: `${row.apartmentName || row.apartmentCode || ""}`.trim()
            }));
    }

    function renderChipGroup($host, fieldName, options, labels, selected, onPick) {
        $host.empty().addClass("pms-chip-group");
        options.forEach((code) => {
            const $chip = $("<button/>", {
                type: "button",
                class: `pms-chip${selected === code ? " pms-chip--active" : ""}`,
                text: labels(code)
            });
            $chip.on("click", () => {
                onPick(code);
                $host.find(".pms-chip").removeClass("pms-chip--active");
                $chip.addClass("pms-chip--active");
            });
            $host.append($chip);
        });
        $host.attr("data-field", fieldName);
    }

    function openFormPopup(title, $content, onSave, onDone) {
        const $popup = $("<div>").appendTo("body");
        let saveButton;
        const popupSaveGuard = SG ? SG.create() : null;

        $popup.dxPopup({
            title,
            visible: true,
            showCloseButton: true,
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
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
                        onInitialized(e) {
                            saveButton = e.component;
                        },
                        onClick() {
                            const work = () =>
                                Promise.resolve(onSave()).then(() => {
                                    DevExpress.ui.notify(t("common.saved"), "success", 2200);
                                    if (typeof onDone === "function") {
                                        onDone();
                                    }
                                });

                            let ran;
                            if (SG && popupSaveGuard) {
                                ran = SG.run(
                                    popupSaveGuard,
                                    () =>
                                        work().then(() => {
                                            SG.closePopupThenRun($popup);
                                        }),
                                    { button: saveButton }
                                );
                            } else {
                                ran = work()
                                    .then(() => {
                                        $popup.dxPopup("instance").hide();
                                    })
                                    .catch((err) => {
                                        const msg =
                                            (err && err.responseJSON && err.responseJSON.message) ||
                                            t("common.error");
                                        DevExpress.ui.notify(msg, "error", 3500);
                                    });
                            }

                            if (ran === false) {
                                return;
                            }

                            if (ran && typeof ran.catch === "function") {
                                ran.catch((err) => {
                                    const msg =
                                        (err && err.responseJSON && err.responseJSON.message) ||
                                        t("common.error");
                                    DevExpress.ui.notify(msg, "error", 3500);
                                });
                            }
                        }
                    }
                }
            ],
            onHidden() {
                $popup.remove();
            }
        });
    }

    function unwrapUploadResponse(res) {
        const data = res && (res.data !== undefined ? res.data : res);
        return data && data.imageUrl ? data.imageUrl : null;
    }

    function uploadPropertyImageFile(file) {
        if (!file) {
            return $.when(null);
        }

        const fd = new FormData();
        fd.append("file", file);
        return api
            .postForm("/api/v1/pms/property/upload-image", fd)
            .then(unwrapUploadResponse)
            .fail(() => {
                DevExpress.ui.notify(t("property.upload.failed"), "error", 3200);
                return $.Deferred().reject().promise();
            });
    }

    function uploadPropertyImageFiles(files) {
        const list = (files || []).filter(Boolean);
        if (!list.length) {
            return $.when([]);
        }

        let chain = $.when([]);
        list.forEach((file) => {
            chain = chain.then((urls) =>
                uploadPropertyImageFile(file).then((url) => (url ? urls.concat(url) : urls))
            );
        });
        return chain;
    }

    function previewImageFile(file, $preview) {
        if (!file || !$preview || !$preview.length) {
            return;
        }

        const reader = new FileReader();
        reader.onload = (ev) => {
            $preview.attr("src", ev.target.result).prop("hidden", false);
        };
        reader.readAsDataURL(file);
    }

    /**
     * Pick image(s) locally; upload runs only when caller invokes uploadPending() (on Save).
     */
    function createDeferredImagePicker($uploaderHost, $preview, options) {
        if (!canUpload()) {
            return {
                uploadPending() {
                    const existing = options.getExistingUrls
                        ? options.getExistingUrls()
                        : options.getExistingUrl
                          ? options.getExistingUrl()
                          : null;
                    if (Array.isArray(existing)) {
                        return $.when(existing.slice());
                    }
                    return $.when(existing || null);
                }
            };
        }

        let pendingFiles = [];

        $uploaderHost.dxFileUploader({
            accept: "image/*",
            multiple: !!options.multiple,
            maxFileSize: 1048576,
            allowedFileExtensions: [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"],
            selectButtonText: t("property.upload.pick"),
            labelText: t("property.upload.drop"),
            onValueChanged(e) {
                pendingFiles = (e.value || []).filter(Boolean);
                if (pendingFiles.length) {
                    previewImageFile(pendingFiles[0], $preview);
                }
            }
        });

        if (options.multiple) {
            const urls = options.getExistingUrls ? options.getExistingUrls() : [];
            if (urls.length && urls[0]) {
                $preview.attr("src", urls[0]).prop("hidden", false);
            }
        } else {
            const url = options.getExistingUrl ? options.getExistingUrl() : null;
            if (url) {
                $preview.attr("src", url).prop("hidden", false);
            }
        }

        return {
            uploadPending() {
                if (options.multiple) {
                    const existing = (options.getExistingUrls ? options.getExistingUrls() : []).slice();
                    if (!pendingFiles.length) {
                        return $.when(existing);
                    }
                    return uploadPropertyImageFiles(pendingFiles).then((uploaded) => {
                        uploaded.forEach((url) => {
                            if (url && !existing.includes(url)) {
                                existing.push(url);
                            }
                        });
                        return existing;
                    });
                }

                if (!pendingFiles.length) {
                    return $.when(options.getExistingUrl ? options.getExistingUrl() : null);
                }

                return uploadPropertyImageFile(pendingFiles[0]);
            }
        };
    }

    function refreshLookups() {
        return prop.getLookups().then((data) => {
            lookups = data || lookups;
            buildingsCache = lookups.buildings || [];
            applyPropertyModeLabels();
        });
    }

    function renderBuildingCards() {
        const $host = $("#propertyBuildingsCards");
        if (!$host.length) {
            return;
        }
        $host.empty();
        (buildingsCache || []).forEach((b) => {
            const $card = $("<article/>", { class: "pms-building-card", tabindex: 0 });
            $card.append($("<div/>", { class: "pms-building-card__title", text: b.buildingName }));
            const $meta = $("<div/>", { class: "pms-building-card__meta" });
            $meta.append(
                $("<span/>", { class: "pms-building-card__badge" }).append(
                    $("<span/>", { class: "dx-icon dx-icon-map", "aria-hidden": "true" }),
                    document.createTextNode(` ${t("property.buildings.floors")}: ${b.floorCount || 0}`)
                )
            );
            $meta.append(
                $("<span/>", { class: "pms-building-card__badge" }).text(
                    `${b.apartmentCount || 0} ${t("property.buildings.rooms")}`
                )
            );
            $card.append($meta);
            $card.on("click keydown", (ev) => {
                if (ev.type === "keydown" && ev.key !== "Enter" && ev.key !== " ") {
                    return;
                }
                if (ev.type === "keydown") {
                    ev.preventDefault();
                }
                openBuildingEditor(b);
            });
            $host.append($card);
        });
    }

    function openBuildingEditor(row) {
        if (!canManageBuildings() && !row) {
            return;
        }

        const $shell = $("<div/>", { class: "pms-form-panel" });
        const $form = $("<div/>").appendTo($shell);
        const $floorsHost = $("<div/>", { class: "pms-field-label-wrap" })
            .append($("<label/>", { class: "pms-field-label", text: t("property.buildings.floors") }))
            .appendTo($shell);
        const $linesHost = $("<div/>", { id: "propertyFloorsLines" }).appendTo($floorsHost);

        let floorsEditor;
        const initial = {
            buildingName: row ? row.buildingName : "",
            description: row ? row.description : "",
            isActive: row ? row.isActive !== false : true,
            floors: []
        };

        const loadDetail = row
            ? prop.getBuilding(row.buildingId || row.zaaerId).then((detail) => {
                  initial.buildingName = detail.buildingName;
                  initial.description = detail.description;
                  initial.isActive = detail.isActive !== false;
                  initial.floors = detail.floors || [];
              })
            : $.Deferred().resolve();

        withLoad(
            loadDetail.then(() => {
                openFormPopup(
                    row ? t("property.buildings.edit") : t("property.buildings.add"),
                    $shell,
                    () => {
                        const formData = $form.dxForm("instance").option("formData") || initial;
                        const floors = floorsEditor ? floorsEditor.getItems() : [];
                        const body = {
                            buildingName: formData.buildingName,
                            description: formData.description,
                            isActive: !!formData.isActive,
                            floors: floors.map((f, i) => ({
                                floorId: f.floorId,
                                zaaerId: f.zaaerId,
                                floorNumber: f.floorNumber || i + 1,
                                floorName: f.floorName || String(i + 1),
                                sortOrder: f.sortOrder || i + 1,
                                isActive: f.isActive !== false
                            }))
                        };
                        return row
                            ? prop.updateBuilding(row.buildingId || row.zaaerId, body)
                            : prop.createBuilding(body);
                    },
                    () => {
                        refreshLookups().then(() => {
                            refreshBuildings();
                        });
                    }
                );

                const formInstance = $form
                    .dxForm({
                        formData: initial,
                        labelLocation: "top",
                        colCount: 1,
                        items: [
                            {
                                dataField: "buildingName",
                                isRequired: true,
                                label: { text: t("property.buildings.name") }
                            },
                            {
                                dataField: "description",
                                label: { text: t("property.buildings.description") },
                                editorType: "dxTextArea"
                            },
                            {
                                dataField: "isActive",
                                label: { text: t("property.active") },
                                editorType: "dxSwitch"
                            }
                        ],
                        onFieldDataChanged(e) {
                            initial[e.dataField] = e.value;
                        }
                    })
                    .dxForm("instance");

                floorsEditor = window.Zaaer.PmsSortableLines.create($linesHost, {
                    items: initial.floors,
                    nameLabel: t("property.buildings.floorName"),
                    addLabel: t("property.buildings.addFloor"),
                    onChange(items) {
                        initial.floors = items;
                    }
                });

                void formInstance;
            })
        );
    }

    function refreshBuildings() {
        return withLoad(
            prop.listBuildings().then((rows) => {
                buildingsCache = rows || [];
                renderBuildingCards();
                if (window.__propertyBuildingsGrid) {
                    window.__propertyBuildingsGrid.option("dataSource", buildingsCache);
                }
            })
        );
    }

    function renderActiveStatusBadge($container, isActive) {
        const active = isActive !== false;
        $("<span/>")
            .addClass(
                `unit-settings-active-badge${
                    active ? " unit-settings-active-badge--on" : " unit-settings-active-badge--off"
                }`
            )
            .text(active ? t("property.active") : t("property.inactive"))
            .appendTo($container);
    }

    function buildApartmentsGrid($host) {
        const $card = $("<section/>", {
            class: "pms-admin-card pms-admin-grid-card unit-settings-units-card"
        }).appendTo($host);
        const $filters = $("<div/>", { class: "unit-settings-filter-row" }).appendTo($card);
        const filterState = { search: "", buildingZaaerId: null, floorZaaerId: null, roomTypeZaaerId: null };

        $("<div/>")
            .appendTo($filters)
            .dxTextBox({
                placeholder: modeText("property.venue.halls.search", "property.units.search", "property.units.search"),
                mode: "search",
                width: 200,
                onValueChanged(e) {
                    filterState.search = e.value || "";
                    reloadApartments();
                }
            });

        $("<div/>")
            .appendTo($filters)
            .dxSelectBox({
                placeholder: t("property.units.block"),
                dataSource: buildingOptions(),
                valueExpr: "id",
                displayExpr: "name",
                showClearButton: true,
                searchEnabled: true,
                width: 180,
                onValueChanged(e) {
                    filterState.buildingZaaerId = e.value;
                    filterState.floorZaaerId = null;
                    reloadApartments();
                }
            });

        $("<div/>")
            .appendTo($filters)
            .dxSelectBox({
                placeholder: t("property.units.roomType"),
                dataSource: roomTypeOptions(),
                valueExpr: "id",
                displayExpr: "name",
                showClearButton: true,
                searchEnabled: true,
                width: 200,
                onValueChanged(e) {
                    filterState.roomTypeZaaerId = e.value;
                    reloadApartments();
                }
            });

        const $gridHost = $("<div/>", { class: "unit-settings-units-grid-host" }).appendTo($card);
        let grid;
        let activateSelectedButton;
        let deactivateSelectedButton;

        function updateBulkActionButtonsVisible() {
            const hasSelection = grid && (grid.getSelectedRowKeys() || []).length > 0;
            if (activateSelectedButton) {
                activateSelectedButton.option("visible", hasSelection);
            }
            if (deactivateSelectedButton) {
                deactivateSelectedButton.option("visible", hasSelection);
            }
        }

        function reloadApartments() {
            const params = {};
            if (filterState.search) {
                params.search = filterState.search;
            }
            if (filterState.buildingZaaerId) {
                params.buildingZaaerId = filterState.buildingZaaerId;
            }
            if (filterState.floorZaaerId) {
                params.floorZaaerId = filterState.floorZaaerId;
            }
            if (filterState.roomTypeZaaerId) {
                params.roomTypeZaaerId = filterState.roomTypeZaaerId;
            }
            return prop.listApartments(params).then((rows) => {
                grid.option("dataSource", rows || []);
                grid.clearSelection();
                updateBulkActionButtonsVisible();
            });
        }

        function selectedApartmentRows() {
            return grid.getSelectedRowsData() || [];
        }

        function bulkSetActive(isActive) {
            const rows = selectedApartmentRows();
            if (!rows.length) {
                DevExpress.ui.notify(t("property.units.selectRows"), "warning", 2800);
                return;
            }
            withLoad(
                setApartmentsActive(rows, isActive).then(() => {
                    DevExpress.ui.notify(t("common.saved"), "success", 2200);
                    grid.clearSelection();
                    reloadApartments();
                })
            );
        }

        function softDeactivateApartment(row) {
            withLoad(
                setApartmentsActive([row], false).then(() => {
                    DevExpress.ui.notify(t("property.units.deactivated"), "success", 2200);
                    reloadApartments();
                })
            );
        }

        const po = window.Zaaer.PmsGridOptions;
        grid = $gridHost
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                elementAttr: { class: "unit-settings-units-grid" },
                dataSource: [],
                keyExpr: "apartmentId",
                height: Math.max(360, Math.floor(window.innerHeight - 380)),
                wordWrapEnabled: true,
                paging: { pageSize: 50 },
                pager: po.adminPager({ allowedPageSizes: [20, 50, 100] }),
                selection: {
                    mode: "multiple",
                    showCheckBoxesMode: "always",
                    selectAllMode: "allPages"
                },
                columns: [
                    { dataField: "apartmentName", caption: modeText("property.venue.halls.name", "property.resort.chalets.name", "property.units.name") },
                    { dataField: "apartmentCode", caption: modeText("property.venue.halls.number", "property.resort.chalets.number", "property.units.number"), width: 90 },
                    { dataField: "buildingName", caption: t("property.units.block") },
                    { dataField: "floorName", caption: t("property.units.floor"), width: 90 },
                    { dataField: "roomTypeName", caption: modeText("property.venue.halls.type", "property.resort.chalets.type", "property.units.roomType") },
                    {
                        dataField: "parentApartmentName",
                        caption: t("property.units.parentChalet"),
                        visible: !!lookups.isResort,
                        width: 140
                    },
                    {
                        dataField: "childUnitCount",
                        caption: t("property.units.childRooms"),
                        visible: !!lookups.isResort,
                        width: 90
                    },
                    {
                        dataField: "resortAreaType",
                        caption: t("property.resort.area"),
                        visible: !!lookups.isResort,
                        width: 120,
                        customizeText(e) {
                            return e.value ? resortAreaLabel(e.value) : "";
                        }
                    },
                    {
                        dataField: "isActive",
                        caption: t("property.active"),
                        width: 100,
                        alignment: "center",
                        cssClass: "unit-settings-col-active",
                        allowSorting: true,
                        cellTemplate(container, options) {
                            renderActiveStatusBadge($(container), options.data && options.data.isActive);
                        }
                    },
                    {
                        type: "buttons",
                        width: lookups.isResort ? 124 : 88,
                        visible: canManageUnits(),
                        buttons: [
                            {
                                icon: "group",
                                hint: t("property.resort.chalets.manageRooms"),
                                visible: !!lookups.isResort,
                                onClick(e) {
                                    openChaletRoomsManager(e.row.data);
                                }
                            },
                            {
                                icon: "edit",
                                hint: t("common.edit"),
                                onClick(e) {
                                    openApartmentEditor(e.row.data);
                                }
                            },
                            {
                                icon: "trash",
                                hint: t("property.units.deactivate"),
                                visible: api.hasPermission("property.units.update"),
                                onClick(e) {
                                    DevExpress.ui.dialog
                                        .confirm(t("property.units.deactivateConfirm"), t("common.ok"))
                                        .done((ok) => {
                                            if (!ok) {
                                                return;
                                            }
                                            softDeactivateApartment(e.row.data);
                                        });
                                }
                            }
                        ]
                    }
                ],
                onSelectionChanged() {
                    updateBulkActionButtonsVisible();
                },
                onRowPrepared(e) {
                    if (e.rowType === "data" && e.data && e.data.isActive === false) {
                        e.rowElement.addClass("pms-grid-row-inactive");
                    }
                },
                onToolbarPreparing(e) {
                    if (canManageUnits()) {
                        e.toolbarOptions.items.unshift(
                            {
                                location: "after",
                                widget: "dxButton",
                                options: {
                                    text: t("property.units.activateSelected"),
                                    icon: "check",
                                    stylingMode: "contained",
                                    type: "default",
                                    visible: false,
                                    onInitialized(ev) {
                                        activateSelectedButton = ev.component;
                                    },
                                    onClick() {
                                        bulkSetActive(true);
                                    }
                                }
                            },
                            {
                                location: "after",
                                widget: "dxButton",
                                options: {
                                    text: t("property.units.deactivateSelected"),
                                    icon: "close",
                                    stylingMode: "outlined",
                                    visible: false,
                                    onInitialized(ev) {
                                        deactivateSelectedButton = ev.component;
                                    },
                                    onClick() {
                                        bulkSetActive(false);
                                    }
                                }
                            }
                        );
                    }
                    if (!api.hasPermission("property.units.create")) {
                        return;
                    }
                    e.toolbarOptions.items.unshift({
                        location: "after",
                        widget: "dxButton",
                        options: {
                            text: modeText("property.venue.halls.add", "property.resort.chalets.add", "property.units.add"),
                            icon: "add",
                            type: "default",
                            stylingMode: "contained",
                            onClick() {
                                openApartmentEditor(null);
                            }
                        }
                    });
                }
                })
            )
            .dxDataGrid("instance");

        window.__propertyApartmentsGrid = grid;
        window.__reloadPropertyApartments = reloadApartments;
        reloadApartments();
    }

    function openChaletRoomsManager(chaletRow) {
        const parentId = chaletParentLinkId(chaletRow);
        const chaletName = (chaletRow.apartmentName || chaletRow.apartmentCode || "").trim();
        const $content = $("<div/>");
        const $toolbar = $("<div/>").css({ marginBottom: 10 }).appendTo($content);
        const $gridHost = $("<div/>").appendTo($content);
        let childGrid;

        function reloadChildren() {
            return prop.listApartments({ parentApartmentZaaerId: parentId }).then((rows) => {
                childGrid.option("dataSource", rows || []);
            });
        }

        if (canManageUnits()) {
            $("<div/>")
                .dxButton({
                    text: t("property.resort.chalets.addRoom"),
                    icon: "add",
                    type: "default",
                    stylingMode: "contained",
                    onClick() {
                        openApartmentEditor(null, {
                            parentChalet: chaletRow,
                            isChildRoom: true,
                            onSaved: () => reloadChildren()
                        });
                    }
                })
                .appendTo($toolbar);
        }

        const po = window.Zaaer.PmsGridOptions;
        childGrid = $gridHost
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                    elementAttr: { class: "unit-settings-chalet-rooms-grid pms-grid-compact" },
                    dataSource: [],
                    keyExpr: "apartmentId",
                    height: Math.max(280, Math.floor(window.innerHeight * 0.42)),
                    wordWrapEnabled: true,
                    paging: { pageSize: 20 },
                    pager: po.adminPager({ allowedPageSizes: [10, 20, 50] }),
                    columns: [
                        { dataField: "apartmentName", caption: t("property.resort.chalets.roomName") },
                        { dataField: "apartmentCode", caption: t("property.resort.chalets.roomNumber"), width: 90 },
                        { dataField: "roomTypeName", caption: t("property.units.roomType") },
                        {
                            dataField: "isActive",
                            caption: t("property.active"),
                            width: 90,
                            alignment: "center",
                            cellTemplate(container, options) {
                                renderActiveStatusBadge($(container), options.data && options.data.isActive);
                            }
                        },
                        {
                            type: "buttons",
                            width: 72,
                            visible: canManageUnits(),
                            buttons: [
                                {
                                    icon: "edit",
                                    hint: t("common.edit"),
                                    onClick(e) {
                                        openApartmentEditor(e.row.data, {
                                            parentChalet: chaletRow,
                                            isChildRoom: true,
                                            onSaved: () => reloadChildren()
                                        });
                                    }
                                }
                            ]
                        }
                    ]
                })
            )
            .dxDataGrid("instance");

        const $popupHost = $("<div/>").appendTo("body");
        const popup = $popupHost
            .dxPopup({
                title: `${t("property.resort.chalets.manageRooms")}: ${chaletName}`,
                visible: true,
                showTitle: true,
                dragEnabled: true,
                hideOnOutsideClick: true,
                width: Math.min(900, Math.max(420, window.innerWidth - 24)),
                height: "auto",
                maxHeight: "72vh",
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                wrapperAttr: { class: "guest-picker-popup guest-picker-grid--pl" },
                contentTemplate() {
                    return $content;
                },
                toolbarItems: [
                    {
                        widget: "dxButton",
                        toolbar: "bottom",
                        location: "after",
                        options: {
                            text: t("common.close"),
                            stylingMode: "outlined",
                            onClick() {
                                popup.hide();
                            }
                        }
                    }
                ],
                onHidden() {
                    $popupHost.remove();
                }
            })
            .dxPopup("instance");

        reloadChildren();
    }

    function openApartmentEditor(row, editorContext) {
        editorContext = editorContext || {};
        const isChildRoom = !!editorContext.isChildRoom;
        const parentChalet = editorContext.parentChalet || null;

        if (!canManageUnits() && !row) {
            return;
        }

        const state = {
            apartmentCode: row ? row.apartmentCode : "",
            apartmentName: row ? row.apartmentName : "",
            buildingZaaerId: row ? row.buildingZaaerId : null,
            floorZaaerId: row ? row.floorZaaerId : null,
            roomTypeZaaerId: row ? row.roomTypeZaaerId : null,
            parentApartmentZaaerId: row ? row.parentApartmentZaaerId || null : null,
            isActive: row ? row.isActive !== false : true,
            telephoneExtension: row ? row.telephoneExtension : "",
            bathroomsCount: row ? row.bathroomsCount : 1,
            kitchenType: row ? row.kitchenType || "none" : "none",
            hallType: row ? row.hallType || "none" : "none",
            resortAreaType: row ? row.resortAreaType || null : null,
            singleBedsCount: row ? row.singleBedsCount : 0,
            doubleBedsCount: row ? row.doubleBedsCount : 0,
            area: row ? row.area : null,
            description: row ? row.description : "",
            services: row && row.services ? row.services.slice() : [],
            facilityZaaerIds: row && row.facilityZaaerIds ? row.facilityZaaerIds.slice() : []
        };

        if (isChildRoom && parentChalet) {
            state.parentApartmentZaaerId = chaletParentLinkId(parentChalet);
            if (!row) {
                state.buildingZaaerId = parentChalet.buildingZaaerId || null;
                state.floorZaaerId = parentChalet.floorZaaerId || null;
                state.resortAreaType = null;
            }
        }

        const editorTitle = isChildRoom
            ? row
                ? t("property.resort.chalets.editRoom")
                : t("property.resort.chalets.addRoom")
            : row
                ? modeText("property.venue.halls.edit", "property.resort.chalets.edit", "property.units.edit")
                : modeText("property.venue.halls.add", "property.resort.chalets.add", "property.units.add");
        const nameLabel = isChildRoom
            ? t("property.resort.chalets.roomName")
            : modeText("property.venue.halls.name", "property.resort.chalets.name", "property.units.name");
        const numberLabel = isChildRoom
            ? t("property.resort.chalets.roomNumber")
            : modeText("property.venue.halls.number", "property.resort.chalets.number", "property.units.number");
        const typeLabel = isChildRoom
            ? t("property.units.roomType")
            : modeText("property.venue.halls.type", "property.resort.chalets.type", "property.units.roomType");

        const $shell = $("<div/>");
        const $form = $("<div/>").appendTo($shell);
        const $kitchenHost = $("<div/>")
            .append($("<label/>", { class: "pms-field-label", text: t("property.units.kitchen") }))
            .appendTo($shell);
        const $kitchenChips = $("<div/>").appendTo($kitchenHost);
        const $hallHost = $("<div/>")
            .append($("<label/>", { class: "pms-field-label", text: t("property.units.hall") }))
            .appendTo($shell);
        const $hallChips = $("<div/>").appendTo($hallHost);

        renderChipGroup($kitchenChips, "kitchenType", lookups.kitchenTypes || [], kitchenLabel, state.kitchenType, (code) => {
            state.kitchenType = code;
        });
        renderChipGroup($hallChips, "hallType", lookups.hallTypes || [], hallLabel, state.hallType, (code) => {
            state.hallType = code;
        });

        openFormPopup(
            editorTitle,
            $shell,
            () => {
                const body = { ...state };
                return row
                    ? prop.updateApartment(row.apartmentId, body)
                    : prop.createApartment(body);
            },
            () => {
                if (window.__reloadPropertyApartments) {
                    window.__reloadPropertyApartments();
                }
                if (typeof editorContext.onSaved === "function") {
                    editorContext.onSaved();
                }
                refreshLookups();
            }
        );

        $form.dxForm({
            formData: state,
            labelLocation: "top",
            colCount: 2,
            items: [
                { dataField: "apartmentName", isRequired: true, label: { text: nameLabel } },
                { dataField: "apartmentCode", isRequired: true, label: { text: numberLabel } },
                {
                    dataField: "roomTypeZaaerId",
                    label: { text: typeLabel },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: isChildRoom ? roomTypeOptionsForChildRoom() : roomTypeOptions(),
                        valueExpr: "id",
                        displayExpr: "name",
                        searchEnabled: true
                    }
                },
                {
                    dataField: "buildingZaaerId",
                    label: { text: t("property.units.block") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: buildingOptions(),
                        valueExpr: "id",
                        displayExpr: "name",
                        searchEnabled: true,
                        showClearButton: true,
                        onValueChanged(e) {
                            state.buildingZaaerId = e.value;
                            state.floorZaaerId = null;
                            const floorEditor = $form.dxForm("instance").getEditor("floorZaaerId");
                            if (floorEditor) {
                                floorEditor.option("dataSource", floorOptionsForBuilding(e.value));
                                floorEditor.option("value", null);
                            }
                        }
                    }
                },
                {
                    dataField: "floorZaaerId",
                    label: { text: t("property.units.floor") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: floorOptionsForBuilding(state.buildingZaaerId),
                        valueExpr: "id",
                        displayExpr: "name",
                        searchEnabled: true,
                        showClearButton: true
                    }
                },
                {
                    dataField: "parentApartmentZaaerId",
                    label: { text: t("property.units.parentChalet") },
                    editorType: "dxSelectBox",
                    visible: !!lookups.isResort && isChildRoom,
                    editorOptions: {
                        dataSource: parentChalet
                            ? [{ id: chaletParentLinkId(parentChalet), name: parentChalet.apartmentName || parentChalet.apartmentCode }]
                            : parentChaletOptions(row && row.apartmentId),
                        valueExpr: "id",
                        displayExpr: "name",
                        readOnly: !!parentChalet,
                        showClearButton: false,
                        searchEnabled: false
                    }
                },
                {
                    dataField: "resortAreaType",
                    label: { text: t("property.resort.area") },
                    editorType: "dxSelectBox",
                    visible: !!lookups.isResort && !isChildRoom,
                    editorOptions: {
                        dataSource: resortAreaOptions(),
                        valueExpr: "id",
                        displayExpr: "name",
                        showClearButton: true,
                        searchEnabled: false
                    }
                },
                { dataField: "telephoneExtension", label: { text: t("property.units.phoneExt") } },
                {
                    dataField: "bathroomsCount",
                    label: { text: t("property.units.bathrooms") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, showSpinButtons: true }
                },
                {
                    dataField: "singleBedsCount",
                    label: { text: t("property.units.singleBeds") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, showSpinButtons: true }
                },
                {
                    dataField: "doubleBedsCount",
                    label: { text: t("property.units.doubleBeds") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, showSpinButtons: true }
                },
                {
                    dataField: "area",
                    label: { text: t("property.units.area") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, format: "#0.##" }
                },
                {
                    dataField: "services",
                    label: { text: t("property.units.services") },
                    editorType: "dxTagBox",
                    colSpan: 2,
                    editorOptions: {
                        dataSource: serviceOptionsDataSource(),
                        valueExpr: "id",
                        displayExpr: "name",
                        searchEnabled: true,
                        showSelectionControls: true
                    }
                },
                {
                    dataField: "facilityZaaerIds",
                    label: { text: t("property.units.facilities") },
                    editorType: "dxTagBox",
                    colSpan: 2,
                    editorOptions: {
                        dataSource: facilityOptionsDataSource(),
                        valueExpr: "id",
                        displayExpr: "name",
                        searchEnabled: true,
                        showSelectionControls: true
                    }
                },
                {
                    dataField: "description",
                    label: { text: t("property.units.unitDescription") },
                    editorType: "dxTextArea",
                    colSpan: 2
                },
                {
                    dataField: "isActive",
                    label: { text: t("property.active") },
                    editorType: "dxSwitch"
                }
            ],
            onFieldDataChanged(e) {
                state[e.dataField] = e.value;
            }
        });
    }

    function buildRoomTypesGrid($host) {
        const po = window.Zaaer.PmsGridOptions;
        const grid = $host
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: [],
                keyExpr: "roomTypeId",
                height: "calc(100vh - 300px)",
                columns: [
                    { dataField: "roomTypeName", caption: t("property.roomTypes.nameAr") },
                    { dataField: "roomTypeNameEn", caption: t("property.roomTypes.nameEn") },
                    { dataField: "roomCount", caption: t("property.roomTypes.roomCount"), width: 90 },
                    { dataField: "sortOrder", caption: t("property.roomTypes.sortOrder"), width: 72 },
                    {
                        dataField: "isActive",
                        caption: t("property.active"),
                        width: 72,
                        dataType: "boolean"
                    },
                    {
                        type: "buttons",
                        width: 56,
                        visible: canManageRoomTypes(),
                        buttons: [
                            {
                                icon: "edit",
                                onClick(e) {
                                    openRoomTypeEditor(e.row.data);
                                }
                            }
                        ]
                    }
                ],
                onToolbarPreparing(e) {
                    if (!api.hasPermission("property.room_types.create")) {
                        return;
                    }
                    e.toolbarOptions.items.unshift({
                        location: "after",
                        widget: "dxButton",
                        options: {
                            text: modeText("property.venue.hallTypes.add", "property.resort.chaletTypes.add", "property.roomTypes.add"),
                            icon: "add",
                            type: "default",
                            onClick() {
                                openRoomTypeEditor(null);
                            }
                        }
                    });
                }
                })
            )
            .dxDataGrid("instance");

        window.__propertyRoomTypesGrid = grid;

        function reload() {
            return prop.listRoomTypes().then((rows) => grid.option("dataSource", rows || []));
        }

        window.__reloadPropertyRoomTypes = reload;
        reload();
    }

    function openRoomTypeEditor(row) {
        const state = {
            roomTypeName: row ? row.roomTypeName : "",
            roomTypeNameEn: row ? row.roomTypeNameEn : "",
            roomTypeDesc: row ? row.roomTypeDesc : "",
            roomCategory: row ? row.roomCategory || "other" : "other",
            roomCount: row ? row.roomCount : 0,
            sortOrder: row ? row.sortOrder : 0,
            isActive: row ? row.isActive !== false : true,
            imageUrl: row ? row.imageUrl : null
        };

        const $shell = $("<div/>");
        const $form = $("<div/>").appendTo($shell);
        const $uploaderHost = $("<div/>").appendTo($shell);
        const $preview = $("<img/>", { class: "unit-settings-image-preview", hidden: true }).appendTo($shell);
        const imagePicker = createDeferredImagePicker($uploaderHost, $preview, {
            getExistingUrl: () => state.imageUrl
        });

        openFormPopup(
            row
                ? modeText("property.venue.hallTypes.edit", "property.resort.chaletTypes.edit", "property.roomTypes.edit")
                : modeText("property.venue.hallTypes.add", "property.resort.chaletTypes.add", "property.roomTypes.add"),
            $shell,
            () =>
                imagePicker.uploadPending().then((url) => {
                    if (url) {
                        state.imageUrl = url;
                    }
                    const body = { ...state };
                    return row
                        ? prop.updateRoomType(row.roomTypeId, body)
                        : prop.createRoomType(body);
                }),
            () => {
                if (window.__reloadPropertyRoomTypes) {
                    window.__reloadPropertyRoomTypes();
                }
                refreshLookups();
            }
        );

        $form.dxForm({
            formData: state,
            labelLocation: "top",
            colCount: 2,
            items: [
                {
                    dataField: "roomCategory",
                    label: { text: modeText("property.venue.hallTypes.category", "property.resort.chaletTypes.category", "property.roomTypes.category") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: roomCategoryOptions(),
                        valueExpr: "id",
                        displayExpr: "name",
                        searchEnabled: false
                    }
                },
                { dataField: "roomTypeName", isRequired: true, label: { text: t("property.roomTypes.nameAr") }, colSpan: 2 },
                { dataField: "roomTypeNameEn", label: { text: t("property.roomTypes.nameEn") }, colSpan: 2 },
                {
                    dataField: "roomTypeDesc",
                    label: { text: t("property.roomTypes.description") },
                    editorType: "dxTextArea",
                    colSpan: 2
                },
                {
                    dataField: "roomCount",
                    label: { text: t("property.roomTypes.roomCount") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, showSpinButtons: true }
                },
                {
                    dataField: "sortOrder",
                    label: { text: t("property.roomTypes.sortOrder") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0, showSpinButtons: true }
                },
                {
                    dataField: "isActive",
                    label: { text: t("property.active") },
                    editorType: "dxSwitch"
                }
            ],
            onFieldDataChanged(e) {
                state[e.dataField] = e.value;
            }
        });
    }

    function buildFacilitiesGrid($host) {
        const po = window.Zaaer.PmsGridOptions;
        const grid = $host
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: [],
                keyExpr: "facilityId",
                height: "calc(100vh - 300px)",
                columns: [
                    { dataField: "facilityName", caption: t("property.facilities.name") },
                    { dataField: "buildingName", caption: t("property.units.block") },
                    { dataField: "floorName", caption: t("property.units.floor") },
                    {
                        dataField: "isActive",
                        caption: t("property.active"),
                        width: 72,
                        dataType: "boolean"
                    },
                    {
                        type: "buttons",
                        width: 56,
                        visible: canManageFacilities(),
                        buttons: [
                            {
                                icon: "edit",
                                onClick(e) {
                                    openFacilityEditor(e.row.data);
                                }
                            }
                        ]
                    }
                ],
                onToolbarPreparing(e) {
                    if (!api.hasPermission("property.facilities.create")) {
                        return;
                    }
                    e.toolbarOptions.items.unshift({
                        location: "after",
                        widget: "dxButton",
                        options: {
                            text: t("property.facilities.add"),
                            icon: "add",
                            type: "default",
                            onClick() {
                                openFacilityEditor(null);
                            }
                        }
                    });
                }
                })
            )
            .dxDataGrid("instance");

        window.__propertyFacilitiesGrid = grid;

        function reload() {
            return prop.listFacilities().then((rows) => grid.option("dataSource", rows || []));
        }

        window.__reloadPropertyFacilities = reload;
        reload();
    }

    function openFacilityEditor(row) {
        const state = {
            facilityName: row ? row.facilityName : "",
            facilityNameEn: row ? row.facilityNameEn || "" : "",
            description: row ? row.description : "",
            buildingZaaerId: row ? row.buildingZaaerId : null,
            floorZaaerId: row ? row.floorZaaerId : null,
            isActive: row ? row.isActive !== false : true,
            imageUrls: row && row.imageUrls ? row.imageUrls.slice() : []
        };

        const $shell = $("<div/>");
        const $form = $("<div/>").appendTo($shell);
        const $uploaderHost = $("<div/>").appendTo($shell);
        const $preview = $("<img/>", { class: "unit-settings-image-preview", hidden: true }).appendTo($shell);
        const imagePicker = createDeferredImagePicker($uploaderHost, $preview, {
            multiple: true,
            getExistingUrls: () => state.imageUrls || []
        });

        openFormPopup(
            row ? t("property.facilities.edit") : t("property.facilities.add"),
            $shell,
            () =>
                imagePicker.uploadPending().then((urls) => {
                    const body = { ...state, imageUrls: urls || [] };
                    return row
                        ? prop.updateFacility(row.facilityId, body)
                        : prop.createFacility(body);
                }),
            () => {
                if (window.__reloadPropertyFacilities) {
                    window.__reloadPropertyFacilities();
                }
            }
        );

        $form.dxForm({
            formData: state,
            labelLocation: "top",
            colCount: 2,
            items: [
                { dataField: "facilityName", isRequired: true, label: { text: t("property.facilities.name") }, colSpan: 2 },
                {
                    dataField: "facilityNameEn",
                    label: { text: t("property.facilities.nameEn") },
                    colSpan: 2
                },
                {
                    dataField: "buildingZaaerId",
                    label: { text: t("property.units.block") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: buildingOptions(),
                        valueExpr: "id",
                        displayExpr: "name",
                        showClearButton: true,
                        searchEnabled: true,
                        onValueChanged(e) {
                            state.buildingZaaerId = e.value;
                            state.floorZaaerId = null;
                            const floorEditor = $form.dxForm("instance").getEditor("floorZaaerId");
                            if (floorEditor) {
                                floorEditor.option("dataSource", floorOptionsForBuilding(e.value));
                                floorEditor.option("value", null);
                            }
                        }
                    }
                },
                {
                    dataField: "floorZaaerId",
                    label: { text: t("property.units.floor") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        dataSource: floorOptionsForBuilding(state.buildingZaaerId),
                        valueExpr: "id",
                        displayExpr: "name",
                        showClearButton: true,
                        searchEnabled: true
                    }
                },
                {
                    dataField: "description",
                    label: { text: t("property.facilities.description") },
                    editorType: "dxTextArea",
                    colSpan: 2
                },
                {
                    dataField: "isActive",
                    label: { text: t("property.active") },
                    editorType: "dxSwitch"
                }
            ],
            onFieldDataChanged(e) {
                state[e.dataField] = e.value;
            }
        });
    }

    function renderUnitSettingsTabTitle(item, _index, element) {
        const $el = $(element).empty();
        const tabId = item && item.id ? item.id : "tab";
        const icon = (item && item.icon) || "folder";
        const label = (item && (item.text || item.title)) || "";

        $("<div/>")
            .addClass(`unit-settings-tab-title unit-settings-tab-title--${tabId}`)
            .append(
                $("<span/>")
                    .addClass(`dx-icon dx-icon-${icon}`)
                    .attr("aria-hidden", "true"),
                $("<span/>").addClass("unit-settings-tab-title-text").text(label)
            )
            .appendTo($el);
    }

    function initTabs() {
        const $tabsHost = $("#unitSettingsTabs");
        if ($tabsHost.data("dxTabPanel")) {
            $tabsHost.dxTabPanel("dispose");
        }

        $tabsHost.dxTabPanel({
            rtlEnabled: isAr(),
            animationEnabled: true,
            deferRendering: false,
            stylingMode: "primary",
            iconPosition: "start",
            scrollingEnabled: true,
            scrollByContent: true,
            showNavButtons: true,
            elementAttr: { class: "unit-settings-tabpanel" },
            itemTitleTemplate: renderUnitSettingsTabTitle,
            items: [
                {
                    id: "units",
                    title: modeText("property.venue.tabs.halls", "property.resort.tabs.chalets", "property.tabs.units"),
                    text: modeText("property.venue.tabs.halls", "property.resort.tabs.chalets", "property.tabs.units"),
                    icon: "product",
                    template() {
                        const $g = $("<div/>");
                        buildApartmentsGrid($g);
                        return $g;
                    }
                },
                {
                    id: "buildings",
                    title: t("property.tabs.buildings"),
                    text: t("property.tabs.buildings"),
                    icon: "home",
                    template() {
                        const $wrap = $("<div/>");
                        if (canManageBuildings()) {
                            $("<div/>")
                                .css({ marginBottom: 10 })
                                .append(
                                    $("<div/>").dxButton({
                                        text: t("property.buildings.add"),
                                        icon: "add",
                                        type: "default",
                                        stylingMode: "contained",
                                        onClick() {
                                            openBuildingEditor(null);
                                        }
                                    })
                                )
                                .appendTo($wrap);
                        }
                        $("<div/>", { id: "propertyBuildingsCards", class: "pms-building-cards" }).appendTo($wrap);
                        return $wrap;
                    }
                },
                {
                    id: "roomTypes",
                    title: modeText("property.venue.tabs.hallTypes", "property.resort.tabs.chaletTypes", "property.tabs.roomTypes"),
                    text: modeText("property.venue.tabs.hallTypes", "property.resort.tabs.chaletTypes", "property.tabs.roomTypes"),
                    icon: "tags",
                    template() {
                        const $g = $("<div/>");
                        buildRoomTypesGrid($g);
                        return $g;
                    }
                },
                {
                    id: "facilities",
                    title: t("property.tabs.facilities"),
                    text: t("property.tabs.facilities"),
                    icon: "favorites",
                    template() {
                        const $g = $("<div/>");
                        buildFacilitiesGrid($g);
                        return $g;
                    }
                }
            ],
            onSelectionChanged(e) {
                if (e.component.option("selectedIndex") === 1) {
                    renderBuildingCards();
                }
            }
        });
    }

    function init() {
        if (!canView()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        loadPanel = $("#unitSettingsLoadPanel")
            .dxLoadPanel({ shadingColor: "rgba(15,23,42,0.12)", visible: false })
            .dxLoadPanel("instance");

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-property-settings",
            onRefresh() {
                withLoad(
                    refreshLookups().then(() => {
                        refreshBuildings();
                        if (window.__reloadPropertyApartments) {
                            window.__reloadPropertyApartments();
                        }
                        if (window.__reloadPropertyRoomTypes) {
                            window.__reloadPropertyRoomTypes();
                        }
                        if (window.__reloadPropertyFacilities) {
                            window.__reloadPropertyFacilities();
                        }
                    })
                );
            }
        });

        withLoad(
            refreshLookups()
                .then(() => {
                    initTabs();
                    refreshBuildings();
                })
                .catch((err) => {
                    console.error("unit-settings init failed", err);
                    DevExpress.ui.notify(t("common.loadFailed") || "Failed to load settings.", "error", 4200);
                })
        );
    }

    $(init);
})(window, jQuery);
