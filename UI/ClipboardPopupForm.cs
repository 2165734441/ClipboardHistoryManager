using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ClipboardHistoryManager.Models;

namespace ClipboardHistoryManager.UI;

public sealed class ClipboardPopupForm : Form
{
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WmNcHitTest = 0x0084;
    private static readonly IntPtr HtTransparent = new(-1);
    private static readonly IntPtr HtCaption = new(2);
    private static readonly IntPtr HtRight = new(11);
    private static readonly IntPtr HtBottom = new(15);
    private static readonly IntPtr HtBottomRight = new(17);
    private const int ResizeGripSize = 12;

    private readonly Color _transparentColor = Color.Fuchsia;
    private readonly OutlinedTextControl _contentView = new();
    private AppSettings? _settings;
    private Action? _saveSettings;
    private string _rawContent = "";
    private bool _applyingSettings;
    private bool _isDragging;
    private bool _isResizing;
    private ResizeMode _resizeMode;
    private Point _dragStartCursor;
    private Point _dragStartLocation;
    private Size _resizeStartSize;

    public ClipboardPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(AppSettings.MinimumPopupWidth, AppSettings.MinimumPopupHeight);
        BackColor = _transparentColor;
        TransparencyKey = _transparentColor;

        _contentView.Dock = DockStyle.Fill;
        _contentView.TransparentColor = _transparentColor;
        _contentView.BackColor = _transparentColor;
        _contentView.Padding = new Padding(18, 16, 18, 16);
        _contentView.MouseDown += BeginDrag;
        _contentView.MouseMove += ContinueDrag;
        _contentView.MouseUp += EndDrag;
        _contentView.MouseLeave += (_, _) =>
        {
            if (MouseButtons != MouseButtons.Left)
            {
                EndDrag();
            }
        };
        Controls.Add(_contentView);

