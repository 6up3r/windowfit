namespace WindowFit;

/// <summary>
/// Release notes as plain text: a newer release's signed notes with Install, or the embedded changelog's "What's new".
/// Nothing in it is a link, since a link opened from this elevated process would start the browser elevated.
/// </summary>
internal sealed class NotesWindow : Form
{
    public event EventHandler? InstallClicked;

    public NotesWindow(string title, string text, Icon icon, string closeLabel, string? installLabel)
    {
        Text = $"windowfit - {title}";
        Icon = icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(720, 520);
        MinimumSize = new Size(420, 300);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };

        var header = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12, 10, 12, 8),
            Text = title,
            Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
        };
        var body = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Font = new Font("Consolas", 9F),
            BackColor = SystemColors.Window,
            Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n"),
        };
        var bodyPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
        bodyPanel.Controls.Add(body);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 12, 10),
        };
        var close = new Button { AutoSize = true, Text = closeLabel, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close();
        buttons.Controls.Add(close);
        CancelButton = close;
        if (installLabel is not null)
        {
            var install = new Button { AutoSize = true, Text = installLabel };
            install.Click += (_, _) =>
            {
                Close();
                InstallClicked?.Invoke(this, EventArgs.Empty);
            };
            buttons.Controls.Add(install);
            AcceptButton = install;
        }

        Controls.Add(bodyPanel);
        Controls.Add(buttons);
        Controls.Add(header);
        Shown += (_, _) => body.Select(0, 0);
    }
}
