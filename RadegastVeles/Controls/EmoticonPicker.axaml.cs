using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Radegast.Veles.Controls;

/// <summary>
/// A flyout panel that displays all supported emoticons and inserts them into the chat input.
/// </summary>
public partial class EmoticonPicker : UserControl
{
    /// <summary>
    /// Raised when the user selects an emoticon. The event argument is the emoji character.
    /// </summary>
    public event EventHandler<string>? EmoticonSelected;

    public EmoticonPicker()
    {
        InitializeComponent();

        var panel = this.FindControl<WrapPanel>("EmoticonPanel");
        if (panel == null) return;

        foreach (var (emoticon, emoji) in EmoticonHelper.GetAllEmoticons())
        {
            var btn = new Button
            {
                Content = emoji,
                Width = 32,
                Height = 32,
                Padding = new Avalonia.Thickness(0),
                FontSize = 18,
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = emoticon
            };
            ToolTip.SetTip(btn, emoticon);
            btn.Click += OnEmoticonClick;
            panel.Children.Add(btn);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnEmoticonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string emoticon)
        {
            var emoji = btn.Content as string;
            if (emoji != null)
            {
                EmoticonSelected?.Invoke(this, emoji);
            }
        }
    }
}
