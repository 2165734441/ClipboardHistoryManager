using System.Runtime.InteropServices;
using ClipboardHistoryManager.Infrastructure;
using ClipboardHistoryManager.Models;
using ClipboardHistoryManager.Services;

namespace ClipboardHistoryManager.UI;

public sealed class MainForm : Form
{
    private readonly ClipboardHistoryService _historyService;
    private readonly SettingsManager _settingsManager;
    private readonly HotKeyService _hotKeyService;
    private readonly StartupManager _startupManager;
    private readonly Action _popupSettingsChanged;
    private readonly TextBox _searchBox = new();
    private readonly Button _allButton = new();
    private readonly Button _favoriteOnlyButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _settingsButton = new();
    private readonly Button _recordingButton = new();
    private readonly Button _popupButton = new();
    private readonly Button _topMostButton = new();
    private readonly Button _lockButton = new();
    private readonly DataGridView _historyGrid = new();
    private readonly Label _emptyLabel = new();
    private readonly Label _statusLabel = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new();
    private readonly List<ClipboardEntry> _currentEntries = new();
    private bool _favoritesOnly;

    private static readonly Color WindowBackground = Color.FromArgb(246, 247, 250);
    private static readonly Color PanelBackground = Color.White;
    private static readonly Color BorderColor = Color.FromArgb(222, 226, 232);
    private static readonly Color PrimaryText = Color.FromArgb(30, 35, 45);
    private static readonly Color SecondaryText = Color.FromArgb(92, 100, 112);
    private static readonly Color Accent = Color.FromArgb(0, 103, 192);

    public MainForm(
        ClipboardHistoryService historyService,
        SettingsManager settingsManager,
        HotKeyService hotKeyService,
        StartupManager startupManager,
        Action popupSettingsChanged)
    {
        _historyService = historyService;
        _settingsManager = settingsManager;
        _hotKeyService = hotKeyService;
        _startupManager = startupManager;
        _popupSettingsChanged = popupSettingsChanged;

        _historyService.EntryAdded += HandleEntryAdded;
        _historyService.HistoryChanged += HandleHistoryChanged;
        _historyService.RecordingStateChanged += HandleRecordingStateChanged;

        InitializeUi();
        ReloadHistory();
        UpdateRecordingState();
        RefreshPopupControls();
    }

    public bool AllowExit { get; set; }

    public event EventHandler<string>? ClipboardContentCopied;

