using WindowFit.Core;

namespace WindowFit;

/// <summary>
/// windowfit's license and the third-party notices, embedded in the exe. The addresses are plain text: a link
/// opened from this elevated process would start the browser elevated.
/// </summary>
internal sealed class LicenseWindow : Form
{
    public LicenseWindow(TrayText text, Icon icon)
    {
        var license = ReadResource("LICENSE");
        var sections = LicenseNotices.Sections(license, ReadResource("THIRD-PARTY-NOTICES.txt"));
        var version = typeof(LicenseWindow).Assembly.GetName().Version?.ToString(3) ?? "";

        Text = $"windowfit - {text.License}";
        Icon = icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(1000, 640);
        MinimumSize = new Size(480, 320);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 10, 12, 8) };
        header.Controls.Add(new Label { AutoSize = true, Text = $"windowfit {version}", Font = new Font(Font.FontFamily, 12F, FontStyle.Bold) });
        header.Controls.Add(new Label { AutoSize = true, Text = LicenseNotices.Copyright(license) });
        header.Controls.Add(new Label { AutoSize = true, Text = "github.com/6up3r/windowfit" });

        var works = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        works.Items.AddRange(sections.Select(s => (object)s.Title).ToArray());
        var body = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F),
            BackColor = SystemColors.Window,
        };
        works.SelectedIndexChanged += (_, _) =>
        {
            if (works.SelectedIndex >= 0)
            {
                body.Text = sections[works.SelectedIndex].Text.Replace("\n", "\r\n");
            }
        };
        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Padding = new Padding(12, 0, 12, 12) };
        split.Panel1.Controls.Add(works);
        split.Panel2.Controls.Add(body);

        Controls.Add(split);
        Controls.Add(header);
        Load += (_, _) => split.SplitterDistance = LogicalToDeviceUnits(230);
        works.SelectedIndex = 0;
    }

    internal static string ReadResource(string name)
    {
        using var stream = typeof(LicenseWindow).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"missing resource {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
