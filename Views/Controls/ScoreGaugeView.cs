using Microsoft.Maui.Graphics;

namespace BigLocalHub.Views.Controls;

/// <summary>
/// A semi-circle 0-100 score gauge — 0 at the left, 100 at the right, filled
/// clockwise across the top from left to right as the score rises. One
/// implementation shared by SeoHealthPage (full size) and the Dashboard
/// widget (compact size) via WidthRequest/HeightRequest, rather than two
/// hand-drawn versions of the same shape.
///
/// The arc is built from hand-computed points (basic trig), not
/// ICanvas.DrawArc — DrawArc's angle/winding-direction convention is a
/// frequent source of mirrored or upside-down arcs, and that can only be
/// caught by looking at it. The point math here is simple enough to verify
/// by hand: score 0 must land exactly on the left end of the baseline,
/// score 100 exactly on the right end, and score 50 exactly at the top —
/// each is checked in the comments below.
/// </summary>
public class ScoreGaugeView : GraphicsView
{
    public static readonly BindableProperty ScoreProperty =
        BindableProperty.Create(nameof(Score), typeof(int), typeof(ScoreGaugeView), 0, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(ScoreGaugeView), Colors.LightGray, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty FillColorProperty =
        BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(ScoreGaugeView), Colors.Blue, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty ArcThicknessProperty =
        BindableProperty.Create(nameof(ArcThickness), typeof(float), typeof(ScoreGaugeView), 14f, propertyChanged: OnVisualPropertyChanged);

    /// <summary>0-100. Values outside that range are clamped when drawn.</summary>
    public int Score
    {
        get => (int)GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    /// <summary>The unfilled portion of the arc — always drawn full-width underneath the fill.</summary>
    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    /// <summary>The filled (0..Score) portion of the arc.</summary>
    public Color FillColor
    {
        get => (Color)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    public float ArcThickness
    {
        get => (float)GetValue(ArcThicknessProperty);
        set => SetValue(ArcThicknessProperty, value);
    }

    private readonly GaugeDrawable _drawable = new();

    public ScoreGaugeView()
    {
        Drawable = _drawable;
        SyncDrawable();
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (ScoreGaugeView)bindable;
        view.SyncDrawable();
        view.Invalidate();
    }

    private void SyncDrawable()
    {
        _drawable.Score = Score;
        _drawable.TrackColor = TrackColor;
        _drawable.FillColor = FillColor;
        _drawable.ArcThickness = ArcThickness;
    }

    private sealed class GaugeDrawable : IDrawable
    {
        public int Score { get; set; }
        public Color TrackColor { get; set; } = Colors.LightGray;
        public Color FillColor { get; set; } = Colors.Blue;
        public float ArcThickness { get; set; } = 14f;

        // Points per full 0-100 sweep. 60 is smooth at any size this control
        // is realistically drawn at without wasting cycles on a redraw that
        // happens, at most, once per scan.
        private const int TotalSegments = 60;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var margin = ArcThickness / 2 + 2;
            // The arc occupies the top half of a circle whose diameter fits
            // the available width, capped so it also fits the available
            // height (a semi-circle is exactly half as tall as it is wide).
            var diameter = Math.Min(dirtyRect.Width, dirtyRect.Height * 2) - margin * 2;
            if (diameter <= 0) return;

            var radius = diameter / 2;
            var centerX = dirtyRect.X + dirtyRect.Width / 2;
            // Baseline sits at the bottom of the drawable area (minus
            // margin); the arc bulges upward from there.
            var centerY = dirtyRect.Bottom - margin;

            canvas.StrokeSize = ArcThickness;
            canvas.StrokeLineCap = LineCap.Round;

            canvas.StrokeColor = TrackColor;
            DrawSegment(canvas, centerX, centerY, radius, 0, 100);

            var clamped = Math.Clamp(Score, 0, 100);
            if (clamped > 0)
            {
                canvas.StrokeColor = FillColor;
                DrawSegment(canvas, centerX, centerY, radius, 0, clamped);
            }
        }

        /// <summary>
        /// Traces the arc between two score values (0-100) as a polyline.
        /// angle(score) = 180° - 1.8°·score, in standard math convention
        /// (0° = +x/right, 90° = +y-up/top, 180° = -x/left) — so:
        ///   score 0   → 180° → point (centerX - radius, centerY)      [left end]
        ///   score 50  → 90°  → point (centerX,          centerY - r) [top]
        ///   score 100 → 0°   → point (centerX + radius, centerY)     [right end]
        /// Y is negated because screen coordinates increase downward while
        /// the angle convention above assumes the usual upward-positive Y.
        /// </summary>
        private static void DrawSegment(ICanvas canvas, float centerX, float centerY, float radius, float fromScore, float toScore)
        {
            var startSegment = (int)Math.Round(TotalSegments * fromScore / 100f);
            var endSegment = (int)Math.Round(TotalSegments * toScore / 100f);
            if (endSegment <= startSegment) return;

            (float X, float Y) PointAt(int segment)
            {
                var scoreAtSegment = 100f * segment / TotalSegments;
                var angleDegrees = 180f - 1.8f * scoreAtSegment;
                var angleRadians = angleDegrees * (float)Math.PI / 180f;
                var x = centerX + radius * (float)Math.Cos(angleRadians);
                var y = centerY - radius * (float)Math.Sin(angleRadians);
                return (x, y);
            }

            var path = new PathF();
            var start = PointAt(startSegment);
            path.MoveTo(start.X, start.Y);
            for (var i = startSegment + 1; i <= endSegment; i++)
            {
                var point = PointAt(i);
                path.LineTo(point.X, point.Y);
            }

            canvas.DrawPath(path);
        }
    }
}
