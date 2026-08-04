namespace HomeService.Client.Mobile.Controls;

public sealed class StarRatingView : VerticalStackLayout
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(StarRatingView),
        string.Empty,
        propertyChanged: OnTitleChanged);

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(int),
        typeof(StarRatingView),
        0,
        BindingMode.TwoWay,
        coerceValue: (_, value) => Math.Clamp((int)value, 0, 5),
        propertyChanged: OnValueChanged);

    private readonly Label titleLabel;
    private readonly Label valueLabel;
    private readonly IReadOnlyList<Button> starButtons;

    public StarRatingView()
    {
        Spacing = 4;

        titleLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        valueLabel = new Label
        {
            Text = "À noter",
            FontSize = 11,
            TextColor = Color.FromArgb("#6B7280"),
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        };

        var heading = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        heading.Add(titleLabel);
        heading.Add(valueLabel, 1);

        var stars = new HorizontalStackLayout
        {
            Spacing = 3,
            HorizontalOptions = LayoutOptions.Start
        };
        var buttons = new List<Button>(5);
        for (var rating = 1; rating <= 5; rating++)
        {
            var button = new Button
            {
                Text = "★",
                FontSize = 29,
                FontAttributes = FontAttributes.None,
                TextColor = Color.FromArgb("#CDD5DF"),
                BackgroundColor = Colors.Transparent,
                BorderWidth = 0,
                CornerRadius = 0,
                Padding = 0,
                WidthRequest = 42,
                HeightRequest = 42,
                MinimumHeightRequest = 42,
                CommandParameter = rating
            };
            button.Clicked += OnStarClicked;
            buttons.Add(button);
            stars.Add(button);
        }

        starButtons = buttons;
        Children.Add(heading);
        Children.Add(stars);
        Refresh();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private void OnStarClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: int rating })
        {
            Value = rating;
        }
    }

    private static void OnTitleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((StarRatingView)bindable).Refresh();
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((StarRatingView)bindable).Refresh();
    }

    private void Refresh()
    {
        titleLabel.Text = Title;
        valueLabel.Text = Value == 0 ? "À noter" : $"{Value}/5";

        for (var index = 0; index < starButtons.Count; index++)
        {
            var rating = index + 1;
            var button = starButtons[index];
            button.TextColor = rating <= Value
                ? Color.FromArgb("#155EEF")
                : Color.FromArgb("#CDD5DF");
            SemanticProperties.SetDescription(
                button,
                $"{Title}, {rating} {(rating == 1 ? "étoile" : "étoiles")}");
        }
    }
}
