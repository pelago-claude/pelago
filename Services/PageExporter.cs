namespace DPOper.Services;

/// <summary>
/// Stampa la pagina visibile in un PDF. Lo fa il browser incorporato (il guscio WinForms collega <see cref="Printer"/> dopo
/// averlo avviato): il PDF è la pagina com'è a schermo, senza menu e pulsanti (vedi gli stili di stampa in app.css).
/// </summary>
public sealed class PageExporter
{
    public Func<string, Task<bool>>? Printer { get; set; }

    public Task<bool> PrintToPdfAsync(string path) => Printer is null ? Task.FromResult(false) : Printer(path);
}
