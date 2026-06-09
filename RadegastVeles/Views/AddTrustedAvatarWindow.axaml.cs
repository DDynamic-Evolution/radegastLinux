/*
 * Radegast Metaverse Client
 * Copyright (c) 2026, Sjofn LLC
 * All rights reserved.
 */

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenMetaverse;

namespace Radegast.Veles.Views;

public partial class AddTrustedAvatarWindow : Window
{
    public string? AvatarName { get; private set; }
    public UUID AvatarId { get; private set; }
    public int Mode { get; private set; }
    public bool Result { get; private set; }

    public AddTrustedAvatarWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ShowError(string message)
    {
        var errorText = this.FindControl<TextBlock>("ErrorText");
        if (errorText != null)
        {
            errorText.Text = message;
            errorText.IsVisible = true;
        }
    }

    private void HideError()
    {
        var errorText = this.FindControl<TextBlock>("ErrorText");
        if (errorText != null)
        {
            errorText.IsVisible = false;
        }
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        HideError();

        var nameBox = this.FindControl<TextBox>("NameBox");
        var uuidBox = this.FindControl<TextBox>("UuidBox");
        var modeBox = this.FindControl<ComboBox>("ModeBox");

        if (nameBox == null || uuidBox == null || modeBox == null) return;

        var name = nameBox.Text?.Trim();
        var uuidStr = uuidBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Please enter an avatar name.");
            return;
        }

        if (string.IsNullOrWhiteSpace(uuidStr))
        {
            ShowError("Please enter a valid UUID.");
            return;
        }

        if (!UUID.TryParse(uuidStr, out var uuid))
        {
            ShowError("Invalid UUID format. Expected format: 12345678-1234-1234-1234-123456789012");
            return;
        }

        AvatarName = name;
        AvatarId = uuid;
        Mode = modeBox.SelectedIndex;
        Result = true;

        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
