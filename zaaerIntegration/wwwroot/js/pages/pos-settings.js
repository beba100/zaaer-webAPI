(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const pos = window.Zaaer.PosService;
    const api = window.Zaaer.ApiService;

    let outletsCache = [];
    let categoriesCache = [];
    let loadPanel;
    const SG = window.Zaaer && window.Zaaer.SaveGuard;

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    function canView() {
        return api.hasPermission("pos.settings.view");
    }

    function canManage() {
        return api.hasPermission("pos.settings.manage");
    }

    function label(row, enField, arField) {
        if (!row) {
            return "";
        }

        if (isAr()) {
            return row[arField] || row[enField] || "";
        }

        return row[enField] || row[arField] || "";
    }

    function withLoad(promise) {
        loadPanel.show();
        return $.when(promise).always(() => loadPanel.hide());
    }

    function openFormPopup(title, formItems, initial, onSave, onDone) {
        const $popup = $("<div>").appendTo("body");
        const $form = $("<div>").appendTo($popup);
        let formInstance;
        let saveButton;
        const popupSaveGuard = SG ? SG.create() : null;

        $popup.dxPopup({
            title,
            visible: true,
            showCloseButton: true,
            width: Math.min(560, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate() {
                return $form;
            },
            onShown() {
                formInstance = $form
                    .dxForm({
                        formData: { ...initial },
                        labelLocation: "top",
                        colCount: 2,
                        items: formItems
                    })
                    .dxForm("instance");
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
                            const data = formInstance.option("formData");
                            const work = () =>
                                Promise.resolve(onSave(data)).then(() => {
                                    DevExpress.ui.notify(t("pos.settings.saved"), "success", 2200);
                                    if (typeof onDone === "function") {
                                        onDone();
                                    }
                                });

                            let ran;
                            if (SG && popupSaveGuard) {
                                ran = SG.run(
                                    popupSaveGuard,
                                    () => {
                                        return work().then(() => {
                                            SG.closePopupThenRun($popup);
                                        });
                                    },
                                    { button: saveButton }
                                );
                            } else {
                                ran = work().then(() => {
                                    $popup.dxPopup("instance").hide();
                                });
                            }

                            if (ran === false) {
                                return;
                            }
                            if (ran && typeof ran.catch === "function") {
                                ran.catch((xhr) => {
                                    const msg =
                                        (xhr && xhr.responseJSON && xhr.responseJSON.message) ||
                                        t("common.error");
                                    DevExpress.ui.notify(msg, "error", 3200);
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

    function renderOutletCards() {
        const $host = $("#posOutletsHost");
        $host.empty();
        if (!outletsCache.length) {
            $("<p>").addClass("pos-empty-hint").text(t("activityLog.empty")).appendTo($host);
            return;
        }

        const $grid = $("<div>").addClass("pos-outlet-grid").appendTo($host);
        outletsCache.forEach((o) => {
            const name = label(o, "outletName", "outletNameAr");
            const open = (o.status || "").toLowerCase() === "open";
            const $card = $("<article>").addClass("pos-outlet-card").appendTo($grid);
            const $head = $("<div>").addClass("pos-outlet-card__head").appendTo($card);
            $("<div>").addClass("pos-outlet-card__name").text(name).appendTo($head);
            $("<span>")
                .addClass(`pos-outlet-card__badge${open ? "" : " pos-outlet-card__badge--closed"}`)
                .text(open ? t("pos.settings.open") : t("pos.settings.closed"))
                .appendTo($head);
            if (o.location) {
                $("<div>").addClass("pos-outlet-card__meta").text(o.location).appendTo($card);
            }
            $("<div>")
                .addClass("pos-outlet-card__meta")
                .text(
                    `${t("pos.settings.itemsCount").replace("{0}", o.itemCount || 0)} · ${t("pos.settings.tablesCount").replace("{0}", o.tableCount || 0)}`
                )
                .appendTo($card);
            if (canManage()) {
                $("<div>")
                    .append(
                        $("<div>").dxButton({
                            text: t("pos.settings.edit"),
                            icon: "edit",
                            stylingMode: "outlined",
                            type: "default",
                            onClick() {
                                openOutletEditor(o);
                            }
                        })
                    )
                    .appendTo($card);
            }
        });
    }

    function openOutletEditor(row) {
        openFormPopup(
            row ? t("pos.settings.edit") : t("pos.settings.add"),
            [
                { dataField: "outletName", label: { text: t("pos.settings.tab.outlets") }, isRequired: true },
                { dataField: "outletNameAr", label: { text: t("rbac.roles.nameAr") } },
                { dataField: "location", label: { text: t("pos.settings.location") } },
                {
                    dataField: "status",
                    label: { text: t("pos.settings.status") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        items: [
                            { id: "Open", name: t("pos.settings.open") },
                            { id: "Closed", name: t("pos.settings.closed") }
                        ],
                        valueExpr: "id",
                        displayExpr: "name"
                    }
                },
                { dataField: "isActive", label: { text: t("pos.settings.active") }, editorType: "dxCheckBox" }
            ],
            row || { status: "Open", isActive: true },
            (data) => {
                const body = {
                    outletName: data.outletName,
                    outletNameAr: data.outletNameAr,
                    location: data.location,
                    status: data.status,
                    isActive: !!data.isActive
                };
                return row
                    ? pos.updateOutlet(row.outletId, body)
                    : pos.createOutlet(body);
            },
            () => refreshOutlets()
        );
    }

    function refreshOutlets() {
        return withLoad(pos.listOutlets().then((rows) => {
            outletsCache = rows || [];
            renderOutletCards();
        }));
    }

    function refreshCategories() {
        return withLoad(pos.listCategories().then((rows) => {
            categoriesCache = rows || [];
            if (window.__posCategoriesGrid) {
                window.__posCategoriesGrid.option("dataSource", categoriesCache);
            }
        }));
    }

    function buildCategoriesGrid($host) {
        const po = window.Zaaer.PmsGridOptions;
        const grid = $host
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: [],
                keyExpr: "categoryId",
                height: "calc(100vh - 300px)",
                columns: [
                    {
                        caption: t("pos.settings.category"),
                        calculateCellValue: (r) => label(r, "categoryName", "categoryNameAr")
                    },
                    { dataField: "sortOrder", caption: t("pos.settings.sortOrder"), width: 72 },
                    { dataField: "itemCount", caption: t("pos.settings.itemsCount").replace("{0}", ""), width: 80 },
                    {
                        dataField: "isActive",
                        caption: t("pos.settings.active"),
                        width: 72,
                        dataType: "boolean"
                    },
                    {
                        type: "buttons",
                        width: 56,
                        visible: canManage(),
                        buttons: [
                            {
                                icon: "edit",
                                hint: t("pos.settings.edit"),
                                onClick(e) {
                                    openCategoryEditor(e.row.data);
                                }
                            }
                        ]
                    }
                ],
                onToolbarPreparing(e) {
                    if (!canManage()) {
                        return;
                    }

                    e.toolbarOptions.items.unshift({
                        location: "after",
                        widget: "dxButton",
                        options: {
                            text: t("pos.settings.add"),
                            icon: "add",
                            type: "default",
                            stylingMode: "contained",
                            onClick() {
                                openCategoryEditor(null);
                            }
                        }
                    });
                }
                })
            )
            .dxDataGrid("instance");
        window.__posCategoriesGrid = grid;
        refreshCategories();
    }

    function openCategoryEditor(row) {
        openFormPopup(
            row ? t("pos.settings.edit") : t("pos.settings.add"),
            [
                { dataField: "categoryName", isRequired: true, label: { text: t("pos.settings.category") } },
                { dataField: "categoryNameAr", label: { text: t("rbac.roles.nameAr") } },
                { dataField: "description", label: { text: t("rbac.roles.description") }, editorType: "dxTextArea" },
                { dataField: "sortOrder", label: { text: t("pos.settings.sortOrder") }, editorType: "dxNumberBox" },
                { dataField: "isActive", label: { text: t("pos.settings.active") }, editorType: "dxCheckBox" }
            ],
            row || { sortOrder: 0, isActive: true },
            (data) => {
                const body = {
                    categoryName: data.categoryName,
                    categoryNameAr: data.categoryNameAr,
                    description: data.description,
                    sortOrder: Number(data.sortOrder) || 0,
                    isActive: !!data.isActive
                };
                return row
                    ? pos.updateCategory(row.categoryId, body)
                    : pos.createCategory(body);
            },
            () => refreshCategories()
        );
    }

    function buildItemsGrid($host) {
        const po = window.Zaaer.PmsGridOptions;
        const grid = $host
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: [],
                keyExpr: "itemId",
                height: "calc(100vh - 300px)",
                columns: [
                    {
                        caption: "",
                        width: 48,
                        allowFiltering: false,
                        allowSorting: false,
                        cellTemplate(_container, options) {
                            const src =
                                options.data.imageUrl || options.data.image_url
                                    ? (function () {
                                          const raw = (options.data.imageUrl || options.data.image_url).trim();
                                          if (/^https?:\/\//i.test(raw)) {
                                              return raw;
                                          }
                                          return raw.startsWith("/")
                                              ? window.location.origin + raw
                                              : window.location.origin + "/" + raw;
                                      })()
                                    : null;
                            if (src) {
                                $("<img>")
                                    .addClass("pos-item-thumb")
                                    .attr({ src, alt: "" })
                                    .appendTo(_container);
                            }
                        }
                    },
                    {
                        caption: t("pos.settings.tab.items"),
                        calculateCellValue: (r) => label(r, "itemName", "itemNameAr"),
                        minWidth: 140
                    },
                    { dataField: "itemCode", caption: t("pos.settings.code"), width: 90 },
                    { dataField: "price", caption: t("pos.settings.price"), width: 90, format: "#,##0.00" },
                    { dataField: "categoryName", caption: t("pos.settings.category"), minWidth: 110 },
                    { dataField: "outletName", caption: t("pos.settings.outlet"), minWidth: 110 },
                    {
                        dataField: "isActive",
                        caption: t("pos.settings.active"),
                        width: 72,
                        dataType: "boolean"
                    },
                    {
                        type: "buttons",
                        width: 56,
                        visible: canManage(),
                        buttons: [
                            {
                                icon: "edit",
                                hint: t("pos.settings.edit"),
                                onClick(e) {
                                    openItemEditor(e.row.data);
                                }
                            }
                        ]
                    }
                ],
                onToolbarPreparing(e) {
                    if (!canManage()) {
                        return;
                    }

                    e.toolbarOptions.items.unshift({
                        location: "after",
                        widget: "dxButton",
                        options: {
                            text: t("pos.settings.add"),
                            icon: "add",
                            type: "default",
                            stylingMode: "contained",
                            onClick() {
                                openItemEditor(null);
                            }
                        }
                    });
                }
                })
            )
            .dxDataGrid("instance");
        window.__posItemsGrid = grid;

        function reload() {
            return withLoad(
                pos.listItems().then((rows) => {
                    grid.option("dataSource", rows || []);
                })
            );
        }

        reload();
        window.__posReloadItems = reload;
    }

    function resolvePreviewUrl(url) {
        if (!url || `${url}`.trim() === "") {
            return null;
        }

        const raw = `${url}`.trim();
        if (/^https?:\/\//i.test(raw) || raw.startsWith("data:")) {
            return raw;
        }

        if (raw.startsWith("//")) {
            return `${window.location.protocol}${raw}`;
        }

        if (raw.startsWith("/")) {
            return `${window.location.origin}${raw}`;
        }

        return `${window.location.origin}/${raw.replace(/^\.\//, "")}`;
    }

    function openItemEditor(row) {
        const outletItems = outletsCache.map((o) => ({
            id: o.outletId,
            name: label(o, "outletName", "outletNameAr")
        }));
        const categoryItems = categoriesCache.map((c) => ({
            id: c.categoryId,
            name: label(c, "categoryName", "categoryNameAr")
        }));

        const itemFormItems = [
                { dataField: "itemName", isRequired: true, label: { text: t("pos.settings.tab.items") } },
                { dataField: "itemNameAr", label: { text: t("rbac.roles.nameAr") } },
                { dataField: "itemCode", label: { text: t("pos.settings.code") } },
                {
                    dataField: "outletId",
                    label: { text: t("pos.settings.outlet") },
                    editorType: "dxSelectBox",
                    editorOptions: { items: outletItems, valueExpr: "id", displayExpr: "name", showClearButton: true }
                },
                {
                    dataField: "categoryId",
                    label: { text: t("pos.settings.category") },
                    editorType: "dxSelectBox",
                    editorOptions: { items: categoryItems, valueExpr: "id", displayExpr: "name", showClearButton: true }
                },
                {
                    dataField: "price",
                    label: { text: t("pos.settings.price") },
                    editorType: "dxNumberBox",
                    editorOptions: { format: "#,##0.00", min: 0 }
                },
                {
                    dataField: "quantity",
                    label: { text: t("pos.settings.quantity") },
                    editorType: "dxNumberBox"
                },
                {
                    dataField: "imageUrl",
                    label: { text: t("pos.settings.imageUrl") },
                    editorType: "dxTextBox",
                    editorOptions: {
                        placeholder: t("pos.settings.imageUrlHint")
                    }
                },
                {
                    dataField: "includesTax",
                    label: { text: t("pos.settings.includesTax") },
                    editorType: "dxCheckBox"
                },
                { dataField: "isActive", label: { text: t("pos.settings.active") }, editorType: "dxCheckBox" }
        ];

        const initial = row
            ? {
                  ...row,
                  imageUrl: row.imageUrl || row.image_url || ""
              }
            : { price: 0, includesTax: false, isActive: true, imageUrl: "" };

        const $popup = $("<div>").appendTo("body");
        const $shell = $("<div>").addClass("pos-item-editor-shell").appendTo($popup);
        const $uploadBlock = $("<div>").addClass("pos-item-upload-block").appendTo($shell);
        $("<div>").addClass("pos-item-upload-label").text(t("pos.settings.uploadImage")).appendTo($uploadBlock);
        const $preview = $("<div>").addClass("pos-item-upload-preview").appendTo($uploadBlock);
        const $uploaderHost = $("<div>").appendTo($uploadBlock);
        const $form = $("<div>").appendTo($shell);
        let formInstance;
        let itemSaveButton;
        const itemSaveGuard = SG ? SG.create() : null;

        function renderPreview(imageUrl) {
            $preview.empty();
            const src = resolvePreviewUrl(imageUrl);
            if (!src) {
                return;
            }

            $("<img>").attr({ src, alt: "" }).appendTo($preview);
            $("<button>", { type: "button" })
                .addClass("pos-item-upload-clear")
                .text(t("pos.settings.removeImage"))
                .on("click", () => {
                    formInstance.updateData("imageUrl", "");
                    renderPreview("");
                })
                .appendTo($preview);
        }

        function saveItem(data) {
            const body = {
                itemName: data.itemName,
                itemNameAr: data.itemNameAr,
                itemCode: data.itemCode,
                outletId: data.outletId || null,
                categoryId: data.categoryId || null,
                price: Number(data.price) || 0,
                quantity: data.quantity != null ? Number(data.quantity) : null,
                imageUrl: data.imageUrl ? `${data.imageUrl}`.trim() : null,
                includesTax: !!data.includesTax,
                isActive: !!data.isActive
            };
            return row ? pos.updateItem(row.itemId, body) : pos.createItem(body);
        }

        $popup.dxPopup({
            title: row ? t("pos.settings.edit") : t("pos.settings.add"),
            visible: true,
            showCloseButton: true,
            width: Math.min(720, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "62vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
            contentTemplate() {
                return $shell;
            },
            onShown() {
                formInstance = $form
                    .dxForm({
                        formData: { ...initial },
                        labelLocation: "top",
                        colCount: 2,
                        items: itemFormItems
                    })
                    .dxForm("instance");

                $uploaderHost.dxFileUploader({
                    accept: "image/*",
                    uploadMode: "instantly",
                    uploadUrl: pos.uploadItemImageUrl(),
                    uploadHeaders: pos.uploadItemImageHeaders(),
                    name: "file",
                    multiple: false,
                    maxFileSize: 3145728,
                    allowedFileExtensions: [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"],
                    selectButtonText: t("pos.settings.pickImage"),
                    labelText: t("pos.settings.dropImage"),
                    onUploaded(e) {
                        try {
                            const res = JSON.parse(e.request.response);
                            const url = res && res.data && res.data.imageUrl;
                            if (url) {
                                formInstance.updateData("imageUrl", url);
                                renderPreview(url);
                                DevExpress.ui.notify(t("pos.settings.imageUploaded"), "success", 2200);
                            }
                        } catch {
                            DevExpress.ui.notify(t("pos.settings.imageUploadFailed"), "error", 3200);
                        }
                    },
                    onUploadError() {
                        DevExpress.ui.notify(t("pos.settings.imageUploadFailed"), "error", 3200);
                    }
                });

                renderPreview(initial.imageUrl);
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
                            itemSaveButton = e.component;
                        },
                        onClick() {
                            const data = formInstance.option("formData");
                            const work = () =>
                                Promise.resolve(saveItem(data)).then(() => {
                                    DevExpress.ui.notify(t("pos.settings.saved"), "success", 2200);
                                    if (window.__posReloadItems) {
                                        window.__posReloadItems();
                                    }
                                    refreshOutlets();
                                });

                            let ran;
                            if (SG && itemSaveGuard) {
                                ran = SG.run(
                                    itemSaveGuard,
                                    () =>
                                        work().then(() => {
                                            SG.closePopupThenRun($popup);
                                        }),
                                    { button: itemSaveButton }
                                );
                            } else {
                                ran = work().then(() => {
                                    $popup.dxPopup("instance").hide();
                                });
                            }

                            if (ran === false) {
                                return;
                            }
                            if (ran && typeof ran.catch === "function") {
                                ran.catch((xhr) => {
                                    const msg =
                                        (xhr && xhr.responseJSON && xhr.responseJSON.message) ||
                                        t("common.error");
                                    DevExpress.ui.notify(msg, "error", 3200);
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

    function buildTablesGrid($host) {
        const po = window.Zaaer.PmsGridOptions;
        const grid = $host
            .dxDataGrid(
                po.merge(po.adminBaseline(), {
                dataSource: [],
                keyExpr: "tableId",
                height: "calc(100vh - 300px)",
                columns: [
                    {
                        caption: t("pos.settings.tab.tables"),
                        calculateCellValue: (r) => label(r, "tableName", "tableNameAr")
                    },
                    { dataField: "outletName", caption: t("pos.settings.outlet"), minWidth: 120 },
                    { dataField: "capacity", caption: t("pos.settings.capacity"), width: 72 },
                    { dataField: "status", caption: t("pos.settings.tableStatus"), width: 100 },
                    {
                        dataField: "isActive",
                        caption: t("pos.settings.active"),
                        width: 72,
                        dataType: "boolean"
                    },
                    {
                        type: "buttons",
                        width: 56,
                        visible: canManage(),
                        buttons: [
                            {
                                icon: "edit",
                                hint: t("pos.settings.edit"),
                                onClick(e) {
                                    openTableEditor(e.row.data);
                                }
                            }
                        ]
                    }
                ],
                onToolbarPreparing(e) {
                    if (!canManage()) {
                        return;
                    }

                    e.toolbarOptions.items.unshift({
                        location: "after",
                        widget: "dxButton",
                        options: {
                            text: t("pos.settings.add"),
                            icon: "add",
                            type: "default",
                            stylingMode: "contained",
                            onClick() {
                                openTableEditor(null);
                            }
                        }
                    });
                }
                })
            )
            .dxDataGrid("instance");

        function reload() {
            return withLoad(
                pos.listTables().then((rows) => {
                    grid.option("dataSource", rows || []);
                })
            );
        }

        reload();
        window.__posReloadTables = reload;
    }

    function openTableEditor(row) {
        const outletItems = outletsCache.map((o) => ({
            id: o.outletId,
            name: label(o, "outletName", "outletNameAr")
        }));

        openFormPopup(
            row ? t("pos.settings.edit") : t("pos.settings.add"),
            [
                {
                    dataField: "outletId",
                    isRequired: true,
                    label: { text: t("pos.settings.outlet") },
                    editorType: "dxSelectBox",
                    editorOptions: { items: outletItems, valueExpr: "id", displayExpr: "name" }
                },
                { dataField: "tableName", isRequired: true, label: { text: t("pos.settings.tab.tables") } },
                { dataField: "tableNameAr", label: { text: t("rbac.roles.nameAr") } },
                { dataField: "description", label: { text: t("rbac.roles.description") }, editorType: "dxTextArea" },
                {
                    dataField: "capacity",
                    label: { text: t("pos.settings.capacity") },
                    editorType: "dxNumberBox",
                    editorOptions: { min: 0 }
                },
                {
                    dataField: "status",
                    label: { text: t("pos.settings.tableStatus") },
                    editorType: "dxSelectBox",
                    editorOptions: {
                        items: [
                            { id: "Available", name: t("pos.settings.available") },
                            { id: "Occupied", name: t("reservationDetail.units.statusCheckedIn") },
                            { id: "Reserved", name: t("reservationDetail.status.confirmed") }
                        ],
                        valueExpr: "id",
                        displayExpr: "name"
                    }
                },
                { dataField: "isActive", label: { text: t("pos.settings.active") }, editorType: "dxCheckBox" }
            ],
            row || { status: "Available", isActive: true },
            (data) => {
                const body = {
                    outletId: data.outletId,
                    tableName: data.tableName,
                    tableNameAr: data.tableNameAr,
                    description: data.description,
                    capacity: data.capacity != null ? Number(data.capacity) : null,
                    status: data.status,
                    isActive: !!data.isActive
                };
                return row ? pos.updateTable(row.tableId, body) : pos.createTable(body);
            },
            () => {
                if (window.__posReloadTables) {
                    window.__posReloadTables();
                }
                refreshOutlets();
            }
        );
    }

    function initTabs() {
        $("#posSettingsTabs").dxTabPanel({
            rtlEnabled: isAr(),
            animationEnabled: true,
            deferRendering: false,
            items: [
                {
                    title: t("pos.settings.tab.outlets"),
                    template() {
                        const $wrap = $("<div>");
                        if (canManage()) {
                            $("<div>")
                                .css({ marginBottom: 10 })
                                .append(
                                    $("<div>").dxButton({
                                        text: t("pos.settings.add"),
                                        icon: "add",
                                        type: "default",
                                        stylingMode: "contained",
                                        onClick() {
                                            openOutletEditor(null);
                                        }
                                    })
                                )
                                .appendTo($wrap);
                        }
                        $("<div>").attr("id", "posOutletsHost").appendTo($wrap);
                        return $wrap;
                    }
                },
                {
                    title: t("pos.settings.tab.categories"),
                    template() {
                        const $g = $("<div>");
                        buildCategoriesGrid($g);
                        return $g;
                    }
                },
                {
                    title: t("pos.settings.tab.items"),
                    template() {
                        const $g = $("<div>");
                        buildItemsGrid($g);
                        return $g;
                    }
                },
                {
                    title: t("pos.settings.tab.tables"),
                    template() {
                        const $g = $("<div>");
                        buildTablesGrid($g);
                        return $g;
                    }
                }
            ],
            onSelectionChanged() {
                renderOutletCards();
            }
        });
    }

    function init() {
        if (!canView()) {
            DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
            return;
        }

        loadPanel = $("#posSettingsLoadPanel")
            .dxLoadPanel({ shadingColor: "rgba(15,23,42,0.12)", visible: false })
            .dxLoadPanel("instance");

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-pos-settings",
            onRefresh() {
                refreshOutlets();
                refreshCategories();
                if (window.__posReloadItems) {
                    window.__posReloadItems();
                }
                if (window.__posReloadTables) {
                    window.__posReloadTables();
                }
            }
        });

        if (api.hasPermission("pos.view")) {
            $("#posOpenTerminalBtn").dxButton({
                text: t("pos.settings.openTerminal"),
                icon: "cart",
                type: "default",
                stylingMode: "contained",
                onClick() {
                    window.location.href = "/pos.html";
                }
            });
        }

        initTabs();
        refreshOutlets().then(() => refreshCategories());
    }

    $(init);
})(window, jQuery);
