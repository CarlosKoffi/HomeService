namespace HomeService.Client.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

#if WINDOWS
        window.Width = 430;
        window.Height = 850;
        window.MinimumWidth = 430;
        window.MinimumHeight = 850;
        window.MaximumWidth = 430;
        window.MaximumHeight = 850;
#endif

        return window;
    }
}
