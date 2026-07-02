// Download lato client di un file generato dal server (Blazor Server non può
// scrivere direttamente sul disco del client). Riceve il contenuto in base64,
// ricostruisce un Blob e forza il download tramite un <a download> temporaneo.
window.icmDownloadFile = (fileName, base64, contentType) => {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
