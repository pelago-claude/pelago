using System.ComponentModel;

namespace Pelago.Services;

/// <summary>
/// Ordina manualmente una griglia il cui DataSource è un semplice <c>List&lt;T&gt;</c>: DataGridView
/// sa ordinare automaticamente al click sull'intestazione solo se il DataSource implementa
/// IBindingList con SupportsSorting, cosa che un List&lt;T&gt; non fa (il click, silenziosamente,
/// non farebbe nulla). Stesso problema risolto da WSDoc24Tools con il suo GridSorter.
/// </summary>
public static class DataGridViewSorter
{
    public static void SortBy(DataGridView grid, DataGridViewColumn column, ListSortDirection direction)
    {
        if (grid.DataSource is not System.Collections.IEnumerable source)
        {
            return;
        }

        var items = source.Cast<object>().ToList();
        if (items.Count == 0)
        {
            return;
        }

        var itemType = items[0].GetType();
        var propertyInfo = itemType.GetProperty(column.DataPropertyName);
        if (propertyInfo is null)
        {
            return;
        }

        var comparer = Comparer<object?>.Create(CompareValues);
        var ordered = direction == ListSortDirection.Ascending
            ? items.OrderBy(item => propertyInfo.GetValue(item), comparer)
            : items.OrderByDescending(item => propertyInfo.GetValue(item), comparer);

        var typedList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
        foreach (var item in ordered)
        {
            typedList.Add(item);
        }

        grid.DataSource = typedList;

        foreach (DataGridViewColumn c in grid.Columns)
        {
            c.HeaderCell.SortGlyphDirection = c == column
                ? (direction == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending)
                : SortOrder.None;
        }
    }

    /// <summary>La colonna attualmente ordinata (glifo visibile nell'intestazione), se presente.</summary>
    public static (DataGridViewColumn Column, ListSortDirection Direction)? GetCurrentSort(DataGridView grid)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column.HeaderCell.SortGlyphDirection == SortOrder.Ascending)
            {
                return (column, ListSortDirection.Ascending);
            }
            if (column.HeaderCell.SortGlyphDirection == SortOrder.Descending)
            {
                return (column, ListSortDirection.Descending);
            }
        }
        return null;
    }

    private static int CompareValues(object? a, object? b)
    {
        if (a is null && b is null)
        {
            return 0;
        }
        if (a is null)
        {
            return -1;
        }
        if (b is null)
        {
            return 1;
        }
        if (a is IComparable comparable)
        {
            return comparable.CompareTo(b);
        }
        return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
