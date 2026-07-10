// Minimal print interop for the printable trip page.
// Exposes a single function that opens the browser's native print dialog,
// which is also how the traveler saves the page as a PDF.
window.tripPrint = {
    print: function () {
        window.print();
    }
};
