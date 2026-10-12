namespace DPOper.Services;

/// <summary>Dati fissi del progetto che compaiono nella pagina Crediti: chi detiene il copyright e dove sta il codice.</summary>
public static class ProjectInfo
{
    public const string Copyright = "Copyright © 2026 David";

    /// <summary>Indirizzo del repository pubblico (per esempio <c>github.com/nome/pelago</c>). Se è vuoto, la pagina Crediti non lo mostra.</summary>
    public const string RepositoryUrl = "github.com/pelago-claude/pelago";
}
