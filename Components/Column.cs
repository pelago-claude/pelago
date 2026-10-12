using DPOper.Services;
using Microsoft.AspNetCore.Components;

namespace DPOper.Components;

/// <summary>Descrizione di una colonna della <see cref="DataTable{TItem}"/>: valore per ordinare, testo da mostrare, totale opzionale.</summary>
public sealed class Column<T>
{
    public required string Title { get; init; }
    public required Func<T, IComparable?> SortValue { get; init; }
    public required Func<T, string> Text { get; init; }

    /// <summary>Se presente, la cella mostra questo contenuto (per esempio un codice cliccabile); <see cref="Text"/> resta per ordinare, filtrare ed esportare.</summary>
    public Func<T, RenderFragment>? Render { get; init; }

    /// <summary>Gli ISIN scritti nel testo della cella diventano codici cliccabili (colonne di testo).</summary>
    public bool LinkIsins { get; init; }

    /// <summary>Nella cella, accanto all'importo, una barra verso destra (guadagno) o verso sinistra (perdita), in proporzione al valore più grande della tabella.</summary>
    public bool Bar { get; init; }
    public bool Right { get; init; }
    public ColumnKind Kind { get; init; }
    public Func<T, string?>? Css { get; init; }
    public Func<IReadOnlyList<T>, string>? Footer { get; init; }
    public string? HeaderCss { get; init; }
    public string? CellCss { get; init; }
    public string Key => Title;
}

/// <summary>Costruttori rapidi delle colonne più comuni.</summary>
public static class Col
{
    public static Column<T> Text<T>(string title, Func<T, string?> value, Func<IReadOnlyList<T>, string>? footer = null,
        string? cellCss = null) => new()
    {
        Title = title,
        SortValue = x => value(x) ?? "",
        Text = x => value(x) ?? "",
        Footer = footer,
        CellCss = cellCss,
        LinkIsins = true,
    };

    public static Column<T> Money<T>(string title, Func<T, decimal?> value, bool signColor = false,
        Func<IReadOnlyList<T>, decimal?>? total = null, string? headerCss = null, string? cellCss = null, bool bar = false) => new()
    {
        Title = title,
        SortValue = x => value(x),
        Text = x => Fmt.Money(value(x)),
        Right = true,
        Kind = ColumnKind.Money,
        Css = signColor ? x => Fmt.Sign(value(x)) : null,
        Footer = total is null ? null : rows => Fmt.Money(total(rows)),
        HeaderCss = headerCss,
        CellCss = cellCss,
        Bar = bar,
    };

    public static Column<T> Money4<T>(string title, Func<T, decimal?> value) => new()
    {
        Title = title,
        SortValue = x => value(x),
        Text = x => Fmt.Money4(value(x)),
        Right = true,
        Kind = ColumnKind.Number,
    };

    public static Column<T> Num<T>(string title, Func<T, decimal?> value, int decimals = 4,
        Func<IReadOnlyList<T>, decimal?>? total = null, string? headerCss = null, string? cellCss = null) => new()
    {
        Title = title,
        SortValue = x => value(x),
        Text = x => Fmt.Num(value(x), decimals),
        Right = true,
        Kind = ColumnKind.Number,
        Footer = total is null ? null : rows => Fmt.Num(total(rows), decimals),
        HeaderCss = headerCss,
        CellCss = cellCss,
    };

    public static Column<T> Pct<T>(string title, Func<T, double?> value, string? headerCss = null, string? cellCss = null,
        bool signColor = false) => new()
    {
        Title = title,
        SortValue = x => value(x),
        Text = x => Fmt.Pct(value(x)),
        Right = true,
        Kind = ColumnKind.Percent,
        Css = signColor ? x => value(x) is { } v ? (v >= 0 ? "pos" : "neg") : null : null,
        HeaderCss = headerCss,
        CellCss = cellCss,
    };

    public static Column<T> Date<T>(string title, Func<T, DateTime?> value, string format = "dd/MM/yyyy") => new()
    {
        Title = title,
        SortValue = x => value(x),
        Text = x => Fmt.Date(value(x), format),
        Kind = ColumnKind.Date,
    };

    /// <summary>Un codice (ISIN o ticker) cliccabile: apre la finestra per dargli un nome.</summary>
    public static Column<T> Isin<T>(string title, Func<T, string?> value) => new()
    {
        Title = title,
        SortValue = x => value(x) ?? "",
        Text = x => value(x) ?? "",
        Render = x => builder =>
        {
            if (!string.IsNullOrWhiteSpace(value(x)))
            {
                builder.OpenComponent<DPOper.Components.Isin>(0);
                builder.AddAttribute(1, nameof(DPOper.Components.Isin.Code), value(x));
                builder.CloseComponent();
            }
        },
    };

    /// <summary>
    /// L'emittente di un derivato (Vontobel, Société Générale), che si capisce dal prefisso del codice ISIN; vuoto per azioni, fondi e il resto.
    /// Serve la categoria perché alcuni ISIN di azioni tedesche cominciano come quelli dei derivati.
    /// </summary>
    public static Column<T> Issuer<T>(Func<T, string?> code, Func<T, string?> assetClass) => new()
    {
        Title = "Emittente",
        SortValue = x => Issuers.ShortName(code(x), assetClass(x)),
        Text = x => Issuers.ShortName(code(x), assetClass(x)),
    };

    /// <summary>Il nome di un titolo con la matita per cambiarlo (il codice serve per sapere quale titolo rinominare).</summary>
    public static Column<T> Name<T>(string title, Func<T, string> name, Func<T, string?> code) => new()
    {
        Title = title,
        SortValue = x => name(x),
        Text = x => name(x),
        Render = x => builder =>
        {
            builder.OpenComponent<NameCell>(0);
            builder.AddAttribute(1, nameof(NameCell.Name), name(x));
            builder.AddAttribute(2, nameof(NameCell.Code), code(x));
            builder.CloseComponent();
        },
    };

    public static Column<T> Badge<T>(string title, Func<T, string> value) => new()
    {
        Title = title,
        SortValue = x => value(x),
        Text = x => value(x),
        CellCss = "badge-cell",
    };
}
