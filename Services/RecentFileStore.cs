namespace Pelago.Services;

/// <summary>
/// Ricorda l'ultimo file CSV caricato, così l'app può riaprirlo automaticamente al prossimo avvio.
/// </summary>
public static class RecentFileStore
{
    private static readonly string ConfigPath = Path.Combine(
        DataLocations.Root, "lastfile.txt");

    public static string? GetLastFilePath()
    {
        try
        {
            return File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void SetLastFilePath(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(ConfigPath, path);
        }
        catch (IOException)
        {
            // Non critico: se non riusciamo a salvare la preferenza, al prossimo avvio si chiederà di nuovo il file.
        }
    }
}
