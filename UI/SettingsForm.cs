using ClipboardHistoryManager.Infrastructure;
using ClipboardHistoryManager.Models;
using ClipboardHistoryManager.Services;

namespace ClipboardHistoryManager.UI;

public sealed class SettingsForm : Form
{
    private readonly SettingsManager _settingsManager;
    private readonly HotKeyService _hotKeyService;
    private readonly StartupManager _startupManager;
    private readonly ClipboardHistoryService _historyService;
    private readonly Action _popupSettingsChanged;
    private readonly TextBox _openHotKeyBox = new();
    private readonly TextBox _popupLockHotKeyBox = new();
    private readonly TextBox[] _quickSwitchHotKeyBoxes = new TextBox[9];
    private readonly NumericUpDown _maxHistoryBox = new();
    private readonly NumericUpDown _popupWidthBox = new();
    private readonly NumericUpDown _popupHeightBox = new();
    private readonly NumericUpDown _popupFontSizeBox = new();
    private readonly CheckBox _startWithWindowsCheckBox = new();
    private readonly CheckBox _startInTrayCheckBox = new();
    private readonly CheckBox _autoHideAfterCopyCheckBox = new();
    private readonly CheckBox _showClipboardPopupCheckBox = new();
    private readonly CheckBox _clipboardPopupTopMostCheckBox = new();
    private readonly CheckBox _clipboardPopupUnlockedCheckBox = new();
    private readonly ComboBox _popupColorBox = new();
    private readonly Button _customColorButton = new();
    private readonly Label _messageLabel = new();
    private bool _loading;

    private static readonly (string Name, string Value)[] PresetColors =
    [
        ("柔和白", "#F4F1E8"),
        ("浅灰", "#D8DDE3"),
        ("暖白", "#F1E4C8"),
        ("低饱和浅蓝", "#AFC8D8"),
        ("低饱和浅绿", "#B8D3C2"),
        ("柔和米黄色", "#E6D7A8")
    ];

    public SettingsForm(
        SettingsManager settingsManager,
        HotKeyService hotKeyService,
        StartupManager startupManager,
        ClipboardHistoryService historyService,
        Action popupSettingsChanged)
    {
        _settingsManager = settingsManager;
        _hotKeyService = hotKeyService;
        _startupManager = startupManager;
        _historyService = historyService;
        _popupSettingsChanged = popupSettingsChanged;
        InitializeUi();
        LoadSettings();
    }

    private void InitializeUi()
    {
        Text = "设置";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ClientSize = new Size(720, 840);
        BackColor = Color.FromArgb(246, 247, 250);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            BackColor = BackColor
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = BackColor
        };

        _openHotKeyBox.Tag = 0;
        ConfigureHotKeyBox(_openHotKeyBox);
        _popupLockHotKeyBox.Tag = 10;
        ConfigureHotKeyBox(_popupLockHotKeyBox);

        root.Controls.Add(CreateSectionTitle("全局快捷键"));
        root.Controls.Add(CreateLabeledRow("打开剪贴板历史", _openHotKeyBox));
        root.Controls.Add(CreateLabeledRow("悬浮窗口锁快捷键", _popupLockHotKeyBox));

        root.Controls.Add(CreateSectionTitle("Ctrl+1～Ctrl+9 快速切换"));
        for (var i = 0; i < _quickSwitchHotKeyBoxes.Length; i++)
        {
            var box = new TextBox { Tag = i + 1 };
            ConfigureHotKeyBox(box);
            _quickSwitchHotKeyBoxes[i] = box;
            var label = i == 0 ? "历史位置1（上一个内容）" : $"历史位置{i + 1}";
            root.Controls.Add(CreateLabeledRow(label, box));
        }

        root.Controls.Add(CreateSectionTitle("运行方式"));
        ConfigureCheckBox(_startWithWindowsCheckBox, "开机自动启动");
        _startWithWindowsCheckBox.CheckedChanged += HandleStartWithWindowsChanged;
        root.Controls.Add(_startWithWindowsCheckBox);

