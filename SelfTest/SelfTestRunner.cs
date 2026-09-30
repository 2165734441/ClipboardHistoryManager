using ClipboardHistoryManager.Data;
using ClipboardHistoryManager.Models;
using ClipboardHistoryManager.Services;
using ClipboardHistoryManager.UI;
using System.Reflection;

namespace ClipboardHistoryManager.SelfTest;

public static class SelfTestRunner
{
    public static int Run()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "ClipboardHistoryManagerSelfTest");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, $"test-{Guid.NewGuid():N}.db");

        try
        {
            using (var database = new ClipboardDatabase(databasePath))
            {
                database.Initialize();
                using var service = new ClipboardHistoryService(database);
                service.SaveCopiedText("");
                service.SaveCopiedText("   ");
                service.SaveCopiedText("\r\n");
                service.SaveCopiedText("你好");
                service.SaveCopiedText("你好");
                service.SaveCopiedText("第一行\r\nSecond line 123 !@#");
                service.SaveCopiedText("今天需要发货");
                service.SetRecordingPaused(true);
                service.SaveCopiedText("暂停后不应该保存");
                service.SetRecordingPaused(false);
                service.SaveCopiedText("恢复后应该保存");

                var entries = service.GetRecent();
                if (entries.Count != 4)
                {
                    throw new InvalidOperationException($"应保存 4 条记录，实际为 {entries.Count} 条。");
                }

                if (entries[0].Content != "恢复后应该保存" ||
                    entries[1].Content != "今天需要发货" ||
                    entries[2].Content != "第一行\r\nSecond line 123 !@#" ||
                    entries[3].Content != "你好")
                {
                    throw new InvalidOperationException("文字内容、中文或多行文本没有被正确保存。");
                }

                if (service.Search("发货", favoritesOnly: false).Count != 1)
                {
                    throw new InvalidOperationException("中文搜索没有返回预期记录。");
                }

                var englishSearchResults = service.Search("second LINE", favoritesOnly: false);
                if (englishSearchResults.Count != 1 || !englishSearchResults[0].Content.Contains("Second line", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("英文大小写不敏感搜索没有返回预期记录。");
                }

                service.SetFavorite(entries[3].Id, true);
                if (service.Search(null, favoritesOnly: true).Count != 1)
                {
                    throw new InvalidOperationException("收藏状态没有保存。");
                }

                service.Delete(entries[2].Id);
                if (service.Search("Second", favoritesOnly: false).Count != 0)
                {
                    throw new InvalidOperationException("删除后的记录仍可被搜索到。");
                }

                service.Clear(includeFavorites: false);
                var remainingEntries = service.GetRecent();
                if (remainingEntries.Count != 1 || !remainingEntries[0].IsFavorite || remainingEntries[0].Content != "你好")
                {
                    throw new InvalidOperationException("清空普通历史没有正确保留收藏记录。");
                }

                using var firstHotKeyService = new HotKeyService();
                using var secondHotKeyService = new HotKeyService();
                var testHotKey = new HotKeyDefinition(HotKeyModifiers.Control | HotKeyModifiers.Alt | HotKeyModifiers.Shift, Keys.F24);
                var testLockHotKey = new HotKeyDefinition(HotKeyModifiers.Control | HotKeyModifiers.Alt | HotKeyModifiers.Shift, Keys.F23);
                var testQuickHotKeys = Enumerable.Range(1, 9)
                    .Select(number => new HotKeyDefinition(
                        HotKeyModifiers.Control | HotKeyModifiers.Alt | HotKeyModifiers.Shift,
                        (Keys)((int)Keys.F1 + number - 1)))
                    .ToList();
                if (!firstHotKeyService.RegisterAll(testHotKey, testQuickHotKeys, testLockHotKey, out var firstHotKeyError))
                {
                    throw new InvalidOperationException($"快捷键注册意外失败：{firstHotKeyError}");
                }

                if (secondHotKeyService.RegisterAll(testHotKey, testQuickHotKeys, testLockHotKey, out _))
                {
                    throw new InvalidOperationException("重复快捷键注册应当失败。");
                }

                var settingsPath = Path.Combine(testDirectory, $"settings-{Guid.NewGuid():N}.json");
                var settingsManager = new SettingsManager(settingsPath);
                settingsManager.Load();
                settingsManager.Current.OpenHistoryHotKey = new HotKeyDefinition(HotKeyModifiers.Alt, Keys.V);
                settingsManager.Current.PopupLockHotKey = new HotKeyDefinition(HotKeyModifiers.Control | HotKeyModifiers.Alt, Keys.D0);
                settingsManager.Current.StartInTray = true;
                settingsManager.Current.StartWithWindows = true;
                settingsManager.Current.AutoHideAfterCopy = true;
                settingsManager.Current.ShowClipboardPopup = false;
                settingsManager.Current.ClipboardPopupTopMost = false;
                settingsManager.Current.ClipboardPopupUnlocked = true;
                settingsManager.Current.ClipboardPopupFontSize = 36;
                settingsManager.Current.ClipboardPopupTextColor = "#AFC8D8";
                settingsManager.Current.ClipboardPopupX = 123;
                settingsManager.Current.ClipboardPopupY = 234;
                settingsManager.Current.ClipboardPopupWidth = 720;
                settingsManager.Current.ClipboardPopupHeight = 140;
                settingsManager.Current.MaxHistoryEntries = 300;
                settingsManager.Current.QuickSwitchHotKeys = Enumerable.Range(1, 9)
                    .Select(number => new HotKeyDefinition(HotKeyModifiers.Control | HotKeyModifiers.Alt, (Keys)((int)Keys.D0 + number)))
                    .ToList();
                settingsManager.Save();

                var reopenedSettingsManager = new SettingsManager(settingsPath);
                reopenedSettingsManager.Load();
                if (reopenedSettingsManager.Current.OpenHistoryHotKey != new HotKeyDefinition(HotKeyModifiers.Alt, Keys.V) ||
                    reopenedSettingsManager.Current.PopupLockHotKey != new HotKeyDefinition(HotKeyModifiers.Control | HotKeyModifiers.Alt, Keys.D0) ||
                    !reopenedSettingsManager.Current.StartInTray ||
                    !reopenedSettingsManager.Current.StartWithWindows ||
                    !reopenedSettingsManager.Current.AutoHideAfterCopy ||
                    reopenedSettingsManager.Current.ShowClipboardPopup ||
                    reopenedSettingsManager.Current.ClipboardPopupTopMost ||
                    !reopenedSettingsManager.Current.ClipboardPopupUnlocked ||
                    reopenedSettingsManager.Current.ClipboardPopupFontSize != 36 ||
                    reopenedSettingsManager.Current.ClipboardPopupTextColor != "#AFC8D8" ||
                    reopenedSettingsManager.Current.ClipboardPopupWidth != 720 ||
                    reopenedSettingsManager.Current.ClipboardPopupHeight != 140 ||
                    reopenedSettingsManager.Current.QuickSwitchHotKeys[8] != new HotKeyDefinition(HotKeyModifiers.Control | HotKeyModifiers.Alt, Keys.D9) ||
                    reopenedSettingsManager.Current.MaxHistoryEntries != 300)
                {
                    throw new InvalidOperationException("设置没有正确持久化。");
                }

                var duplicateQuickSwitches = AppSettings.CreateDefaultQuickSwitchHotKeys();
                duplicateQuickSwitches[1] = duplicateQuickSwitches[0];
                using var duplicateHotKeyService = new HotKeyService();
                if (duplicateHotKeyService.RegisterAll(HotKeyDefinition.Default, duplicateQuickSwitches, AppSettings.DefaultPopupLockHotKey, out _))
                {
                    throw new InvalidOperationException("重复快速切换快捷键应当失败。");
                }

                using var duplicateLockHotKeyService = new HotKeyService();
                if (duplicateLockHotKeyService.RegisterAll(
                        HotKeyDefinition.Default,
                        AppSettings.CreateDefaultQuickSwitchHotKeys(),
                        new HotKeyDefinition(HotKeyModifiers.Control, Keys.D1),
                        out _))
                {
                    throw new InvalidOperationException("锁快捷键与 Ctrl+1 冲突时应当失败。");
                }

                service.Clear(includeFavorites: true);
                for (var i = 0; i < 130; i++)
                {
                    var saved = service.SaveCopiedText($"bulk-normal-{i:0000}");
                    if (i is 0 or 1 or 2 && saved is not null)
                    {
                        service.SetFavorite(saved.Id, true);
                    }
                }

                service.SetMaxHistoryEntries(100);
                var trimmedEntries = service.GetRecent(500);
                if (trimmedEntries.Count != 100 ||
                    trimmedEntries.Count(entry => entry.IsFavorite) != 3 ||
                    trimmedEntries.Count(entry => !entry.IsFavorite) != 97)
                {
                    throw new InvalidOperationException("历史数量清理没有正确保留收藏。");
                }
            }

            using (var reopenedDatabase = new ClipboardDatabase(databasePath))
            {
                reopenedDatabase.Initialize();
                using var reopenedService = new ClipboardHistoryService(reopenedDatabase);
                var entries = reopenedService.GetRecent();
                if (entries.Count != 100 || entries.Count(entry => entry.IsFavorite) != 3)
                {
                    throw new InvalidOperationException("历史记录和收藏状态重启后没有保留。");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(testDirectory, "last-error.txt"), ex.ToString());
            return 1;
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    public static int RunClipboardMonitorTest()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "ClipboardHistoryManagerSelfTest");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, $"clipboard-monitor-{Guid.NewGuid():N}.db");
        var firstText = $"监听测试中文 {Guid.NewGuid():N}";
        var secondText = $"多行测试\r\nLine two {Guid.NewGuid():N}";
        var popupSawText = false;
        var exitCode = 1;

        try
        {
            using var database = new ClipboardDatabase(databasePath);
            database.Initialize();
            using var service = new ClipboardHistoryService(database);
            using var monitor = new ClipboardMonitor(service);
            monitor.ClipboardContentChanged += (_, e) =>
            {
                if (e.Kind == ClipboardContentKind.Text && e.Text == secondText)
                {
                    popupSawText = true;
                }
            };
            monitor.Start();

            var step = 0;
            using var timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += (_, _) =>
            {
                try
                {
                    step++;
                    if (step == 1)
                    {
                        Clipboard.SetText(firstText, TextDataFormat.UnicodeText);
                    }
                    else if (step == 2)
                    {
                        Clipboard.SetText(firstText, TextDataFormat.UnicodeText);
                    }
                    else if (step == 3)
                    {
                        Clipboard.SetText(secondText, TextDataFormat.UnicodeText);
                    }
                    else if (step >= 6)
                    {
                        timer.Stop();
                        var entries = service.GetRecent(20);
                        var firstMatches = entries.Count(entry => entry.Content == firstText);
                        var secondMatches = entries.Count(entry => entry.Content == secondText);
                        exitCode = firstMatches == 1 && secondMatches == 1 && popupSawText ? 0 : 1;
                        Application.ExitThread();
                    }
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(testDirectory, "clipboard-last-error.txt"), ex.ToString());
                    timer.Stop();
                    exitCode = 1;
                    Application.ExitThread();
                }
            };

            timer.Start();
            Application.Run();
            return exitCode;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(testDirectory, "clipboard-last-error.txt"), ex.ToString());
            return 1;
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    public static int RunPopupDragTest()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "ClipboardHistoryManagerSelfTest");
        Directory.CreateDirectory(testDirectory);

        try
        {
            using var form = new ClipboardPopupForm();
            var settings = new AppSettings
            {
                ShowClipboardPopup = true,
                ClipboardPopupTopMost = false,
                ClipboardPopupUnlocked = true,
                ClipboardPopupX = 120,
                ClipboardPopupY = 120,
                ClipboardPopupWidth = 480,
                ClipboardPopupHeight = 150,
                ClipboardPopupTextColor = "#F4F1E8",
                ClipboardPopupFontSize = 28
            };
            form.ApplySettings(settings, () => { });
            form.UpdateContent("短文字测试\r\n第二行拖动测试");
            Application.DoEvents();

            var contentView = GetPopupContentView(form);
            var originalLocation = form.Location;
            SimulateDrag(contentView, new Point(24, 24), new Point(100, 74));
            Application.DoEvents();

            if (form.Location == originalLocation)
            {
                throw new InvalidOperationException("锁开启时，按住悬浮文字拖动没有移动窗口。");
            }

            var movedLocation = form.Location;
            var originalSize = form.Size;
            SimulateDrag(contentView, new Point(contentView.Width - 3, contentView.Height / 2), new Point(contentView.Width + 97, contentView.Height / 2));
            Application.DoEvents();
            if (form.Width <= originalSize.Width)
            {
                throw new InvalidOperationException("锁开启时，拖动右边缘没有增加窗口宽度。");
            }

            var widthAfterResize = form.Width;
            SimulateDrag(contentView, new Point(contentView.Width / 2, contentView.Height - 3), new Point(contentView.Width / 2, contentView.Height + 57));
            Application.DoEvents();
            if (form.Height <= originalSize.Height)
            {
                throw new InvalidOperationException("锁开启时，拖动下边缘没有增加窗口高度。");
            }

            if (settings.ClipboardPopupWidth != form.Width || settings.ClipboardPopupHeight != form.Height)
            {
                throw new InvalidOperationException("鼠标调整后的宽高没有同步保存到设置对象。");
            }

            settings.ClipboardPopupUnlocked = false;
            form.ApplySettings(settings, () => { });
            Application.DoEvents();
            var lockedLocation = form.Location;
            var lockedSize = form.Size;
            SimulateDrag(contentView, new Point(24, 24), new Point(110, 110));
            SimulateDrag(contentView, new Point(contentView.Width - 3, contentView.Height - 3), new Point(contentView.Width + 90, contentView.Height + 90));
            Application.DoEvents();

            if (form.Location != lockedLocation)
            {
                throw new InvalidOperationException("锁关闭时，悬浮窗口仍然被拖动了。");
            }

            if (form.Size != lockedSize)
            {
                throw new InvalidOperationException("锁关闭时，悬浮窗口仍然被缩放了。");
            }

            settings.ClipboardPopupUnlocked = true;
            form.ApplySettings(settings, () => { });
            form.UpdateContent("这是一段比较长的文字，用来确认文字区域本身也能拖动窗口，而不是只能拖动边缘或空白区域。");
            Application.DoEvents();
            var secondLocation = form.Location;
            SimulateDrag(contentView, new Point(30, 30), new Point(80, 85));
            Application.DoEvents();

            if (form.Location == secondLocation)
            {
                throw new InvalidOperationException("长文字状态下，按住文字拖动没有移动窗口。");
            }

            if (form.Width < widthAfterResize)
            {
                throw new InvalidOperationException("长文字显示导致窗口尺寸异常回退。");
            }

            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(testDirectory, "popup-drag-last-error.txt"), ex.ToString());
            return 1;
        }
        finally
        {
            try
            {
                Application.ExitThread();
            }
            catch
            {
                // 测试清理失败不影响结果。
            }
        }
    }

    private static Control GetPopupContentView(ClipboardPopupForm form)
    {
        var field = typeof(ClipboardPopupForm).GetField("_contentView", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("没有找到悬浮文字控件。");
        return (Control)(field.GetValue(form) ?? throw new InvalidOperationException("悬浮文字控件为空。"));
    }

    private static void SimulateDrag(Control control, Point fromClient, Point toClient)
    {
        var onMouseDown = typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("无法触发 MouseDown。");
        var onMouseMove = typeof(Control).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("无法触发 MouseMove。");
        var onMouseUp = typeof(Control).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("无法触发 MouseUp。");

        onMouseDown.Invoke(control, new object[] { new MouseEventArgs(MouseButtons.Left, 1, fromClient.X, fromClient.Y, 0) });
        onMouseMove.Invoke(control, new object[] { new MouseEventArgs(MouseButtons.Left, 0, toClient.X, toClient.Y, 0) });
        onMouseUp.Invoke(control, new object[] { new MouseEventArgs(MouseButtons.Left, 1, toClient.X, toClient.Y, 0) });
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 临时文件清理失败不影响测试结果。
        }
    }
}