        MouseDown += BeginDrag;
        MouseMove += ContinueDrag;
        MouseUp += EndDrag;
        Resize += (_, _) => UpdateContentLayout();
        Move += (_, _) => SaveBounds();
        SizeChanged += (_, _) => SaveBounds();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= WsExToolWindow | WsExNoActivate;
            return createParams;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNcHitTest)
        {
            if (_settings?.ClipboardPopupUnlocked != true)
            {
                m.Result = HtTransparent;
                return;
            }

            var point = PointToClient(GetPointFromLParam(m.LParam));
            var nearRight = point.X >= ClientSize.Width - ResizeGripSize;
            var nearBottom = point.Y >= ClientSize.Height - ResizeGripSize;
            m.Result = nearRight && nearBottom
                ? HtBottomRight
                : nearRight
                    ? HtRight
                    : nearBottom
                        ? HtBottom
                        : HtCaption;
            return;
        }

        base.WndProc(ref m);
    }

    public void ApplySettings(AppSettings settings, Action saveSettings)
    {
        _settings = settings;
        _saveSettings = saveSettings;
        _applyingSettings = true;
        try
        {
            TopMost = settings.ClipboardPopupTopMost;
            Size = ClampSizeToVisibleScreen(new Size(settings.ClipboardPopupWidth, settings.ClipboardPopupHeight), settings);
            settings.ClipboardPopupWidth = Width;
            settings.ClipboardPopupHeight = Height;
            Location = ResolveLocation(settings);
            UpdateContentLayout();
        }
        finally
        {
            _applyingSettings = false;
        }

        if (settings.ShowClipboardPopup)
        {
            ShowWithoutFocus();
        }
        else
        {
            Hide();
        }
    }

    public void UpdateContent(string content)
    {
        if (IsDisposed || _settings?.ShowClipboardPopup != true)
        {
            return;
        }

        _rawContent = content;
        TopMost = _settings.ClipboardPopupTopMost;
        UpdateContentLayout();
        ShowWithoutFocus();
    }

    private void ShowWithoutFocus()
    {
        Show();
        NativeMethods.ShowWindow(Handle, NativeMethods.SwShownoactivate);
    }

    private void BeginDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _settings?.ClipboardPopupUnlocked != true)
        {
            return;
        }

        _dragStartCursor = GetMouseScreenLocation(sender, e);
        _resizeMode = HitTestResizeMode(sender, e);
        if (_resizeMode != ResizeMode.None)
        {
            _isResizing = true;
            _resizeStartSize = Size;
            SetMouseCapture(sender);
            return;
        }

        _isDragging = true;
        _dragStartLocation = Location;
        SetMouseCapture(sender);
    }

    private void ContinueDrag(object? sender, MouseEventArgs e)
    {
        if (_settings?.ClipboardPopupUnlocked != true)
        {
            return;
        }

        var currentCursor = GetMouseScreenLocation(sender, e);
        if (_isResizing)
        {
            var resizeDelta = new Size(currentCursor.X - _dragStartCursor.X, currentCursor.Y - _dragStartCursor.Y);
            ResizeFromDrag(resizeDelta);
            return;
        }

        if (!_isDragging)
        {
            UpdateResizeCursor(sender, e);
            return;
        }

        var moveDelta = new Size(currentCursor.X - _dragStartCursor.X, currentCursor.Y - _dragStartCursor.Y);
        Location = _dragStartLocation + moveDelta;
    }

    private void EndDrag(object? sender, MouseEventArgs e)
    {
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_isDragging && !_isResizing)
        {
            return;
        }

        _isDragging = false;
        _isResizing = false;
        _resizeMode = ResizeMode.None;
        _contentView.Capture = false;
        Capture = false;
        SaveBounds();
    }

    private Point GetMouseScreenLocation(object? sender, MouseEventArgs e)
    {
        return sender is Control control
            ? control.PointToScreen(e.Location)
            : PointToScreen(e.Location);
    }

    private void SetMouseCapture(object? sender)
    {
        if (sender is Control control)
        {
            control.Capture = true;
        }
        else
        {
            Capture = true;
        }
    }

    private ResizeMode HitTestResizeMode(object? sender, MouseEventArgs e)
    {
        var point = sender is Control control
            ? PointToClient(control.PointToScreen(e.Location))
            : e.Location;
        var nearRight = point.X >= ClientSize.Width - ResizeGripSize;
        var nearBottom = point.Y >= ClientSize.Height - ResizeGripSize;

        if (nearRight && nearBottom)
        {
            return ResizeMode.BottomRight;
        }

        if (nearRight)
        {
            return ResizeMode.Right;
        }

        return nearBottom ? ResizeMode.Bottom : ResizeMode.None;
    }

    private void UpdateResizeCursor(object? sender, MouseEventArgs e)
    {
        var mode = HitTestResizeMode(sender, e);
        var cursor = mode switch
        {
            ResizeMode.Right => Cursors.SizeWE,
            ResizeMode.Bottom => Cursors.SizeNS,
            ResizeMode.BottomRight => Cursors.SizeNWSE,
            _ => Cursors.SizeAll
        };
        if (sender is Control control)
        {
            control.Cursor = cursor;
        }
        else
        {
            Cursor = cursor;
        }
    }

    private void ResizeFromDrag(Size delta)
    {
        if (_settings is null)
        {
            return;
        }

        var nextWidth = _resizeStartSize.Width;
        var nextHeight = _resizeStartSize.Height;
        if (_resizeMode is ResizeMode.Right or ResizeMode.BottomRight)
        {
            nextWidth += delta.Width;
        }

        if (_resizeMode is ResizeMode.Bottom or ResizeMode.BottomRight)
        {
            nextHeight += delta.Height;
        }

        Size = ClampSizeToVisibleScreen(new Size(nextWidth, nextHeight), _settings);
    }

    private void UpdateContentLayout()
    {
        if (_settings is null)
        {
            return;
        }

        var fillColor = ParseColor(_settings.ClipboardPopupTextColor);
        var outlineColor = CreateOutlineColor(fillColor);
        var sizeByHeight = Math.Max(10, (ClientSize.Height - 24) / 3);
        var fontSize = Math.Clamp(Math.Min(_settings.ClipboardPopupFontSize, sizeByHeight), 10, 96);

        _contentView.Text = CreatePreview(_rawContent, ClientSize, fontSize);
        _contentView.Font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        _contentView.FillColor = fillColor;
        _contentView.OutlineColor = outlineColor;
        _contentView.OutlineWidth = Math.Clamp(fontSize / 12f, 1.35f, 3.6f);
        _contentView.Cursor = _settings.ClipboardPopupUnlocked ? Cursors.SizeAll : Cursors.Default;
        if (!_settings.ClipboardPopupUnlocked)
        {
            EndDrag();
        }
        _contentView.Invalidate();
    }

    private void SaveBounds()
    {
        if (_settings is null || _applyingSettings || WindowState != FormWindowState.Normal)
        {
            return;
        }

        _settings.ClipboardPopupX = Left;
        _settings.ClipboardPopupY = Top;
        _settings.ClipboardPopupWidth = Width;
        _settings.ClipboardPopupHeight = Height;
        _saveSettings?.Invoke();
    }

    private static Size ClampSizeToVisibleScreen(Size requested, AppSettings settings)
    {
        var workingArea = FindBestWorkingArea(settings);
        var maxWidth = Math.Min(AppSettings.MaximumPopupWidth, Math.Max(AppSettings.MinimumPopupWidth, workingArea.Width));
        var maxHeight = Math.Min(AppSettings.MaximumPopupHeight, Math.Max(AppSettings.MinimumPopupHeight, workingArea.Height));
        return new Size(
            Math.Clamp(requested.Width, AppSettings.MinimumPopupWidth, maxWidth),
            Math.Clamp(requested.Height, AppSettings.MinimumPopupHeight, maxHeight));
    }

    private static Point ResolveLocation(AppSettings settings)
    {
        var savedBounds = new Rectangle(
            settings.ClipboardPopupX,
            settings.ClipboardPopupY,
            settings.ClipboardPopupWidth,
            settings.ClipboardPopupHeight);

        foreach (var screen in Screen.AllScreens)
        {
            if (screen.WorkingArea.IntersectsWith(savedBounds))
            {
                return ClampLocationToWorkingArea(savedBounds.Location, savedBounds.Size, screen.WorkingArea);
            }
        }

        var area = FindBestWorkingArea(settings);
        return new Point(
            Math.Max(area.Left, area.Right - settings.ClipboardPopupWidth - 32),
            Math.Max(area.Top, area.Bottom - settings.ClipboardPopupHeight - 42));
    }

    private static Rectangle FindBestWorkingArea(AppSettings settings)
    {
        var savedPoint = new Point(settings.ClipboardPopupX, settings.ClipboardPopupY);
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.WorkingArea.Contains(savedPoint))
            {
                return screen.WorkingArea;
            }
        }

        return Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
    }

    private static Point ClampLocationToWorkingArea(Point requested, Size size, Rectangle area)
    {
        var maxX = Math.Max(area.Left, area.Right - Math.Min(size.Width, area.Width));
        var maxY = Math.Max(area.Top, area.Bottom - Math.Min(size.Height, area.Height));
        return new Point(
            Math.Clamp(requested.X, area.Left, maxX),
            Math.Clamp(requested.Y, area.Top, maxY));
    }

    private static string CreatePreview(string content, Size windowSize, float fontSize)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            return "";
        }

        var columns = Math.Max(8, (int)(Math.Max(80, windowSize.Width - 48) / Math.Max(8, fontSize * 0.62f)));
        var rows = Math.Max(1, (int)(Math.Max(30, windowSize.Height - 42) / Math.Max(12, fontSize * 1.35f)));
        var maxLength = Math.Clamp(columns * rows, 12, 900);
        return normalized.Length <= maxLength ? normalized : normalized[..Math.Max(1, maxLength - 3)] + "...";
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

    private static Color CreateOutlineColor(Color fillColor)
    {
        var luminance = (0.2126 * fillColor.R + 0.7152 * fillColor.G + 0.0722 * fillColor.B) / 255.0;
        if (luminance >= 0.62)
        {
            return Color.FromArgb(170, 46, 50, 56);
        }

        if (luminance <= 0.32)
        {
            return Color.FromArgb(175, 238, 235, 224);
        }

        return Color.FromArgb(160, 35, 38, 43);
    }

    private static Point GetPointFromLParam(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xffff));
        var y = unchecked((short)((value >> 16) & 0xffff));
        return new Point(x, y);
    }

    private sealed class OutlinedTextControl : Control
    {
        public OutlinedTextControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        public Color TransparentColor { get; set; } = Color.Fuchsia;

        public Color FillColor { get; set; } = ColorTranslator.FromHtml("#F4F1E8");

        public Color OutlineColor { get; set; } = Color.FromArgb(170, 46, 50, 56);

        public float OutlineWidth { get; set; } = 2.2f;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(TransparentColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            if (string.IsNullOrWhiteSpace(Text))
            {
                return;
            }

            var layout = new RectangleF(
                Padding.Left,
                Padding.Top,
                Math.Max(1, Width - Padding.Horizontal),
                Math.Max(1, Height - Padding.Vertical));
            using var format = new StringFormat(StringFormatFlags.LineLimit)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            using var path = new GraphicsPath();
            path.AddString(Text, Font.FontFamily, (int)Font.Style, Font.Size, layout, format);

            using var outlinePen = new Pen(OutlineColor, OutlineWidth)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using var fillBrush = new SolidBrush(FillColor);
            e.Graphics.DrawPath(outlinePen, path);
            e.Graphics.FillPath(fillBrush, path);
        }
    }

    private enum ResizeMode
    {
        None,
        Right,
        Bottom,
        BottomRight
    }

    private static class NativeMethods
    {
        internal const int SwShownoactivate = 4;

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
