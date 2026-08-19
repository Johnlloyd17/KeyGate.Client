window.downloadCsv = function (fileName, csvContent) {
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

window.downloadFile = function (fileName, bytesBase64, contentType) {
    var binary = atob(bytesBase64);
    var bytes = new Uint8Array(binary.length);
    for (var i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    var blob = new Blob([bytes], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 5000);
};

window.downloadFileBytes = function (fileName, byteArray, contentType) {
    var blob = new Blob([new Uint8Array(byteArray)], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 5000);
};

window.printQr = function (imageDataUrl, title) {
    const w = window.open('', '_blank', 'width=440,height=560');
    if (!w) {
        return;
    }
    w.document.write('<html><head><title>KeyGate QR — ' + title + '</title></head>');
    w.document.write('<body style="margin:0;padding:24px;text-align:center;font-family:Segoe UI,Arial,sans-serif;color:#1e2430;">');
    w.document.write('<h3 style="margin:0 0 16px;">' + title + '</h3>');
    w.document.write('<img src="' + imageDataUrl + '" alt="QR code" style="width:280px;height:280px;display:block;margin:0 auto 16px;" />');
    w.document.write('<p style="color:#555;">Scan with your phone to complete registration.</p>');
    w.document.write('</body></html>');
    w.document.close();
    w.focus();
    w.print();
    w.close();
};
