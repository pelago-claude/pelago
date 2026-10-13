using System.Text;

namespace Pelago.Services;

/// <summary>Finestre di dialogo di Windows (apri/salva file) richiamabili dall'interfaccia web.</summary>
public sealed class NativeDialogs
{
    /// <summary>Chiede un file CSV delle operazioni. Null se l'utente annulla.</summary>
    public string? PickCsvFile(string? initialPath, string? initialFolder = null, string? title = null)
    {
        using var dialog = new OpenFileDialog
        {
            Title = title ?? Loc.T("Seleziona il file CSV delle operazioni"),
            Filter = Loc.T("File CSV (*.csv)|*.csv|Tutti i file (*.*)|*.*"),
            DefaultExt = "csv",
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && File.Exists(initialPath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(initialPath)!;
            dialog.FileName = Path.GetFileName(initialPath);
        }
        else if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    /// <summary>Salva un testo CSV (separatore ";", UTF-8 con BOM per Excel). Restituisce il percorso, o null se annullato.</summary>
    public string? SaveCsv(string suggestedName, string content)
    {
        using var dialog = new SaveFileDialog
        {
            Title = Loc.T("Esporta in CSV"),
            Filter = Loc.T("File CSV (*.csv)|*.csv"),
            DefaultExt = "csv",
            FileName = suggestedName,
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return null;
        }

        File.WriteAllText(dialog.FileName, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return dialog.FileName;
    }

    /// <summary>Chiede dove salvare il backup (zip). Null se l'utente annulla.</summary>
    public string? PickBackupFile()
    {
        using var dialog = new SaveFileDialog
        {
            Title = Loc.T("Salva il backup dei dati"),
            Filter = Loc.T("File zip (*.zip)|*.zip"),
            DefaultExt = "zip",
            FileName = $"pelago-backup-{DateTime.Now:yyyyMMdd}.zip",
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    /// <summary>Chiede dove salvare un file Excel (.xlsx). Null se l'utente annulla.</summary>
    public string? PickXlsxFile(string suggestedName)
    {
        using var dialog = new SaveFileDialog
        {
            Title = Loc.T("Esporta in Excel"),
            Filter = Loc.T("File Excel (*.xlsx)|*.xlsx"),
            DefaultExt = "xlsx",
            FileName = suggestedName,
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    /// <summary>Chiede dove salvare un PDF. Null se l'utente annulla.</summary>
    public string? PickPdfFile(string suggestedName)
    {
        using var dialog = new SaveFileDialog
        {
            Title = Loc.T("Salva come PDF"),
            Filter = Loc.T("File PDF (*.pdf)|*.pdf"),
            DefaultExt = "pdf",
            FileName = suggestedName,
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    /// <summary>Chiede una cartella. Null se l'utente annulla.</summary>
    public string? PickFolder(string? initial, string description)
    {
        using var dialog = new FolderBrowserDialog { Description = description, UseDescriptionForTitle = true };
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
        {
            dialog.InitialDirectory = initial;
        }
        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }
}
