namespace DPOper;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private MenuStrip menuStrip1;
    private ToolStripMenuItem fileMenuItem;
    private ToolStripMenuItem loadCsvMenuItem;
    private ToolStripMenuItem andamentoGiornalieroMenuItem;
    private ToolStripMenuItem exitMenuItem;
    private ToolStripMenuItem movimentiMenuItem;
    private ToolStripMenuItem entrateUsciteMenuItem;
    private ToolStripMenuItem interessiMenuItem;
    private ToolStripMenuItem titoliMenuItem;
    private ToolStripMenuItem cryptoMenuItem;
    private ToolStripMenuItem derivatiMenuItem;
    private ToolStripMenuItem plusvalenzeMenuItem;
    private ToolStripMenuItem graficoMenuItem;
    private ToolStripMenuItem andamentoMenuItem;
    private ToolStripMenuItem aggiornaDatiMenuItem;
    private ToolStripMenuItem aggiornaCryptoMenuItem;
    private ToolStripMenuItem aggiornaAzioniFondiMenuItem;
    private ToolStripMenuItem aggiornaCambioMenuItem;
    private ToolStripMenuItem aggiornaTuttoMenuItem;
    private ToolStripSeparator aggiornaDatiSeparator1;
    private ToolStripMenuItem elencoQuotazioniMenuItem;
    private ToolStripMenuItem composizioneMenuItem;
    private ToolStripMenuItem composizioneAdUnaDataMenuItem;
    private ToolStripMenuItem btpMenuItem;
    private ToolStripMenuItem elencoBtpMenuItem;
    private StatusStrip statusStrip1;
    private ToolStripStatusLabel statusLabel;
    private OpenFileDialog openFileDialog1;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        menuStrip1 = new MenuStrip();
        fileMenuItem = new ToolStripMenuItem();
        loadCsvMenuItem = new ToolStripMenuItem();
        andamentoGiornalieroMenuItem = new ToolStripMenuItem();
        exitMenuItem = new ToolStripMenuItem();
        movimentiMenuItem = new ToolStripMenuItem();
        entrateUsciteMenuItem = new ToolStripMenuItem();
        interessiMenuItem = new ToolStripMenuItem();
        titoliMenuItem = new ToolStripMenuItem();
        cryptoMenuItem = new ToolStripMenuItem();
        derivatiMenuItem = new ToolStripMenuItem();
        plusvalenzeMenuItem = new ToolStripMenuItem();
        graficoMenuItem = new ToolStripMenuItem();
        andamentoMenuItem = new ToolStripMenuItem();
        aggiornaDatiMenuItem = new ToolStripMenuItem();
        aggiornaCryptoMenuItem = new ToolStripMenuItem();
        aggiornaAzioniFondiMenuItem = new ToolStripMenuItem();
        aggiornaCambioMenuItem = new ToolStripMenuItem();
        aggiornaTuttoMenuItem = new ToolStripMenuItem();
        aggiornaDatiSeparator1 = new ToolStripSeparator();
        elencoQuotazioniMenuItem = new ToolStripMenuItem();
        composizioneMenuItem = new ToolStripMenuItem();
        composizioneAdUnaDataMenuItem = new ToolStripMenuItem();
        btpMenuItem = new ToolStripMenuItem();
        elencoBtpMenuItem = new ToolStripMenuItem();
        statusStrip1 = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        openFileDialog1 = new OpenFileDialog();
        menuStrip1.SuspendLayout();
        statusStrip1.SuspendLayout();
        SuspendLayout();
        //
        // menuStrip1
        //
        menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenuItem, movimentiMenuItem, graficoMenuItem, aggiornaDatiMenuItem, composizioneMenuItem, btpMenuItem });
        menuStrip1.Location = new Point(0, 0);
        menuStrip1.Name = "menuStrip1";
        menuStrip1.Size = new Size(1000, 24);
        menuStrip1.TabIndex = 0;
        //
        // fileMenuItem
        //
        fileMenuItem.DropDownItems.AddRange(new ToolStripItem[] { loadCsvMenuItem, andamentoGiornalieroMenuItem, exitMenuItem });
        fileMenuItem.Name = "fileMenuItem";
        fileMenuItem.Text = "&File";
        //
        // loadCsvMenuItem
        //
        loadCsvMenuItem.Name = "loadCsvMenuItem";
        loadCsvMenuItem.ShortcutKeys = Keys.Control | Keys.O;
        loadCsvMenuItem.Text = "&Carica CSV operazioni...";
        loadCsvMenuItem.Click += loadCsvMenuItem_Click;
        //
        // andamentoGiornalieroMenuItem
        //
        andamentoGiornalieroMenuItem.Name = "andamentoGiornalieroMenuItem";
        andamentoGiornalieroMenuItem.Text = "&Andamento giornaliero";
        andamentoGiornalieroMenuItem.Click += andamentoGiornalieroMenuItem_Click;
        //
        // exitMenuItem
        //
        exitMenuItem.Name = "exitMenuItem";
        exitMenuItem.Text = "&Esci";
        exitMenuItem.Click += exitMenuItem_Click;
        //
        // movimentiMenuItem
        //
        movimentiMenuItem.DropDownItems.AddRange(new ToolStripItem[] { entrateUsciteMenuItem, interessiMenuItem, titoliMenuItem, cryptoMenuItem, derivatiMenuItem, plusvalenzeMenuItem });
        movimentiMenuItem.Name = "movimentiMenuItem";
        movimentiMenuItem.Text = "&Movimenti";
        //
        // entrateUsciteMenuItem
        //
        entrateUsciteMenuItem.Name = "entrateUsciteMenuItem";
        entrateUsciteMenuItem.Text = "&Entrate/Uscite";
        entrateUsciteMenuItem.Click += entrateUsciteMenuItem_Click;
        //
        // interessiMenuItem
        //
        interessiMenuItem.Name = "interessiMenuItem";
        interessiMenuItem.Text = "&Interessi";
        interessiMenuItem.Click += interessiMenuItem_Click;
        //
        // titoliMenuItem
        //
        titoliMenuItem.Name = "titoliMenuItem";
        titoliMenuItem.Text = "&Titoli";
        titoliMenuItem.Click += titoliMenuItem_Click;
        //
        // cryptoMenuItem
        //
        cryptoMenuItem.Name = "cryptoMenuItem";
        cryptoMenuItem.Text = "&Crypto";
        cryptoMenuItem.Click += cryptoMenuItem_Click;
        //
        // derivatiMenuItem
        //
        derivatiMenuItem.Name = "derivatiMenuItem";
        derivatiMenuItem.Text = "&Derivati/Warrant";
        derivatiMenuItem.Click += derivatiMenuItem_Click;
        //
        // plusvalenzeMenuItem
        //
        plusvalenzeMenuItem.Name = "plusvalenzeMenuItem";
        plusvalenzeMenuItem.Text = "&Plusvalenze/Minusvalenze";
        plusvalenzeMenuItem.Click += plusvalenzeMenuItem_Click;
        //
        // graficoMenuItem
        //
        graficoMenuItem.DropDownItems.AddRange(new ToolStripItem[] { andamentoMenuItem });
        graficoMenuItem.Name = "graficoMenuItem";
        graficoMenuItem.Text = "&Grafico";
        //
        // andamentoMenuItem
        //
        andamentoMenuItem.Name = "andamentoMenuItem";
        andamentoMenuItem.Text = "&Andamento nel tempo";
        andamentoMenuItem.Click += andamentoMenuItem_Click;
        //
        // aggiornaDatiMenuItem
        //
        aggiornaDatiMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aggiornaCryptoMenuItem, aggiornaAzioniFondiMenuItem, aggiornaCambioMenuItem, aggiornaTuttoMenuItem, aggiornaDatiSeparator1, elencoQuotazioniMenuItem });
        aggiornaDatiMenuItem.Name = "aggiornaDatiMenuItem";
        aggiornaDatiMenuItem.Text = "&Aggiorna Dati";
        //
        // aggiornaCryptoMenuItem
        //
        aggiornaCryptoMenuItem.Name = "aggiornaCryptoMenuItem";
        aggiornaCryptoMenuItem.Text = "&Crypto";
        aggiornaCryptoMenuItem.Click += aggiornaCryptoMenuItem_Click;
        //
        // aggiornaAzioniFondiMenuItem
        //
        aggiornaAzioniFondiMenuItem.Name = "aggiornaAzioniFondiMenuItem";
        aggiornaAzioniFondiMenuItem.Text = "&Azioni e Fondi";
        aggiornaAzioniFondiMenuItem.Click += aggiornaAzioniFondiMenuItem_Click;
        //
        // aggiornaCambioMenuItem
        //
        aggiornaCambioMenuItem.Name = "aggiornaCambioMenuItem";
        aggiornaCambioMenuItem.Text = "Ca&mbio (EUR/USD)";
        aggiornaCambioMenuItem.Click += aggiornaCambioMenuItem_Click;
        //
        // aggiornaTuttoMenuItem
        //
        aggiornaTuttoMenuItem.Name = "aggiornaTuttoMenuItem";
        aggiornaTuttoMenuItem.Text = "Aggiorna &tutto";
        aggiornaTuttoMenuItem.Click += aggiornaTuttoMenuItem_Click;
        //
        // elencoQuotazioniMenuItem
        //
        elencoQuotazioniMenuItem.Name = "elencoQuotazioniMenuItem";
        elencoQuotazioniMenuItem.Text = "&Elenco quotazioni";
        elencoQuotazioniMenuItem.Click += elencoQuotazioniMenuItem_Click;
        //
        // composizioneMenuItem
        //
        composizioneMenuItem.DropDownItems.AddRange(new ToolStripItem[] { composizioneAdUnaDataMenuItem });
        composizioneMenuItem.Name = "composizioneMenuItem";
        composizioneMenuItem.Text = "Compo&sizione";
        //
        // composizioneAdUnaDataMenuItem
        //
        composizioneAdUnaDataMenuItem.Name = "composizioneAdUnaDataMenuItem";
        composizioneAdUnaDataMenuItem.Text = "Composizione ad una &data";
        composizioneAdUnaDataMenuItem.Click += composizioneAdUnaDataMenuItem_Click;
        //
        // btpMenuItem
        //
        btpMenuItem.DropDownItems.AddRange(new ToolStripItem[] { elencoBtpMenuItem });
        btpMenuItem.Name = "btpMenuItem";
        btpMenuItem.Text = "&BTP";
        //
        // elencoBtpMenuItem
        //
        elencoBtpMenuItem.Name = "elencoBtpMenuItem";
        elencoBtpMenuItem.Text = "&Elenco";
        elencoBtpMenuItem.Click += elencoBtpMenuItem_Click;
        //
        // statusStrip1
        //
        statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel });
        statusStrip1.Location = new Point(0, 578);
        statusStrip1.Name = "statusStrip1";
        statusStrip1.Size = new Size(1000, 22);
        statusStrip1.TabIndex = 2;
        //
        // statusLabel
        //
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(151, 17);
        statusLabel.Text = "Nessun file caricato";
        //
        // openFileDialog1
        //
        openFileDialog1.DefaultExt = "csv";
        openFileDialog1.Filter = "File CSV (*.csv)|*.csv|Tutti i file (*.*)|*.*";
        openFileDialog1.Title = "Seleziona il file CSV delle operazioni";
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 600);
        Controls.Add(statusStrip1);
        Controls.Add(menuStrip1);
        Font = new Font("Consolas", 9F);
        IsMdiContainer = true;
        MainMenuStrip = menuStrip1;
        MinimumSize = new Size(700, 400);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "DPOper";
        WindowState = FormWindowState.Maximized;
        Load += MainForm_Load;
        menuStrip1.ResumeLayout(false);
        menuStrip1.PerformLayout();
        statusStrip1.ResumeLayout(false);
        statusStrip1.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
