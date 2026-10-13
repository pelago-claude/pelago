using System.ComponentModel;

namespace Pelago.Services;

/// <summary>
/// Aggancia una form a <see cref="UiSettingsStore"/>: al Load ripristina posizione/dimensione della
/// finestra e, per ogni griglia, larghezza/ordine delle colonne e colonna/direzione di ordinamento;
/// alla chiusura salva lo stato corrente. Stesso approccio "save on close, load on open" di WSDoc24Tools.
/// </summary>
public static class FormLayoutPersistence
{
    public static void Attach(Form form, string key, params DataGridView[] grids)
    {
        form.Load += (_, _) => Restore(form, key, grids);
        form.FormClosing += (_, _) => Save(form, key, grids);
    }

    private static void Restore(Form form, string key, DataGridView[] grids)
    {
        var state = UiSettingsStore.Get(key);
        if (state is null)
        {
            return;
        }

        if (state.Width > 0 && state.Height > 0 && form.MdiParent is null)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.SetBounds(state.Left, state.Top, state.Width, state.Height);
        }

        if (Enum.IsDefined(typeof(FormWindowState), state.WindowState))
        {
            form.WindowState = (FormWindowState)state.WindowState;
        }

        foreach (var grid in grids)
        {
            if (string.IsNullOrEmpty(grid.Name) || !state.Grids.TryGetValue(grid.Name, out var gridState))
            {
                continue;
            }

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (gridState.Columns.TryGetValue(column.Name, out var columnState) && columnState.Width > 0)
                {
                    column.Width = columnState.Width;
                }
            }

            var orderedNames = gridState.Columns
                .Where(kv => grid.Columns.Contains(kv.Key))
                .OrderBy(kv => kv.Value.DisplayIndex)
                .Select(kv => kv.Key)
                .ToList();
            for (var i = 0; i < orderedNames.Count; i++)
            {
                grid.Columns[orderedNames[i]].DisplayIndex = i;
            }

            if (!string.IsNullOrEmpty(gridState.SortColumn) && grid.Columns.Contains(gridState.SortColumn)
                && grid.Columns[gridState.SortColumn].SortMode != DataGridViewColumnSortMode.NotSortable)
            {
                var direction = gridState.SortDirection == "Descending"
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
                DataGridViewSorter.SortBy(grid, grid.Columns[gridState.SortColumn], direction);
            }
        }
    }

    private static void Save(Form form, string key, DataGridView[] grids)
    {
        var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;

        var state = new FormLayoutState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = (int)form.WindowState,
        };

        foreach (var grid in grids)
        {
            if (string.IsNullOrEmpty(grid.Name))
            {
                continue;
            }

            var gridState = new GridState();
            foreach (DataGridViewColumn column in grid.Columns)
            {
                gridState.Columns[column.Name] = new GridColumnState
                {
                    Width = column.Width,
                    DisplayIndex = column.DisplayIndex,
                };
            }

            var currentSort = DataGridViewSorter.GetCurrentSort(grid);
            if (currentSort is { } sort)
            {
                gridState.SortColumn = sort.Column.Name;
                gridState.SortDirection = sort.Direction == ListSortDirection.Descending ? "Descending" : "Ascending";
            }

            state.Grids[grid.Name] = gridState;
        }

        UiSettingsStore.Set(key, state);
    }
}
