using Gma.System.MouseKeyHook;
using JTranslator.Model;
using JTranslator.Properties;
using JTranslator.Util;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Forms.Clipboard;
using MessageBox = System.Windows.Forms.MessageBox;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using MouseEventHandler = System.Windows.Forms.MouseEventHandler;
using Point = System.Drawing.Point;
using Task = System.Threading.Tasks.Task;
using TextBox = System.Windows.Forms.TextBox;
using Timer = System.Windows.Forms.Timer;

namespace JTranslator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string KanjiFileName = "Kanji.bin";
        private const string SettingFileName = "Setting.bin";
        private const string HistoryFileName = "History.bin";
        private readonly NotifyChanged _notify;
        private CancellationTokenSource _cancellationTokenSource;
        private IKeyboardMouseEvents _globalMouseHook;
        private Google? _google;
        private List<string> _histories = new();
        private List<Result> _kanjiList = new();
        private List<MaziiComment> _maziiComments = new();
        private Setting _setting = new();
        private Mazii? _mazii;
        private bool _isLoadingNew;
        private bool _isLoadingKanji;
        private bool _isMouseHoverText;
        //private Guid _currentGuid;
        private Timer _typingTimer;
        private Timer _hoverTimer;
        private IntPtr _hWndNextViewer;
        private volatile bool _isMouseDown;
        private Point? _mouseFirstPoint;
        private Point? _mouseSecondPoint;
        private System.Windows.Point _startPoint;
        private HwndSource _hWndSource;
        private readonly MouseEventHandler _doubleClickHandler;
        private readonly MouseEventHandler _mouseUpHandler;
        private readonly MouseEventHandler _mouseDownHandler;
        //private System.Windows.Forms.NotifyIcon notifyIcon = null;

        private Thread? _maziiThread;
        private Thread? _googleThread;

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080;

        private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36";

        public MainWindow()
        {
            InitializeComponent();
            System.Windows.Forms.Application.EnableVisualStyles();
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;
            CheckGitHubNewerVersion();

            InitNotifyicon();
            _setting.Init();
            DeserializeData(SettingFileName);
            //Console.OutputEncoding = Encoding.UTF8;
            _notify = new NotifyChanged
            {
                //_notify.IsDoubleClickOn = Settings.Default.IsDoubleClickOn;
                //_notify.IsRunOnStartUp = Settings.Default.IsRunOnStartUp;
                //_notify.IsLoadKanji = Settings.Default.IsLoadKanji;
                //_notify.IsFaded = Settings.Default.IsFaded;
                //_notify.IsAutoTranslate = Settings.Default.IsAutoTranslate;
                //_notify.IsJaVi = Settings.Default.IsJaVi;
                IsJaVi = _setting.IsJavi,
                IsAutoTranslate = _setting.IsAutoTranslate,
                IsFaded = _setting.IsFaded,
                IsLoadKanji = _setting.IsLoadKanji,
                IsRunOnStartUp = _setting.IsRunOnStartUp,
                IsDoubleClickOn = _setting.IsDoubleClickOn
            };
            DataContext = _notify;

            StartHooks();
            var kbh = new KeyboardHandlerMulti(this);
            kbh.Regist(ModifierKeys.Control | ModifierKeys.Alt, Key.T, ToggleTranslate);
            kbh.Regist(ModifierKeys.Control | ModifierKeys.Alt, Key.W, ClearResult);
            kbh.Regist(ModifierKeys.Control | ModifierKeys.Alt, Key.Q, ExitApplication);
            kbh.Regist(ModifierKeys.Control | ModifierKeys.Alt, Key.M, MiniMaximize);

            InitData();
            DeserializeData(HistoryFileName);
            DeserializeData(KanjiFileName);
            HistoryListView.ItemsSource = _histories;

            _mouseUpHandler = async (o, args) => await MouseUp(o, args);
            _mouseDownHandler = async (o, args) => await MouseDown(o, args);
            _doubleClickHandler = async (o, args) => await MouseDoubleClicked(o, args);
            SubscribeLocalEvents();

            // Check run at startup on first launch
            RunOnStartUp();
        }

        private async void CheckGitHubNewerVersion(bool forceShowMessage = false)
        {
            //Get all releases from GitHub
            const string uri = "https://api.github.com/repos/jackypham94/JTranslator/releases";
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.KeepAlive = false;
            request.Method = "GET";
            request.ContentLength = 0;
            request.ContentType = "application/json";
            request.UserAgent = USER_AGENT;
            request.Proxy = null;
            var git = new Git();
            void Action()
            {
                try
                {
                    using var response = (HttpWebResponse)request.GetResponse();
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        var message = $"Request failed. Received HTTP {response.StatusCode}";
                        throw new ApplicationException(message);
                    }

                    using (var responseStream = response.GetResponseStream())
                    {
                        if (responseStream == null) return;
                        using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                        {
                            var jsonString = reader.ReadToEnd();
                            var gits = JsonConvert.DeserializeObject<List<Git>>(jsonString);
                            //var data = JObject.Parse(jsonString);
                            if (gits != null) git = gits.First();
                            reader.Close();
                        }

                        responseStream.Flush();
                        responseStream.Close();
                    }
                    response.Close();
                }
                catch (WebException)
                {
                    // Do nothing
                }
            }

            await Task.Run(Action).ContinueWith(x =>
            {
                if (!string.IsNullOrEmpty(git.tag_name))
                {
                    //Assembly.GetExecutingAssembly().GetName().Version;
                    ////Compare the Versions
                    ////Source: https://stackoverflow.com/questions/7568147/compare-version-numbers-without-using-split-function
                    Version currentVersion = new Version(Assembly.GetExecutingAssembly().GetName().Version.ToString());
                    Version gitVersion = new Version(git.tag_name);
                    int versionComparison = currentVersion.CompareTo(gitVersion);
                    if (versionComparison < 0)
                    {
                        //The version on GitHub is more up to date than this local release.
                        var result = MessageBox.Show(new Form { TopMost = true }, "The version on GitHub is more up to date than this local release.\n" +
                            "Do you want to download the new version now?\n" +
                            $"Local version: {currentVersion}\n" +
                            $"New version: {gitVersion}\n" +
                            $"{git.body}", @"JTranslator", MessageBoxButtons.YesNo);
                        if (result == System.Windows.Forms.DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(git.assets.Count > 0 ? git.assets.First().browser_download_url : git.html_url);
                        }
                    }
                    //else if (versionComparison > 0)
                    //{
                    //    //This local version is greater than the release version on GitHub.
                    //    //Do nothing
                    //}
                    else
                    {
                        //This local Version and the Version on GitHub are equal.
                        if (forceShowMessage)
                        {
                            MessageBox.Show(new Form { TopMost = true }, "JTranslator is up to date.", @"JTranslator");
                        }

                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());


        }

        /// <summary>Brings main window to foreground.</summary>
        public void BringToForeground()
        {
            if (this.WindowState == WindowState.Minimized || this.Visibility == Visibility.Hidden)
            {
                this.WindowState = WindowState.Normal;
            }
            this.Show();
            // According to some sources these steps gurantee that an app will be brought to foreground.
            this.Activate();
            this.Topmost = false;
            this.Topmost = true;
            this.Focus();
        }

        private void MoveToMainScreen()
        {
            var allScreens = Screen.AllScreens.ToList();
            var screenOfChoice = allScreens.FirstOrDefault();
            if (screenOfChoice != null)
            {
                var helper = new WindowInteropHelper(this).Handle;
                //MoveWindow(helper, screenOfChoice.WorkingArea.Left, screenOfChoice.WorkingArea.Top, screenOfChoice.WorkingArea.Width, screenOfChoice.WorkingArea.Height, false);
                MoveWindow(helper, (int)((screenOfChoice.WorkingArea.Width - this.Width) / 2), (int)((screenOfChoice.WorkingArea.Height - this.Width) / 2), screenOfChoice.WorkingArea.Width, screenOfChoice.WorkingArea.Height, false);
            }

            BringToForeground();
        }

        private void InitNotifyicon()
        {
            System.Windows.Forms.ContextMenu menu = new System.Windows.Forms.ContextMenu();
            menu.MenuItems.Add("Move window to main screen", (s, e) =>
            {
                MoveToMainScreen();
            });
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Minimize/ Maximize", (s, e) =>
            {
                this.Topmost = true;
                MinimizeButton_Click(null, null);
            });
            menu.MenuItems.Add("Transparent mode", (s, e) =>
            {
                this.Topmost = true;
                OpacityButton_Click(null, null);
            });
            menu.MenuItems.Add("Vietnamese ↔ Japanese", (s, e) =>
            {
                this.Topmost = true;
                LanguageButton_Click(null, null);
            });
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Check for update", (s, e) =>
            {
                this.Topmost = true;
                CheckGitHubNewerVersion(true);
            });
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Exit", (s, e) => { CloseButton_Click(null, null); });
            NotifyIcon notifyIcon = new NotifyIcon(); // Declaration 
            notifyIcon.BalloonTipText = "Hello, JTranslator!"; // Text of BalloonTip 
            notifyIcon.Text = "JTranslator"; // ToolTip of NotifyIcon 
            notifyIcon.Icon = Properties.Resources.translate;
            //notifyIcon.Icon = new Icon(Application.GetResourceStream(new Uri("/translate.ico", UriKind.Relative))?.Stream ?? throw new InvalidOperationException()); // Shown Icon 
            notifyIcon.Visible = true;
            //notifyIcon.ShowBalloonTip(1000);
            notifyIcon.ContextMenu = menu;
            notifyIcon.DoubleClick += (sender, args) =>
            {
                //this.Topmost = true;
                //MinimizeButton_Click(null, null);
                MoveToMainScreen();
            };
        }

        private void DeserializeData(string fileName)
        {
            if (!File.Exists(fileName)) return;
            using var file = File.OpenRead(System.AppDomain.CurrentDomain.BaseDirectory + fileName);
            switch (fileName)
            {
                case KanjiFileName:
                    _kanjiList = Serializer.Deserialize<List<Result>>(file);
                    file.Close();
                    break;
                case SettingFileName:
                    _setting = Serializer.Deserialize<Setting>(file);
                    file.Close();
                    break;
                case HistoryFileName:
                    _histories = Serializer.Deserialize<List<String>>(file);
                    file.Close();
                    if (_histories.Count() > 200)
                    {
                        _histories = _histories.Skip(Math.Max(0, _histories.Count() - 200)).ToList();
                        SerializeData(HistoryFileName);
                    }
                    _histories.Reverse();
                    break;
                default:
                    break;
            }
        }

        private void SerializeData(string fileName)
        {
            using var file = File.Create(System.AppDomain.CurrentDomain.BaseDirectory + fileName);
            switch (fileName)
            {
                case KanjiFileName:
                    Serializer.Serialize(file, _kanjiList);
                    break;
                case SettingFileName:
                    Serializer.Serialize(file, _setting);
                    break;
                case HistoryFileName:
                    Serializer.Serialize(file, _histories);
                    break;
                default:
                    break;
            }
        }

        //private void SaveHistoryData(string text)
        //{
        //    //Dispatcher?.Invoke(DispatcherPriority.Render, new Action(() =>
        //    //{
        //    //    using var file = File.Open(HistoryFileName, System.IO.FileMode.Append, FileAccess.Write);
        //    //    Serializer.Serialize(file, data);
        //    //}));
        //    if (text.Length >= 100) return;
        //    var index = histories.IndexOf(text.Trim());
        //    if (index >= 0) histories.RemoveAt(index);
        //    histories.Insert(0, text.Trim());
        //    if (histories.Count > 100) histories.RemoveAt(100);
        //    if (HistoryPopup.IsOpen) HistoryListView.Items.Refresh();
        //    SerializeData(HistoryFileName);
        //}

        private void SerializeHistoryObject(string text)
        {
            if (text.Length >= 100) return;
            var index = _histories.IndexOf(text.Trim());
            if (index >= 0) _histories.RemoveAt(index);
            _histories.Insert(0, text.Trim());
            if (_histories.Count > 100) _histories.RemoveAt(100);
            if (HistoryPopup.IsOpen) HistoryListView.Items.Refresh();
            Dispatcher?.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                using var file = File.Open(System.AppDomain.CurrentDomain.BaseDirectory + HistoryFileName, System.IO.FileMode.Append, FileAccess.Write);
                Serializer.Serialize(file, text);
            }));
        }

        private void SerializeKanjiObject(List<Result> result)
        {
            Dispatcher?.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                using var file = File.Open(System.AppDomain.CurrentDomain.BaseDirectory + KanjiFileName, System.IO.FileMode.Append, FileAccess.Write);
                Serializer.Serialize(file, result);
            }));
        }

        private void StartHooks()
        {
            System.Windows.Clipboard.Clear();
            var wih = new WindowInteropHelper(this);
            _hWndSource = HwndSource.FromHwnd(wih.EnsureHandle());
            _globalMouseHook = Hook.GlobalEvents();
            _cancellationTokenSource = new CancellationTokenSource();
            var source = this._hWndSource;
            if (source == null) return;
            source.AddHook(this.WinProc); // start processing window messages
            this._hWndNextViewer = NativeMethods.SetClipboardViewer(source.Handle); // set this window as a viewer
        }

        private void InitData()
        {
            var flowDocument = new FlowDocument();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(
                new Run(
                        "• Quét chọn để dịch.\n• Di chuột lên từ vựng để xem comment.\n• Nhấp chuột vào từ vựng để xem chi tiết Kanji.\n• Ctrl + Alt + T để tắt/mở tự động dịch.\n• Ctrl + Alt + W để xóa kết quả.\n• Ctrl + Alt + M để thu nhỏ/phóng to.\n• Ctrl + Alt + Q để thoát.")
                { Foreground = Brushes.Teal });
            flowDocument.Blocks.Add(paragraph);
            MaziiRichTextBox.Document = flowDocument;
            var flowDocument2 = new FlowDocument();
            var paragraph2 = new Paragraph();
            paragraph2.Inlines.Add(new Run("Cài đặt sẽ được lưu lại.") { Foreground = Brushes.Gray, FontSize = 10 });
            paragraph2.Inlines.Add(new Run("\n(2019-2022) Developed by Jacky with ❤️")
            { Foreground = Brushes.Gray, FontSize = 10 });
            paragraph2.Inlines.Add(new Run("\nVer " + Assembly.GetExecutingAssembly().GetName().Version)
            { Foreground = Brushes.Gray, FontSize = 10 });
            flowDocument2.Blocks.Add(paragraph2);
            GoogleRichTextBox.Document = flowDocument2;
        }


        private void SubscribeLocalEvents()
        {
            _globalMouseHook.MouseDown += _mouseDownHandler;
            _globalMouseHook.MouseUp += _mouseUpHandler;
            SubscribeDoubleClickEvent();
        }

        private void UnsubscribeLocalEvents()
        {
            _globalMouseHook.MouseDown -= _mouseDownHandler;
            _globalMouseHook.MouseUp -= _mouseUpHandler;
            _globalMouseHook.MouseDoubleClick -= _doubleClickHandler;
        }

        private void SubscribeDoubleClickEvent()
        {
            if (_notify.IsDoubleClickOn)
                _globalMouseHook.MouseDoubleClick += _doubleClickHandler;
            else
                _globalMouseHook.MouseDoubleClick -= _doubleClickHandler;
        }

        private async Task MouseDoubleClicked(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_notify.IsAutoTranslate || Logo.Visibility != Visibility.Visible || IsActive) return;
            await Task.Run(() =>
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) return;
                SendKeys.SendWait("^c");
                SendKeys.Flush();
            });
            _isMouseDown = false;
        }

        private new async Task MouseUp(object sender, MouseEventArgs e)
        {
            if (!_notify.IsAutoTranslate || Logo.Visibility != Visibility.Visible || IsActive) return;
            _mouseSecondPoint = e.Location;
            if (!_isMouseDown || _mouseSecondPoint.Equals(_mouseFirstPoint))
            {
                _isMouseDown = false;
                return;
            }
            await Task.Run(() =>
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    return;
                Thread.Sleep(200);
                SendKeys.SendWait("^c");
                SendKeys.Flush();
            });
            _isMouseDown = false;
        }

        private new async Task MouseDown(object sender, MouseEventArgs e)
        {
            if (_notify.IsAutoTranslate && Logo.Visibility == Visibility.Visible)
                await Task.Run(() =>
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested) return;
                    _mouseFirstPoint = e.Location;
                    _isMouseDown = true;
                });
        }

        private void DisposeHooks()
        {
            NativeMethods.ChangeClipboardChain(_hWndSource.Handle, _hWndNextViewer);
            _hWndNextViewer = IntPtr.Zero;
            _hWndSource.RemoveHook(WinProc);
            _globalMouseHook.Dispose();
            GC.SuppressFinalize(this);
        }

        //private void ClipboardChanged(object sender, EventArgs e)
        //{
        //    // Handle your clipboard update here, debug logging example:
        //    if (!Clipboard.ContainsText() || !_notify.IsAutoTranslate || Logo.Visibility == Visibility.Hidden) return;
        //    SearchTextBox.Text = Clipboard.GetText().Trim();
        //    if (_typingTimer == null)
        //    {
        //        /* WinForms: */
        //        _typingTimer = new Timer { Interval = 300 };
        //        _typingTimer.Tick += HandleTypingTimerTimeout;
        //    }

        //    _typingTimer.Stop(); // Resets the timer
        //    _typingTimer.Tag = (sender as TextBox)?.Text; // This should be done with EventArgs
        //    _typingTimer.Start();
        //}


        public void ExitApplication(object sender, EventArgs e)
        {
            Close();
        }

        public void MiniMaximize(object sender, EventArgs e)
        {
            MinimizeButton_Click(null, null);
        }

        public void ToggleTranslate(object sender, EventArgs e)
        {
            //QueryTextBlock.Text = "";
            _notify.IsAutoTranslate = !_notify.IsAutoTranslate;
            //MaziiRichTextBox.Document.Blocks.Clear();
            //GoogleRichTextBox.Document.Blocks.Clear();
        }

        public void ClearResult(object sender, EventArgs e)
        {
            ClearButton_Click(null, null);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Environment.Exit(0);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _setting.IsJavi = _notify.IsJaVi;
            _setting.IsAutoTranslate = _notify.IsAutoTranslate;
            _setting.IsFaded = _notify.IsFaded;
            _setting.IsLoadKanji = _notify.IsLoadKanji;
            _setting.IsRunOnStartUp = _notify.IsRunOnStartUp;
            _setting.IsDoubleClickOn = _notify.IsDoubleClickOn;
            SerializeData(SettingFileName);
            DisposeHooks();
            UnsubscribeLocalEvents();
            base.OnClosing(e);

        }

        private IntPtr WinProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case NativeMethods.WmChangecbchain:
                    if (wParam == _hWndNextViewer)
                        _hWndNextViewer = lParam; //clipboard viewer chain changed, need to fix it.
                    else if (_hWndNextViewer != IntPtr.Zero)
                        NativeMethods.SendMessage(_hWndNextViewer, msg, wParam, lParam); //pass the message to the next viewer.
                    break;
                case NativeMethods.WmDrawclipboard:
                    if (_notify.IsAutoTranslate && Logo.Visibility == Visibility.Visible)
                        _ = HandleTextCaptured(msg, wParam, lParam).ConfigureAwait(false);
                    break;
            }

            return IntPtr.Zero;
        }

        private Task? HandleTextCaptured(int msg, IntPtr wParam, IntPtr lParam)
        {
            return Dispatcher?.InvokeAsync(() =>
            {
                NativeMethods.SendMessage(_hWndNextViewer, msg, wParam, lParam); //pass the message to the next viewer //clipboard content changed
                if (!Clipboard.ContainsText()) return;
                var currentText = Clipboard.GetText().Trim();

                if (!string.IsNullOrEmpty(currentText))
                {
                    SearchTextBox.Text = currentText;
                }
            }, DispatcherPriority.Background).Task;
        }


        private void SearchAsync(string text)
        {
            //_currentGuid = Guid.NewGuid();
            if (text.Length <= 0) return;
            if (text.Equals(":init"))
            {
                InitDate();
            }
            else
            {
                MaziiRichTextBox.Document.Blocks.Clear();
                GoogleRichTextBox.Document.Blocks.Clear();
                MaziiTranslate(text);
                GoogleTranslate(text, _notify.IsJaVi ? "ja" : "vi", _notify.IsJaVi ? "vi" : "ja");
                SerializeHistoryObject(text);
            }
        }

        private void InitDate()
        {
            foreach (var item in _kanjiList.Where(item => item.date == DateTime.MinValue))
            {
                item.date = DateTime.Now;
            }
            SerializeData(KanjiFileName);
            MessageBox.Show(@"Done!");
        }

        private void GoogleTranslate(string sourceText, string from, string to)
        {
            // Initialize
            TransProgressBar.Visibility = Visibility.Visible;
            //https://translate.googleapis.com/translate_a/single?client=gtx&dt=t&dt=bd&dj=1&dt=ex&dt=ld&dt=md&dt=qca&dt=rw&dt=rm&dt=ss&dt=at&sl=ja&tl=vi&q=
            var uri =
                $"https://translate.googleapis.com/translate_a/single?client=gtx&dt=t&dt=bd&dj=1&dt=ex&dt=ld&dt=md&dt=qca&dt=rw&dt=rm&dt=ss&dt=at&sl={@from}&tl={to}&q=";
            var request = (HttpWebRequest)WebRequest.Create(uri + Uri.EscapeDataString(sourceText));
            request.KeepAlive = false;
            request.UserAgent = USER_AGENT;
            request.Method = "GET";
            request.ContentLength = 0;
            request.ContentType = "application/json";
            request.Proxy = null;

            void Action()
            {
                try
                {
                    _googleThread = Thread.CurrentThread;
                    using var response = (HttpWebResponse)request.GetResponse();
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        var message = $"Request failed. Received HTTP {response.StatusCode}";
                        throw new ApplicationException(message);
                    }

                    using (var responseStream = response.GetResponseStream())
                    {
                        if (responseStream == null) return;
                        using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                        {
                            var jsonString = reader.ReadToEnd();
                            jsonString = jsonString.Replace(@"\r", "");
                            jsonString = jsonString.Replace(@"\n", "");
                            _google = new Google();
                            _google = JsonConvert.DeserializeObject<Google>(jsonString);
                        }

                        responseStream.Flush();
                        responseStream.Close();
                    }
                    response.Dispose();
                }
                catch (WebException)
                {
                    _google = new Google
                    {
                        status = 500
                    };
                }
            }

            Task.Run(Action).ContinueWith(x =>
            {
                var flowDocument = new FlowDocument();
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run("From Google Translate") { Foreground = Brushes.Gray, FontSize = 10 });
                if (_google?.sentences != null && _google.sentences.Count > 0)
                {
                    foreach (var item in _google.sentences.Where(item => !String.IsNullOrEmpty(item.trans))
                        .Select(item => item))
                    {
                        paragraph.Inlines.Add(new Run("\n" + item.orig) { Foreground = Brushes.DarkCyan });
                        paragraph.Inlines.Add(new Run("\n" + item.trans));
                        //MessageBox.Show(item.Trans);
                    }

                    if (!String.IsNullOrEmpty(_google.sentences[_google.sentences.Count - 1].src_translit))
                        paragraph.Inlines.Add(
                            new Run("\n" + _google.sentences[_google.sentences.Count - 1].src_translit)
                            { Foreground = Brushes.OrangeRed, FontSize = 12 });
                }
                else if (_google != null && _google.status == 500)
                {
                    paragraph.Inlines.Add(new Run("\nKhông thể kết nối đến server!") { Foreground = Brushes.OrangeRed });
                }

                flowDocument.Blocks.Add(paragraph);
                GoogleRichTextBox.Document = flowDocument;
                //GoogleRichTextBox.Visibility = Visibility.Visible;
                if (Logo.Visibility == Visibility.Visible && GoogleRichTextBox.Visibility == Visibility.Collapsed)
                    GoogleRichTextBox.Visibility = Visibility.Visible;

                HideProgressBar();
                _googleThread = null;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void MaziiTranslate(string sourceText)
        {
            _isLoadingNew = true;
            if (sourceText.Length > 20)
            {
                var flowDocument = new FlowDocument();
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run("Mazii") { Foreground = Brushes.Gray, FontSize = 10 });
                paragraph.Inlines.Add(new Run("\nKhông có trong từ điển") { Foreground = Brushes.DarkOrange });
                flowDocument.Blocks.Add(paragraph);
                MaziiRichTextBox.Document = flowDocument;
                HideProgressBar();
            }
            else
            {
                // Initialize
                TransProgressBar.Visibility = Visibility.Visible;
                const string uri = "https://mazii.net/api/search";
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.KeepAlive = false;
                request.Proxy = null;

                request.Method = "POST";
                request.ContentType = "application/json";
                request.UserAgent = USER_AGENT;
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    var myData = new
                    {
                        dict = "javi",
                        type = "word",
                        query = sourceText,
                        limit = 20,
                        page = 1
                    };
                    streamWriter.Write(JsonConvert.SerializeObject(myData));
                }

                void Action()
                {
                    try
                    {
                        _maziiThread = Thread.CurrentThread;
                        using var response = (HttpWebResponse)request.GetResponse();
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            var message = $"Request failed. Received HTTP {response.StatusCode}";
                            throw new ApplicationException(message);
                        }

                        using (var responseStream = response.GetResponseStream())
                        {
                            if (responseStream == null) return;
                            using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                            {
                                var jsonString = reader.ReadToEnd();
                                _mazii = new Mazii();
                                _mazii = JsonConvert.DeserializeObject<Mazii>(jsonString);
                            }

                            responseStream.Flush();
                            responseStream.Close();
                        }
                        response.Dispose();
                    }
                    catch (WebException)
                    {
                        _mazii = new Mazii
                        {
                            status = 500
                        };
                    }
                }

                Task.Run(Action).ContinueWith(x =>
                {
                    _isLoadingNew = false;
                    var flowDocument = new FlowDocument();
                    var paragraph = new Paragraph();
                    //paragraph.Inlines.Add(new Run(sourceText) {Foreground = Brushes.DarkCyan});
                    paragraph.Inlines.Add(new Run("From Mazii") { Foreground = Brushes.Gray, FontSize = 10 });
                    if (_mazii?.data != null)
                    {
                        _mazii.data = _mazii.data.Take(5).ToList();
                        foreach (var item in _mazii.data)
                        {
                            //create new link
                            Hyperlink kanjiLink = new Hyperlink(new Run(item.word))
                            {
                                IsEnabled = true,
                                Foreground = Brushes.DarkGreen,
                                FontWeight = FontWeights.DemiBold,
                                TextDecorations = null,
                                //ToolTip = "Xem chi tiết Kanji",
                            };
                            var s = GetCharsInRange(item.word, 0x4E00, 0x9FBF).ToList();
                            if (s.Any())
                                kanjiLink.Click += (sender, args) =>
                                {
                                    KanjiLookup(s.Select(c => c.ToString()).Distinct().ToList(), 0);
                                    //GetMaziiComment(item.mobileId);
                                };
                            else
                                kanjiLink.Cursor = System.Windows.Input.Cursors.Arrow;
                            kanjiLink.MouseEnter += (sender, args) =>
                            {
                                if (_hoverTimer == null)
                                {
                                    _hoverTimer = new Timer { Interval = 500 };
                                    _hoverTimer.Tick += (sender, args) =>
                                    {
                                        // var timer = sender as DispatcherTimer; // WPF
                                        if (!(sender is Timer timer))
                                            return;
                                        // The timer must be stopped! We want to act only once per keystroke.
                                        timer.Stop();
                                        _isMouseHoverText = true;
                                        GetMaziiComment((int)timer.Tag);
                                    };
                                }
                                _hoverTimer.Stop(); // Resets the timer
                                _hoverTimer.Tag = item.mobileId;
                                _hoverTimer.Start();
                            };

                            kanjiLink.MouseLeave += (sender, args) =>
                            {
                                Mouse.SetCursor(System.Windows.Input.Cursors.Arrow);
                                _hoverTimer.Stop();
                                _isMouseHoverText = false;
                                CommentPopup.IsOpen = false;
                                ToastBorder.Visibility = Visibility.Collapsed;
                            };


                            paragraph.Inlines.Add("\n");
                            paragraph.Inlines.Add(kanjiLink);
                            //paragraph.Inlines.Add(new Run("\n" + item.word)
                            //{ Foreground = Brushes.DarkGreen, FontWeight = FontWeights.DemiBold });
                            if (item.phonetic?.Length > 0)
                                paragraph.Inlines.Add(new Run(" (" + item.phonetic + ")")
                                { Foreground = Brushes.OrangeRed });

                            paragraph.Inlines.Add(new Run(" ") { Foreground = Brushes.DarkSlateGray, FontSize = 11 });
                            foreach (var mean in item.means)
                            {
                                paragraph.Inlines.Add(new Run("\n• "));
                                if (!String.IsNullOrEmpty(mean.kind))
                                    paragraph.Inlines.Add(new Run(" (" + mean.kind + ") ") { Foreground = Brushes.Teal });
                                paragraph.Inlines.Add(new Run(mean.mean + ""));
                            }
                            //var cmtLink = new Hyperlink(new Run("\ncomments")
                            //{
                            //    IsEnabled = true,
                            //    Foreground = Brushes.DarkGray,
                            //    FontWeight = FontWeights.DemiBold,
                            //    FontSize = 8,
                            //    TextDecorations = null,
                            //    ToolTip = "Xem comment của từ này",
                            //});
                            //cmtLink.Click += (sender, args) =>
                            //{
                            //    GetMaziiComment(item.mobileId);
                            //};
                            //paragraph.Inlines.Add(cmtLink);

                            //if (item.related_words?.word?.Count > 0)
                            //{
                            //    //dict = new List<string>(item.opposite_word);
                            //    paragraph.Inlines.Add(new Run("\nTừ đồng nghĩa: ") { Foreground = Brushes.Gray });
                            //    var last = item.related_words.word.Last();
                            //    foreach (var w in item.related_words.word)
                            //    {
                            //        paragraph.Inlines.Add(GetHyperlink(w));
                            //        if (w != last)
                            //            paragraph.Inlines.Add(new Run(", ") { Foreground = Brushes.DarkMagenta });
                            //    }
                            //}
                            if (item.opposite_word == null || item.opposite_word.Count <= 0) continue;
                            paragraph.Inlines.Add(new Run("\nTỪ TRÁI NGHĨA: ") { Foreground = Brushes.Gray, FontSize = 11 });
                            var last = item.opposite_word.Last();
                            foreach (var w in item.opposite_word)
                            {
                                paragraph.Inlines.Add(GetHyperlink(w, Brushes.DarkMagenta));
                                if (w != last)
                                    paragraph.Inlines.Add(new Run(", ") { Foreground = Brushes.DarkMagenta });
                            }
                        }
                    }
                    else if (_mazii != null && _mazii.status == 500)
                        paragraph.Inlines.Add(new Run("\nKhông thể kết nối đến server!")
                        { Foreground = Brushes.OrangeRed });
                    else
                        paragraph.Inlines.Add(new Run("\nNot found!") { Foreground = Brushes.OrangeRed });

                    flowDocument.Blocks.Add(paragraph);

                    MaziiRichTextBox.Document = flowDocument;
                    //new InlineUIContainer(new System.Windows.Controls.Button() { Content = "五​" }, MaziiRichTextBox.CaretPosition);
                    InitLoadKanjiReading();

                    HideProgressBar();
                    if (Logo.Visibility == Visibility.Visible && MaziiRichTextBox.Visibility == Visibility.Collapsed)
                        MaziiRichTextBox.Visibility = Visibility.Visible;
                    _maziiThread = null;
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private Hyperlink GetHyperlink(string word, System.Windows.Media.SolidColorBrush foreground)
        {
            var link = new Hyperlink
            {
                IsEnabled = true,
                Foreground = foreground,
                FontWeight = FontWeights.SemiBold,
                TextDecorations = null,
                //ToolTip = "Xem nghĩa",
            };
            link.Inlines.Add(word);
            link.Click += (sender, args) => { SearchTextBox.Text = word; };
            return link;
        }

        private void InitLoadKanjiReading()
        {
            if (_mazii?.data == null || !_notify.IsLoadKanji || _isLoadingNew) return;
            var word = string.Concat(_mazii.data.Select(w => w.word));
            var onlyKanji = GetCharsInRange(word, 0x4E00, 0x9FBF).Select(c => c.ToString()).Distinct();
            // Refresh if data is older than 3 month
            var exists = _kanjiList.Where(x => onlyKanji.Contains(x.kanji) && (DateTime.Now - x.date).Days < 30 * 3).Select(k => k.kanji);
            word = string.Concat(onlyKanji.Except(exists));
            var words = SplitInParts(word, 5).ToList();

            void Action()
            {
                //var romaji = GetCharsInRange(searchKeyword, 0x0020, 0x007E);
                //var hiragana = GetCharsInRange(searchKeyword, 0x3040, 0x309F);
                //var katakana = GetCharsInRange(searchKeyword, 0x30A0, 0x30FF);
                //var kanji = GetCharsInRange(searchKeyword, 0x4E00, 0x9FBF);
                //var chars = GetCharsInRange(word, 0x4E00, 0x9FBF);
                //
                if (!words.Any()) return;
                _isLoadingKanji = true;
                const string uri = "https://mazii.net/api/mazii/";
                foreach (var word in words)
                {
                    //request.Method = "GET";
                    //request.ContentLength = 0;
                    //request.ContentType = "application/json";
                    //request.UserAgent = USER_AGENT;
                    var request = (HttpWebRequest)WebRequest.Create(uri);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.UserAgent = USER_AGENT;
                    using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                    {
                        var myData = new
                        {
                            dict = "javi",
                            type = "kanji",
                            query = word,
                            limit = 20,
                            page = 1
                        };
                        streamWriter.Write(JsonConvert.SerializeObject(myData));
                    }
                    var kanji = new MaziiKanji();
                    try
                    {
                        var response = (HttpWebResponse)request.GetResponse();
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            var message = $"Request failed. Received HTTP {response.StatusCode}";
                            throw new ApplicationException(message);
                        }

                        var responseStream = response.GetResponseStream();
                        if (responseStream == null) return;
                        using var reader = new StreamReader(responseStream);
                        var jsonString = reader.ReadToEnd();
                        kanji = JsonConvert.DeserializeObject<MaziiKanji>(jsonString);
                        if (kanji.status == 200)
                        {
                            var count = _kanjiList.RemoveAll(k => kanji.results.Exists(e => e.kanji == k.kanji));
                            foreach (var kan in kanji.results)
                            {
                                kan.date = DateTime.Now;
                            }
                            _kanjiList.AddRange(kanji.results);
                            if (count > 0)
                                SerializeData(KanjiFileName);
                            else
                                SerializeKanjiObject(kanji.results);
                        }
                        else
                        {
                            //Console.WriteLine(@"Error: " + string.Concat(str2));
                        }
                        responseStream.Flush();
                        responseStream.Close();
                    }
                    catch (WebException)
                    {
                        //Do nothing
                    }
                }
            }
            Task.Run(Action).ContinueWith(x =>
            {
                foreach (var data in _mazii.data.Where(data => GetCharsInRange(data.word, 0x4E00, 0x9FBF).Any()))
                {
                    InsertCaretPosition(data.word, data.phonetic);
                }
                _isLoadingKanji = false;
                HideProgressBar();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static IEnumerable<string> SplitInParts(string s, int partLength)
        {
            //if (s == null)
            //    throw new ArgumentNullException("s");
            //if (partLength <= 0)
            //    throw new ArgumentException(@"Part length has to be positive.", nameof(partLength));
            for (var i = 0; i < s.Length; i += partLength)
                yield return s.Substring(i, Math.Min(partLength, s.Length - i));
        }


        private void InsertCaretPosition(string word, string phonetic)
        {
            var onlyKanji = string.Concat(GetCharsInRange(word, 0x4E00, 0x9FBF).Select(c => c.ToString()));
            var hantu = Regex.Replace(_kanjiList.Aggregate(onlyKanji,
                (current, next) => current.Replace(next.kanji, (next.mean.Length > 0 ? next.mean.Split(',')[0] : "-") + " ")).TrimEnd(),
                @"\p{IsCJKUnifiedIdeographs}", "-");
            //Console.WriteLine(word + hantu);
            var position = MaziiRichTextBox.Document.ContentStart;
            while (position != null)
            {
                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    var textRun = position.GetTextInRun(LogicalDirection.Forward);
                    // Find the starting index of any substring that matches "word".
                    var indexInRun = textRun.IndexOf(word, StringComparison.Ordinal);
                    if (indexInRun >= 0)
                    {
                        var str = word + (phonetic.Length > 0 ? @" (" + phonetic + @")" : "");
                        var start = position.GetPositionAtOffset(indexInRun);
                        var textRng = new TextRange(start, start?.GetPositionAtOffset(str.Length + 4));
                        if (textRng.Text.Trim().Equals(str.Trim()))
                        {
                            textRng = new TextRange(start?.GetPositionAtOffset(str.Length + 6), start?.GetPositionAtOffset(str.Length + hantu.Length + 7));
                            if (!textRng.Text.Trim().Equals(hantu))
                            {
                                MaziiRichTextBox.CaretPosition = start?.GetPositionAtOffset(str.Length + 6);
                                //MaziiRichTextBox.CaretPosition?.InsertLineBreak();
                                MaziiRichTextBox.CaretPosition?.InsertTextInRun("\n" + hantu);
                                //var caretPosition = MaziiRichTextBox.CaretPosition?.GetPositionAtOffset(4);
                                //if (caretPosition != null)
                                //{
                                //var insertPosition = MaziiRichTextBox.CaretPosition.IsAtInsertionPosition ?
                                //    MaziiRichTextBox.CaretPosition : MaziiRichTextBox.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
                                // hyperlink(run,insertpos)
                                //}
                                break;
                            }

                        }
                    }
                }
                //Console.WriteLine("Still run");
                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        private void GetMaziiComment(int wordId)
        {
            Mouse.SetCursor(System.Windows.Input.Cursors.Wait);
            //KanjiPopup.IsOpen ? -260 : -5
            if (!KanjiPopup.IsOpen)
            {
                CommentPopup.PlacementRectangle = new Rect(-5, 0, 0, 0);
                var maziiComment = _maziiComments.FirstOrDefault(e => e.wordId == wordId);
                void Action()
                {
                    if (maziiComment == null)
                    {
                        maziiComment = new();
                        const string uri = "https://api.mazii.net/api/get-mean";
                        var request = (HttpWebRequest)WebRequest.Create(uri);
                        request.Method = "POST";
                        request.ContentType = "application/json";
                        request.UserAgent = USER_AGENT;
                        using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                        {
                            var myData = new
                            {
                                wordId = wordId,
                                type = "word",
                                dict = "javi"
                            };
                            streamWriter.Write(JsonConvert.SerializeObject(myData));
                        }
                        try
                        {
                            var response = (HttpWebResponse)request.GetResponse();
                            if (response.StatusCode != HttpStatusCode.OK)
                            {
                                var message = $"Request failed. Received HTTP {response.StatusCode}";
                                throw new ApplicationException(message);
                            }
                            using (var responseStream = response.GetResponseStream())
                            {
                                if (responseStream == null) return;
                                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                                {
                                    var jsonString = reader.ReadToEnd();
                                    maziiComment = new MaziiComment();
                                    maziiComment = JsonConvert.DeserializeObject<MaziiComment>(jsonString);
                                    maziiComment.wordId = wordId;
                                    _maziiComments.Add(maziiComment);
                                    if (_maziiComments.Count > 10)
                                    {
                                        _maziiComments = _maziiComments.Skip(Math.Max(0, _maziiComments.Count() - 10)).ToList();
                                    }
                                }

                                responseStream.Flush();
                                responseStream.Close();
                            }
                            response.Dispose();
                        }
                        catch (WebException)
                        {
                            maziiComment = new MaziiComment
                            {
                                status = 500
                            };
                        }
                    }
                }
                Task.Run(Action).ContinueWith(x =>
                {
                    var flowDocument = new FlowDocument();
                    var paragraph = new Paragraph();
                    //paragraph.Inlines.Add(new Run("Comment") { Foreground = Brushes.Gray, FontSize = 10 });
                    if (maziiComment.result != null)
                    {
                        foreach (var comment in maziiComment.result.OrderByDescending(i => i.like).Take(8))
                        {
                            paragraph.Inlines.Add(new Run(comment.mean.Trim()) { Foreground = Brushes.Black });
                            paragraph.Inlines.Add(new Run($"\n {comment.username}") { Foreground = Brushes.DarkGray, FontSize = 10 });
                            paragraph.Inlines.Add(new Run($"    {comment.like}") { Foreground = Brushes.DarkCyan, FontSize = 10 });
                            paragraph.Inlines.Add(new Run($"↑") { Foreground = Brushes.DarkGray, FontSize = 10 });
                            paragraph.Inlines.Add(new Run($" {comment.dislike}") { Foreground = Brushes.DarkCyan, FontSize = 10 });
                            paragraph.Inlines.Add(new Run($"↓ \n\n") { Foreground = Brushes.DarkGray, FontSize = 10 });
                        }

                        flowDocument.Blocks.Add(paragraph);
                        CommentRichTextBox.Document = flowDocument;

                        CommentPopup.IsOpen = maziiComment.result.Count > 0 && _isMouseHoverText;
                    }
                    if (maziiComment.result == null || maziiComment.result.Count == 0)
                    {
                        ToastBorder.Visibility = Visibility.Visible;
                        ToastTextBlock.Text = "Không có comment";
                    }
                    Mouse.SetCursor(System.Windows.Input.Cursors.Hand);

                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void KanjiLookup(List<string> kanChars, int index)
        {
            KanjiPopup.IsOpen = true;
            var find = _kanjiList.FirstOrDefault(x => x.kanji == kanChars[index]);
            var flowDocument = new FlowDocument();
            var paragraph = new Paragraph();
            paragraph.TextAlignment = TextAlignment.Center;
            paragraph.Inlines.Add(new Run(kanChars[index] + "\n") { Foreground = Brushes.Teal, FontWeight = FontWeights.DemiBold, FontSize = 50 });
            paragraph.Inlines.Add(new Run("(" + (find == null || find.mean.Length == 0 ? "-" : find.mean) + ")\n") { Foreground = Brushes.DarkMagenta, FontSize = 12 });
            for (var i = 0; i < kanChars.Count; i++)
            {
                var button = new Button();
                var text = new TextBlock
                {
                    Text = kanChars[i],
                    FontWeight = (i == index ? FontWeights.Bold : FontWeights.SemiBold),
                    Foreground = (i == index ? Brushes.Teal : Brushes.Black),
                };
                button.Content = text;
                var i1 = i;
                button.Click += (sender, args) => { KanjiLookup(kanChars, i1); };
                //button.Padding = new Thickness(5);
                button.Style = (Style)FindResource("RoundButtonStyle");
                new InlineUIContainer(button, paragraph.ContentEnd);
            }
            flowDocument.Blocks.Add(paragraph);
            KanjiMainRichTextBox.Document = flowDocument;
            flowDocument = new FlowDocument();
            paragraph = new Paragraph();
            if (find != null)
            {
                paragraph.Inlines.Add(new Run("CẤU THÀNH: ") { Foreground = Brushes.DarkSlateBlue, FontSize = 11, FontWeight = FontWeights.DemiBold });
                if (find.compDetail != null)
                    paragraph.Inlines.Add(new Run(string.Join(", ", find.compDetail?.Select(x => x.w))) { Foreground = Brushes.Black });
                paragraph.Inlines.Add(new Run("\nÂM ON: ") { Foreground = Brushes.DarkSlateBlue, FontSize = 11, FontWeight = FontWeights.DemiBold });
                paragraph.Inlines.Add(new Run(find.on) { Foreground = Brushes.Black });
                paragraph.Inlines.Add(new Run("\nÂM KUN: ") { Foreground = Brushes.DarkSlateBlue, FontSize = 11, FontWeight = FontWeights.DemiBold });
                paragraph.Inlines.Add(new Run(find.kun?.Replace(" ", ", ")) { Foreground = Brushes.Black });
                paragraph.Inlines.Add(new Run("\nSỐ NÉT: ") { Foreground = Brushes.DarkSlateBlue, FontSize = 11, FontWeight = FontWeights.DemiBold });
                paragraph.Inlines.Add(new Run(find.stroke_count) { Foreground = Brushes.Black });
                paragraph.Inlines.Add(new Run("\nĐỊNH NGHĨA:") { Foreground = Brushes.DarkSlateBlue, FontSize = 11, FontWeight = FontWeights.DemiBold });
                paragraph.Inlines.Add(new Run("\n• " + find.detail.Replace("##", "\n• ")) { Foreground = Brushes.Black });
                flowDocument.Blocks.Add(paragraph);
                if (find.examples != null)
                {
                    paragraph.Inlines.Add(new Run("\nVÍ DỤ:\n") { Foreground = Brushes.DarkSlateBlue, FontSize = 11, FontWeight = FontWeights.DemiBold });
                    foreach (var example in find.examples)
                    {
                        paragraph.Inlines.Add(GetHyperlink(example.w, Brushes.DarkGreen));
                        //paragraph.Inlines.Add(new Run("\n" + example.w) { Foreground = Brushes.DarkGreen, FontWeight = FontWeights.DemiBold });
                        if (example.p?.Length > 0)
                            paragraph.Inlines.Add(new Run(" (" + example.p + ")") { Foreground = Brushes.OrangeRed });
                        paragraph.Inlines.Add(new Run(" - " + example.h) { Foreground = Brushes.DarkSlateGray, FontSize = 11 });
                        paragraph.Inlines.Add(new Run("\n" + example.m + "\n"));
                    }
                    flowDocument.Blocks.Add(paragraph);
                }
                else
                {
                    paragraph.Inlines.Add(new Run("\nKhông có dữ liệu!") { Foreground = Brushes.DarkRed });
                    flowDocument.Blocks.Add(paragraph);
                }



                //var start = flowDocument.ContentStart;
                //var end = start.GetPositionAtOffset(find.kanji.Length + $"\n({find.mean})\n".Length + 4, LogicalDirection.Forward);
                //if (end != null) KanjiRichTextBox.Selection.Select(start, end);
            }
            else
            {
                paragraph.Inlines.Add(new Run("\nKhông có dữ liệu!") { Foreground = Brushes.DarkRed });
                flowDocument.Blocks.Add(paragraph);
            }

            KanjiRichTextBox.Document = flowDocument;

        }

        private static IEnumerable<char> GetCharsInRange(string text, int min, int max)
        {
            return text.Where(e => e >= min && e <= max);
        }


        //    Task.Run(Action).ContinueWith(x =>
        //    {
        //        if (!guid.Equals(_currentGuid)) return;
        //        if (kanji.results == null)
        //            kanji.results = new List<Result>();
        //        kanji.results.AddRange(exists);
        //        kanji.results = kanji.results.OrderBy(k => word.IndexOf(k.kanji, StringComparison.Ordinal))
        //            .ToList();
        //        //foreach (var t in kanji.results) t.mean = t.mean.Split(',')[0].Trim();
        //        if (kanji.results.Any())
        //            InsertCaretPosition(string.Join(" ", kanji.results.Select(k => k.mean.Split(',')[0].Trim())), word, phonetic);
        //    }, TaskScheduler.FromCurrentSynchronizationContext());
        //}
        //else
        //{
        //exists = exists.OrderBy(k => word.IndexOf(k.kanji, StringComparison.Ordinal)).ToList();
        //InsertCaretPosition(string.Join(" ", exists.Select(k => k.mean.Split(',')[0].Trim())), word, phonetic);
        //}


        private void HideProgressBar(bool force = false)
        {
            if (force)
            {
                TransProgressBar.Visibility = Visibility.Collapsed;
            }
            if (MaziiRichTextBox.Document.ContentStart.CompareTo(MaziiRichTextBox.Document.ContentEnd) == 0 ||
                GoogleRichTextBox.Document.ContentStart.CompareTo(GoogleRichTextBox.Document.ContentEnd) == 0 || _isLoadingKanji) return;
            TransProgressBar.Visibility = Visibility.Collapsed;
        }

        private void ShowToast(string message)
        {
            ToastBorder.Visibility = Visibility.Visible;
            ToastTextBlock.Text = message;
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Tick += (sender, args) =>
            {
                ToastBorder.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            InvalidateMeasure();
        }

        private void LanguageButton_Click(object? sender, RoutedEventArgs? e)
        {
            _notify.IsJaVi = !_notify.IsJaVi;
            var text = SearchTextBox.Text.Trim();
            SearchAsync(text);
        }

        private void AutoTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            _notify.IsAutoTranslate = !_notify.IsAutoTranslate;
        }

        private void OpacityButton_Click(object? sender, RoutedEventArgs? e)
        {
            switch (_notify.IsFaded)
            {
                case 0:
                    _notify.IsFaded = 1;
                    break;
                case 1:
                    _notify.IsFaded = 2;
                    break;
                case 2:
                    _notify.IsFaded = 0;
                    break;
                default:
                    break;
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs? e)
        {
            Dispatcher?.Invoke((Action)(() =>
            {
                var result = MessageBox.Show(new Form { TopMost = true }, "Bạn có chắc muốn đóng ứng dụng?\nAre you sure you'd like to close the app?", @"JTranslator", MessageBoxButtons.YesNo);
                if (result == System.Windows.Forms.DialogResult.Yes) Close();
            }));

        }

        private void ClearButton_Click(object? sender, RoutedEventArgs? e)
        {
            HideProgressBar(true);
            if (_maziiThread != null && _maziiThread.IsAlive) _maziiThread.Abort();
            if (_googleThread != null && _googleThread.IsAlive) _googleThread.Abort();
            SearchTextBox.Text = "";
            MaziiRichTextBox.Document.Blocks.Clear();
            GoogleRichTextBox.Document.Blocks.Clear();
            _google = null;
            _mazii = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            InitData();
        }

        private void KanjiButton_Click(object sender, RoutedEventArgs e)
        {
            _notify.IsLoadKanji = !_notify.IsLoadKanji;
            if (_mazii?.data == null || !_notify.IsLoadKanji) return;
            InitLoadKanjiReading();
        }

        private void MinimizeButton_Click(object? sender, RoutedEventArgs? e)
        {

            Logo.Visibility = Logo.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            _notify.IsMinimized = !_notify.IsMinimized;

            if (Logo.Visibility == Visibility.Visible)
            {
                if (MaziiRichTextBox.Document.ContentStart.CompareTo(MaziiRichTextBox.Document.ContentEnd) != 0)
                    MaziiRichTextBox.Visibility = Visibility.Visible;
                if (GoogleRichTextBox.Document.ContentStart.CompareTo(GoogleRichTextBox.Document.ContentEnd) != 0)
                    GoogleRichTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                MaziiRichTextBox.Visibility = Visibility.Collapsed;
                GoogleRichTextBox.Visibility = Visibility.Collapsed;
                BringToForeground();
            }

            HistoryPopup.IsOpen = false;
            KanjiPopup.IsOpen = false;
            _notify.IsOpenedHistories = HistoryPopup.IsOpen;
            HideProgressBar(true);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_typingTimer == null)
            {
                /* WinForms: */
                _typingTimer = new Timer { Interval = 800 };
                _typingTimer.Tick += HandleTypingTimerTimeout;
            }

            _typingTimer.Stop(); // Resets the timer
            _typingTimer.Tag = (sender as TextBox)?.Text; // This should be done with EventArgs
            _typingTimer.Start();
        }

        private void HandleTypingTimerTimeout(object sender, EventArgs e)
        {
            // var timer = sender as DispatcherTimer; // WPF
            if (!(sender is Timer timer))
                return;
            // The timer must be stopped! We want to act only once per keystroke.
            timer.Stop();

            // Stop previous thread
            if (_maziiThread != null && _maziiThread.IsAlive) _maziiThread.Abort();
            if (_googleThread != null && _googleThread.IsAlive) _googleThread.Abort();

            // Search
            var text = SearchTextBox.Text.Trim();
            SearchAsync(text);
        }

        private void MinimizeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(MinimizeButton);
        }

        private void MinimizeButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var currentPoint = e.GetPosition(MinimizeButton);
            if (e.LeftButton == MouseButtonState.Pressed &&
                MinimizeButton.IsMouseCaptured &&
                (Math.Abs(currentPoint.X - _startPoint.X) >
                 SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(currentPoint.Y - _startPoint.Y) >
                 SystemParameters.MinimumVerticalDragDistance))
            {
                // Prevent Click from firing
                MinimizeButton.ReleaseMouseCapture();
                DragMove();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _notify.IsOpenedSettings = !_notify.IsOpenedSettings;
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Focus();
            HistoryListView.Items.Refresh();
            HistoryPopup.IsOpen = !HistoryPopup.IsOpen;
            _notify.IsOpenedHistories = !_notify.IsOpenedHistories;
        }

        private void CloseHisPopupButton_Click(object sender, RoutedEventArgs e)
        {
            HistoryPopup.IsOpen = false;
        }

        private void HistoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index;
            try
            {
                index = HistoryListView.SelectedIndex;
            }
            catch (Exception)
            {
                //Console.WriteLine(exception);
                return;
            }

            if (index < 0) return;
            SearchTextBox.Text = _histories[index];
            HistoryPopup.IsOpen = false;
            _notify.IsOpenedHistories = HistoryPopup.IsOpen;
        }

        //Popup position
        //private void Main_Loaded(object sender, RoutedEventArgs e)
        //{
        //    var w = GetWindow(this);
        //    // w should not be Null now!
        //    if (null == w) return;
        //    w.LocationChanged += delegate
        //    {
        //        var offset = KanjiPopup.HorizontalOffset;
        //        // "bump" the offset to cause the popup to reposition itself
        //        //   on its own
        //        KanjiPopup.HorizontalOffset = offset + 1;
        //        KanjiPopup.HorizontalOffset = offset;
        //    };
        //    // Also handle the window being resized (so the popup's position stays
        //    //  relative to its target element if the target element moves upon 
        //    //  window resize)
        //    w.SizeChanged += delegate
        //    {
        //        var offset = KanjiPopup.HorizontalOffset;
        //        KanjiPopup.HorizontalOffset = offset + 1;
        //        KanjiPopup.HorizontalOffset = offset;
        //    };
        //}

        private void ClearHisPopupButton_Click(object sender, RoutedEventArgs e)
        {
            _histories.Clear();
            HistoryListView.Items.Refresh();
            SerializeData(KanjiFileName);
        }

        private void RunOnStartUp()
        {
            using var key = Registry.CurrentUser.OpenSubKey
                ("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            var appName = Assembly.GetExecutingAssembly().GetName().Name;
            if (_notify.IsRunOnStartUp)
            {
                var values = key?.GetValueNames();
                if (values != null && Array.FindIndex(values, x => x == appName) == -1)
                {
                    //key?.SetValue(appName, "\"" + System.Environment.GetCommandLineArgs()[0] + "\"");
                    key?.SetValue(appName, System.Windows.Forms.Application.ExecutablePath);
                }
            }
            else
            {
                key?.DeleteValue(appName, false);
            }

        }

        private void RunOnStartUpButton_Click(object sender, RoutedEventArgs e)
        {
            _notify.IsRunOnStartUp = !_notify.IsRunOnStartUp;
            RunOnStartUp();
        }

        private void DClickButton_Click(object sender, RoutedEventArgs e)
        {
            _notify.IsDoubleClickOn = !_notify.IsDoubleClickOn;
            SubscribeDoubleClickEvent();
        }

        //private void CopyMenu_Click(object sender, RoutedEventArgs e)
        //{
        //    MaziiRichTextBox.Copy();
        //}

        //private void LookupMenu_Click(object sender, RoutedEventArgs e)
        //{
        //    MessageBox.Show($"Selected hantu: {MaziiRichTextBox.Selection.Text}. {kanjiList.FirstOrDefault(x => x.kanji.Equals(MaziiRichTextBox.Selection.Text))?.comp}.\nDeveloping... Stay tune 😘");
        //}

        private void CloseKanjiPopupButton_Click(object sender, RoutedEventArgs e)
        {
            KanjiPopup.IsOpen = !KanjiPopup.IsOpen;
        }

        private void CloseCommentPopupButton_Click(object sender, RoutedEventArgs e)
        {
            CommentPopup.IsOpen = !CommentPopup.IsOpen;
        }

        private void HistoryPopup_Closed(object sender, EventArgs e)
        {
            _notify.IsOpenedHistories = HistoryPopup.IsOpen;
        }

        private void Root_Loaded(object sender, RoutedEventArgs e)
        {
            //Hide from Alt+Tab
            //Variable to hold the handle for the form
            var helper = new WindowInteropHelper(this).Handle;
            //Performing some magic to hide the form from Alt+Tab
            SetWindowLong(helper, GWL_EX_STYLE, (GetWindowLong(helper, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        }

        private void KanjiPopup_Closed(object sender, EventArgs e)
        {
            KanjiMainRichTextBox.Document.Blocks.Clear();
            KanjiRichTextBox.Document.Blocks.Clear();
            //GC.Collect();
            //GC.WaitForPendingFinalizers();

        }
    }
}
