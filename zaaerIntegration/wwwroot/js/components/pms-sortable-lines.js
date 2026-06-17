(function (window, $) {
    "use strict";

    /**
     * Editable sortable list for block floors (not DataGrid).
     * @param {JQuery} $host
     * @param {{ items?: Array, onChange?: Function, minRows?: number, nameLabel?: string, orderLabel?: string, addLabel?: string }} options
     */
    function create($host, options) {
        const opts = options || {};
        const minRows = opts.minRows != null ? opts.minRows : 1;
        let items = normalizeItems(opts.items || []);

        const $list = $("<ul/>", { class: "pms-sortable-lines" });
        const $addBtn = $("<div/>", { class: "pms-sortable-lines__add" });

        $host.empty().addClass("pms-sortable-lines-host").append($list, $addBtn);

        $addBtn.dxButton({
            icon: "add",
            text: opts.addLabel || "+",
            stylingMode: "text",
            type: "default",
            onClick() {
                const nextNum = items.length + 1;
                items.push({
                    floorId: null,
                    zaaerId: null,
                    floorNumber: nextNum,
                    floorName: String(nextNum),
                    sortOrder: nextNum,
                    isActive: true
                });
                render();
                notifyChange();
            }
        });

        $list.dxSortable({
            filter: ".pms-sortable-lines__item",
            dragDirection: "vertical",
            handle: ".pms-sortable-lines__handle",
            onReorder(e) {
                const moved = items.splice(e.fromIndex, 1)[0];
                items.splice(e.toIndex, 0, moved);
                reindexSortOrders();
                render();
                notifyChange();
            }
        });

        function normalizeItems(rows) {
            return (rows || []).map((r, i) => ({
                floorId: r.floorId ?? r.FloorId ?? null,
                zaaerId: r.zaaerId ?? r.ZaaerId ?? null,
                floorNumber: Number(r.floorNumber ?? r.FloorNumber ?? i + 1),
                floorName: r.floorName ?? r.FloorName ?? String(i + 1),
                sortOrder: Number(r.sortOrder ?? r.SortOrder ?? i + 1),
                isActive: r.isActive !== false && r.IsActive !== false
            }));
        }

        function reindexSortOrders() {
            items.forEach((row, idx) => {
                row.sortOrder = idx + 1;
            });
        }

        function notifyChange() {
            if (typeof opts.onChange === "function") {
                opts.onChange(getItems());
            }
        }

        function removeAt(index) {
            if (items.length <= minRows) {
                return;
            }
            items.splice(index, 1);
            reindexSortOrders();
            render();
            notifyChange();
        }

        function render() {
            $list.empty();
            items.forEach((row, index) => {
                const $item = $("<li/>", { class: "pms-sortable-lines__item" });
                const $handle = $("<span/>", {
                    class: "pms-sortable-lines__handle dx-icon dx-icon-dragvertical",
                    title: opts.orderLabel || "",
                    "aria-hidden": "true"
                });
                const $nameHost = $("<div/>", { class: "pms-sortable-lines__name" });
                const $removeHost = $("<div/>", { class: "pms-sortable-lines__remove" });

                $nameHost.dxTextBox({
                    value: row.floorName,
                    stylingMode: "filled",
                    placeholder: opts.nameLabel || "",
                    onValueChanged(e) {
                        row.floorName = e.value;
                        row.floorNumber = parseInt(String(e.value).replace(/\D/g, ""), 10) || row.floorNumber;
                        notifyChange();
                    }
                });

                $removeHost.dxButton({
                    icon: "remove",
                    stylingMode: "text",
                    hint: "Remove",
                    onClick() {
                        removeAt(index);
                    }
                });

                $item.append($handle, $nameHost, $removeHost);
                $list.append($item);
            });
        }

        function getItems() {
            return items.map((r) => ({ ...r }));
        }

        function setItems(next) {
            items = normalizeItems(next);
            render();
        }

        render();

        return { getItems, setItems };
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsSortableLines = { create };
})(window, jQuery);
