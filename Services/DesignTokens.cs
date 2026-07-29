namespace BigLocalHub.Services;

/// <summary>
/// C# mirror of the XAML design tokens.
///
/// XAML is the source of truth — this exists only for the handful of pages
/// built in code (SplashPage, NoticePage) that run before or outside a
/// ResourceDictionary lookup. Keep the two in sync: if a value changes in
/// Resources/Styles/Tokens/, change it here too.
/// </summary>
public static class Tokens
{
    public static class Palette
    {
        public static readonly Color PageBg        = Color.FromArgb("#F4F6F8");
        public static readonly Color Surface       = Color.FromArgb("#FFFFFF");
        public static readonly Color Border        = Color.FromArgb("#DFE3E8");
        public static readonly Color BorderStrong  = Color.FromArgb("#C6CCD4");
        public static readonly Color TextPrimary   = Color.FromArgb("#131820");
        public static readonly Color TextSecondary = Color.FromArgb("#55606E");
        public static readonly Color TextTertiary  = Color.FromArgb("#6B7684");
        public static readonly Color Accent        = Color.FromArgb("#0B63C4");
        public static readonly Color AccentTint    = Color.FromArgb("#E8F1FC");
        public static readonly Color Success       = Color.FromArgb("#146B3F");
        public static readonly Color SuccessTint   = Color.FromArgb("#E6F4EC");
        public static readonly Color Warning       = Color.FromArgb("#8A5A00");
        public static readonly Color WarningTint   = Color.FromArgb("#FDF3E1");
        public static readonly Color Danger        = Color.FromArgb("#B3271A");
        public static readonly Color DangerTint    = Color.FromArgb("#FCEBE9");
        public static readonly Color Neutral       = Color.FromArgb("#55606E");
        public static readonly Color NeutralTint   = Color.FromArgb("#EEF1F4");
    }

    public static class Type
    {
        public const double Display = 28;
        public const double Title   = 22;
        public const double Heading = 18;
        public const double Body    = 16;
        public const double Label   = 14;
    }

    public static class Space
    {
        public const double S1 = 4;
        public const double S2 = 8;
        public const double S3 = 12;
        public const double S4 = 16;
        public const double S5 = 20;
        public const double S6 = 24;
        public const double S8 = 32;
    }

    /// <summary>Minimum interactive size, in device-independent points.</summary>
    public const double TouchTarget = 44;
}

/// <summary>
/// Maps a stored status string to its status color pair.
///
/// Centralized so "green means active" holds across every module: Leads, Jobs,
/// and anything added later resolve through this one function rather than each
/// screen inventing its own mapping.
/// </summary>
public enum StatusTone { Neutral, Success, Warning, Danger }

public static class StatusTones
{
    /// <summary>
    /// Lead statuses. Won is done (green), Lost is closed (neutral — a lost
    /// lead is not an error and shouldn't shout red), New needs first contact
    /// (red = needs you), and the middle stages are in-flight (amber).
    /// </summary>
    public static StatusTone ForLead(string status) => status switch
    {
        Models.LeadStatuses.New            => StatusTone.Danger,
        Models.LeadStatuses.Contacted      => StatusTone.Warning,
        Models.LeadStatuses.QuoteScheduled => StatusTone.Warning,
        Models.LeadStatuses.Quoted         => StatusTone.Warning,
        Models.LeadStatuses.Won            => StatusTone.Success,
        Models.LeadStatuses.Lost           => StatusTone.Neutral,
        _                                   => StatusTone.Neutral,
    };

    public static StatusTone ForJob(string status) => status switch
    {
        Models.JobStatuses.InProgress     => StatusTone.Success,
        Models.JobStatuses.Scheduled      => StatusTone.Success,
        Models.JobStatuses.Complete       => StatusTone.Neutral,
        Models.JobStatuses.Cancelled      => StatusTone.Neutral,
        Models.JobStatuses.QuoteScheduled => StatusTone.Warning,
        Models.JobStatuses.Quoted         => StatusTone.Warning,
        _                                  => StatusTone.Neutral,
    };

    /// <summary>Style key for the badge container, for XAML binding.</summary>
    public static string BadgeStyleKey(StatusTone tone) => tone switch
    {
        StatusTone.Success => "BadgeSuccess",
        StatusTone.Warning => "BadgeWarning",
        StatusTone.Danger  => "BadgeDanger",
        _                  => "BadgeNeutral",
    };

    public static Color Ink(StatusTone tone) => tone switch
    {
        StatusTone.Success => Tokens.Palette.Success,
        StatusTone.Warning => Tokens.Palette.Warning,
        StatusTone.Danger  => Tokens.Palette.Danger,
        _                  => Tokens.Palette.Neutral,
    };

    public static Color Tint(StatusTone tone) => tone switch
    {
        StatusTone.Success => Tokens.Palette.SuccessTint,
        StatusTone.Warning => Tokens.Palette.WarningTint,
        StatusTone.Danger  => Tokens.Palette.DangerTint,
        _                  => Tokens.Palette.NeutralTint,
    };
}
