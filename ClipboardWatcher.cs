using System.Runtime.InteropServices;

namespace YtGrab;

internal sealed class ClipboardWatcher : NativeWindow, IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private bool disposed;

    public event EventHandler? ClipboardChanged;

    public ClipboardWatcher()
    {
        CreateHandle(new CreateParams());
        AddClipboardFormatListener(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmClipboardUpdate)
        {
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        RemoveClipboardFormatListener(Handle);
        DestroyHandle();
        disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
