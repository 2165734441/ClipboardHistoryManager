using System.ComponentModel;
using System.Runtime.InteropServices;
using ClipboardHistoryManager.Models;

namespace ClipboardHistoryManager.Services;

public sealed class HotKeyService : NativeWindow, IDisposable
{
    private const int WmHotKey = 0x0312;
    private const int OpenHistoryHotKeyId = 1001;
    private const int PopupLockHotKeyId = 1002;
    private const int QuickSwitchHotKeyBaseId = 1100;

    private readonly Dictionary<int, HotKeyDefinition> _registeredHotKeys = new();
    private bool _disposed;

    public event EventHandler? HotKeyPressed;
    public event EventHandler? PopupLockHotKeyPressed;
    public event EventHandler<int>? QuickSwitchHotKeyPressed;

    public string? LastError { get; private set; }

    public bool RegisterAll(
        HotKeyDefinition openHistoryHotKey,
        IReadOnlyList<HotKeyDefinition> quickSwitchHotKeys,
        HotKeyDefinition popupLockHotKey,
        out string? error)
    {
        EnsureHandle();

        if (quickSwitchHotKeys.Count != 9)
        {
            error = "快速切换快捷键必须是 9 个。";
            LastError = error;
            return false;
        }

        var candidates = new Dictionary<int, HotKeyDefinition>
        {
            [OpenHistoryHotKeyId] = openHistoryHotKey,
            [PopupLockHotKeyId] = popupLockHotKey
        };

        for (var index = 1; index <= 9; index++)
        {
            candidates[QuickSwitchHotKeyBaseId + index] = quickSwitchHotKeys[index - 1];
        }

        if (!ValidateCandidates(candidates, out error))
        {
            LastError = error;
            return false;
        }

        var previous = _registeredHotKeys.ToDictionary(pair => pair.Key, pair => pair.Value);
        UnregisterAll();

        var registeredIds = new List<int>();
        foreach (var (id, hotKey) in candidates)
        {
            if (RegisterHotKey(Handle, id, (uint)hotKey.Modifiers, (uint)hotKey.Key))
            {
                registeredIds.Add(id);
                continue;
            }

            var exception = new Win32Exception(Marshal.GetLastWin32Error());
            error = $"快捷键注册失败，可能已被其他程序占用：{hotKey}。系统信息：{exception.Message}";
            LastError = error;
            UnregisterIds(registeredIds);
            RestorePrevious(previous);
            return false;
        }

        _registeredHotKeys.Clear();
        foreach (var (id, hotKey) in candidates)
        {
            _registeredHotKeys[id] = hotKey;
        }

        LastError = null;
        error = null;
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey)
        {
            var id = m.WParam.ToInt32();
            if (id == OpenHistoryHotKeyId)
            {
                HotKeyPressed?.Invoke(this, EventArgs.Empty);
            }
            else if (id == PopupLockHotKeyId)
            {
                PopupLockHotKeyPressed?.Invoke(this, EventArgs.Empty);
            }
            else if (id > QuickSwitchHotKeyBaseId && id <= QuickSwitchHotKeyBaseId + 9)
            {
                QuickSwitchHotKeyPressed?.Invoke(this, id - QuickSwitchHotKeyBaseId);
            }
        }

        base.WndProc(ref m);
    }

    private static bool ValidateCandidates(Dictionary<int, HotKeyDefinition> candidates, out string? error)
    {
        foreach (var hotKey in candidates.Values)
        {
            if (!IsValidHotKey(hotKey))
            {
                error = "快捷键需要包含 Ctrl、Alt、Shift 或 Win 中至少一个修饰键。";
                return false;
            }
        }

        var duplicate = candidates.Values
            .GroupBy(hotKey => hotKey)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            error = $"快捷键重复：{duplicate.Key}。请为每个功能设置不同快捷键。";
            return false;
        }

        error = null;
        return true;
    }

    private void RestorePrevious(Dictionary<int, HotKeyDefinition> previous)
    {
        _registeredHotKeys.Clear();
        foreach (var (id, hotKey) in previous)
        {
            if (RegisterHotKey(Handle, id, (uint)hotKey.Modifiers, (uint)hotKey.Key))
            {
                _registeredHotKeys[id] = hotKey;
            }
        }
    }

    private void UnregisterAll()
    {
        UnregisterIds(_registeredHotKeys.Keys.ToList());
        _registeredHotKeys.Clear();
    }

    private void UnregisterIds(IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            if (Handle != IntPtr.Zero)
            {
                UnregisterHotKey(Handle, id);
            }
        }
    }

    private void EnsureHandle()
    {
        if (Handle == IntPtr.Zero)
        {
            CreateHandle(new CreateParams());
        }
    }

    private static bool IsValidHotKey(HotKeyDefinition definition)
    {
        return definition.Key != Keys.None && definition.Modifiers != HotKeyModifiers.None;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnregisterAll();
        DestroyHandle();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
