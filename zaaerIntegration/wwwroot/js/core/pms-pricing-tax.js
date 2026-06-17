(function (window) {
    "use strict";

    function defaultConfig() {
        return { vatRate: 15, ewaRate: 0, vatIncluded: true, ewaIncluded: true };
    }

    function normalizeConfig(raw) {
        const d = defaultConfig();
        if (!raw || typeof raw !== "object") {
            return d;
        }

        return {
            vatRate: Number(raw.vatRate ?? raw.VatRate) || d.vatRate,
            ewaRate: Number(raw.ewaRate ?? raw.EwaRate) || d.ewaRate,
            vatIncluded: (raw.vatTaxIncluded ?? raw.VatTaxIncluded ?? raw.vatIncluded) !== false,
            ewaIncluded: (raw.lodgingTaxIncluded ?? raw.LodgingTaxIncluded ?? raw.ewaIncluded) !== false
        };
    }

    /**
     * Matches C# HotelPricingTaxHelper.CalculateAmounts.
     */
    function calculateAmounts(grossRate, cfg) {
        const vatRate = (Number(cfg.vatRate) || 0) / 100;
        const ewaRate = (Number(cfg.ewaRate) || 0) / 100;
        const gross = Math.round((Number(grossRate) || 0) * 100) / 100;

        if (vatRate <= 0 && ewaRate <= 0) {
            return { net: gross, ewa: 0, vat: 0, total: gross };
        }

        if (cfg.ewaIncluded !== false && cfg.vatIncluded !== false) {
            const lr = ewaRate;
            const vr = vatRate;
            const divisor = 1 + lr + (1 + lr) * vr;
            if (divisor === 0) {
                return { net: gross, ewa: 0, vat: 0, total: gross };
            }

            const net = Math.round((gross / divisor) * 100) / 100;
            const ewa = Math.round(net * lr * 100) / 100;
            let vat = Math.round((net + ewa) * vr * 100) / 100;
            let total = Math.round((net + ewa + vat) * 100) / 100;
            const drift = Math.round((gross - total) * 100) / 100;
            if (drift !== 0) {
                vat = Math.round((vat + drift) * 100) / 100;
                total = gross;
            }

            return { net, ewa, vat, total };
        }

        const ewa = Math.round(gross * ewaRate * 100) / 100;
        const vat = Math.round((gross + ewa) * vatRate * 100) / 100;
        return {
            net: gross,
            ewa,
            vat,
            total: Math.round((gross + ewa + vat) * 100) / 100
        };
    }

    function computePosLineTax(gross, includesTax, cfg) {
        const calc = calculateAmounts(gross, cfg);
        return {
            net: calc.net,
            tax: calc.ewa + calc.vat,
            total: calc.total
        };
    }

    /** Matches C# HotelPricingTaxHelper.ComputePosOrderTotals (discount on gross, then VAT split). */
    function computePosOrderTotals(lineGrossAmounts, orderDiscount, cfg) {
        const list = Array.isArray(lineGrossAmounts) ? lineGrossAmounts : [];
        let grossSum = 0;
        list.forEach((raw) => {
            grossSum += Math.round(Math.max(0, Number(raw) || 0) * 100) / 100;
        });
        grossSum = Math.round(grossSum * 100) / 100;
        let discount = Math.round(Math.max(0, Number(orderDiscount) || 0) * 100) / 100;
        if (discount > grossSum) {
            discount = grossSum;
        }
        const adjustedGross = Math.round((grossSum - discount) * 100) / 100;
        if (adjustedGross <= 0) {
            return { subtotal: 0, tax: 0, discount, total: 0, grossSum };
        }

        const calc = calculateAmounts(adjustedGross, cfg);
        return {
            subtotal: calc.net,
            tax: calc.ewa + calc.vat,
            discount,
            total: calc.total,
            grossSum
        };
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsPricingTax = {
        defaultConfig,
        normalizeConfig,
        calculateAmounts,
        computePosLineTax,
        computePosOrderTotals
    };
})(window);
