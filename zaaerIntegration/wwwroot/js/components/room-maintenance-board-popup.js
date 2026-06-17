(function (window, $, DevExpress) {
    "use strict";

    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function reasonOptions(t) {
        return [
            { id: "maintenance", text: t("roomBoard.maintenance.reasonMaintenance") },
            { id: "staff_shortage", text: t("roomBoard.maintenance.reasonStaffShortage") },
            { id: "owner_request", text: t("roomBoard.maintenance.reasonOwnerRequest") },
            { id: "other", text: t("roomBoard.maintenance.reasonOther") }
        ];
    }

    function categoryOptions(t) {
        return [
            { id: "ac", text: t("roomBoard.maintenance.category.ac") },
            { id: "water_heater", text: t("roomBoard.maintenance.category.waterHeater") },
            { id: "plumbing", text: t("roomBoard.maintenance.category.plumbing") },
            { id: "electrical", text: t("roomBoard.maintenance.category.electrical") },
            { id: "paint", text: t("roomBoard.maintenance.category.paint") },
            { id: "flooring", text: t("roomBoard.maintenance.category.flooring") },
            { id: "doors_locks", text: t("roomBoard.maintenance.category.doorsLocks") },
            { id: "furniture", text: t("roomBoard.maintenance.category.furniture") },
            { id: "appliances", text: t("roomBoard.maintenance.category.appliances") },
            { id: "kitchen", text: t("roomBoard.maintenance.category.kitchen") },
            { id: "bathroom", text: t("roomBoard.maintenance.category.bathroom") },
            { id: "pest_control", text: t("roomBoard.maintenance.category.pestControl") },
            { id: "deep_cleaning", text: t("roomBoard.maintenance.category.deepCleaning") },
            { id: "wifi", text: t("roomBoard.maintenance.category.wifi") },
            { id: "other", text: t("roomBoard.maintenance.category.other") }
        ];
    }

    function parseMaintenanceCategories(value) {
        if (Array.isArray(value)) {
            return value.map((x) => `${x || ""}`.trim()).filter(Boolean);
        }
        if (typeof value === "string" && value.trim()) {
            return value
                .split(",")
                .map((x) => x.trim())
                .filter(Boolean);
        }
        return [];
    }

    function maintenanceReasonLabel(reasonId, t) {
        const id = `${reasonId || ""}`.trim() || "maintenance";
        const match = reasonOptions(t).find((x) => x.id === id);
        return match ? match.text : id;
    }

    function formatMaintenanceCategoryLabels(categories, t) {
        const ids = parseMaintenanceCategories(categories);
        if (!ids.length) {
            return "";
        }
        const lookup = categoryOptions(t);
        return ids
            .map((id) => {
                const match = lookup.find((x) => x.id === id);
                return match ? match.text : id;
            })
            .join("، ");
    }

    function formatMaintenanceDisplayLabel(room, t) {
        const reason = maintenanceReasonLabel(room && room.maintenanceReason, t);
        const cats = formatMaintenanceCategoryLabels(room && room.maintenanceCategories, t);
        if (reason && cats) {
            return `${reason} - ${cats}`;
        }
        return cats || reason || "";
    }

    function formatMaintenanceDateDisplay(value) {
        const s = `${value ?? ""}`.trim();
        if (!s) {
            return "";
        }

        if (/^\d{2}\/\d{2}\/\d{4}$/.test(s)) {
            return s;
        }

        const isoMatch = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
        if (isoMatch) {
            return `${isoMatch[3]}/${isoMatch[2]}/${isoMatch[1]}`;
        }

        const d = new Date(s);
        if (!Number.isNaN(d.getTime())) {
            const day = String(d.getDate()).padStart(2, "0");
            const m = String(d.getMonth() + 1).padStart(2, "0");
            const y = d.getFullYear();
            return `${day}/${m}/${y}`;
        }

        return s;
    }

    function formatMaintenanceEndDateLine(room, t) {
        const raw =
            (room && (room.maintenanceToDateShort || room.maintenanceToDate)) || "";
        const dateText = formatMaintenanceDateDisplay(raw);
        if (!dateText) {
            return "";
        }

        return t("roomBoard.maintenance.tooltipEndDate").replace("{0}", dateText);
    }

    function formatMaintenanceTooltip(room, t) {
        const parts = [];
        const comment = `${(room && room.maintenanceComment) || ""}`.trim();
        const endLine = formatMaintenanceEndDateLine(room, t);

        if (comment) {
            parts.push(comment);
        }
        if (endLine) {
            parts.push(endLine);
        }
        if (parts.length) {
            return parts.join("\n");
        }

        const label = formatMaintenanceDisplayLabel(room, t);
        return label || t("status.maintenance");
    }

    function isActiveMaintenanceRow(row) {
        const s = `${row && row.status ? row.status : ""}`.trim().toLowerCase().replace(/_/g, "-");
        return (
            s === "" ||
            s === "active" ||
            s === "maintenance" ||
            s === "open" ||
            s === "inprogress" ||
            s === "in-progress"
        );
    }

    function unwrapPayload(response) {
        if (!response || typeof response !== "object") {
            return {};
        }
        return response.data !== undefined ? response.data : response.Data !== undefined ? response.Data : response;
    }

    function extractRows(response) {
        const inner = unwrapPayload(response);
        const r = inner.rows !== undefined ? inner.rows : inner.Rows;
        return Array.isArray(r) ? r : [];
    }

    function normalizeRow(x) {
        return {
            id: x.id !== undefined ? x.id : x.Id,
            fromDate: x.fromDate || x.FromDate,
            toDate: x.toDate || x.ToDate,
            reason: x.reason || x.Reason || "",
            comment: x.comment !== undefined ? x.comment : x.Comment,
            categories: parseMaintenanceCategories(
                x.categories !== undefined ? x.categories : x.Categories
            ),
            status: x.status || x.Status || ""
        };
    }

    function maintenanceHotelQuery(room) {
        if (room && room.hotelId !== undefined && room.hotelId !== null && !Number.isNaN(Number(room.hotelId))) {
            return { hotelId: Number(room.hotelId) };
        }
        return {};
    }

    function formatUnitStatusLabel(raw, t) {
        const k = `${raw ?? ""}`.trim().toLowerCase().replace(/_/g, "-");
        if (!k) {
            return "-";
        }
        const statusKey = `status.${k}`;
        const tx = t(statusKey);
        if (tx !== statusKey) {
            return tx;
        }
        const extraKeys = {
            vacant: "reservationDetail.units.statusVacant",
            rented: "reservationDetail.units.statusRented"
        };
        const ek = extraKeys[k];
        if (ek) {
            const t2 = t(ek);
            if (t2 !== ek) {
                return t2;
            }
        }
        return `${raw}`.trim() || "-";
    }

    function formatMaintenanceRecordStatus(raw, t) {
        const k = `${raw ?? ""}`.trim().toLowerCase().replace(/_/g, "-");
        if (!k) {
            return "—";
        }
        const key = `roomBoard.maintenance.recordStatus.${k}`;
        const tx = t(key);
        return tx !== key ? tx : `${raw}`.trim();
    }

    function showsMaintenanceCategories(reason) {
        return `${reason || ""}`.trim() === "maintenance";
    }

    function syncMaintenanceFormReasonLayout(form, t) {
        if (!form) {
            return;
        }

        const reason = (form.option("formData") || {}).reason;
        const showCategories = showsMaintenanceCategories(reason);

        form.beginUpdate();
        form.itemOption("categories", "visible", showCategories);
        form.itemOption("categories", "isRequired", showCategories);
        form.itemOption("reason", "colSpan", showCategories ? 1 : 2);
        if (t) {
            form.itemOption(
                "categories",
                "validationRules",
                showCategories
                    ? [
                          {
                              type: "custom",
                              reevaluate: true,
                              message: t("roomBoard.maintenance.error.categoriesRequired"),
                              validationCallback(e) {
                                  return parseMaintenanceCategories(e.value).length > 0;
                              }
                          }
                      ]
                    : []
            );
        }
        form.endUpdate();
    }

    function maintenanceFormReasonChanged(form, reason, t) {
        if (!form) {
            return;
        }

        if (!showsMaintenanceCategories(reason)) {
            form.updateData("categories", []);
        }

        syncMaintenanceFormReasonLayout(form, t);
    }

    function notifyMaintenanceError(xhrOrErr, t) {
        const xhr =
            xhrOrErr && typeof xhrOrErr === "object" && "responseJSON" in xhrOrErr ? xhrOrErr : null;
        const code = xhr && xhr.responseJSON && xhr.responseJSON.code ? String(xhr.responseJSON.code) : "";
        const key = code ? `roomBoard.maintenance.error.${code}` : "roomBoard.maintenance.error.generic";
        const msg = t(key);
        DevExpress.ui.notify(msg !== key ? msg : t("roomBoard.maintenance.error.generic"), "error", 3400);
    }

    function open(room, t, onBoardRefresh) {
        const apartmentId = room.apartmentId;
        const hotelQuery = maintenanceHotelQuery(room);
        const $host = $("<div>").appendTo("body");

        let gridInst = null;

        function reloadGrid() {
            window.Zaaer.RoomBoardService.getApartmentMaintenances(apartmentId, hotelQuery)
                .then((res) => {
                    const rows = extractRows(res).map(normalizeRow);
                    if (gridInst) {
                        gridInst.option("dataSource", rows);
                    }
                })
                .catch(() => {
                    DevExpress.ui.notify(t("roomBoard.maintenance.error.load"), "error", 2800);
                });
        }

        function openEditPopup(editRow) {
            const isEdit = !!editRow;
            const $formHost = $("<div>").appendTo("body");
            const draft = {
                fromDate: null,
                toDate: null,
                reason: "maintenance",
                categories: [],
                comment: ""
            };

            if (isEdit && editRow) {
                const fd = editRow.fromDate ? new Date(editRow.fromDate) : null;
                const td = editRow.toDate ? new Date(editRow.toDate) : null;
                draft.fromDate = fd && !Number.isNaN(fd.getTime()) ? fd : null;
                draft.toDate = td && !Number.isNaN(td.getTime()) ? td : null;
                draft.reason = editRow.reason || "maintenance";
                draft.categories = parseMaintenanceCategories(editRow.categories);
                draft.comment = editRow.comment || "";
            }

            const reasons = reasonOptions(t);
            const categories = categoryOptions(t);

            $formHost.dxPopup({
                width: Math.min(640, Math.max(380, window.innerWidth - 24)),
                height: "auto",
                maxHeight: "62vh",
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                showCloseButton: true,
                title: isEdit ? t("roomBoard.maintenance.editTitle") : t("roomBoard.maintenance.addTitle"),
                visible: true,
                hideOnOutsideClick: true,
                wrapperAttr: { class: "room-maintenance-form-popup res-extra-popup" },
                contentTemplate(contentElement) {
                    const $c = $(contentElement).empty().addClass("room-maintenance-form-body");
                    const $f = $("<div>").appendTo($c);
                    let formInstRef = null;

                    $f.dxForm({
                        formData: draft,
                        labelLocation: "top",
                        colCount: 2,
                        items: [
                            {
                                dataField: "fromDate",
                                editorType: "dxDateBox",
                                isRequired: true,
                                label: { text: t("roomBoard.maintenance.dateFrom") },
                                editorOptions: {
                                    type: "date",
                                    openOnFieldClick: true,
                                    displayFormat: "yyyy-MM-dd"
                                }
                            },
                            {
                                dataField: "toDate",
                                editorType: "dxDateBox",
                                isRequired: true,
                                label: { text: t("roomBoard.maintenance.dateTo") },
                                editorOptions: {
                                    type: "date",
                                    openOnFieldClick: true,
                                    displayFormat: "yyyy-MM-dd"
                                }
                            },
                            {
                                dataField: "reason",
                                editorType: "dxSelectBox",
                                isRequired: true,
                                colSpan: showsMaintenanceCategories(draft.reason) ? 1 : 2,
                                label: { text: t("roomBoard.maintenance.reason") },
                                editorOptions: {
                                    dataSource: reasons,
                                    valueExpr: "id",
                                    displayExpr: "text",
                                    searchEnabled: false,
                                    openOnFieldClick: true,
                                    onValueChanged(e) {
                                        if (!formInstRef) {
                                            return;
                                        }
                                        maintenanceFormReasonChanged(formInstRef, e.value, t);
                                    }
                                }
                            },
                            {
                                dataField: "categories",
                                editorType: "dxTagBox",
                                visible: showsMaintenanceCategories(draft.reason),
                                isRequired: showsMaintenanceCategories(draft.reason),
                                label: { text: t("roomBoard.maintenance.categories") },
                                validationRules: showsMaintenanceCategories(draft.reason)
                                    ? [
                                          {
                                              type: "custom",
                                              reevaluate: true,
                                              message: t("roomBoard.maintenance.error.categoriesRequired"),
                                              validationCallback(e) {
                                                  return parseMaintenanceCategories(e.value).length > 0;
                                              }
                                          }
                                      ]
                                    : [],
                                editorOptions: {
                                    dataSource: categories,
                                    valueExpr: "id",
                                    displayExpr: "text",
                                    showSelectionControls: true,
                                    hideSelectedItems: false,
                                    searchEnabled: false,
                                    openOnFieldClick: true,
                                    maxDisplayedTags: 2,
                                    multiline: false
                                }
                            },
                            {
                                dataField: "comment",
                                editorType: "dxTextArea",
                                colSpan: 2,
                                label: { text: t("roomBoard.maintenance.comment") },
                                editorOptions: {
                                    height: 88
                                }
                            }
                        ]
                    });
                    formInstRef = $f.dxForm("instance");
                },
                toolbarItems: [
                    {
                        toolbar: "bottom",
                        location: "before",
                        widget: "dxButton",
                        options: {
                            text: t("roomBoard.maintenance.discard"),
                            stylingMode: "outlined",
                            onClick() {
                                $formHost.dxPopup("instance").hide();
                            }
                        }
                    },
                    {
                        toolbar: "bottom",
                        location: "after",
                        widget: "dxButton",
                        options: {
                            text: t("roomBoard.maintenance.save"),
                            type: "default",
                            stylingMode: "contained",
                            onClick() {
                                const inst = $formHost.dxPopup("instance");
                                const $content = $(inst.content());
                                const form = $content.find(".dx-form").first().dxForm("instance");
                                if (!form) {
                                    return;
                                }
                                const data = form.option("formData") || {};
                                if (!data.fromDate || !data.toDate || !data.reason) {
                                    DevExpress.ui.notify(t("roomBoard.maintenance.error.required"), "warning", 2600);
                                    return;
                                }
                                if (
                                    showsMaintenanceCategories(data.reason) &&
                                    !parseMaintenanceCategories(data.categories).length
                                ) {
                                    DevExpress.ui.notify(
                                        t("roomBoard.maintenance.error.categoriesRequired"),
                                        "warning",
                                        2800
                                    );
                                    return;
                                }
                                const validationResult = form.validate();
                                if (!validationResult.isValid) {
                                    return;
                                }
                                const fromStr = formatLocalDateParam(data.fromDate);
                                const toStr = formatLocalDateParam(data.toDate);
                                if (!fromStr || !toStr) {
                                    DevExpress.ui.notify(t("roomBoard.maintenance.error.badDates"), "warning", 2600);
                                    return;
                                }
                                const payload = {
                                    fromDate: fromStr,
                                    toDate: toStr,
                                    reason: data.reason || "maintenance",
                                    categories: showsMaintenanceCategories(data.reason)
                                        ? parseMaintenanceCategories(data.categories)
                                        : [],
                                    comment: data.comment || null
                                };

                                const done = () => {
                                    inst.hide();
                                    DevExpress.ui.notify(t("roomBoard.maintenance.saved"), "success", 1600);
                                    reloadGrid();
                                    if (typeof onBoardRefresh === "function") {
                                        onBoardRefresh();
                                    }
                                };

                                const fail = (xhr) => notifyMaintenanceError(xhr, t);

                                if (isEdit && editRow) {
                                    window.Zaaer.RoomBoardService
                                        .updateApartmentMaintenance(apartmentId, editRow.id, payload, hotelQuery)
                                        .then(done)
                                        .catch(fail);
                                } else {
                                    window.Zaaer.RoomBoardService
                                        .createApartmentMaintenance(apartmentId, payload, hotelQuery)
                                        .then(done)
                                        .catch(fail);
                                }
                            }
                        }
                    }
                ],
                onHidden() {
                    $formHost.remove();
                }
            });
        }

        const unitTitle = room.apartmentName || room.apartmentCode || "-";
        const unitType = room.roomTypeName || t("roomBoard.roomTypeNotSet");
        const unitStatusRaw = room.apartmentStatus || room.operationalStatus || "";
        const unitStatus = formatUnitStatusLabel(unitStatusRaw, t);

        $host.dxPopup({
            width: Math.min(880, Math.max(520, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "68vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            showCloseButton: true,
            title: t("roomBoard.maintenance.managerTitle"),
            visible: true,
            hideOnOutsideClick: true,
            wrapperAttr: { class: "room-maintenance-manager-popup res-extra-popup" },
            contentTemplate(contentElement) {
                const $root = $(contentElement).empty().addClass("room-maintenance-manager-body");
                const $meta = $("<div>").addClass("room-maintenance-unit-meta").appendTo($root);

                function appendMetaPart(label, value, withSep) {
                    if (withSep) {
                        $("<span>")
                            .addClass("room-maintenance-unit-meta-sep")
                            .attr("aria-hidden", "true")
                            .text("·")
                            .appendTo($meta);
                    }
                    $("<span>")
                        .addClass("room-maintenance-unit-meta-item")
                        .append(
                            $("<strong>").text(`${label}: `),
                            $("<span>").text(value)
                        )
                        .appendTo($meta);
                }

                appendMetaPart(t("roomBoard.maintenance.unitNumber"), unitTitle, false);
                appendMetaPart(t("roomBoard.maintenance.unitType"), unitType, true);
                appendMetaPart(t("roomBoard.maintenance.unitStatus"), unitStatus, true);

                const $gridHost = $("<div>").addClass("room-maintenance-manager-grid-wrap").appendTo($root);
                const po = window.Zaaer.PmsGridOptions;
                $gridHost.dxDataGrid(
                    po.merge(po.baseline(), {
                    dataSource: [],
                    keyExpr: "id",
                    height: 228,
                    width: "100%",
                    columnAutoWidth: false,
                    wordWrapEnabled: true,
                    searchPanel: { visible: false },
                    paging: { pageSize: 8 },
                    pager: {
                        visible: true,
                        showPageSizeSelector: false,
                        showNavigationButtons: true,
                        displayMode: "compact"
                    },
                    toolbar: {
                        items: [
                            {
                                location: "before",
                                widget: "dxButton",
                                options: {
                                    text: t("roomBoard.maintenance.addNew"),
                                    type: "default",
                                    stylingMode: "contained",
                                    elementAttr: { class: "room-maintenance-add-btn" },
                                    onClick() {
                                        openEditPopup(null);
                                    }
                                }
                            }
                        ]
                    },
                    columns: [
                        {
                            dataField: "reason",
                            width: 96,
                            caption: t("roomBoard.maintenance.colReason"),
                            lookup: {
                                dataSource: reasonOptions(t),
                                valueExpr: "id",
                                displayExpr: "text"
                            }
                        },
                        {
                            dataField: "categories",
                            width: 156,
                            caption: t("roomBoard.maintenance.colCategories"),
                            allowHeaderFiltering: false,
                            cssClass: "room-maintenance-col-categories",
                            calculateCellValue(row) {
                                if (!showsMaintenanceCategories(row && row.reason)) {
                                    return "—";
                                }
                                const label = formatMaintenanceCategoryLabels(row.categories, t);
                                return label || "—";
                            }
                        },
                        {
                            dataField: "fromDate",
                            width: 102,
                            caption: t("roomBoard.maintenance.colFrom"),
                            dataType: "date",
                            format: "yyyy-MM-dd"
                        },
                        {
                            dataField: "toDate",
                            width: 102,
                            caption: t("roomBoard.maintenance.colTo"),
                            dataType: "date",
                            format: "yyyy-MM-dd"
                        },
                        {
                            dataField: "comment",
                            minWidth: 168,
                            width: 196,
                            caption: t("roomBoard.maintenance.colComment"),
                            cssClass: "room-maintenance-col-comment",
                            calculateCellValue(row) {
                                const c = row.comment;
                                return c && `${c}`.trim() ? c : "—";
                            }
                        },
                        {
                            dataField: "status",
                            width: 84,
                            caption: t("roomBoard.maintenance.colStatus"),
                            allowHeaderFiltering: false,
                            calculateCellValue(row) {
                                return formatMaintenanceRecordStatus(row.status, t);
                            }
                        },
                        {
                            type: "buttons",
                            width: 76,
                            allowHeaderFiltering: false,
                            buttons: [
                                {
                                    hint: t("roomBoard.maintenance.edit"),
                                    icon: "edit",
                                    visible(e) {
                                        return isActiveMaintenanceRow(e.row && e.row.data);
                                    },
                                    onClick(e) {
                                        openEditPopup(e.row.data);
                                    }
                                },
                                {
                                    hint: t("roomBoard.maintenance.remove"),
                                    icon: "trash",
                                    visible(ev) {
                                        return isActiveMaintenanceRow(ev.row && ev.row.data);
                                    },
                                    onClick(ev) {
                                        const row = ev.row.data;
                                        DevExpress.ui.dialog
                                            .confirm(
                                                t("roomBoard.maintenance.confirmRemove"),
                                                t("roomBoard.maintenance.remove")
                                            )
                                            .done((yes) => {
                                                if (!yes) {
                                                    return;
                                                }
                                                window.Zaaer.RoomBoardService
                                                    .cancelApartmentMaintenance(apartmentId, row.id, hotelQuery)
                                                    .then(() => {
                                                        DevExpress.ui.notify(
                                                            t("roomBoard.maintenance.removed"),
                                                            "success",
                                                            1600
                                                        );
                                                        reloadGrid();
                                                        if (typeof onBoardRefresh === "function") {
                                                            onBoardRefresh();
                                                        }
                                                    })
                                                    .catch((xhr) => notifyMaintenanceError(xhr, t));
                                            });
                                    }
                                }
                            ]
                        }
                    ]
                    })
                );
                gridInst = $gridHost.dxDataGrid("instance");
                reloadGrid();
            },
            onHidden() {
                $host.remove();
            }
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.RoomMaintenanceBoardPopup = {
        open,
        formatMaintenanceDisplayLabel,
        formatMaintenanceTooltip
    };
})(window, jQuery, DevExpress);
