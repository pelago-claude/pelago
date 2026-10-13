namespace Pelago;

static class Program
{
    /// <summary>
    /// Di default parte la nuova interfaccia (ShellForm). Con l'argomento <c>--classic</c> parte la vecchia
    /// interfaccia a finestre MDI (MainForm), rimasta nell'app per poter tornare indietro in ogni momento.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var classic = args.Any(a => a.Equals("--classic", StringComparison.OrdinalIgnoreCase));
        Application.Run(classic ? new MainForm() : new ShellForm());
    }
}
