using BigLocalHub.Services;

namespace BigLocalHub.Views;

/// <summary>
/// Shown while the auth state resolves. Built in code rather than XAML because
/// it is constructed before any ResourceDictionary lookup is available, which
/// is why it reads the C# token mirror (Tokens) instead of StaticResource.
/// </summary>
public class SplashPage : ContentPage
{
    public SplashPage()
    {
        BackgroundColor = Tokens.Palette.PageBg;
        Content = new VerticalStackLayout
        {
            Spacing = Tokens.Space.S4,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Big Local Hub",
                    FontSize = Tokens.Type.Title,
                    FontAttributes = FontAttributes.Bold,
                    FontAutoScalingEnabled = true,
                    TextColor = Tokens.Palette.TextPrimary,
                    HorizontalTextAlignment = TextAlignment.Center,
                },
                new ActivityIndicator
                {
                    IsRunning = true,
                    Color = Tokens.Palette.Accent,
                },
            },
        };
    }
}

/// <summary>
/// Terminal state with one way out — used for accounts the mobile hub can't
/// serve (no company, admin-only, unreadable profile). Always offers sign-out
/// so the user is never stuck on a dead end.
/// </summary>
public class NoticePage : ContentPage
{
    public NoticePage(string title, string message, Func<Task> onSignOut)
    {
        BackgroundColor = Tokens.Palette.PageBg;

        var button = new Button
        {
            Text = "Sign Out",
            BackgroundColor = Tokens.Palette.Surface,
            TextColor = Tokens.Palette.Accent,
            BorderColor = Tokens.Palette.BorderStrong,
            BorderWidth = 1,
            FontSize = Tokens.Type.Body,
            FontAttributes = FontAttributes.Bold,
            FontAutoScalingEnabled = true,
            CornerRadius = 16, // matches ButtonSecondary in Components.xaml
            MinimumHeightRequest = Tokens.TouchTarget,
            HeightRequest = 50,
        };
        button.Clicked += async (_, _) => await onSignOut();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24),
                Spacing = Tokens.Space.S4,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = Tokens.Type.Title,
                        FontAttributes = FontAttributes.Bold,
                        FontAutoScalingEnabled = true,
                        TextColor = Tokens.Palette.TextPrimary,
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    new Label
                    {
                        Text = message,
                        FontSize = Tokens.Type.Body,
                        FontAutoScalingEnabled = true,
                        TextColor = Tokens.Palette.TextSecondary,
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    button,
                },
            },
        };
    }
}
