namespace Pelago.Services;

/// <summary>
/// Nella demo nel browser non ci sono finestre di Windows (apri/salva file): ogni richiesta resta senza risposta, come se l'utente
/// avesse annullato. I pulsanti che le usano sono nascosti o spenti (vedi <see cref="AppState.IsWeb"/>).
/// Stessa interfaccia di Services\NativeDialogs.cs del programma per Windows.
/// </summary>
public sealed class NativeDialogs
{
    public string? PickCsvFile(string? initialPath, string? initialFolder = null, string? title = null) => null;

    public string? SaveCsv(string suggestedName, string content) => null;

    public string? PickBackupFile() => null;

    public string? PickXlsxFile(string suggestedName) => null;

    public string? PickPdfFile(string suggestedName) => null;

    public string? PickFolder(string? initial, string description) => null;
}
