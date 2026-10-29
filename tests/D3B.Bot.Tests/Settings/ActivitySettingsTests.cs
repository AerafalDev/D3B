using System.Globalization;
using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Settings;

public sealed class ActivitySettingsTests
{
    private static LocalizedText Text(string text)
    {
        return LocalizedText.Invariant(text);
    }

    private static ActivityDefinition Register(Action<ActivitySettingsBuilder<TestSettings>> describe)
    {
        var services = new ServiceCollection();

        services.AddActivity<SettingsActivity, TestSettings>("test", Text("Test"), describe);

        return Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
    }

    private static ActivitySettings Every()
    {
        return Register(settings =>
        {
            settings.Section(Text("Pace"));
            settings.Number(s => s.Walks, Text("Walks"), 1, 100).Help(Text("How many walks"));
            settings.Flag(s => s.UseBank, Text("Use the bank"));
            settings.Text(s => s.Greeting, Text("Greeting"), 10);
            settings.Section(Text("Focus"));
            settings.Choice(s => s.Focus, Text("Focus"), focus => Text($"On {focus}"));
            settings.Choices(s => s.Order, Text("Order"), focus => Text($"On {focus}")).Ordered();
            settings.Job(s => s.Job, Text("Job")).VisibleWhen(s => s.Focus == TestFocus.Job);
            settings.Resources(s => s.Resources, Text("Resources")).Within(s => s.Job).Ordered();
            settings.Area(s => s.Area, Text("Area"));
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).Within(s => s.Area);
            settings.Number(s => s.Lowest, Text("Lowest"), 0, 10);
            settings.Number(s => s.Highest, Text("Highest"), 0, 10);
            settings.Rule(s => s.Lowest <= s.Highest, Text("The lowest is above the highest."));
        }).Settings;
    }

    private static TestSettings Read(ActivitySettings settings, string json, out IReadOnlyList<string> reset)
    {
        var reading = settings.Read(json);

        reset = [.. reading.Reset.Select(setting => setting.Key)];

        return Assert.IsType<TestSettings>(reading.Settings);
    }

    [Fact]
    public void AModuleDeclaresEachSettingInOrderUnderItsSection()
    {
        var settings = Every();
        var invariant = CultureInfo.InvariantCulture;

        Assert.Equal(typeof(TestSettings), settings.Type);
        Assert.Equal(
            ["Walks", "UseBank", "Greeting", "Focus", "Order", "Job", "Resources", "Area", "SubAreas", "Lowest", "Highest"],
            settings.All.Select(setting => setting.Key));
        Assert.Equal(
            [SettingKind.Number, SettingKind.Flag, SettingKind.Text, SettingKind.Choice, SettingKind.Choice, SettingKind.Job, SettingKind.Resource, SettingKind.Area, SettingKind.SubArea],
            settings.All.Take(9).Select(setting => setting.Kind));
        Assert.Equal(["Pace", "Pace", "Pace", "Focus"], settings.All.Take(4).Select(setting => setting.Section?.In(invariant)));

        var walks = settings.All[0];

        Assert.Equal((1, 100), (walks.Minimum, walks.Maximum));
        Assert.Equal("How many walks", walks.Help?.In(invariant));
        Assert.Equal(10, settings.All[2].MaximumLength);
        Assert.Equal(["On Job", "On Area", "On Nothing"], settings.All[3].Options.Select(option => option.Text.In(invariant)));
        Assert.Equal([TestFocus.Job, TestFocus.Area, TestFocus.Nothing], settings.All[3].Options.Select(option => (TestFocus)option.Value));
        Assert.True(settings.All[4].IsList && settings.All[4].IsOrdered);
        Assert.True(settings.All[5].IsConditional);
        Assert.Same(settings.All[5], settings.All[6].Within);
        Assert.Same(settings.All[7], settings.All[8].Within);
        Assert.False(settings.All[8].IsOrdered);
    }

    [Fact]
    public void TheDefaultsAreTheClassOwnValues()
    {
        var settings = Assert.IsType<TestSettings>(Every().CreateDefault());

        Assert.Equal((10, true, "Hello", TestFocus.Job), (settings.Walks, settings.UseBank, settings.Greeting, settings.Focus));
        Assert.Equal([TestFocus.Area], settings.Order);
        Assert.Null(settings.Job);
        Assert.Empty(settings.Resources);
    }

    [Fact]
    public void SettingsWrittenAreReadBackAsTheyWere()
    {
        var declared = Every();
        var settings = new TestSettings
        {
            Walks = 42,
            UseBank = false,
            Greeting = "Salut",
            Focus = TestFocus.Area,
            Order = [TestFocus.Nothing, TestFocus.Job],
            Job = TestReferences.Lumberjack,
            Resources = [TestReferences.Chestnut, TestReferences.Ash],
            Area = TestReferences.Amakna,
            SubAreas = [TestReferences.Hills],
            Undeclared = 99,
        };

        var json = declared.Write(settings);
        var read = Read(declared, json, out var reset);

        Assert.Empty(reset);
        Assert.Equal(
            """{"Walks":42,"UseBank":false,"Greeting":"Salut","Focus":"Area","Order":["Nothing","Job"],"Job":2,"Resources":[304,303],"Area":1,"SubAreas":[11],"Lowest":1,"Highest":5}""",
            json);
        Assert.Equal((42, false, "Salut", TestFocus.Area), (read.Walks, read.UseBank, read.Greeting, read.Focus));
        Assert.Equal([TestFocus.Nothing, TestFocus.Job], read.Order);
        Assert.Equal(TestReferences.Lumberjack, read.Job);
        Assert.Equal([TestReferences.Chestnut, TestReferences.Ash], read.Resources);
        Assert.Equal([TestReferences.Hills], read.SubAreas);

        // What the module does not declare is not a setting: it keeps the class's own value.
        Assert.Equal(3, read.Undeclared);
    }

    [Fact]
    public void AStoredValueThatNoLongerReadsKeepsItsDefaultAndIsReported()
    {
        var declared = Every();
        var read = Read(declared, """{"Walks":"many","Greeting":7,"Focus":"Dungeon","Order":["Job","Dungeon"],"Job":2,"Resources":[303,"ash"],"Gone":true}""", out var reset);

        Assert.Equal(["Walks", "Greeting", "Focus", "Order", "Resources"], reset);
        Assert.Equal((10, "Hello", TestFocus.Job), (read.Walks, read.Greeting, read.Focus));

        // What still reads of a list is kept.
        Assert.Equal([TestFocus.Job], read.Order);
        Assert.Equal([TestReferences.Ash], read.Resources);
        Assert.Equal(TestReferences.Lumberjack, read.Job);
    }

    [Fact]
    public void StoredSettingsThatAreNoJsonObjectAreAllReset()
    {
        var declared = Every();

        Read(declared, "not json", out var corrupt);
        Read(declared, "[1, 2]", out var array);
        Read(declared, "", out var empty);

        Assert.Equal(declared.All.Count, corrupt.Count);
        Assert.Equal(declared.All.Count, array.Count);
        Assert.Empty(empty);
    }

    [Fact]
    public void EachSettingThatShowsIsCheckedAgainstItsBounds()
    {
        var declared = Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Text(s => s.Greeting, Text("Greeting"), 5).Required();
            settings.Choices(s => s.Order, Text("Order"), focus => Text($"{focus}")).Required();
            settings.Job(s => s.Job, Text("Job")).VisibleWhen(s => s.Focus == TestFocus.Job);
            settings.Area(s => s.Area, Text("Area")).Required();
            settings.Number(s => s.Lowest, Text("Lowest"), 0, 10);
            settings.Number(s => s.Highest, Text("Highest"), 0, 10);
            settings.Rule(s => s.Lowest <= s.Highest, Text("The lowest is above the highest."));
        }).Settings;
        var settings = new TestSettings { Walks = 101, Greeting = "  ", Order = [], Area = TestReferences.Amakna, Lowest = 6, Highest = 4 };

        Assert.True(declared.All.Single(setting => setting.Key == "Job").IsVisible(settings));
        Assert.False(declared.IsVisible(declared.All[3], new TestSettings { Focus = TestFocus.Area }));

        var problems = declared.Check(settings);

        Assert.Equal(
            [("Walks", SettingProblemKind.OutOfRange), ("Greeting", SettingProblemKind.Required), ("Order", SettingProblemKind.Required), (null, SettingProblemKind.Rule)],
            problems.Select(problem => (problem.Setting?.Key, problem.Kind)));
        Assert.Equal("The lowest is above the highest.", problems[^1].Message?.In(CultureInfo.InvariantCulture));

        settings.Greeting = "Bonjour";
        settings.Area = null;
        settings.Walks = 0;

        Assert.Equal(
            [("Walks", SettingProblemKind.OutOfRange), ("Greeting", SettingProblemKind.TooLong), ("Order", SettingProblemKind.Required), ("Area", SettingProblemKind.Required), (null, SettingProblemKind.Rule)],
            declared.Check(settings).Select(problem => (problem.Setting?.Key, problem.Kind)));
    }

    [Fact]
    public void AHiddenSettingIsNotChecked()
    {
        var declared = Every();
        var settings = new TestSettings { Focus = TestFocus.Area, Job = 999 };

        Assert.Empty(declared.Check(settings, TestReferences.Game()));

        settings.Focus = TestFocus.Job;

        Assert.Equal([("Job", SettingProblemKind.Unknown)], declared.Check(settings, TestReferences.Game()).Select(problem => (problem.Setting?.Key, problem.Kind)));
    }

    [Fact]
    public void TheGamesValuesAreCheckedAgainstThePackAndWithinTheirParent()
    {
        var declared = Every();
        var game = TestReferences.Game();
        var settings = new TestSettings
        {
            Job = TestReferences.Miner,
            Resources = [TestReferences.Ash],
            Area = TestReferences.Astrub,
            SubAreas = [TestReferences.Forest, 404],
        };

        // Without the pack, ids are not checked; with it, an unknown one comes before one of another parent.
        Assert.Empty(declared.Check(settings));
        Assert.Equal(
            [("Resources", SettingProblemKind.NotWithin), ("SubAreas", SettingProblemKind.Unknown)],
            declared.Check(settings, game).Select(problem => (problem.Setting?.Key, problem.Kind)));

        settings.SubAreas = [TestReferences.Forest];
        settings.Job = TestReferences.Lumberjack;

        Assert.Equal([("SubAreas", SettingProblemKind.NotWithin)], declared.Check(settings, game).Select(problem => (problem.Setting?.Key, problem.Kind)));

        // No parent chosen: every value of the game is within it.
        settings.Area = null;

        Assert.Empty(declared.Check(settings, game));
    }

    [Fact]
    public void AListHoldsEachValueOnceInOrder()
    {
        var declared = Every();
        var resources = declared.All.Single(setting => setting.Key == "Resources");
        var order = declared.All.Single(setting => setting.Key == "Order");
        var settings = new TestSettings();

        resources.SetValue(settings, new List<int> { TestReferences.Iron, TestReferences.Ash, TestReferences.Iron });
        order.SetValue(settings, new object[] { TestFocus.Nothing, TestFocus.Job, TestFocus.Nothing });

        Assert.Equal([TestReferences.Iron, TestReferences.Ash], settings.Resources);
        Assert.Equal([TestFocus.Nothing, TestFocus.Job], settings.Order);
        Assert.Equal([TestFocus.Nothing, TestFocus.Job], order.GetValues(settings).Cast<TestFocus>());

        settings.Resources = [TestReferences.Ash, TestReferences.Ash];

        Assert.Equal([("Resources", SettingProblemKind.Repeated)], declared.Check(settings).Select(problem => (problem.Setting?.Key, problem.Kind)));
    }

    [Fact]
    public void ASettingRefusesAValueItDoesNotHold()
    {
        var declared = Every();
        var settings = new TestSettings();

        Assert.Throws<ArgumentException>(() => declared.All[0].SetValue(settings, "ten"));
        Assert.Throws<ArgumentException>(() => declared.All[3].SetValue(settings, 1));
        Assert.Throws<ArgumentException>(() => declared.All[6].SetValue(settings, 303));
        Assert.Throws<ArgumentException>(() => declared.All[6].SetValue(settings, new List<string> { "ash" }));

        declared.All[2].SetValue(settings, null);
        declared.All[5].SetValue(settings, null);

        Assert.Equal("", settings.Greeting);
        Assert.Equal("", declared.All[2].GetValue(new TestSettings { Greeting = null }));
    }

    [Fact]
    public void ACopyHoldsTheSameValuesApart()
    {
        var declared = Every();
        var settings = new TestSettings { Walks = 7, Resources = [TestReferences.Ash] };
        var copy = Assert.IsType<TestSettings>(declared.Copy(settings));

        copy.Walks = 8;

        Assert.Equal(7, settings.Walks);
        Assert.Equal([TestReferences.Ash], copy.Resources);
        Assert.NotSame(settings.Resources, copy.Resources);
    }

    [Fact]
    public void AnActivitysRuleOrConditionThatThrowsNeverThrowsInTheApp()
    {
        var declared = Register(settings =>
        {
            settings.Job(s => s.Job, Text("Job")).VisibleWhen(s => s.Greeting!.Length > 0);
            settings.Rule(s => s.Greeting!.Length > 0, Text("A greeting"));
        }).Settings;
        var settings = new TestSettings { Greeting = "Hi" };

        settings.Greeting = null;

        Assert.True(declared.IsVisible(declared.All[0], settings));
        Assert.Equal([SettingProblemKind.Rule], declared.Check(settings).Select(problem => problem.Kind));
    }

    [Fact]
    public void DefaultsThatDoNotHoldStopTheModuleFromRegisteringItsActivity()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Number(s => s.Walks, Text("Walks"), 1, 5)));

        Assert.Equal("The default settings of TestSettings do not hold: Walks (OutOfRange).", exception.Message);
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Text(s => s.Greeting, Text("Greeting"), 3)));
    }

    [Fact]
    public void AValueTheUserMustGiveNeedsNoDefault()
    {
        var declared = Register(settings =>
        {
            settings.Job(s => s.Job, Text("Job")).Required();
            settings.Rule(s => s.Job is not null, Text("Choose a profession."));
        }).Settings;

        Assert.Equal([SettingProblemKind.Required, SettingProblemKind.Rule], declared.Check(declared.CreateDefault()).Select(problem => problem.Kind));
    }

    [Fact]
    public void ASettingIsAPublicPropertyDeclaredOnce()
    {
        Assert.Throws<ArgumentException>(() => Register(settings => settings.Number(s => s.Walks + 1, Text("Walks"), 1, 100)));
        Assert.Throws<ArgumentException>(() => Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Number(s => s.Walks, Text("Walks again"), 1, 100);
        }));
    }

    [Fact]
    public void AValueOfTheGameIsSoughtWithinAParentDeclaredBeforeOfItsKind()
    {
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).Within(s => s.Area)));
        Assert.Throws<InvalidOperationException>(() => Register(settings =>
        {
            settings.Job(s => s.Job, Text("Job"));
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).Within(s => s.Job);
        }));
        Assert.Throws<InvalidOperationException>(() => Register(settings =>
        {
            settings.Area(s => s.Area, Text("Area"));
            settings.Job(s => s.Job, Text("Job")).Within(s => s.Area);
        }));
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Flag(s => s.UseBank, Text("Bank")).Required()));
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Job(s => s.Job, Text("Job")).Ordered()));
    }

    [Fact]
    public void AnActivityReceivesItsSettingsThroughItsConstructor()
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddActivity<SettingsActivity, TestSettings>("test", Text("Test"), settings => settings.Number(s => s.Walks, Text("Walks"), 1, 100));

        using var provider = services.BuildServiceProvider();
        var definition = Assert.Single(provider.GetServices<ActivityDefinition>());
        var settings = new TestSettings { Walks = 3 };
        var activity = Assert.IsType<SettingsActivity>(definition.Create(provider, settings));
        var byDefault = Assert.IsType<SettingsActivity>(definition.Create(provider));

        Assert.Same(settings, activity.Settings);
        Assert.Same(TimeProvider.System, activity.Time);
        Assert.Equal(10, byDefault.Settings.Walks);
        Assert.Throws<ArgumentException>(() => definition.Create(provider, null));
        Assert.Throws<ArgumentException>(() => definition.Create(provider, "settings"));
    }

    [Fact]
    public void AnActivityWithNoSettingDeclaresNone()
    {
        var definition = new ActivityDefinition("test", "Test", _ => new SettingsActivity(new TestSettings(), TimeProvider.System));

        Assert.Same(ActivitySettings.None, definition.Settings);
        Assert.True(definition.Settings.IsEmpty);
        Assert.Null(definition.Settings.CreateDefault());
        Assert.Equal("{}", definition.Settings.Write(null));
        Assert.Null(definition.Settings.Read("""{"Walks":3}""").Settings);
        Assert.Empty(definition.Settings.Check(null));
        Assert.Throws<ArgumentException>(() => definition.Create(new ServiceCollection().BuildServiceProvider(), new TestSettings()));
    }

    [Fact]
    public void AStoredListOfAnotherShapeIsReset()
    {
        var declared = Every();
        var json = new JsonObject { ["Resources"] = 303, ["SubAreas"] = new JsonArray(10, 11) }.ToJsonString();
        var read = Read(declared, json, out var reset);

        Assert.Equal(["Resources"], reset);
        Assert.Empty(read.Resources);
        Assert.Equal([TestReferences.Forest, TestReferences.Hills], read.SubAreas);
    }
}
