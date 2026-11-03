using D3B.App.Localization;
using D3B.App.ViewModels.Studio;

namespace D3B.App.Tests.ViewModels.Studio;

public sealed class SearchViewModelTests
{
    private readonly List<string> _ran = [];

    private SearchViewModel Search()
    {
        SearchItem Item(SearchScope scope, string title, string keys = "")
        {
            return new SearchItem(scope, scope + ":" + title, title, "", null, "", keys, () => _ran.Add(title));
        }

        var items = new List<SearchItem>
        {
            Item(SearchScope.Assets, "Mineur Astrub"),
            Item(SearchScope.Assets, "Mines puis banque"),
            Item(SearchScope.Assets, "Minerais 1-60"),
            Item(SearchScope.Assets, "Mines d'Astrub"),
            Item(SearchScope.Assets, "Minerais avancés"),
            Item(SearchScope.Characters, "Aerafal-Iop"),
            Item(SearchScope.Characters, "Mule-Sram"),
            Item(SearchScope.Commands, "Nouveau profil", "Ctrl N"),
            Item(SearchScope.Commands, "Disposition par défaut"),
            Item(SearchScope.Settings, "Langue"),
        };

        return new SearchViewModel(new Localizer(Localizer.French), () => items);
    }

    [Fact]
    public void TheTitlesFieldAndCtrlKOpenItWithNoParameterEverywhere()
    {
        var search = Search();

        search.Open(SearchScope.Commands);
        search.Close();

        Assert.True(search.OpenCommand.CanExecute(null));

        search.OpenCommand.Execute(null);

        Assert.True(search.IsOpen);
        Assert.Equal(SearchScope.All, search.Scope);
    }

    [Fact]
    public void EverywhereGroupsWhatItFindsEachGroupItsFirstFewAndTheTextFoundLit()
    {
        var search = Search();

        search.Open();
        search.Query = "min";

        var groups = search.Groups.Select(group => (group.Title, group.Results.Count, group.More)).ToList();
        var first = search.Groups[0].Results[0];

        Assert.Equal([("Assets", 4, "1 de plus · Tab")], groups);
        Assert.Equal(("", "Min", "eur Astrub", true), (first.Before, first.Match, first.After, first.IsActive));
    }

    [Fact]
    public void NothingTypedShowsTheRecentItemsThenTheAssetsAndCharactersButNotTheCommands()
    {
        var search = Search();

        search.Open();
        search.Query = "iop";
        search.RunActive();
        search.Open();

        Assert.Equal(["Aerafal-Iop"], _ran);
        Assert.Equal(["Récents", "Assets", "Personnages"], search.Groups.Select(group => group.Title));
        Assert.Equal("Aerafal-Iop", search.Groups[0].Results[0].Item.Title);
    }

    [Fact]
    public void APrefixNamesItsScopeAndTabGoesThroughTheScopes()
    {
        var search = Search();

        search.Open();
        search.Query = ">nouv";

        Assert.Equal(SearchScope.Commands, search.ShownScope);
        Assert.Equal(["Nouveau profil"], search.Groups.SelectMany(group => group.Results).Select(result => result.Item.Title));
        Assert.True(search.Groups[0].Results[0].HasKeys);

        search.Query = "@";

        Assert.Equal(["Aerafal-Iop", "Mule-Sram"], search.Groups.SelectMany(group => group.Results).Select(result => result.Item.Title));

        // Chosen by Tab, a scope drops the prefix that named another.
        search.Cycle(forward: true);

        Assert.Equal((SearchScope.Maps, ""), (search.ShownScope, search.Query));

        search.Cycle(forward: false);
        search.Cycle(forward: false);

        Assert.Equal(SearchScope.Assets, search.ShownScope);
        Assert.Equal(["Assets", "Personnages", "Cartes", "Commandes", "Paramètres"], search.Chips.Skip(1).Select(chip => chip.Name));
        Assert.True(search.Chips.Single(chip => chip.Scope is SearchScope.Assets).IsOn);
    }

    [Fact]
    public void TheArrowsStayWithinTheResultsAndEnterRunsTheOneTheyAreOn()
    {
        var search = Search();

        search.Open(SearchScope.Characters);
        search.Move(5);
        search.RunActive();

        Assert.False(search.IsOpen);
        Assert.Equal(["Mule-Sram"], _ran);

        search.Open();
        search.Query = "zzz";

        Assert.True(search.IsEmpty);
        Assert.Equal("Rien ne correspond à « zzz »", search.EmptyText);
    }
}
