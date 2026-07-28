namespace BigLocalHub.Views;

/// <summary>
/// Shown while the auth state resolves. Built in code rather than XAML because
/// it is three controls and is constructed before DI hands out any view model.
/// </summary>
public class SplashPage : ContentPage
{
    public SplashPage()
    {
        BackgroundColor = Color.FromArgb("#0D0D0F");
        Content = new VerticalStackLayout
        {
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Big Local Hub",
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F2F2F5"),
                    HorizontalTextAlignment = TextAlignment.Center,
                },
                new ActivityIndicator
                {
                    IsRunning = true,
                    Color = Color.FromArgb("#0F77E6"),
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
        BackgroundColor = Color.FromArgb("#0D0D0F");

        var button = new Button
        {
            Text = "Sign Out",
            BackgroundColor = Color.FromArgb("#1F1F25"),
            TextColor = Color.FromArgb("#F2F2F5"),
            CornerRadius = 10,
            HeightRequest = 46,
        };
        button.Clicked += async (_, _) => await onSignOut();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 28,
                Spacing = 14,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#F2F2F5"),
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    new Label
                    {
                        Text = message,
                        FontSize = 14.5,
                        TextColor = Color.FromArgb("#A0A0AC"),
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    button,
                },
            },
        };
    }
}
