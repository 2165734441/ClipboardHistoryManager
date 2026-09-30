using ClipboardHistoryManager.Services;

namespace ClipboardHistoryManager.UI;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon = new();
    private readonly ClipboardHistoryService _historyService;
    private readonly SettingsManager _settingsManager;
    private readonly Action _showMainWindow;
    private readonly Action _showSettings;
    private readonly Action _applyPopupSettings;
    private readonly Action _exitApplication;
    private readonly ToolStripMenuItem _openItem = new("打开剪贴板历史");
    private readonly ToolStripMenuItem _pauseItem = new("暂停记录");
    private readonly ToolStripMenuItem _showPopupItem = new("悬浮显示");
    private readonly ToolStripMenuItem _popupTopMostItem = new("悬浮窗口置顶");
    private readonly ToolStripMenuItem _settingsItem = new("设置");
    private readonly ToolStripMenuItem _exitItem = new("退出程序");
    private bool _disposed;

    public TrayIconManager(
        ClipboardHistoryService historyService,
        SettingsManager settingsManager,
        Action showMainWindow,
        Action showSettings,
        Action applyPopupSettings,
        Action exitApplication)
    {
        _historyService = historyService;
        _settingsManager = settingsManager;
        _showMainWindow = showMainWindow;
        _showSettings = showSettings;
        _applyPopupSettings = applyPopupSettings;
        _exitApplication = exitApplication;
    }

    public void Start()
    {
        _notifyIcon.Icon = IconLoader.LoadApplicationIcon();
        _notifyIcon.Text = "剪贴板历史管理器";
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += (_, _) => _showMainWindow();

        _openItem.Click += (_, _) => _showMainWindow();
        _pauseItem.Click += (_, _) => ToggleRecording();
        _showPopupItem.Click += (_, _) => TogglePopup();
        _popupTopMostItem.Click += (_, _) => TogglePopupTopMost();
        _settingsItem.Click += (_, _) => _showSettings();
        _exitItem.Click += (_, _) => _exitApplication();

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => UpdateMenuState();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _openItem,
            new ToolStripSeparator(),
            _pauseItem,
            _showPopupItem,
            _popupTopMostItem,
            _settingsItem,
            new ToolStripSeparator(),
            _exitItem
        });
        _notifyIcon.ContextMenuStrip = menu;

        _historyService.RecordingStateChanged += HandleRecordingStateChanged;
        UpdateMenuState();
    }

    private void ToggleRecording()
    {
        _historyService.SetRecordingPaused(!_historyService.IsRecordingPaused);
        UpdateMenuState();
    }

    private void TogglePopup()
    {
        _settingsManager.Current.ShowClipboardPopup = !_settingsManager.Current.ShowClipboardPopup;
        _applyPopupSettings();
        UpdateMenuState();
    }

    private void TogglePopupTopMost()
    {
        _settingsManager.Current.ClipboardPopupTopMost = !_settingsManager.Current.ClipboardPopupTopMost;
        _applyPopupSettings();
        UpdateMenuState();
    }

    private void HandleRecordingStateChanged(object? sender, EventArgs e)
    {
        UpdateMenuState();
    }

    private void UpdateMenuState()
    {
        var paused = _historyService.IsRecordingPaused;
        _pauseItem.Text = paused ? "继续记录" : "暂停记录";
        _showPopupItem.Checked = _settingsManager.Current.ShowClipboardPopup;
        _popupTopMostItem.Checked = _settingsManager.Current.ClipboardPopupTopMost;
        _popupTopMostItem.Enabled = _settingsManager.Current.ShowClipboardPopup;
        _notifyIcon.Text = paused
            ? "剪贴板历史管理器 - 已暂停记录"
            : "剪贴板历史管理器 - 正在记录";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _historyService.RecordingStateChanged -= HandleRecordingStateChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
