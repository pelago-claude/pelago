using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class InstrumentNamesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pelago-names-" + Guid.NewGuid().ToString("N"));

    private string File_ => Path.Combine(_dir, "nomi-titoli.json");

    private InstrumentNames New() => new(File_);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void A_name_is_found_whatever_the_case_or_spaces_of_the_code()
    {
        var names = New();

        names.Set(" ie00test0001 ", "Mondo");

        Assert.Equal("Mondo", names.Get("IE00TEST0001"));
        Assert.Equal("Mondo", names.Get("ie00test0001"));
        Assert.Null(names.Get("IE00OTHER"));
        Assert.True(names.Has("IE00TEST0001"));
    }

    [Fact]
    public void Names_are_cleaned_and_an_empty_name_removes_the_choice()
    {
        var names = New();

        names.Set("AAA", "   Il   mio    titolo  ");
        Assert.Equal("Il mio titolo", names.Get("AAA"));

        names.Set("AAA", "   ");
        Assert.Null(names.Get("AAA"));
        Assert.Equal(0, names.Count);
    }

    [Fact]
    public void A_very_long_name_is_cut_to_the_limit()
    {
        var names = New();

        names.Set("AAA", new string('x', 500));

        Assert.Equal(InstrumentNames.MaxLength, names.Get("AAA")!.Length);
    }

    [Fact]
    public void Names_survive_a_restart_and_the_changed_event_fires_only_on_real_changes()
    {
        var names = New();
        var events = 0;
        names.Changed += () => events++;

        Assert.True(names.Set("AAA", "Alfa"));
        Assert.False(names.Set("AAA", "Alfa"));              // stesso nome: niente cambia
        Assert.False(names.Set("ZZZ", ""));                  // nulla da togliere
        Assert.True(names.Set("AAA", "Alfa 2"));
        Assert.Equal(2, events);

        var reloaded = New();
        Assert.Equal("Alfa 2", reloaded.Get("AAA"));
        Assert.False(File.Exists(File_ + ".tmp"));
        Assert.NotNull(reloaded.Entry("AAA"));
    }

    [Fact]
    public void A_damaged_file_is_kept_aside_and_the_register_starts_empty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File_, "{ questo non è json");

        var names = New();

        Assert.Equal(0, names.Count);
        Assert.True(File.Exists(File_ + ".rovinato"));
        names.Set("AAA", "Alfa");                            // e si può continuare a usarlo
        Assert.Equal("Alfa", New().Get("AAA"));
    }

    [Fact]
    public void The_same_name_on_another_code_is_reported_so_the_dialog_can_warn()
    {
        var names = New();
        names.Set("AAA", "Oracle");

        Assert.Equal("AAA", names.OtherCodeWithName("BBB", "oracle"));
        Assert.Null(names.OtherCodeWithName("AAA", "Oracle"));
        Assert.Null(names.OtherCodeWithName("BBB", "Altro"));
    }

    [Fact]
    public void Export_then_import_gives_back_the_same_names_including_quotes_and_semicolons()
    {
        var source = New();
        source.Set("AAA", "Alfa; \"il primo\"");
        source.Set("BBB", "Beta");

        var csv = source.ExportCsv();
        Assert.StartsWith("isin;nome", csv);

        var target = new InstrumentNames(Path.Combine(_dir, "altro.json"));
        var (imported, skipped) = target.ImportCsv(csv);

        Assert.Equal(2, imported);
        Assert.Equal(0, skipped);
        Assert.Equal("Alfa; \"il primo\"", target.Get("AAA"));
        Assert.Equal("Beta", target.Get("BBB"));
    }

    [Fact]
    public void Import_accepts_commas_no_header_and_skips_incomplete_rows()
    {
        var names = New();
        names.Set("AAA", "Vecchio");

        var (imported, skipped) = names.ImportCsv("aaa,Nuovo\r\nBBB,Secondo\r\n,Senza codice\r\nCCC,\r\n");

        Assert.Equal(2, imported);
        Assert.Equal(2, skipped);
        Assert.Equal("Nuovo", names.Get("AAA"));            // quello già presente viene sostituito
        Assert.Equal("Secondo", names.Get("BBB"));
        Assert.Null(names.Get("CCC"));
    }

    [Fact]
    public void The_app_shows_the_chosen_name_and_can_still_give_the_original_one()
    {
        var names = New();
        var state = new AppState(new UiPrefs(), names);

        Assert.Equal("dal file", state.FullNameOf("AAA", "dal file"));

        names.Set("AAA", "Mio nome");

        Assert.Equal("Mio nome", state.FullNameOf("AAA", "dal file"));
        Assert.Equal("dal file", state.OriginalNameOf("AAA", "dal file"));
    }

    [Fact]
    public void The_editor_remembers_which_title_is_being_renamed()
    {
        var editor = new NameEditor();
        var events = 0;
        editor.Changed += () => events++;

        editor.Open("  de000abc1234 ");
        Assert.Equal("DE000ABC1234", editor.Code);

        editor.Open("   ");                                  // un codice vuoto non apre nulla
        Assert.Equal("DE000ABC1234", editor.Code);

        editor.Close();
        Assert.Null(editor.Code);
        Assert.Equal(2, events);
    }
}
