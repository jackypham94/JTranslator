using System;
using System.Windows;
using System.Windows.Interop;

namespace JTranslator
{
    internal class ClipboardManager
    {
        private static readonly IntPtr WndProcSuccess = IntPtr.Zero;

        public ClipboardManager(Window windowSource)
        {
            if (PresentationSource.FromVisual(windowSource) is not HwndSource source)
                throw new ArgumentException(
                    @"Window source MUST be initialized first, such as in the Window's OnSourceInitialized handler."
                    , nameof(windowSource));

            source.AddHook(WndProc);

            // get window handle for interop
            var windowHandle = new WindowInteropHelper(windowSource).Handle;

            // register for clipboard events
            NativeMethods.AddClipboardFormatListener(windowHandle);
        }

        public event EventHandler ClipboardChanged;

        private void OnClipboardChanged()
        {
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WmClipboardupdate)
            {
                OnClipboardChanged();
                handled = true;
            }

            //if (msg == 0x0084) // WM_NCHITTEST
            //{
            //    handled = true;
            //    return (IntPtr)2; // HTCAPTION
            //}

            return WndProcSuccess;
        }
    }
}
