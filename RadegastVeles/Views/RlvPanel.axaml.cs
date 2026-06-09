/*
 * Radegast Metaverse Client
 * Copyright (c) 2026, Sjofn LLC
 * All rights reserved.
 */

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Radegast.Veles.Views;

public partial class RlvPanel : UserControl
{
    public RlvPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
