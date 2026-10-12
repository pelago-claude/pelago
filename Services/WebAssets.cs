using System.Reflection;

namespace DPOper.Services;

/// <summary>
/// I file dell'interfaccia web (HTML, CSS, JS, grafici, icone) sono incorporati nell'eseguibile, così l'exe portabile
/// resta un file unico. All'avvio vengono copiati in <c>%LocalAppData%\DPOper\www</c> (solo se mancano o sono cambiati),
/// da dove li serve il browser incorporato.
/// </summary>
public static class WebAssets
{
    private const string Prefix = "www/";

    /// <summary>Estrae i file e restituisce il percorso completo di index.html.</summary>
    public static string ExtractAndGetHostPage()
    {
        var root = Path.Combine(
            DataLocations.Root, "www");
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            var relative = resource[Prefix.Length..].Replace('\\', '/');
            var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var content = memory.ToArray();

            // un file con la stessa lunghezza ma contenuto diverso (per esempio un colore cambiato) va comunque sostituito
            if (File.Exists(target) && new FileInfo(target).Length == content.Length && File.ReadAllBytes(target).AsSpan().SequenceEqual(content))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, content);
        }

        return Path.Combine(root, "index.html");
    }
}
