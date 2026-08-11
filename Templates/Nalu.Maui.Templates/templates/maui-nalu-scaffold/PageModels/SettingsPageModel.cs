using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiNaluApp.PageModels;

// ObservableObject (INotifyPropertyChanged) is REQUIRED even without observable properties:
// the source-generated AddPages() only pairs a page with the model assigned to its
// BindingContext when the model implements INotifyPropertyChanged — a plain class would make
// the generator skip the page and the Settings tab would not resolve.
public partial class SettingsPageModel : ObservableObject
{
    public string AppVersion => AppInfo.Current.VersionString;
}