    public void ShowAndFocusSearch()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        Activate();
        BringToFront();
        _searchBox.Focus();
        _searchBox.SelectAll();
        NativeMethods.SetForegroundWindow(Handle);
    }

    public void OpenSettings()
    {
        using var form = new SettingsForm(
            _settingsManager,
            _hotKeyService,
            _startupManager,
            _historyService,
            _popupSettingsChanged);
        form.ShowDialog(this);
        UpdateRecordingState();
        RefreshPopupControls();
    }

    public void RefreshPopupControls()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(RefreshPopupControls);
            return;
        }

        _popupButton.Text = _settingsManager.Current.ShowClipboardPopup ? "悬浮显示：开" : "悬浮显示：关";
        _popupButton.ForeColor = _settingsManager.Current.ShowClipboardPopup ? Accent : SecondaryText;
        _topMostButton.Text = _settingsManager.Current.ClipboardPopupTopMost ? "置顶：开" : "置顶：关";
        _topMostButton.ForeColor = _settingsManager.Current.ClipboardPopupTopMost ? Accent : SecondaryText;
        _topMostButton.Enabled = _settingsManager.Current.ShowClipboardPopup;
        _lockButton.Text = _settingsManager.Current.ClipboardPopupUnlocked ? "锁：开启" : "锁：关闭";
        _lockButton.ForeColor = _settingsManager.Current.ClipboardPopupUnlocked ? Accent : SecondaryText;
        _lockButton.Enabled = _settingsManager.Current.ShowClipboardPopup;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _historyService.EntryAdded -= HandleEntryAdded;
        _historyService.HistoryChanged -= HandleHistoryChanged;
        _historyService.RecordingStateChanged -= HandleRecordingStateChanged;
        base.OnFormClosed(e);
    }

    private void InitializeUi()
    {
        Text = "剪贴板历史管理器";
        Icon = IconLoader.LoadApplicationIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 540);
        Size = new Size(1000, 680);
        BackColor = WindowBackground;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
            BackColor = WindowBackground
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        root.Controls.Add(BuildTitleRow(), 0, 0);
        root.Controls.Add(BuildToolbar(), 0, 1);
        root.Controls.Add(BuildGridPanel(), 0, 2);
        root.Controls.Add(BuildBottomRow(), 0, 3);
        Controls.Add(root);

        _searchTimer.Interval = 180;
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ReloadHistory();
        };
    }

    private Control BuildTitleRow()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = WindowBackground
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "剪贴板历史",
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            ForeColor = PrimaryText,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _recordingButton.Dock = DockStyle.Fill;
        _recordingButton.Margin = new Padding(0, 4, 8, 4);
        _recordingButton.Click += (_, _) => _historyService.SetRecordingPaused(!_historyService.IsRecordingPaused);
        StyleLightButton(_recordingButton);

        _settingsButton.Text = "设置";
        _settingsButton.Dock = DockStyle.Fill;
        _settingsButton.Margin = new Padding(0, 4, 0, 4);
        StyleLightButton(_settingsButton);
        _settingsButton.Click += (_, _) => OpenSettings();

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(_recordingButton, 1, 0);
        panel.Controls.Add(_settingsButton, 2, 0);
        return panel;
    }

    private Control BuildToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            Padding = new Padding(0, 4, 0, 8),
            BackColor = WindowBackground
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.PlaceholderText = "搜索历史内容";
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.Margin = new Padding(0, 0, 12, 0);
        _searchBox.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        };

        _allButton.Text = "全部";
        _favoriteOnlyButton.Text = "收藏";
        _clearButton.Text = "清空历史";
        StyleSegmentButton(_allButton, true);
        StyleSegmentButton(_favoriteOnlyButton, false);
        StyleDangerButton(_clearButton);

        _allButton.Click += (_, _) => SetFavoriteFilter(false);
        _favoriteOnlyButton.Click += (_, _) => SetFavoriteFilter(true);
        _clearButton.Click += (_, _) => ClearHistory();

        toolbar.Controls.Add(_searchBox, 0, 0);
        toolbar.Controls.Add(_allButton, 1, 0);
        toolbar.Controls.Add(_favoriteOnlyButton, 2, 0);
        toolbar.Controls.Add(_clearButton, 3, 0);
        return toolbar;
    }

    private Control BuildBottomRow()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            BackColor = WindowBackground,
            Padding = new Padding(0, 8, 0, 0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = SecondaryText;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        StyleLightButton(_popupButton);
        _popupButton.Dock = DockStyle.Fill;
        _popupButton.Margin = new Padding(0, 0, 8, 0);
        _popupButton.Click += (_, _) =>
        {
            _settingsManager.Current.ShowClipboardPopup = !_settingsManager.Current.ShowClipboardPopup;
            _popupSettingsChanged();
            RefreshPopupControls();
        };

        StyleLightButton(_topMostButton);
        _topMostButton.Dock = DockStyle.Fill;
        _topMostButton.Margin = new Padding(0, 0, 8, 0);
        _topMostButton.Click += (_, _) =>
        {
            _settingsManager.Current.ClipboardPopupTopMost = !_settingsManager.Current.ClipboardPopupTopMost;
            _popupSettingsChanged();
            RefreshPopupControls();
        };

        StyleLightButton(_lockButton);
        _lockButton.Dock = DockStyle.Fill;
        _lockButton.Click += (_, _) => TogglePopupLock();

        panel.Controls.Add(_statusLabel, 0, 0);
        panel.Controls.Add(_popupButton, 1, 0);
        panel.Controls.Add(_topMostButton, 2, 0);
        panel.Controls.Add(_lockButton, 3, 0);
        return panel;
    }

    private Control BuildGridPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelBackground,
            Padding = new Padding(1)
        };

        _historyGrid.Dock = DockStyle.Fill;
        _historyGrid.BackgroundColor = PanelBackground;
        _historyGrid.BorderStyle = BorderStyle.FixedSingle;
        _historyGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _historyGrid.GridColor = Color.FromArgb(236, 239, 244);
        _historyGrid.AllowUserToAddRows = false;
        _historyGrid.AllowUserToDeleteRows = false;
        _historyGrid.AllowUserToResizeRows = false;
        _historyGrid.MultiSelect = false;
        _historyGrid.ReadOnly = true;
        _historyGrid.RowHeadersVisible = false;
        _historyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _historyGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _historyGrid.RowTemplate.Height = 58;
        _historyGrid.EnableHeadersVisualStyles = false;
        _historyGrid.ColumnHeadersHeight = 38;
        _historyGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 243, 247);
        _historyGrid.ColumnHeadersDefaultCellStyle.ForeColor = SecondaryText;
        _historyGrid.ColumnHeadersDefaultCellStyle.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
        _historyGrid.DefaultCellStyle.BackColor = PanelBackground;
        _historyGrid.DefaultCellStyle.ForeColor = PrimaryText;
        _historyGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(229, 241, 255);
        _historyGrid.DefaultCellStyle.SelectionForeColor = PrimaryText;
        _historyGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
        _historyGrid.CellClick += HandleGridCellClick;
        _historyGrid.CellDoubleClick += HandleGridCellDoubleClick;

        _historyGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Favorite", HeaderText = "", Width = 54, FlatStyle = FlatStyle.Flat });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Preview", HeaderText = "内容", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CopiedAt", HeaderText = "复制时间", Width = 154 });
        _historyGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Details", HeaderText = "", Width = 82, Text = "详情", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat });
        _historyGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Width = 72, Text = "删除", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat });

        _emptyLabel.Dock = DockStyle.Fill;
        _emptyLabel.BackColor = PanelBackground;
        _emptyLabel.ForeColor = SecondaryText;
        _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyLabel.Font = new Font(Font.FontFamily, 11F, FontStyle.Regular);
        _emptyLabel.Visible = false;

        panel.Controls.Add(_historyGrid);
        panel.Controls.Add(_emptyLabel);
        return panel;
    }

    private void TogglePopupLock()
    {
        _settingsManager.Current.ClipboardPopupUnlocked = !_settingsManager.Current.ClipboardPopupUnlocked;
        _popupSettingsChanged();
        RefreshPopupControls();
        SetStatus(_settingsManager.Current.ClipboardPopupUnlocked
            ? "锁已开启，悬浮窗口可以拖动。"
            : "锁已关闭，悬浮窗口固定在当前位置。");
    }

    private void HandleHistoryChanged(object? sender, EventArgs e) => SafeBeginInvoke(ReloadHistory);

    private void HandleEntryAdded(object? sender, ClipboardEntry entry)
    {
        SafeBeginInvoke(() => SetStatus($"已保存新内容：{entry.CopiedAt:yyyy-MM-dd HH:mm:ss}"));
    }

    private void HandleRecordingStateChanged(object? sender, EventArgs e)
    {
        SafeBeginInvoke(UpdateRecordingState);
    }

    private void ReloadHistory()
    {
        var entries = _historyService.Search(_searchBox.Text, _favoritesOnly);

        _historyGrid.SuspendLayout();
        try
        {
            _historyGrid.Rows.Clear();
            _currentEntries.Clear();
            _currentEntries.AddRange(entries);

            foreach (var entry in _currentEntries)
            {
                var rowIndex = _historyGrid.Rows.Add(
                    entry.IsFavorite ? "★" : "☆",
                    CreatePreview(entry.Content),
                    entry.CopiedAt.ToString("yyyy-MM-dd HH:mm"),
                    "详情",
                    "删除");

                var row = _historyGrid.Rows[rowIndex];
                row.Tag = entry;
                row.Cells["Preview"].ToolTipText = "单击复制完整内容，双击查看详情";
                row.Cells["Favorite"].ToolTipText = entry.IsFavorite ? "取消收藏" : "收藏";
                row.Cells["Delete"].ToolTipText = "删除这条历史";
            }
        }
        finally
        {
            _historyGrid.ResumeLayout();
        }

        UpdateEmptyState(entries.Count);
        UpdateFilterButtons();
        SetStatus(CreateStatusText(entries.Count));
    }

    private void HandleGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || GetEntryAt(e.RowIndex) is not { } entry)
        {
            return;
        }

        var columnName = _historyGrid.Columns[e.ColumnIndex].Name;
        if (columnName == "Favorite")
        {
            ToggleFavorite(entry);
        }
        else if (columnName == "Delete")
        {
            DeleteEntry(entry);
        }
        else if (columnName == "Details")
        {
            ShowDetails(entry);
        }
        else
        {
            CopyEntry(entry);
        }
    }

    private void HandleGridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && GetEntryAt(e.RowIndex) is { } entry)
        {
            ShowDetails(entry);
        }
    }

    private ClipboardEntry? GetEntryAt(int rowIndex)
    {
        return rowIndex >= 0 && rowIndex < _historyGrid.Rows.Count
            ? _historyGrid.Rows[rowIndex].Tag as ClipboardEntry
            : null;
    }

    private void CopyEntry(ClipboardEntry entry)
    {
        try
        {
            _historyService.MarkClipboardWriteFromHistory(entry.Content);
            Clipboard.SetText(entry.Content, TextDataFormat.UnicodeText);
            SetStatus("已复制");
            ClipboardContentCopied?.Invoke(this, entry.Content);
            if (_settingsManager.Current.AutoHideAfterCopy)
            {
                Hide();
            }
        }
        catch (ExternalException)
        {
            SetStatus("剪贴板暂时被其他程序占用，请稍后再试。");
        }
    }

    private void ToggleFavorite(ClipboardEntry entry)
    {
        _historyService.SetFavorite(entry.Id, !entry.IsFavorite);
        SetStatus(entry.IsFavorite ? "已取消收藏" : "已收藏");
    }

    private void DeleteEntry(ClipboardEntry entry)
    {
        _historyService.Delete(entry.Id);
        SetStatus("已删除");
    }

    private void ClearHistory()
    {
        var result = MessageBox.Show(
            this,
            "确定要清空普通历史记录吗？\r\n\r\n收藏内容会保留。这个操作不可恢复。",
            "清空历史",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.OK)
        {
            return;
        }

        _historyService.Clear(includeFavorites: false);
        SetStatus("已清空普通历史，收藏内容已保留。");
    }

    private void ShowDetails(ClipboardEntry entry)
    {
        using var detailForm = new DetailForm(entry, CopyEntry);
        detailForm.ShowDialog(this);
    }

    private void SetFavoriteFilter(bool favoritesOnly)
    {
        if (_favoritesOnly == favoritesOnly)
        {
            return;
        }

        _favoritesOnly = favoritesOnly;
        ReloadHistory();
    }

    private void UpdateRecordingState()
    {
        var paused = _historyService.IsRecordingPaused;
        _recordingButton.Text = paused ? "已暂停记录" : "正在记录";
        _recordingButton.ForeColor = paused ? Color.FromArgb(168, 42, 42) : Accent;
        SetStatus(paused ? "已暂停记录。历史仍可查看、搜索和复制。" : "正在记录剪贴板文字。");
    }

    private void UpdateEmptyState(int count)
    {
        var hasItems = count > 0;
        _historyGrid.Visible = hasItems;
        _emptyLabel.Visible = !hasItems;

        if (hasItems)
        {
            return;
        }

        if (_favoritesOnly)
        {
            _emptyLabel.Text = string.IsNullOrWhiteSpace(_searchBox.Text)
                ? "暂无收藏内容"
                : "收藏中没有匹配内容";
        }
        else
        {
            _emptyLabel.Text = string.IsNullOrWhiteSpace(_searchBox.Text)
                ? "暂无剪贴板历史，复制一些文字后会显示在这里"
                : "没有匹配的历史记录";
        }
    }

    private string CreateStatusText(int count)
    {
        var recording = _historyService.IsRecordingPaused ? "已暂停记录" : "正在记录";
        var scope = _favoritesOnly ? "收藏" : "历史";
        var suffix = string.IsNullOrWhiteSpace(_searchBox.Text) ? "" : "，已应用搜索";
        return $"{recording}。{scope}记录 {count} 条{suffix}。数据位置：{AppPaths.DatabasePath}";
    }

    private static string CreatePreview(string content)
    {
        var preview = content
            .Replace("\r\n", " / ", StringComparison.Ordinal)
            .Replace("\r", " / ", StringComparison.Ordinal)
            .Replace("\n", " / ", StringComparison.Ordinal)
            .Trim();

        return preview.Length > 180 ? preview[..180] + "..." : preview;
    }

    private void SafeBeginInvoke(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }

    private void SetStatus(string message) => _statusLabel.Text = message;

    private void UpdateFilterButtons()
    {
        StyleSegmentButton(_allButton, !_favoritesOnly);
        StyleSegmentButton(_favoriteOnlyButton, _favoritesOnly);
    }

    private static void StyleSegmentButton(Button button, bool selected)
    {
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(0, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = selected ? Accent : BorderColor;
        button.BackColor = selected ? Color.FromArgb(225, 240, 255) : PanelBackground;
        button.ForeColor = selected ? Accent : PrimaryText;
        button.Cursor = Cursors.Hand;
    }

    private static void StyleLightButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = BorderColor;
        button.BackColor = PanelBackground;
        button.ForeColor = PrimaryText;
        button.Cursor = Cursors.Hand;
    }

    private static void StyleDangerButton(Button button)
    {
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(8, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(230, 188, 188);
        button.BackColor = Color.FromArgb(255, 245, 245);
        button.ForeColor = Color.FromArgb(168, 42, 42);
        button.Cursor = Cursors.Hand;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
