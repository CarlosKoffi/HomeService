using Android.App;
using Android.Graphics;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.BottomNavigation;
using AndroidView = Android.Views.View;

namespace HomeService.Company.Mobile;

internal static class BottomNavigationTypography
{
    private const float LabelSizeSp = 10f;
    private static Typeface? plusJakartaSans;

    public static void Apply(Activity activity)
    {
        var root = activity.Window?.DecorView;
        if (root is null) return;

        root.Post(() => ApplyToTree(activity, root));
        root.PostDelayed(() => ApplyToTree(activity, root), 250);
        root.PostDelayed(() => ApplyToTree(activity, root), 1000);
    }

    private static void ApplyToTree(Activity activity, AndroidView view)
    {
        if (view is BottomNavigationView bottomNavigation)
        {
            bottomNavigation.ItemIconSize = Dp(activity, 23);
            ApplyToLabels(activity, bottomNavigation);
            return;
        }

        if (view is not ViewGroup group) return;
        for (var index = 0; index < group.ChildCount; index++)
        {
            var child = group.GetChildAt(index);
            if (child is not null) ApplyToTree(activity, child);
        }
    }

    private static void ApplyToLabels(Activity activity, AndroidView view)
    {
        if (view is TextView label)
        {
            label.SetTextSize(ComplexUnitType.Sp, LabelSizeSp);
            label.SetTypeface(GetTypeface(activity), TypefaceStyle.Normal);
            label.SetSingleLine(true);
            label.SetIncludeFontPadding(false);
        }

        if (view is not ViewGroup group) return;
        for (var index = 0; index < group.ChildCount; index++)
        {
            var child = group.GetChildAt(index);
            if (child is not null) ApplyToLabels(activity, child);
        }
    }

    private static Typeface GetTypeface(Activity activity)
    {
        if (plusJakartaSans is not null) return plusJakartaSans;
        try
        {
            plusJakartaSans = Typeface.CreateFromAsset(activity.Assets, "PlusJakartaSans-Variable.ttf");
        }
        catch
        {
            plusJakartaSans = Typeface.Create("sans-serif", TypefaceStyle.Normal);
        }

        return plusJakartaSans!;
    }

    private static int Dp(Activity activity, int value)
        => (int)Math.Round(value * activity.Resources!.DisplayMetrics!.Density);
}
