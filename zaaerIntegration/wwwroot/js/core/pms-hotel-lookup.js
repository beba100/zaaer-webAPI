/**
 * Optional shim — PmsHotelLookup is registered in api-service.js (always loaded).
 * Keeps older HTML that references this file working after deploy.
 */
(function (window) {
    "use strict";
    if (window.Zaaer && window.Zaaer.PmsHotelLookup) {
        return;
    }
    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsHotelLookup = {
        tenantDisplayName: function () { return ""; },
        createSingleHotelLookup: function () {
            throw new Error("PmsHotelLookup: load api-service.js before this page.");
        },
        createMultiHotelLookup: function () {
            throw new Error("PmsHotelLookup: load api-service.js before this page.");
        }
    };
})(window);
