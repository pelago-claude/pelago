using DPOper.Services;
using Microsoft.AspNetCore.Components;

namespace DPOper.Components;

/// <summary>Base delle pagine: ricalcola il modello (<see cref="Rebuild"/>) quando cambiano i dati e si ridisegna.</summary>
public abstract class StatePage : ComponentBase, IDisposable
{
    [Inject] protected AppState State { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected NativeDialogs Dialogs { get; set; } = default!;
    [Inject] protected UiPrefs Prefs { get; set; } = default!;

    protected override void OnInitialized()
    {
        State.Changed += OnStateChanged;
        if (State.HasData)
        {
            Rebuild();
        }
    }

    private void OnStateChanged()
    {
        _ = InvokeAsync(() =>
        {
            if (State.HasData)
            {
                Rebuild();
            }
            StateHasChanged();
        });
    }

    /// <summary>Ricostruisce i dati derivati dallo stato (chiamato solo se c'è un file caricato).</summary>
    protected virtual void Rebuild()
    {
    }

    public virtual void Dispose() => State.Changed -= OnStateChanged;
}
