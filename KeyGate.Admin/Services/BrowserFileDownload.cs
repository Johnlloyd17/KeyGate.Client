using Microsoft.JSInterop;

namespace KeyGate.Admin.Services;

public class BrowserFileDownload
{
    private readonly IJSRuntime _js;

    public BrowserFileDownload(IJSRuntime js)
    {
        _js = js;
    }

    public async Task DownloadFileAsync(string fileName, byte[] content, string contentType)
    {
        await _js.InvokeVoidAsync("downloadFileBytes", fileName, content, contentType);
    }
}
