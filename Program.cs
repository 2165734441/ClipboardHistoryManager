using System.Runtime.InteropServices;
using System.Threading;
using ClipboardHistoryManager.Infrastructure;
using ClipboardHistoryManager.Models;

namespace ClipboardHistoryManager;

internal static class Program
{
    private const string SingleInstanceMutexName = "ClipboardHistoryManager.SingleInstance";
    private const string ShowWindowEventName = "ClipboardHistoryManager.ShowWindow";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.ThreadException += (_, e) => LogUnhandledException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                LogUnhandledException(exception);
            }
        };

        ApplicationConfiguration.Initialize();

        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTest.SelfTestRunner.Run();
        }

        if (args.Contains("--clipboard-self-test", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTest.SelfTestRunner.RunClipboardMonitorTest();
        }

        if (args.Contains("--popup-drag-self-test", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTest.SelfTestRunner.RunPopupDragTest();
        }

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            SignalExistingInstance();
            return 0;
        }

        using var showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        using var appContext = new ClipboardApplicationContext(showWindowEvent);
        Application.Run(appContext);
        return 0;
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var existingEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
            existingEvent.Set();
        }
        catch
        {
            // 第一实例尚未完成初始化时，第二实例直接退出。
        }
    }

    private static void LogUnhandledException(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            File.WriteAllText(Path.Combine(AppPaths.AppDataDirectory, "last-error.log"), exception.ToString());
            AppLogger.Error("未处理异常。", exception);
        }
        catch
        {
            // 避免异常报告自身再触发异常。
        }
    }

    private sealed class ClipboardApplicationContext : ApplicationContext
    {
        private readonly Data.ClipboardDatabase _database = new();
        private readonly Services.SettingsManager _settingsManager = new();
        private readonly Services.StartupManager _startupManager = new();
        private readonly Services.ClipboardHistoryService _historyService;
        private readonly Services.ClipboardMonitor _clipboardMonitor;
        private readonly Services.HotKeyService _hotKeyService = new();
        private readonly UI.MainForm _mainForm;
        private readonly UI.ClipboardPopupForm _clipboardPopupForm = new();
        private readonly UI.TrayIconManager _trayIconManager;
        private readonly RegisteredWaitHandle _singleInstanceSignalWait;
        private bool _disposed;

        public ClipboardApplicationContext(EventWaitHandle showWindowEvent)
        {
            _settingsManager.Load();
            AppLogger.Info("程序启动。");
            _database.Initialize();

            _historyService = new Services.ClipboardHistoryService(_database);
            _historyService.SetMaxHistoryEntries(_settingsManager.Current.MaxHistoryEntries);
            _clipboardMonitor = new Services.ClipboardMonitor(_historyService);
            _mainForm = new UI.MainForm(
                _historyService,
                _settingsManager,
                _hotKeyService,
                _startupManager,
                ApplyPopupSettings);
            _mainForm.ClipboardContentCopied += (_, content) => ShowClipboardPopup(content, allowWhenPaused: true);
            _ = _mainForm.Handle;

            _clipboardPopupForm.ApplySettings(_settingsManager.Current, SaveSettings);
            _trayIconManager = new UI.TrayIconManager(
                _historyService,
                _settingsManager,
                ShowMainWindow,
                ShowSettings,
                ApplyPopupSettings,
                RequestExit);

            _trayIconManager.Start();
            _clipboardMonitor.ClipboardContentChanged += (_, e) =>
                ShowClipboardPopup(e.DisplayText, allowWhenPaused: true);
            _clipboardMonitor.Start();

            _hotKeyService.HotKeyPressed += (_, _) => ShowMainWindow();
            _hotKeyService.QuickSwitchHotKeyPressed += (_, rank) => CopyRecentEntry(rank);
            _hotKeyService.PopupLockHotKeyPressed += (_, _) => TogglePopupLockFromHotKey();
            if (!_hotKeyService.RegisterAll(
                    _settingsManager.Current.OpenHistoryHotKey,
                    _settingsManager.Current.QuickSwitchHotKeys,
                    _settingsManager.Current.PopupLockHotKey,
                    out var hotKeyError) &&
                !string.IsNullOrWhiteSpace(hotKeyError))
            {
                AppLogger.Error(hotKeyError);
                MessageBox.Show(hotKeyError, "快捷键注册失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _singleInstanceSignalWait = ThreadPool.RegisterWaitForSingleObject(
                showWindowEvent,
                (_, _) => ShowMainWindow(),
                null,
                Timeout.Infinite,
                executeOnlyOnce: false);

            if (!_settingsManager.Current.StartInTray)
            {
                ShowMainWindow();
            }
        }

        private void ShowMainWindow()
        {
            if (_mainForm.IsDisposed)
            {
                return;
            }

            if (_mainForm.InvokeRequired)
            {
                _mainForm.BeginInvoke(ShowMainWindow);
                return;
            }

            _mainForm.ShowAndFocusSearch();
        }

        private void ShowSettings()
        {
            ShowMainWindow();
            _mainForm.OpenSettings();
            ApplyPopupSettings();
        }

        private void TogglePopupLockFromHotKey()
        {
            if (_mainForm.InvokeRequired)
            {
                _mainForm.BeginInvoke(TogglePopupLockFromHotKey);
                return;
            }

            _settingsManager.Current.ClipboardPopupUnlocked = !_settingsManager.Current.ClipboardPopupUnlocked;
            ApplyPopupSettings();
            _mainForm.RefreshPopupControls();
            ShowClipboardPopup(
                _settingsManager.Current.ClipboardPopupUnlocked
                    ? "锁：开启，可以移动悬浮窗口"
                    : "锁：关闭，悬浮窗口已固定",
                allowWhenPaused: true);
        }

        private void CopyRecentEntry(int rank)
        {
            if (_mainForm.InvokeRequired)
            {
                _mainForm.BeginInvoke(() => CopyRecentEntry(rank));
                return;
            }

            var entries = _historyService.GetRecent(10);
            var startIndex = CalculateQuickSwitchStartIndex(entries);
            var targetIndex = startIndex + rank - 1;
            if (rank < 1 || rank > 9 || startIndex < 0 || targetIndex >= entries.Count)
            {
                ShowClipboardPopup("没有更早的记录", allowWhenPaused: true);
                return;
            }

            var entry = entries[targetIndex];
            try
            {
                _historyService.MarkClipboardWriteFromHistory(entry.Content);
                Clipboard.SetText(entry.Content, TextDataFormat.UnicodeText);
                ShowClipboardPopup(entry.Content, allowWhenPaused: true);
            }
            catch (ExternalException ex)
            {
                AppLogger.Error("快速切换写入剪贴板失败。", ex);
                ShowClipboardPopup("剪贴板暂时被其他程序占用，请稍后再试。", allowWhenPaused: true);
            }
        }

        private static int CalculateQuickSwitchStartIndex(IReadOnlyList<ClipboardEntry> entries)
        {
            if (entries.Count == 0)
            {
                return -1;
            }

            var currentText = TryGetCurrentClipboardText();
            return currentText is not null &&
                   string.Equals(currentText, entries[0].Content, StringComparison.Ordinal)
                ? 1
                : 0;
        }

        private static string? TryGetCurrentClipboardText()
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    return Clipboard.ContainsText(TextDataFormat.UnicodeText)
                        ? Clipboard.GetText(TextDataFormat.UnicodeText)
                        : null;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(60 + attempt * 60);
                }
            }

            return null;
        }

        private void ShowClipboardPopup(string content, bool allowWhenPaused = false)
        {
            if (!_settingsManager.Current.ShowClipboardPopup ||
                (!allowWhenPaused && _historyService.IsRecordingPaused))
            {
                return;
            }

            if (_mainForm.InvokeRequired)
            {
                _mainForm.BeginInvoke(() => ShowClipboardPopup(content, allowWhenPaused));
                return;
            }

            _clipboardPopupForm.UpdateContent(content);
        }

        private void SaveSettings()
        {
            try
            {
                _settingsManager.Current.Normalize();
                _settingsManager.Save();
            }
            catch (Exception ex)
            {
                AppLogger.Error("设置保存失败。", ex);
            }
        }

        private void ApplyPopupSettings()
        {
            SaveSettings();
            if (_mainForm.InvokeRequired)
            {
                _mainForm.BeginInvoke(() =>
                {
                    _clipboardPopupForm.ApplySettings(_settingsManager.Current, SaveSettings);
                    SaveSettings();
                    _mainForm.RefreshPopupControls();
                });
                return;
            }

            _clipboardPopupForm.ApplySettings(_settingsManager.Current, SaveSettings);
            SaveSettings();
            _mainForm.RefreshPopupControls();
        }

        private void RequestExit()
        {
            if (_mainForm.InvokeRequired)
            {
                _mainForm.BeginInvoke(RequestExit);
                return;
            }

            _mainForm.AllowExit = true;
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }

            if (disposing)
            {
                _singleInstanceSignalWait.Unregister(null);
                AppLogger.Info("程序退出。");
                _trayIconManager.Dispose();
                _clipboardPopupForm.Dispose();
                _hotKeyService.Dispose();
                _clipboardMonitor.Dispose();
                _mainForm.Dispose();
                _historyService.Dispose();
                _database.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
