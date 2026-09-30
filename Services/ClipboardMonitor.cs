using System.ComponentModel;
using System.Runtime.InteropServices;
using ClipboardHistoryManager.Infrastructure;
using ClipboardHistoryManager.Models;

namespace ClipboardHistoryManager.Services;

public sealed class ClipboardMonitor : NativeWindow, IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private readonly ClipboardHistoryService _historyService;
    private bool _isStarted;
    private bool _disposed;

    public ClipboardMonitor(ClipboardHistoryService historyService)
    {
        _historyService = historyService;
    }

    public event EventHandler<ClipboardContentChangedEventArgs>? ClipboardContentChanged;

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        CreateHandle(new CreateParams());
        if (!AddClipboardFormatListener(Handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to listen to Windows clipboard changes.");
        }

        _isStarted = true;
        TryCaptureClipboardContent();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmClipboardUpdate)
        {
            TryCaptureClipboardContent();
        }

        base.WndProc(ref m);
    }

    private void TryCaptureClipboardContent()
    {
        _ = CaptureClipboardContentAsync();
    }

    private async Task CaptureClipboardContentAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                {
                    var text = Clipboard.GetText(TextDataFormat.UnicodeText);
                    ClipboardContentChanged?.Invoke(
                        this,
                        new ClipboardContentChangedEventArgs(ClipboardContentKind.Text, text, text));
                    _historyService.SaveCopiedText(text);
                    return;
                }

                if (Clipboard.ContainsImage())
                {
                    ClipboardContentChanged?.Invoke(
                        this,
                        new ClipboardContentChangedEventArgs(ClipboardContentKind.Image, "[图片]", null));
                    return;
                }

                ClipboardContentChanged?.Invoke(
                    this,
                    new ClipboardContentChangedEventArgs(ClipboardContentKind.Unknown, "未知", null));
                return;
            }
            catch (ExternalException)
            {
                await Task.Delay(80 + attempt * 80);
            }
            catch (ThreadStateException)
            {
                return;
            }
            catch (Exception ex)
            {
                AppLogger.Error("剪贴板内容读取失败。", ex);
                return;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_isStarted && Handle != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(Handle);
        }

        DestroyHandle();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
