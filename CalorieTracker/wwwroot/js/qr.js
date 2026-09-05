(() => {
    "use strict";

    window.addEventListener("load", () => {
        const qrCodeElement = document.getElementById("qrCode");
        const qrCodeDataElement = document.getElementById("qrCodeData");
        const authenticatorUri = qrCodeDataElement?.dataset.url;

        if (!qrCodeElement || !authenticatorUri || typeof QRCode === "undefined") {
            return;
        }

        new QRCode(qrCodeElement, {
            text: authenticatorUri,
            width: 160,
            height: 160,
            colorDark: "#000000",
            colorLight: "#ffffff"
        });

        // qrcode.js assigns the encoded URI as a title; the manual key already
        // provides the accessible fallback without exposing the secret in a tooltip.
        qrCodeElement.removeAttribute("title");
    });
})();
