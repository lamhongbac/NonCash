window.noncashContractPrint = {
    _windowName: "contractPrintWindow",

    /**
     * Opens a blank print window immediately so the browser treats it as a user-initiated popup.
     * Call this synchronously from a click handler before any async work.
     */
    openWindow: function () {
        var w = window.open("", this._windowName);
        if (!w) {
            alert("Please allow popups for this site to print the contract.");
            return false;
        }
        return true;
    },

    /**
     * Writes the contract HTML into the previously opened print window.
     */
    writeHtml: function (html) {
        var w = window.open("", this._windowName);
        if (!w) {
            alert("The print window was closed. Please try again.");
            return;
        }
        w.document.open();
        w.document.write(html);
        w.document.close();
        w.focus();
    }
};
