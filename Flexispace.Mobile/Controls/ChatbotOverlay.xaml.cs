using System.Collections.ObjectModel;
using Microsoft.Maui.Layouts;

namespace Flexispace.Mobile.Controls;

public partial class ChatbotOverlay : ContentView
{
    public const string HostId = "ChatHostAbs";
    public const double FabSize = 58;

    private readonly ChatbotSheet _sheet = new();
    private bool _pointerDown;
    private bool _moved;
    private bool _open;
    private bool _bobbing;
    private Point _pressInParent;
    private Rect _startBounds;

    public ObservableCollection<ChatLine> Lines { get; } = [];
    public ObservableCollection<FaqItem> Questions { get; } = [];
    public bool HasCustomPosition { get; set; }
    public Command<FaqItem> AskCommand { get; }

    public ChatbotOverlay()
    {
        AskCommand = new Command<FaqItem>(Ask);
        InitializeComponent();
        BindingContext = this;
        WidthRequest = FabSize;
        HeightRequest = FabSize;

        _sheet.BindingContext = this;
        _sheet.CloseRequested += (_, _) => CloseSheet();

        Questions.Add(new FaqItem
        {
            Question = "How do I book a room?",
            Answer = "Open Book, choose a location and room, then pick a date and time. Centre Managers and Admins are confirmed straight away. Staff and Clients are sent for approval."
        });
        Questions.Add(new FaqItem
        {
            Question = "Where are your centres?",
            Answer = "Flexispace has professionally appointed rooms in Houghton Estate, Centurion, and Eagle Canyon."
        });
        Questions.Add(new FaqItem
        {
            Question = "Who approves bookings?",
            Answer = "Centre Managers approve bookings for their own centre. Administrators can approve across every location."
        });
        Questions.Add(new FaqItem
        {
            Question = "How do I cancel?",
            Answer = "Open the booking from Home, Alerts, or My bookings, then tap Cancel booking. You can cancel your own confirmed or pending bookings."
        });
        Questions.Add(new FaqItem
        {
            Question = "What does Live show?",
            Answer = "Live is a snapshot of room status — available, reserved, or busy — so you can see what is free right now."
        });
        Questions.Add(new FaqItem
        {
            Question = "How do I block a room?",
            Answer = "Centre Managers and Admins can open Manage, choose a room and time window, and block it so new bookings cannot overlap. Anyone already booked in that window is notified."
        });

        Lines.Add(new ChatLine
        {
            Text = "Hello — I am the Flexispace assistant. Tap a question below and I will point you in the right direction."
        });

        Loaded += (_, _) => _ = BobAsync();
        Unloaded += (_, _) => _bobbing = false;
    }

    public static void PlaceFab(AbsoluteLayout host, ChatbotOverlay fab, Rect bounds)
    {
        AbsoluteLayout.SetLayoutFlags(fab, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(fab, bounds);
        fab.ZIndex = 20;
    }

    private async Task BobAsync()
    {
        if (_bobbing) return;
        _bobbing = true;
        while (_bobbing && IsLoaded)
        {
            if (!_pointerDown && !_open)
            {
                await FabButton.ScaleToAsync(1.06, 700, Easing.SinInOut);
                if (!_bobbing || _open) break;
                await FabButton.ScaleToAsync(1.0, 700, Easing.SinInOut);
            }
            else
            {
                await Task.Delay(400);
            }
        }

        _bobbing = false;
        FabButton.Scale = 1;
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (_open || Parent is not AbsoluteLayout) return;
        var pos = e.GetPosition(Parent as Element);
        if (pos is null) return;
        _pointerDown = true;
        _moved = false;
        _pressInParent = pos.Value;
        _startBounds = AbsoluteLayout.GetLayoutBounds(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pointerDown || _open || Parent is not AbsoluteLayout host) return;
        var pos = e.GetPosition(host);
        if (pos is null) return;
        var dx = pos.Value.X - _pressInParent.X;
        var dy = pos.Value.Y - _pressInParent.Y;
        if (Math.Abs(dx) > 6 || Math.Abs(dy) > 6)
            _moved = true;
        if (!_moved) return;

        HasCustomPosition = true;
        var x = Math.Clamp(_startBounds.X + dx, 8, Math.Max(8, host.Width - FabSize - 8));
        var y = Math.Clamp(_startBounds.Y + dy, 8, Math.Max(8, host.Height - FabSize - 8));
        PlaceFab(host, this, new Rect(x, y, FabSize, FabSize));
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e) => _pointerDown = false;

    private void OnFabClicked(object? sender, EventArgs e)
    {
        if (_moved)
        {
            _moved = false;
            return;
        }

        if (_open)
            CloseSheet();
        else
            OpenSheet();
    }

    private void OpenSheet()
    {
        if (Parent is not AbsoluteLayout host) return;
        _open = true;
        FabButton.Text = "\ue5cd";

        if (_sheet.Parent is not AbsoluteLayout)
        {
            AbsoluteLayout.SetLayoutFlags(_sheet, AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutBounds(_sheet, new Rect(0, 0, 1, 1));
            _sheet.ZIndex = 10;
            host.Add(_sheet);
        }

        _sheet.IsVisible = true;
        _sheet.InputTransparent = false;
    }

    private void CloseSheet()
    {
        _open = false;
        FabButton.Text = "\ue0b7";
        if (_sheet.Parent is AbsoluteLayout host)
            host.Remove(_sheet);
        _ = BobAsync();
    }

    private async void Ask(FaqItem? faq)
    {
        if (faq is null) return;
        Lines.Add(new ChatLine { Text = faq.Question, IsUser = true });
        Lines.Add(new ChatLine { Text = faq.Answer });
        await _sheet.ScrollToEndAsync();
    }
}
