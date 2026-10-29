using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Activities;

namespace D3B.App.ViewModels;

/// <summary>An activity a bot can be given, named in the app's language (D96).</summary>
internal sealed partial class ActivityChoiceViewModel : ObservableObject
{
    /// <summary>Gets the activity, as its module registered it.</summary>
    public ActivityDefinition Definition { get; }

    /// <summary>Gets the activity's name, in the app's language.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="ActivityChoiceViewModel"/> class.</summary>
    /// <param name="definition">The activity.</param>
    /// <param name="culture">The app's language.</param>
    public ActivityChoiceViewModel(ActivityDefinition definition, CultureInfo culture)
    {
        Definition = definition;
        Name = definition.Name.In(culture);
    }

    /// <summary>Names the activity again, in the app's language.</summary>
    /// <param name="culture">The app's language.</param>
    public void Refresh(CultureInfo culture)
    {
        Name = Definition.Name.In(culture);
    }
}
