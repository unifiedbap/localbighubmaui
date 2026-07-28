using BigLocalHub.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BigLocalHub.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly SessionService _session;

    public LoginViewModel(SessionService session) => _session = session;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _busy;

    [ObservableProperty]
    private string? _error;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (Busy) return;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Enter your email and password.";
            return;
        }

        Busy = true;
        Error = null;
        try
        {
            await _session.SignInAsync(Email, Password);
            // No navigation here on purpose — App observes SessionChanged and
            // swaps the root page, so the one code path handles sign-in,
            // sign-out, and a session restored at cold start alike.
        }
        catch (Exception ex)
        {
            Error = FriendlyMessage(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Firebase surfaces raw codes like "ERROR_WRONG_PASSWORD". Show something a
    /// person can act on, and never distinguish "no such user" from "wrong
    /// password" — that difference tells an attacker which emails are real.
    /// </summary>
    private static string FriendlyMessage(Exception ex)
    {
        var raw = ex.Message ?? string.Empty;
        if (raw.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("USER_NOT_FOUND", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("INVALID_CREDENTIAL", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("INVALID_EMAIL", StringComparison.OrdinalIgnoreCase))
            return "That email and password don't match an account.";

        if (raw.Contains("NETWORK", StringComparison.OrdinalIgnoreCase))
            return "Can't reach the network. Check your connection and try again.";

        if (raw.Contains("TOO_MANY_REQUESTS", StringComparison.OrdinalIgnoreCase))
            return "Too many attempts. Wait a moment and try again.";

        return "Couldn't sign in. Please try again.";
    }
}
