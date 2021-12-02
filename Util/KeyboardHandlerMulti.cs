using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace JTranslator.Util
{
    class KeyboardHandlerMulti
    {
        // HotKey Message ID
        private const int WM_HOTKEY = 0x0312;

        // ホットキーIDとイベントで対になるディクショナリ
        private readonly Dictionary<int, EventHandler> _hotkeyEvents;

        // ウィンドウハンドラ
        private readonly IntPtr _windowHandle;

        // HotKeyの登録
        private int i;

        // 初期化
        public KeyboardHandlerMulti(Window window)
        {
            // WindowのHandleを取得
            var _host = new WindowInteropHelper(window);
            _windowHandle = _host.Handle;

            // ホットキーのイベントハンドラを設定
            ComponentDispatcher.ThreadPreprocessMessage
                += ComponentDispatcher_ThreadPreprocessMessage;

            // イベントディクショナリを初期化
            _hotkeyEvents = new Dictionary<int, EventHandler>();
        }

        // HotKeyの動作を設定する
        public void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            // ホットキーを表すメッセージであるか否か
            if (msg.message != WM_HOTKEY) return;

            // 自分が登録したホットキーか否か
            var hotkeyID = msg.wParam.ToInt32();
            if (_hotkeyEvents.All(x => x.Key != hotkeyID)) return;

            // 両方を満たす場合は登録してあるホットキーのイベントを実行
            new ThreadStart(
                () => _hotkeyEvents[hotkeyID](this, EventArgs.Empty)
            ).Invoke();
        }

        public void Regist(ModifierKeys modkey, Key trigger, EventHandler eh)
        {
            // 引数をintにキャスト
            var imod = modkey.ToInt32();
            var itrg = KeyInterop.VirtualKeyFromKey(trigger);

            // HotKey登録時に指定するIDを決定する
            while (++i < 0xc000 && NativeMethods.RegisterHotKey(_windowHandle, i, imod, itrg) == 0) ;
            // 0xc000～0xffff はDLL用なので使用不可能
            // 0x0000～0xbfff はIDとして使用可能

            if (i < 0xc000) _hotkeyEvents.Add(i, eh);
        }


        // HotKeyの全開放
        public void Unregist()
        {
            foreach (var hotkeyid in _hotkeyEvents.Keys) NativeMethods.UnregisterHotKey(_windowHandle, hotkeyid);
        }
    }

    internal static class Extention
    {
        public static int ToInt32(this ModifierKeys m)
        {
            return (int)m;
        }
    }
}
