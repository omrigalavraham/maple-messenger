using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MapleMessenger.UI;

/// <summary>Colors, type and small building blocks shared by the window.</summary>
internal static class Ui
{
    public static readonly Color Bg = C("#111113"), Card = C("#1b1b1f"), Line = C("#2c2c33"), Text = C("#f5f5f7"),
        Muted = C("#8e8e96"), Accent = C("#ff8a3d"), Accent2 = C("#ff5b36"), Ink = C("#1f0f05"),
        Green = C("#30d158"), Yellow = C("#ffd60a"), Red = C("#ff453a");

    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public const string Chat = "", Play = "", Stop = "", Plus = "", Minus = "", Shield = "";

    public static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static Color A(Color c, double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), c.R, c.G, c.B);

    public static SolidColorBrush B(Color c, double alpha = 1)
    {
        var b = new SolidColorBrush(A(c, alpha * c.A / 255.0));
        b.Freeze();
        return b;
    }

    public static LinearGradientBrush AccentFill()
    {
        var b = new LinearGradientBrush(Accent, Accent2, new Point(0, 0), new Point(1, 1));
        b.Freeze();
        return b;
    }

    public static TextBlock Label(string text, double size, Color color, FontWeight? weight = null) => new()
    {
        Text = text, FontSize = size, Foreground = B(color), FontWeight = weight ?? FontWeights.Normal,
        VerticalAlignment = VerticalAlignment.Center, FontFamily = Font,
    };

    public static TextBlock Glyph(string glyph, double size, Brush brush) => new()
    {
        Text = glyph, FontFamily = Icons, FontSize = size, Foreground = brush,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };

    public static void To(IAnimatable target, DependencyProperty p, double to, int ms, IEasingFunction? ease = null) =>
        target.BeginAnimation(p, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease ?? Ease });

    public static void Fade(SolidColorBrush b, Color to, int ms = 220) =>
        b.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Ease });

    /// <summary>The app's mark: an orange rounded square with a chat bubble.</summary>
    public static Border Logo(double size, bool glow = true) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.28), Background = AccentFill(),
        Child = Glyph(Chat, size * 0.46, Brushes.White),
        Effect = glow ? new DropShadowEffect { Color = Accent, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.35 } : null,
    };

    public static BitmapSource LogoBitmap(int px)
    {
        var logo = Logo(px, glow: false);
        logo.Measure(new Size(px, px));
        logo.Arrange(new Rect(0, 0, px, px));
        logo.UpdateLayout();
        var bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(logo);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Writes a Windows icon file holding the logo at several sizes (PNG entries).</summary>
    public static void WriteIcon(string path)
    {
        int[] sizes = [16, 24, 32, 48, 64, 256];
        var pngs = sizes.Select(s =>
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(LogoBitmap(s)));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }).ToArray();

        using var w = new BinaryWriter(File.Create(path));
        w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
            w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32); w.Write(pngs[i].Length); w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var png in pngs) w.Write(png);
    }

    /// <summary>Makes an element a button: dims on hover, sinks a little when pressed, clicks on release inside.
    /// With <paramref name="repeat"/> it clicks on press and keeps clicking while held.</summary>
    public static void Clicky(FrameworkElement e, Action click, bool repeat = false)
    {
        e.Cursor = Cursors.Hand;
        var scale = new ScaleTransform(1, 1);
        e.RenderTransform = scale;
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        DispatcherTimer? again = null;

        void Press(bool down)
        {
            To(scale, ScaleTransform.ScaleXProperty, down ? 0.96 : 1, down ? 90 : 180);
            To(scale, ScaleTransform.ScaleYProperty, down ? 0.96 : 1, down ? 90 : 180);
        }
        void StopRepeat() { again?.Stop(); again = null; }

        e.MouseEnter += (_, _) => To(e, UIElement.OpacityProperty, 0.86, 120);
        e.MouseLeave += (_, _) => To(e, UIElement.OpacityProperty, 1, 180);
        e.MouseLeftButtonDown += (_, a) =>
        {
            a.Handled = true;
            e.CaptureMouse();
            Press(true);
            if (!repeat) return;
            click();
            again = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
            again.Tick += (_, _) => { again!.Interval = TimeSpan.FromMilliseconds(70); click(); };
            again.Start();
        };
        e.MouseLeftButtonUp += (_, a) =>
        {
            a.Handled = true;
            var p = a.GetPosition(e);
            bool inside = p.X >= 0 && p.Y >= 0 && p.X <= e.ActualWidth && p.Y <= e.ActualHeight;
            e.ReleaseMouseCapture();
            StopRepeat();
            Press(false);
            if (!repeat && inside) click();
        };
        e.LostMouseCapture += (_, _) => { StopRepeat(); Press(false); };
    }
}

/// <summary>An on/off switch. Always left-to-right: on is the knob on the right, in orange.</summary>
internal sealed class Switch : Border
{
    private readonly TranslateTransform _x = new();
    private readonly SolidColorBrush _bg = new(Ui.Line);

    public bool On { get; private set; }
    public event Action<bool>? Toggled;

