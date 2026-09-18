using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinPoint = global::Windows.Foundation.Point;
using WinButton = Microsoft.UI.Xaml.Controls.Button;
using WinCorner = Microsoft.UI.Xaml.CornerRadius;
using WinGrid = Microsoft.UI.Xaml.Controls.Grid;
using WinThickness = Microsoft.UI.Xaml.Thickness;
using WinAlign = Microsoft.UI.Xaml.HorizontalAlignment;
using WinVAlign = Microsoft.UI.Xaml.VerticalAlignment;
using WinColor = Windows.UI.Color;
using WinBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinText = Microsoft.UI.Xaml.Controls.TextBlock;
using WinStack = Microsoft.UI.Xaml.Controls.StackPanel;
using WinBorder = Microsoft.UI.Xaml.Controls.Border;
using WinScroll = Microsoft.UI.Xaml.Controls.ScrollViewer;

namespace Flexispace.Mobile.Platforms.Windows;

/// <summary>
/// Floating assistant that lives on the WinUI window, not inside a MAUI page.
/// Wrapping page content to host a chatbot was intercepting every click — this
/// popup is a 58px control, so Welcome/Home buttons keep working.
/// </summary>
public static class ChatbotWindowOverlay
{
    private const double FabSize = 58;
    private static Popup? _fabPopup;
    private static Popup? _sheetPopup;
    private static FrameworkElement? _root;
    private static bool _attached;
    private static bool _dragged;
    private static bool _moved;
    private static double _offsetX;
    private static double _offsetY;
    private static WinPoint _press;
    private static WinText? _answer;

    private static readonly (string Question, string Answer)[] Faqs =
    [
        ("How do I book a room?",
            "Open Book, choose a location and room, then pick a date and time. Bookings are confirmed automatically as soon as they are made."),
        ("Where are your centres?",
            "Flexispace has professionally appointed rooms in Houghton Estate, Centurion, and Eagle Canyon."),
        ("When is my booking confirmed?",
            "Immediately. The system confirms every booking automatically — no Centre Manager or Admin approval step."),
        ("Can I combine rooms?",
            "At Eagle Canyon, Thingamajik and Whachamacallit open into one suite for larger groups (up to 14 seats). Pick the Combined option when booking, or follow the tip if your headcount is too big for one room."),
        ("How do I cancel?",
            "Open the booking from Home, Alerts, or My bookings, then tap Cancel booking. You can cancel your own confirmed bookings."),
        ("What does Live show?",
            "Live is a snapshot of room status — available, reserved, or busy — so you can see what is free right now."),
        ("How do I block a room?",
            "Centre Managers and Admins can open Manage, choose a room and time window, and block it so new bookings cannot overlap. Anyone already booked in that window is notified.")
    ];

    public static void TryAttach(Microsoft.UI.Xaml.Window? native, Microsoft.Maui.Controls.Window mauiWindow)
    {
        if (_attached || native?.Content is not FrameworkElement root)
            return;

        void Ready(object sender, RoutedEventArgs e)
        {
            root.Loaded -= Ready;
            Attach(root, mauiWindow);
        }

        if (root.IsLoaded)
            Attach(root, mauiWindow);
        else
            root.Loaded += Ready;
    }

    private static void Attach(FrameworkElement root, Microsoft.Maui.Controls.Window mauiWindow)
    {
        if (_attached || root.XamlRoot is null)
            return;

        _attached = true;
        _root = root;

        var gold = new WinBrush(WinColor.FromArgb(255, 0xC4, 0xA0, 0x35));
        var black = new WinBrush(WinColor.FromArgb(255, 0x11, 0x11, 0x11));
        var ivory = new WinBrush(WinColor.FromArgb(255, 0xFA, 0xF8, 0xF4));
        var ink = new WinBrush(WinColor.FromArgb(255, 0x1A, 0x1A, 0x1A));
        var muted = new WinBrush(WinColor.FromArgb(255, 0x5A, 0x55, 0x4D));

        var fab = new WinButton
        {
            Width = FabSize,
            Height = FabSize,
            CornerRadius = new WinCorner(29),
            Background = gold,
            BorderThickness = new WinThickness(0),
            Padding = new WinThickness(0),
            Content = new WinText
            {
                Text = "\uE8BD",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 20,
                Foreground = black,
                HorizontalAlignment = WinAlign.Center,
                VerticalAlignment = WinVAlign.Center
            }
        };

        fab.PointerPressed += (_, e) =>
        {
            _moved = false;
            _press = e.GetCurrentPoint(root).Position;
            fab.CapturePointer(e.Pointer);
        };
        fab.PointerMoved += (_, e) =>
        {
            if (!fab.PointerCaptures?.Any() ?? true)
                return;
            var now = e.GetCurrentPoint(root).Position;
            var dx = now.X - _press.X;
            var dy = now.Y - _press.Y;
            if (Math.Abs(dx) > 6 || Math.Abs(dy) > 6)
                _moved = true;
            if (!_moved) return;
            _dragged = true;
            _offsetX += dx;
            _offsetY += dy;
            _press = now;
            ClampAndApply();
        };
        fab.PointerReleased += (_, e) =>
        {
            fab.ReleasePointerCapture(e.Pointer);
            if (!_moved)
                ToggleSheet();
        };

        _fabPopup = new Popup
        {
            Child = fab,
            IsLightDismissEnabled = false,
            ShouldConstrainToRootBounds = true,
            XamlRoot = root.XamlRoot,
            IsOpen = true
        };

        _sheetPopup = new Popup
        {
            Child = BuildSheet(gold, black, ivory, ink, muted),
            IsLightDismissEnabled = true,
            ShouldConstrainToRootBounds = true,
            XamlRoot = root.XamlRoot,
            IsOpen = false
        };

        mauiWindow.SizeChanged += (_, _) =>
        {
            if (!_dragged)
                PlaceDefault();
            else
                ClampAndApply();
        };

        root.SizeChanged += (_, _) =>
        {
            if (!_dragged)
                PlaceDefault();
        };

        PlaceDefault();
    }

