using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MapleMessenger.Core;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace MapleMessenger.UI;

internal sealed class MainWindow : Window
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int HotkeyId = 0x4D4D, WmHotkey = 0x0312;

    /// <summary>What the window shows. Comes from the messenger, or is made up for screenshots.</summary>
    private readonly record struct View(bool Running, Phase Phase, int Slot, double Progress, int SecondsLeft, int Sent, string Missing);

    private readonly Settings _s;
    private readonly Messenger _m;
    private readonly string? _snapPath, _demo;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _saveSoon = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private HwndSource? _source;
    private string? _registered; // the hotkey we hold, or null when another program has it

    private readonly MessageCard[] _cards = new MessageCard[2];
    private readonly SolidColorBrush _pillBg = new(Ui.Line), _pillFg = new(Ui.Muted), _dotFill = new(Ui.Muted);
    private readonly SolidColorBrush _warnBg = new(Ui.Line), _warnFg = new(Ui.Text);
    private readonly List<(string Key, SolidColorBrush Bg, SolidColorBrush Fg)> _keys = [];
    private TextBlock _pillText = null!, _interval = null!, _warnText = null!, _goText = null!, _goIcon = null!, _hint = null!, _sent = null!;
    private Ellipse _dot = null!;
    private Border _warn = null!, _warnAction = null!, _go = null!;
    private Color _pillColor = Ui.Muted;
    private string? _shownKey;
    private bool _pulsing, _goRunning;

    public MainWindow(string[] args)
    {
        _snapPath = Arg(args, "--snap");
        _demo = Arg(args, "--demo");
        _s = _snapPath != null ? DemoSettings() : Settings.Load(Settings.DefaultPath);
        _m = new Messenger(() => _s);

        Title = "Maple Messenger";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.B(Ui.Bg);
        UseLayoutRounding = true;
        FontFamily = Ui.Font;
        Icon = Ui.LogoBitmap(64);
        Content = Build();

        _m.Changed += Render;
        _tick.Tick += (_, _) => Render();
        _saveSoon.Tick += (_, _) => { _saveSoon.Stop(); Save(); };
        SourceInitialized += (_, _) => OnHandle();
        Closing += (_, _) => { _m.Stop(); Save(); if (_source != null) UnregisterHotKey(_source.Handle, HotkeyId); };

        if (_snapPath != null)
        {
            // render off screen without taking focus, save a picture, and quit
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -30000; Top = 0; ShowActivated = false; ShowInTaskbar = false;
            _registered = _s.Hotkey;
            ContentRendered += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => { Snap(_snapPath); Close(); });
        }
        Render();
    }

    private static string? Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // ---------- building ----------

    private Border Build()
    {
        var stack = new StackPanel();
        stack.Children.Add(Header());
        string[] examples = ["S> Ilbi 28m | Chaos Scroll 60% 9m @ FM7", "B> Dark Scroll 30% - PM me!"];
        for (int i = 0; i < 2; i++)
        {
            int k = i;
            var card = _cards[i] = new MessageCard(i + 1, _s.Messages[i], examples[i]) { Margin = new Thickness(0, i == 0 ? 18 : 10, 0, 0) };
            card.Box.TextChanged += (_, _) => { _s.Messages[k] = card.Box.Text; SaveSoon(); Render(); };
            stack.Children.Add(card);
        }
        stack.Children.Add(Options());
        stack.Children.Add(Warning());
        stack.Children.Add(GoButton());
        stack.Children.Add(Footer());
        return new Border
        {
            Background = Ui.B(Ui.Bg), Padding = new Thickness(20, 18, 20, 16),
            FlowDirection = FlowDirection.RightToLeft, Child = stack,
        };
    }

    private UIElement Header()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var logo = Ui.Logo(42);
        g.Children.Add(logo);

        var titles = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var name = Ui.Label("Maple Messenger", 19, Ui.Text, FontWeights.Bold);
        name.FontFamily = Ui.Display;
        titles.Children.Add(name);
        titles.Children.Add(Ui.Label("שתי הודעות לשוק, בתורות", 12.5, Ui.Muted));
        Grid.SetColumn(titles, 1);
        g.Children.Add(titles);

        _dot = new Ellipse { Width = 7, Height = 7, Fill = _dotFill, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        _pillText = Ui.Label("", 12.5, Ui.Muted, FontWeights.SemiBold);
        _pillText.Foreground = _pillFg;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_dot);
        row.Children.Add(_pillText);
        var pill = new Border
        {
            CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 5, 11, 6), Background = _pillBg,
            VerticalAlignment = VerticalAlignment.Center, Child = row,
        };
        Grid.SetColumn(pill, 2);
        g.Children.Add(pill);
        return g;
    }

    private UIElement Options()
    {
        var stack = new StackPanel();
        stack.Children.Add(OptionRow("כל כמה זמן", "מתחילת הודעה אחת לתחילת הבאה", Stepper()));
        stack.Children.Add(Divider());
        var background = new Switch(_s.Background);
        background.Toggled += on => { _s.Background = on; SaveSoon(); Render(); };
        stack.Children.Add(OptionRow("הקלדה ברקע", "ממשיך להקליד במייפל גם כשאתה בחלון אחר. אם לא מופיע כלום בצ'אט, לכבות", background));
        stack.Children.Add(Divider());
        stack.Children.Add(OptionRow("מקש הפעלה", "עובד גם מתוך המשחק", KeyPicker()));
        stack.Children.Add(Divider());
        var enter = new Switch(_s.EnterFirst);
        enter.Toggled += on => { _s.EnterFirst = on; SaveSoon(); };
        stack.Children.Add(OptionRow("פתיחת הצ'אט עם Enter", "לכבות אם הצ'אט נשאר פתוח אחרי שליחה", enter));
        return new Border
        {
            CornerRadius = new CornerRadius(14), Background = Ui.B(Ui.Card), Padding = new Thickness(16, 2, 16, 2),
            Margin = new Thickness(0, 10, 0, 0), Child = stack,
        };
    }

    private static UIElement OptionRow(string title, string sub, UIElement control)
    {
        var g = new Grid { MinHeight = 58 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 12, 8) };
        text.Children.Add(Ui.Label(title, 14, Ui.Text, FontWeights.SemiBold));
        var s = Ui.Label(sub, 11.5, Ui.Muted);
        s.Margin = new Thickness(0, 1, 0, 0);
        s.TextWrapping = TextWrapping.Wrap;
        text.Children.Add(s);
        g.Children.Add(text);
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        return g;
    }

    private static Border Divider() => new() { Height = 1, Background = Ui.B(Ui.Line, 0.8) };

    private UIElement Stepper()
    {
        Border Round(string glyph) => new()
        {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Ui.B(Ui.Line),
            Child = Ui.Glyph(glyph, 11, Ui.B(Ui.Text)),
        };
        var plus = Round(Ui.Plus);
        var minus = Round(Ui.Minus);
        Ui.Clicky(plus, () => Step(+1), repeat: true);
        Ui.Clicky(minus, () => Step(-1), repeat: true);
        _interval = Ui.Label("", 14, Ui.Text, FontWeights.SemiBold);
        _interval.MinWidth = 76;
        _interval.TextAlignment = TextAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(plus);
        row.Children.Add(_interval);
        row.Children.Add(minus);
        return row;
    }

    private void Step(int dir)
    {
        int n = _s.IntervalSeconds;
        int step = dir > 0 ? (n < 30 ? 1 : n < 120 ? 5 : 30) : (n <= 30 ? 1 : n <= 120 ? 5 : 30);        _s.IntervalSeconds = Math.Clamp(n + dir * step, Settings.MinInterval, Settings.MaxInterval);
        SaveSoon();
        Render();
    }

    private UIElement KeyPicker()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center };
        foreach (var key in Settings.Hotkeys)
        {
            var bg = new SolidColorBrush(Ui.Line);
            var fg = new SolidColorBrush(Ui.Muted);
            var text = Ui.Label(key, 12, Ui.Muted, FontWeights.SemiBold);
            text.Foreground = fg;
            var pill = new Border
            {
                CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 4, 7, 5), Background = bg,
                Margin = new Thickness(row.Children.Count == 0 ? 0 : 4, 0, 0, 0), Child = text,
            };
            Ui.Clicky(pill, () => { _s.Hotkey = key; RegisterHotkey(); SaveSoon(); Render(); });
            _keys.Add((key, bg, fg));
            row.Children.Add(pill);
        }
        return row;
    }

    private UIElement Warning()
    {
        _warnText = Ui.Label("", 12.5, Ui.Text);
        _warnText.Foreground = _warnFg;
        _warnText.TextWrapping = TextWrapping.Wrap;
        _warnText.LineHeight = 19;
        var actionText = Ui.Label("הפעלה כמנהל", 12, Ui.Text, FontWeights.SemiBold);
        _warnAction = new Border
        {
            CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 6, 10, 7), Background = Ui.B(Ui.Red),
            Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = actionText,
        };
        Ui.Clicky(_warnAction, RelaunchAsAdmin);
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(_warnText);
        Grid.SetColumn(_warnAction, 1);
        g.Children.Add(_warnAction);
        return _warn = new Border
        {
            CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 11), Background = _warnBg,
            Margin = new Thickness(0, 10, 0, 0), Child = g, Visibility = Visibility.Collapsed,
        };
    }

    private UIElement GoButton()
    {
        _goIcon = Ui.Glyph(Ui.Play, 13, Ui.B(Ui.Ink));
        _goText = Ui.Label("התחלה", 16.5, Ui.Ink, FontWeights.SemiBold);
        _goText.Margin = new Thickness(9, 0, 0, 1);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_goIcon);
        row.Children.Add(_goText);
        _go = new Border { Height = 54, CornerRadius = new CornerRadius(15), Margin = new Thickness(0, 14, 0, 0), Background = Ui.AccentFill(), Child = row };
        Ui.Clicky(_go, Toggle);
        return _go;
    }

    private UIElement Footer()
    {
        var g = new Grid { Margin = new Thickness(2, 10, 2, 0) };
        _hint = Ui.Label("", 12, Ui.Muted);
        _hint.HorizontalAlignment = HorizontalAlignment.Left;
        _sent = Ui.Label("", 12, Ui.Muted);
        _sent.HorizontalAlignment = HorizontalAlignment.Right;
        g.Children.Add(_hint);
        g.Children.Add(_sent);
        return g;
    }

    // ---------- behaviour ----------

    private void Toggle()
    {
        if (_m.Running) _m.Stop(); else _m.Start();
        _tick.IsEnabled = _m.Running;
        Render();
    }

    private void OnHandle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1, caption = Ui.Bg.R | Ui.Bg.G << 8 | Ui.Bg.B << 16;
        DwmSetWindowAttribute(hwnd, 20, ref dark, 4);    // dark title bar
        DwmSetWindowAttribute(hwnd, 35, ref caption, 4); // title bar in the window's color (Windows 11)
        if (_snapPath != null) return;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(WndProc);
        RegisterHotkey();
        if (_registered == null)
        {
            // just relaunched as admin: the old copy may still be letting go of the key
            var retry = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            retry.Tick += (_, _) => { retry.Stop(); RegisterHotkey(); };
            retry.Start();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId) { Toggle(); handled = true; }
        return IntPtr.Zero;
    }

    private void RegisterHotkey()
    {
        if (_source == null) { Render(); return; }
        UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = RegisterHotKey(_source.Handle, HotkeyId, 0x4000 /* no auto-repeat */, (uint)Settings.HotkeyVk(_s.Hotkey)) ? _s.Hotkey : null;
        Render();
    }

    private void RelaunchAsAdmin()
    {
        Save();
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch (Win32Exception) { } // the user said no to the Windows prompt
    }

    private void SaveSoon() { if (_snapPath == null) { _saveSoon.Stop(); _saveSoon.Start(); } }

    private void Save()
    {
        if (_snapPath != null) return;
        try { _s.Save(Settings.DefaultPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ---------- showing ----------

    private View Current() => _demo switch
    {
        null => new View(_m.Running, _m.Phase, _m.Slot, _m.Progress, _m.SecondsLeft, _m.Sent, _m.Missing),
        "typing" => new View(true, Phase.Typing, 0, 0, 0, 13, ""),
        "countdown" => new View(true, Phase.Countdown, 1, 0.62, 4, 14, ""),
        "waiting" => new View(true, Phase.WaitingForGame, 0, 0, 0, 0, ""),
        "admin" => new View(true, Phase.NeedsAdmin, 0, 0, 0, 0, ""),
        "lang" => new View(true, Phase.WrongLanguage, 0, 0, 0, 0, "שמוע"),
        _ => new View(false, Phase.Off, -1, 0, 0, 0, ""),
    };

    private void Render()
    {
        if (_go == null) return;
        var v = Current();

        // status pill
        var (text, color, pulse) = !v.Running ? ("כבוי", Ui.Muted, false) : v.Phase switch
        {
            Phase.Typing => ("מקליד", Ui.Accent, true),
            Phase.Countdown => ($"הבאה בעוד {v.SecondsLeft}", Ui.Green, false),
            Phase.WaitingForGame => ("ממתין למייפל", Ui.Yellow, true),
            Phase.NeedsAdmin => ("צריך הרשאה", Ui.Red, false),
            Phase.WrongLanguage => ("שפת מקלדת", Ui.Red, false),
            Phase.NoText => ("אין מה לשלוח", Ui.Yellow, false),
            _ => ("מתחיל", Ui.Accent, true),
        };
        _pillText.Text = text;
        if (color != _pillColor)
        {
            _pillColor = color;
            Ui.Fade(_pillBg, color == Ui.Muted ? Ui.Line : Ui.A(color, 0.16));
            Ui.Fade(_pillFg, color == Ui.Muted ? Ui.Muted : color);
            Ui.Fade(_dotFill, color);
        }
        if (pulse != _pulsing)
        {
            _pulsing = pulse;
            _dot.BeginAnimation(OpacityProperty, pulse
                ? new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(650)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
                : null);
        }

        // the cards: the one being typed glows, the next one fills up
        for (int i = 0; i < 2; i++)
        {
            var state = !v.Running || v.Slot != i ? CardState.Idle : v.Phase == Phase.Typing ? CardState.Typing : CardState.Next;
            _cards[i].Show(state, v.Phase == Phase.Countdown ? v.Progress : 0);
        }

        _interval.Text = _s.IntervalSeconds == 1 ? "שנייה" : $"{_s.IntervalSeconds} שניות";
        if (_shownKey != _s.Hotkey)
        {
            _shownKey = _s.Hotkey;
            foreach (var (key, bg, fg) in _keys)
            {
                bool on = key == _s.Hotkey;
                Ui.Fade(bg, on ? Ui.Accent : Ui.Line, 160);
                Ui.Fade(fg, on ? Ui.Ink : Ui.Muted, 160);
            }
        }

        // a warning or a tip, when something needs the player
        string? missing = v.Missing.Length > 0 ? string.Join(" ", v.Missing.ToCharArray()) : null;
        var (warn, tone, action) = !v.Running
            ? (_registered == null && _source != null ? $"המקש {_s.Hotkey} תפוס בתוכנה אחרת. בחר מקש אחר." : null, Ui.Yellow, false)
            : v.Phase switch
            {
                Phase.WaitingForGame => (_s.Background ? "פתח את מייפל, וההקלדה תתחיל לבד." : "עבור לחלון של מייפל, וההקלדה תתחיל לבד.", Ui.Muted, false),
                Phase.NeedsAdmin => ("מייפל רץ כמנהל, ולכן Windows לא נותן לתוכנה אחרת להקליד בו. צריך להפעיל אותי מחדש כמנהל.", Ui.Red, true),
                Phase.WrongLanguage => ($"המקלדת במשחק לא יכולה להקליד את: {missing}\nאם המקלדת בעברית, עבור לאנגלית (Alt+Shift). אם אלה סימנים מיוחדים, מחק אותם.", Ui.Yellow, false),
                Phase.NoText => ("כתוב לפחות הודעה אחת.", Ui.Yellow, false),
                _ => ((string?)null, Ui.Muted, false),
            };
        _warn.Visibility = warn == null ? Visibility.Collapsed : Visibility.Visible;
        if (warn != null)
        {
            _warnText.Text = warn;
            _warnBg.Color = tone == Ui.Muted ? Ui.Line : Ui.A(tone, 0.13);
            _warnFg.Color = tone == Ui.Muted ? Ui.Muted : Ui.A(Ui.Text, 0.92);
            _warnAction.Visibility = action ? Visibility.Visible : Visibility.Collapsed;
        }

        // start / stop
        if (v.Running != _goRunning || _goText.Text.Length == 0)
        {
            _goRunning = v.Running;
            _go.Background = v.Running ? Ui.B(Ui.Red, 0.16) : Ui.AccentFill();
            var ink = v.Running ? Ui.B(Ui.Red) : Ui.B(Ui.Ink);
            _goIcon.Text = v.Running ? Ui.Stop : Ui.Play;
            _goIcon.Foreground = ink;
            _goText.Text = v.Running ? "עצירה" : "התחלה";
            _goText.Foreground = ink;
        }

        _hint.Text = _registered == _s.Hotkey ? $"או {_s.Hotkey} מתוך המשחק" : $"המקש {_s.Hotkey} לא זמין";
        _sent.Text = v.Sent > 0 ? $"נשלחו {v.Sent}" : "";
    }

    // ---------- screenshots ----------

    private static Settings DemoSettings() => new Settings
    {
        Messages = ["S> Ilbi 28m | Chaos Scroll 60% 9m @ FM7 ch3", "B> Dark Scroll 30% - PM me!"],
        IntervalSeconds = 10,
    }.Normalize();

    private void Snap(string path)
    {
        var root = (FrameworkElement)Content;
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX), h = (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // exactly the window's area: the cards' glow reaches past it and would otherwise shrink the picture
            var area = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
            var brush = new VisualBrush(root)
            {
                Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = area,
                ViewportUnits = BrushMappingMode.Absolute, Viewport = area,
            };
            dc.DrawRectangle(brush, null, area);
        }
        var bmp = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(path);
        enc.Save(f);
    }
}
