using ClipboardHistoryManager.Models;

namespace ClipboardHistoryManager.UI;

public sealed class DetailForm : Form
{
    private readonly ClipboardEntry _entry;
    private readonly Action<ClipboardEntry> _copyAction;

    public DetailForm(ClipboardEntry entry, Action<ClipboardEntry> copyAction)
    {
        _entry = entry;
        _copyAction = copyAction;
        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = "查看详情";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(560, 420);
        Size = new Size(720, 560);
        BackColor = Color.FromArgb(246, 247, 250);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var meta = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"复制时间：{_entry.CopiedAt:yyyy-MM-dd HH:mm:ss}    收藏：{(_entry.IsFavorite ? "是" : "否")}",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(92, 100, 112)
        };

        var contentBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = true,
            Text = _entry.Content,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 35, 45),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = BackColor,
            Padding = new Padding(0, 8, 0, 0)
        };

        var closeButton = new Button
        {
            Text = "关闭",
            Width = 88,
            Height = 32,
            DialogResult = DialogResult.Cancel
        };
        var copyButton = new Button
        {
            Text = "复制完整内容",
            Width = 124,
            Height = 32
        };
        copyButton.Click += (_, _) =>
        {
            _copyAction(_entry);
            Close();
        };

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(copyButton);
        AcceptButton = copyButton;
        CancelButton = closeButton;

        root.Controls.Add(meta, 0, 0);
        root.Controls.Add(contentBox, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
    }
}
