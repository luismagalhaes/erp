// Hands a file the server produced to the browser's download manager. The bytes travel over the
// SignalR circuit as base64, so this is meant for documents, not for large data sets.
window.erpDownloadFile = (fileName, contentType, base64) => {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);

    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    const link = document.createElement('a');

    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Revoking immediately can cancel the download in some browsers, so give it a moment.
    setTimeout(() => URL.revokeObjectURL(url), 10000);
};