    public Switch(bool on)
    {
        Width = 46; Height = 28; CornerRadius = new CornerRadius(14); Background = _bg;
        FlowDirection = FlowDirection.LeftToRight; Cursor = Cursors.Hand; VerticalAlignment = VerticalAlignment.Center;
        Child = new Border
        {
            Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 0, 0), RenderTransform = _x,
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.3 },
        };
        Set(on, animate: false);
        MouseLeftButtonUp += (_, _) => { Set(!On, animate: true); Toggled?.Invoke(On); };
    }

    public void Set(bool on, bool animate)
    {
        On = on;
        double x = on ? 18 : 0;
        var c = on ? Ui.Accent : Ui.Line;
        if (animate)
        {
            Ui.To(_x, TranslateTransform.XProperty, x, 260, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut });
            Ui.Fade(_bg, c);
        }
        else
        {
            _x.BeginAnimation(TranslateTransform.XProperty, null); _x.X = x;
            _bg.BeginAnimation(SolidColorBrush.ColorProperty, null); _bg.Color = c;
        }
    }
}

internal enum CardState { Idle, Next, Typing }

/// <summary>One message: its text box, a character count, and a bar that fills until it is typed.</summary>
internal sealed class MessageCard : Border
{
    public readonly TextBox Box;
    private readonly TextBlock _title, _count, _hint;
    private readonly SolidColorBrush _edge = new(Ui.Line), _titleBrush = new(Ui.Muted);
    private readonly ScaleTransform _fill = new(0, 1);
    private readonly DropShadowEffect _glow = new() { Color = Ui.Accent, BlurRadius = 24, ShadowDepth = 0, Opacity = 0 };
    private CardState _state = CardState.Idle;

    public MessageCard(int number, string text, string placeholder)
    {
        CornerRadius = new CornerRadius(14); Background = Ui.B(Ui.Card); BorderBrush = _edge;
        BorderThickness = new Thickness(1.5); Padding = new Thickness(16, 12, 16, 14); Effect = _glow;

        _title = Ui.Label($"הודעה {number}", 12.5, Ui.Muted, FontWeights.SemiBold);
        _title.Foreground = _titleBrush;
        _title.HorizontalAlignment = HorizontalAlignment.Left;
        _count = Ui.Label("", 12, Ui.Muted);
        _count.HorizontalAlignment = HorizontalAlignment.Right;
        var head = new Grid();
        head.Children.Add(_title);
        head.Children.Add(_count);

        Box = new TextBox
        {
            Text = text, FlowDirection = FlowDirection.LeftToRight, Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), Foreground = Ui.B(Ui.Text), CaretBrush = Ui.B(Ui.Accent),
            SelectionBrush = Ui.B(Ui.Accent), FontSize = 15.5, FontFamily = Ui.Font, TextWrapping = TextWrapping.Wrap,
            MaxLength = 120, Padding = new Thickness(0), Margin = new Thickness(-2, 8, -2, 0), MinHeight = 44,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        _hint = Ui.Label(placeholder, 15.5, Ui.Muted);
        _hint.FlowDirection = FlowDirection.LeftToRight;
        _hint.Opacity = 0.5;
        _hint.IsHitTestVisible = false;
        _hint.TextWrapping = TextWrapping.Wrap;
        _hint.VerticalAlignment = VerticalAlignment.Top;
        _hint.Margin = new Thickness(0, 8, 0, 0);
        var field = new Grid();
        field.Children.Add(_hint);
        field.Children.Add(Box);

        // the bar grows from the right, the way Hebrew reads
        var bar = new Border
        {
            Height = 3, CornerRadius = new CornerRadius(1.5), Background = Ui.AccentFill(),
            RenderTransform = _fill, RenderTransformOrigin = new Point(0, 0.5),
        };
        var track = new Border
        {
            Height = 3, CornerRadius = new CornerRadius(1.5), Background = Ui.B(Ui.Line),
            Margin = new Thickness(0, 10, 0, 0), Child = bar, ClipToBounds = true,
        };

        var stack = new StackPanel();
        stack.Children.Add(head);
        stack.Children.Add(field);
        stack.Children.Add(track);
        Child = stack;

        Box.TextChanged += (_, _) => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        _hint.Visibility = Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _count.Text = Box.Text.Length == 0 ? "" : Box.Text.Length.ToString();
    }

    public void Show(CardState state, double progress)
    {
        if (state != _state)
        {
            _state = state;
            Ui.Fade(_edge, state switch { CardState.Typing => Ui.Accent, CardState.Next => Ui.A(Ui.Accent, 0.45), _ => Ui.Line });
            Ui.Fade(_titleBrush, state == CardState.Idle ? Ui.Muted : Ui.Accent);
            Ui.To(_glow, DropShadowEffect.OpacityProperty, state == CardState.Typing ? 0.5 : 0, 260);
            if (state != CardState.Next) Ui.To(_fill, ScaleTransform.ScaleXProperty, state == CardState.Typing ? 1 : 0, 260);
        }
        if (state == CardState.Next)
        {
            _fill.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _fill.ScaleX = progress;
        }
    }
}