    private static UIElement BuildSheet(WinBrush gold, WinBrush black, WinBrush ivory, WinBrush ink, WinBrush muted)
    {
        var questions = new WinStack { Spacing = 8 };
        _answer = new WinText
        {
            Text = "Tap a question for a short answer.",
            FontSize = 13,
            Foreground = ink,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Margin = new WinThickness(0, 4, 0, 8)
        };

        foreach (var faq in Faqs)
        {
            var item = faq;
            var q = new WinButton
            {
                Content = item.Question,
                HorizontalAlignment = WinAlign.Stretch,
                HorizontalContentAlignment = WinAlign.Left,
                Padding = new WinThickness(12, 8, 12, 8),
                CornerRadius = new WinCorner(16),
                Background = new WinBrush(WinColor.FromArgb(0x22, 0xC4, 0xA0, 0x35)),
                BorderBrush = gold,
                BorderThickness = new WinThickness(1.2),
                Foreground = ink,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            q.Click += (_, _) =>
            {
                if (_answer is not null)
                    _answer.Text = item.Answer;
            };
            questions.Children.Add(q);
        }

        var close = new WinButton
        {
            Content = "Close",
            HorizontalAlignment = WinAlign.Stretch,
            Margin = new WinThickness(0, 8, 0, 0),
            CornerRadius = new WinCorner(18),
            Background = gold,
            Foreground = black,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            BorderThickness = new WinThickness(0)
        };
        close.Click += (_, _) =>
        {
            if (_sheetPopup is not null)
                _sheetPopup.IsOpen = false;
        };

        var header = new WinText
        {
            Text = "Flexispace Assistant",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = ivory
        };
        var subtitle = new WinText
        {
            Text = "Prototype help · tap a question",
            FontSize = 11,
            Foreground = new WinBrush(WinColor.FromArgb(255, 0xC8, 0xC2, 0xB8)),
            Margin = new WinThickness(0, 2, 0, 0)
        };

        var headerBlock = new WinStack { Spacing = 0 };
        headerBlock.Children.Add(header);
        headerBlock.Children.Add(subtitle);

        var headerBar = new WinBorder
        {
            Background = black,
            Padding = new WinThickness(16, 14, 16, 14),
            Child = headerBlock
        };

        var body = new WinStack { Spacing = 8, Padding = new WinThickness(16) };
        body.Children.Add(_answer);
        var label = new WinText
        {
            Text = "QUICK QUESTIONS",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new WinBrush(WinColor.FromArgb(255, 0x9A, 0x7A, 0x1F)),
            CharacterSpacing = 80
        };
        body.Children.Add(label);
        body.Children.Add(questions);
        body.Children.Add(close);

        var card = new WinBorder
        {
            Background = ivory,
            BorderBrush = gold,
            BorderThickness = new WinThickness(1.2),
            CornerRadius = new WinCorner(18),
            Width = 320,
            Child = new WinStack()
        };
        var cardStack = new WinStack();
        cardStack.Children.Add(headerBar);
        cardStack.Children.Add(new WinScroll
        {
            Content = body,
            MaxHeight = 380,
            VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto
        });
        card.Child = cardStack;

        return card;
    }

    private static void ToggleSheet()
    {
        if (_sheetPopup is null || _fabPopup is null)
            return;
        _sheetPopup.IsOpen = !_sheetPopup.IsOpen;
        if (_sheetPopup.IsOpen)
        {
            _sheetPopup.HorizontalOffset = Math.Max(12, _fabPopup.HorizontalOffset - 260);
            _sheetPopup.VerticalOffset = Math.Max(12, _fabPopup.VerticalOffset - 360);
        }
    }

    private static void PlaceDefault()
    {
        if (_root is null || _fabPopup is null)
            return;
        var w = _root.ActualWidth;
        var h = _root.ActualHeight;
        if (w <= 0 || h <= 0)
            return;
        _offsetX = Math.Max(8, w - FabSize - 16);
        _offsetY = Math.Max(8, h - FabSize - 16);
        ClampAndApply();
    }

    private static void ClampAndApply()
    {
        if (_root is null || _fabPopup is null)
            return;
        var w = _root.ActualWidth;
        var h = _root.ActualHeight;
        _offsetX = Math.Clamp(_offsetX, 8, Math.Max(8, w - FabSize - 8));
        _offsetY = Math.Clamp(_offsetY, 8, Math.Max(8, h - FabSize - 8));
        _fabPopup.HorizontalOffset = _offsetX;
        _fabPopup.VerticalOffset = _offsetY;
    }
}
