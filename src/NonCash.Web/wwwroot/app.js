window.downloadFromBase64 = function (fileName, contentType, base64Data) {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64Data}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.renderBarcode = function (svgId, code) {
    if (window.JsBarcode && code) {
        JsBarcode('#' + svgId, code, {
            format: 'CODE128',
            width: 2,
            height: 80,
            displayValue: true,
            fontSize: 16,
            margin: 10
        });
    }
};

let currentScanner = null;

window.startScanner = function (elementId, dotNetHelper) {
    if (!window.Html5QrcodeScanner) {
        console.error('Html5QrcodeScanner library not loaded.');
        return;
    }

    currentScanner = new Html5QrcodeScanner(elementId, {
        fps: 10,
        qrbox: { width: 250, height: 250 },
        rememberLastUsedCamera: false
    }, false);

    currentScanner.render(
        function (decodedText) {
            dotNetHelper.invokeMethodAsync('OnScanResult', decodedText);
        },
        function () {
            // Scan errors are expected between successful reads; ignore them.
        }
    );
};

window.stopScanner = function () {
    if (currentScanner) {
        currentScanner.clear();
        currentScanner = null;
    }
};
