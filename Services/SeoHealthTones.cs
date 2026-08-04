namespace BigLocalHub.Services;

/// <summary>
/// Maps a computeSeoHealthScore scoreLabel/impact string to a StatusTone.
/// Shared by SeoHealthViewModel and the Dashboard SEO widget so "Good" means
/// the same green in both places rather than two independent switch statements
/// drifting apart.
/// </summary>
public static class SeoHealthTones
{
    public static StatusTone ForScoreLabel(string label) => label switch
    {
        "Excellent"  => StatusTone.Success,
        "Good"       => StatusTone.Success,
        "Needs Work" => StatusTone.Warning,
        "Poor"       => StatusTone.Danger,
        _            => StatusTone.Neutral,
    };

    public static StatusTone ForImpact(string impact) => impact switch
    {
        Models.SeoImpacts.High   => StatusTone.Danger,
        Models.SeoImpacts.Medium => StatusTone.Warning,
        Models.SeoImpacts.Low    => StatusTone.Neutral,
        _                        => StatusTone.Neutral,
    };
}
