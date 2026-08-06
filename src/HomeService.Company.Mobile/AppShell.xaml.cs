using HomeService.Company.Mobile.Pages;

namespace HomeService.Company.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(MissionDetailPage), typeof(MissionDetailPage));
    }
}
