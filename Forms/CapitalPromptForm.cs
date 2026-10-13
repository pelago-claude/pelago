namespace Pelago.Forms;

/// <summary>Finestra modale che chiede il capitale da investire prima di aprire il calcolatore BTP.</summary>
public sealed class CapitalPromptForm : Form
{
    private readonly NumericUpDown _capital;

    public decimal Capital => _capital.Value;

    public CapitalPromptForm(string bondDescription, decimal defaultCapital)
    {
        Font = new Font("Consolas", 9f);
        Text = "Capitale da investire";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 150);

        var question = new Label
        {
            Text = $"Quanto capitale vuoi investire in {bondDescription}?",
            AutoSize = false,
            Location = new Point(16, 16),
            Size = new Size(428, 36),
        };

        _capital = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1_000_000_000,
            Increment = 1000,
            DecimalPlaces = 0,
            ThousandsSeparator = true,
            Value = Math.Clamp(defaultCapital, 0, 1_000_000_000),
            Location = new Point(16, 60),
            Width = 180,
        };

        var currency = new Label { Text = "€ (commissioni comprese)", AutoSize = true, Location = new Point(204, 63) };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(268, 106), Size = new Size(80, 28) };
        var cancel = new Button { Text = "Annulla", DialogResult = DialogResult.Cancel, Location = new Point(360, 106), Size = new Size(84, 28) };

        Controls.AddRange(new Control[] { question, _capital, currency, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;

        Shown += (_, _) =>
        {
            _capital.Focus();
            _capital.Select(0, _capital.Text.Length);
        };
    }
}
