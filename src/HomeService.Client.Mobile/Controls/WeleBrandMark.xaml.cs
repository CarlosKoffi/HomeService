namespace HomeService.Client.Mobile.Controls;

public partial class WeleBrandMark : ContentView
{
    public static readonly BindableProperty MarkHeightProperty = BindableProperty.Create(
        nameof(MarkHeight), typeof(double), typeof(WeleBrandMark), 30d);

    public static readonly BindableProperty MarkAlignmentProperty = BindableProperty.Create(
        nameof(MarkAlignment), typeof(LayoutOptions), typeof(WeleBrandMark), LayoutOptions.Start);

    public WeleBrandMark() => InitializeComponent();

    public double MarkHeight
    {
        get => (double)GetValue(MarkHeightProperty);
        set => SetValue(MarkHeightProperty, value);
    }

    public LayoutOptions MarkAlignment
    {
        get => (LayoutOptions)GetValue(MarkAlignmentProperty);
        set => SetValue(MarkAlignmentProperty, value);
    }
}