        ConfigureCheckBox(_startInTrayCheckBox, "启动时直接后台运行");
        _startInTrayCheckBox.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settingsManager.Current.StartInTray = _startInTrayCheckBox.Checked;
            SaveSettings("启动方式已保存。");
        };
        root.Controls.Add(_startInTrayCheckBox);

        ConfigureCheckBox(_autoHideAfterCopyCheckBox, "从历史记录复制后自动隐藏主窗口");
        _autoHideAfterCopyCheckBox.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settingsManager.Current.AutoHideAfterCopy = _autoHideAfterCopyCheckBox.Checked;
            SaveSettings("复制行为已保存。");
        };
        root.Controls.Add(_autoHideAfterCopyCheckBox);

        ConfigureNumericBox(_maxHistoryBox, AppSettings.MinimumHistoryLimit, AppSettings.MaximumHistoryLimit, 100);
        _maxHistoryBox.ValueChanged += HandleMaxHistoryChanged;
        root.Controls.Add(CreateLabeledRow("最大历史数量", _maxHistoryBox));

        root.Controls.Add(CreateSectionTitle("悬浮显示"));
        ConfigureCheckBox(_showClipboardPopupCheckBox, "悬浮显示 开/关");
        _showClipboardPopupCheckBox.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settingsManager.Current.ShowClipboardPopup = _showClipboardPopupCheckBox.Checked;
            UpdatePopupControlsEnabled();
            SavePopupSettings("悬浮显示设置已保存。");
        };
        root.Controls.Add(_showClipboardPopupCheckBox);

        ConfigureCheckBox(_clipboardPopupTopMostCheckBox, "悬浮窗口置顶");
        _clipboardPopupTopMostCheckBox.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settingsManager.Current.ClipboardPopupTopMost = _clipboardPopupTopMostCheckBox.Checked;
            SavePopupSettings("置顶设置已保存。");
        };
        root.Controls.Add(_clipboardPopupTopMostCheckBox);

        ConfigureCheckBox(_clipboardPopupUnlockedCheckBox, "锁打开：允许移动和调整悬浮窗口");
        _clipboardPopupUnlockedCheckBox.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settingsManager.Current.ClipboardPopupUnlocked = _clipboardPopupUnlockedCheckBox.Checked;
            SavePopupSettings("锁状态已保存。");
        };
        root.Controls.Add(_clipboardPopupUnlockedCheckBox);

        ConfigureNumericBox(_popupWidthBox, AppSettings.MinimumPopupWidth, AppSettings.MaximumPopupWidth, 20);
        _popupWidthBox.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _settingsManager.Current.ClipboardPopupWidth = (int)_popupWidthBox.Value;
            SavePopupSettings("悬浮窗口宽度已保存。");
        };
        root.Controls.Add(CreateLabeledRow("悬浮窗口宽度（px）", _popupWidthBox));

        ConfigureNumericBox(_popupHeightBox, AppSettings.MinimumPopupHeight, AppSettings.MaximumPopupHeight, 10);
        _popupHeightBox.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _settingsManager.Current.ClipboardPopupHeight = (int)_popupHeightBox.Value;
            SavePopupSettings("悬浮窗口高度已保存。");
        };
        root.Controls.Add(CreateLabeledRow("悬浮窗口高度（px）", _popupHeightBox));

        ConfigureNumericBox(_popupFontSizeBox, 12, 96, 1);
        _popupFontSizeBox.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _settingsManager.Current.ClipboardPopupFontSize = (int)_popupFontSizeBox.Value;
            SavePopupSettings("悬浮文字大小已保存。");
        };
        root.Controls.Add(CreateLabeledRow("悬浮文字大小", _popupFontSizeBox));

        var colorPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _popupColorBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _popupColorBox.Width = 180;
        foreach (var preset in PresetColors)
        {
            _popupColorBox.Items.Add(preset.Name);
        }

        _popupColorBox.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _popupColorBox.SelectedIndex < 0) return;
            _settingsManager.Current.ClipboardPopupTextColor = PresetColors[_popupColorBox.SelectedIndex].Value;
            SavePopupSettings("悬浮文字颜色已保存。");
        };
        _customColorButton.Text = "选择颜色";
        _customColorButton.Width = 96;
        _customColorButton.Height = 28;
        _customColorButton.Click += (_, _) => ChooseCustomColor();
        colorPanel.Controls.Add(_popupColorBox);
        colorPanel.Controls.Add(_customColorButton);
        root.Controls.Add(CreateLabeledRow("悬浮文字颜色", colorPanel));

        _messageLabel.Dock = DockStyle.Top;
        _messageLabel.Height = 64;
        _messageLabel.ForeColor = Color.FromArgb(92, 100, 112);
        _messageLabel.TextAlign = ContentAlignment.TopLeft;
        root.Controls.Add(_messageLabel);

        var closeButton = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            Width = 88,
            Height = 32,
            Anchor = AnchorStyles.Right
        };
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = BackColor
        };
        buttonPanel.Controls.Add(closeButton);
        root.Controls.Add(buttonPanel);

        scrollPanel.Controls.Add(root);
        Controls.Add(scrollPanel);
        AcceptButton = closeButton;
    }

    private static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 34,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 35, 45)
        };
    }

    private static Control CreateLabeledRow(string labelText, Control valueControl)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            ColumnCount = 2,
            BackColor = Color.FromArgb(246, 247, 250)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(30, 35, 45)
        };

        row.Controls.Add(label, 0, 0);
        row.Controls.Add(valueControl, 1, 0);
        return row;
    }

    private void ConfigureHotKeyBox(TextBox box)
    {
        box.Dock = DockStyle.Fill;
        box.ReadOnly = true;
        box.BackColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.KeyDown += HandleHotKeyBoxKeyDown;
        box.GotFocus += (_, _) => _messageLabel.Text = "按下新的组合键，例如 Ctrl + Alt + V。";
    }

    private static void ConfigureCheckBox(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.Dock = DockStyle.Top;
        checkBox.Height = 34;
    }

    private static void ConfigureNumericBox(NumericUpDown box, int minimum, int maximum, int increment)
    {
        box.Dock = DockStyle.Left;
        box.Width = 140;
        box.Minimum = minimum;
        box.Maximum = maximum;
        box.Increment = increment;
    }

    private void LoadSettings()
    {
        _loading = true;
        try
        {
            _settingsManager.Current.Normalize();
            _openHotKeyBox.Text = _settingsManager.Current.OpenHistoryHotKey.ToString();
            _popupLockHotKeyBox.Text = _settingsManager.Current.PopupLockHotKey.ToString();
            for (var i = 0; i < _quickSwitchHotKeyBoxes.Length; i++)
            {
                _quickSwitchHotKeyBoxes[i].Text = _settingsManager.Current.QuickSwitchHotKeys[i].ToString();
            }

            _maxHistoryBox.Value = _settingsManager.Current.MaxHistoryEntries;
            _startWithWindowsCheckBox.Checked = _settingsManager.Current.StartWithWindows || _startupManager.IsEnabled();
            _startInTrayCheckBox.Checked = _settingsManager.Current.StartInTray;
            _autoHideAfterCopyCheckBox.Checked = _settingsManager.Current.AutoHideAfterCopy;
            _showClipboardPopupCheckBox.Checked = _settingsManager.Current.ShowClipboardPopup;
            _clipboardPopupTopMostCheckBox.Checked = _settingsManager.Current.ClipboardPopupTopMost;
            _clipboardPopupUnlockedCheckBox.Checked = _settingsManager.Current.ClipboardPopupUnlocked;
            _popupWidthBox.Value = _settingsManager.Current.ClipboardPopupWidth;
            _popupHeightBox.Value = _settingsManager.Current.ClipboardPopupHeight;
            _popupFontSizeBox.Value = _settingsManager.Current.ClipboardPopupFontSize;
            SelectColorPreset(_settingsManager.Current.ClipboardPopupTextColor);
            UpdatePopupControlsEnabled();
            _messageLabel.Text = _hotKeyService.LastError ?? "设置会在修改后自动保存。";
        }
        finally
        {
            _loading = false;
        }
    }

    private void HandleHotKeyBoxKeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        if (sender is not TextBox box || box.Tag is not int slot)
        {
            return;
        }

        var key = e.KeyCode;
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return;
        }

        var modifiers = HotKeyModifiers.None;
        if (e.Control) modifiers |= HotKeyModifiers.Control;
        if (e.Shift) modifiers |= HotKeyModifiers.Shift;
        if (e.Alt) modifiers |= HotKeyModifiers.Alt;

        var next = new HotKeyDefinition(modifiers, key);
        var oldOpen = _settingsManager.Current.OpenHistoryHotKey;
        var oldLock = _settingsManager.Current.PopupLockHotKey;
        var oldQuick = _settingsManager.Current.QuickSwitchHotKeys.ToList();

        var candidateOpen = oldOpen;
        var candidateLock = oldLock;
        var candidateQuick = oldQuick.ToList();
        if (slot == 0)
        {
            candidateOpen = next;
        }
        else if (slot == 10)
        {
            candidateLock = next;
        }
        else
        {
            candidateQuick[slot - 1] = next;
        }

        if (!_hotKeyService.RegisterAll(candidateOpen, candidateQuick, candidateLock, out var error))
        {
            _hotKeyService.RegisterAll(oldOpen, oldQuick, oldLock, out _);
            LoadSettings();
            _messageLabel.Text = error ?? "快捷键注册失败，可能已被其他程序占用。";
            AppLogger.Error(_messageLabel.Text);
            return;
        }

        _settingsManager.Current.OpenHistoryHotKey = candidateOpen;
        _settingsManager.Current.PopupLockHotKey = candidateLock;
        _settingsManager.Current.QuickSwitchHotKeys = candidateQuick;
        box.Text = next.ToString();
        SaveSettings("快捷键已保存。");
    }

    private void HandleMaxHistoryChanged(object? sender, EventArgs e)
    {
        if (_loading) return;

        var limit = (int)_maxHistoryBox.Value;
        _settingsManager.Current.MaxHistoryEntries = limit;
        _historyService.SetMaxHistoryEntries(limit);
        SaveSettings("最大历史数量已保存，旧的普通记录会自动清理。");
    }

    private void HandleStartWithWindowsChanged(object? sender, EventArgs e)
    {
        if (_loading) return;

        try
        {
            _startupManager.SetEnabled(_startWithWindowsCheckBox.Checked);
            _settingsManager.Current.StartWithWindows = _startWithWindowsCheckBox.Checked;
            SaveSettings("开机自动启动设置已保存。");
        }
        catch (Exception ex)
        {
            AppLogger.Error("开机自动启动注册失败。", ex);
            _loading = true;
            _startWithWindowsCheckBox.Checked = _settingsManager.Current.StartWithWindows;
            _loading = false;
            _messageLabel.Text = "开机自动启动设置失败，请检查当前用户权限。";
        }
    }

    private void ChooseCustomColor()
    {
        using var dialog = new ColorDialog
        {
            Color = ParseColor(_settingsManager.Current.ClipboardPopupTextColor)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settingsManager.Current.ClipboardPopupTextColor = ColorTranslator.ToHtml(dialog.Color);
        SelectColorPreset(_settingsManager.Current.ClipboardPopupTextColor);
        SavePopupSettings("悬浮文字颜色已保存。");
    }

    private void SelectColorPreset(string color)
    {
        var index = Array.FindIndex(PresetColors, preset =>
            string.Equals(preset.Value, color, StringComparison.OrdinalIgnoreCase));
        _popupColorBox.SelectedIndex = index;
        if (index < 0)
        {
            _popupColorBox.Text = "";
        }
    }

    private void UpdatePopupControlsEnabled()
    {
        var enabled = _showClipboardPopupCheckBox.Checked;
        _clipboardPopupTopMostCheckBox.Enabled = enabled;
        _clipboardPopupUnlockedCheckBox.Enabled = enabled;
        _popupWidthBox.Enabled = enabled;
        _popupHeightBox.Enabled = enabled;
        _popupFontSizeBox.Enabled = enabled;
        _popupColorBox.Enabled = enabled;
        _customColorButton.Enabled = enabled;
    }

    private void SavePopupSettings(string message)
    {
        SaveSettings(message);
        _popupSettingsChanged();
        SyncPopupSizeFields();
    }

    private void SyncPopupSizeFields()
    {
        _loading = true;
        try
        {
            _popupWidthBox.Value = Math.Clamp(
                _settingsManager.Current.ClipboardPopupWidth,
                (int)_popupWidthBox.Minimum,
                (int)_popupWidthBox.Maximum);
            _popupHeightBox.Value = Math.Clamp(
                _settingsManager.Current.ClipboardPopupHeight,
                (int)_popupHeightBox.Minimum,
                (int)_popupHeightBox.Maximum);
        }
        finally
        {
            _loading = false;
        }
    }

    private void SaveSettings(string message)
    {
        try
        {
            _settingsManager.Current.Normalize();
            _settingsManager.Save();
            _messageLabel.Text = message;
        }
        catch (Exception ex)
        {
            AppLogger.Error("设置保存失败。", ex);
            _messageLabel.Text = "设置保存失败，请检查本机用户数据目录权限。";
        }
    }

    private static Color ParseColor(string color)
    {
        try
        {
            return ColorTranslator.FromHtml(color);
        }
        catch
        {
            return ColorTranslator.FromHtml("#F4F1E8");
        }
    }
}
