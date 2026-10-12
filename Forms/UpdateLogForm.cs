using DPOper.Services;

namespace DPOper.Forms;

/// <summary>
/// Finestra di log non modale che mostra l'avanzamento di un aggiornamento quotazioni: resta
/// aperta e reattiva mentre le chiamate in parallelo procedono, così si può continuare a usare
/// il resto dell'app nel frattempo.
/// </summary>
public sealed class UpdateLogForm : Form
{
    private readonly TextBox _logBox;

    public UpdateLogForm()
    {
        Font = new Font("Consolas", 9f);
        Text = "Aggiorna Dati - Log";
        ClientSize = new Size(640, 380);
        MinimumSize = new Size(400, 200);
        ShowIcon = false;

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,
            ForeColor = Color.Gainsboro,
            Font = new Font("Consolas", 9f),
        };

        Controls.Add(_logBox);

        FormLayoutPersistence.Attach(this, "UpdateLogForm");
    }

    public void Clear()
    {
        if (InvokeRequired)
        {
            BeginInvoke(Clear);
            return;
        }
        _logBox.Clear();
    }

    public void AppendLine(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLine(line));
            return;
        }

        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}\r\n");
    }
}
