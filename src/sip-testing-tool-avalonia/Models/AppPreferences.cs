using CommunityToolkit.Mvvm.ComponentModel;

namespace Clc.Sip.Desktop.Models;

public partial class AppPreferences : ObservableObject
{
    [ObservableProperty] private SessionFields defaults = new();
    [ObservableProperty] private bool connectOnStartup;
    [ObservableProperty] private bool darkTheme;
    [ObservableProperty] private string encodingName = "UTF-8";
    [ObservableProperty] private bool validateChecksums = true;
    [ObservableProperty] private bool checkUpdatesOnStartup = true;
    [ObservableProperty] private string updateFeedUrl = "https://siptool.clcohio.org/updates/desktop/";
}
