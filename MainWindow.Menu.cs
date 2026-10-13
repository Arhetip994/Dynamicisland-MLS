using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static DynamicIsland.Motion;

namespace DynamicIsland;

public partial class MainWindow
{
    const double LookHeight = 644, LookBaseHeight = 604, LookAuraRowHeight = 40, LookTilesHeight = 72;
    const int LargestScale = 130, ScaleStep = 5;
    const int LargestGap = 24, GapStep = 2;

    Border? _openLookTiles;

    void SettingsRow_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Settings);

    void LyricsSourceRow_Click(object sender, RoutedEventArgs e)
    {
        RefreshLyricsProvidersList();
        ShowPanel(Panel.LyricsSource);
    }

    void LyricsSourceBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Settings);

    void LookRow_Click(object sender, RoutedEventArgs e)
    {
        RefreshLookPage();
        ShowPanel(Panel.Look);
    }

    void SettingsBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Menu);

    void Exit_Click(object sender, RoutedEventArgs e) => Exit();

    void Autostart_Click(object sender, RoutedEventArgs e)
    {
        try { Autostart.Set(!Autostart.Enabled); }
        catch (Exception ex) { App.Log(ex); }
        UpdateSwitches(true);
    }

    void Lyrics_Click(object sender, RoutedEventArgs e)
    {
        Settings.Lyrics = !Settings.Lyrics;
        UpdateSwitches(true);
        TrackLyrics();
    }

    void LyricEffects_Click(object sender, RoutedEventArgs e)
    {
        Settings.LyricEffects = !Settings.LyricEffects;
        UpdateSwitches(true);
        _playerLines = [];
    }

    void Rim_Click(object sender, RoutedEventArgs e)
    {
        Settings.Rim = !Settings.Rim;
        UpdateSwitches(true);
        SyncRim();
    }

    void AppVolume_Click(object sender, RoutedEventArgs e)
    {
        Settings.AppVolume = !Settings.AppVolume;
        UpdateSwitches(true);
    }

    void Network_Click(object sender, RoutedEventArgs e)
    {
        Settings.Network = !Settings.Network;
        UpdateSwitches(true);
    }

    void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        Settings.HideFullscreen = !Settings.HideFullscreen;
        UpdateSwitches(true);
        CheckFullscreen();
    }

    void SmoothLyricsTransition_Click(object sender, RoutedEventArgs e)
    {
        Settings.SmoothLyricsTransition = !Settings.SmoothLyricsTransition;
        UpdateSwitches(true);
    }

    void SmoothLyricsFadeIn_Click(object sender, RoutedEventArgs e)
    {
        Settings.SmoothLyricsFadeIn = !Settings.SmoothLyricsFadeIn;
        UpdateSwitches(true);
    }

    void ClipboardPeek_Click(object sender, RoutedEventArgs e)
    {
        Settings.ClipboardPeekEnabled = !Settings.ClipboardPeekEnabled;
        UpdateSwitches(true);
    }

    void ReactiveAura_Click(object sender, RoutedEventArgs e)
    {
        Settings.ReactiveAuraEnabled = !Settings.ReactiveAuraEnabled;
        UpdateSwitches(true);
        AuraIntensityRow.Visibility = Settings.ReactiveAuraEnabled ? Visibility.Visible : Visibility.Collapsed;
        UpdateLookHeight();
    }

    void AuraIntensitySlider_Changed(object? sender, EventArgs e) => SetAuraIntensity((int)AuraIntensitySlider.Value);

    void SetAuraIntensity(int percent)
    {
        Settings.AuraBassIntensityPercent = Math.Clamp(percent, 10, 100);
        AuraIntensityText.Text = Settings.AuraBassIntensityPercent + "%";
        AuraIntensitySlider.Set(Settings.AuraBassIntensityPercent, true);
    }

    void UpdateSwitches(bool animate)
    {
        LyricsSwitch.Set(Settings.Lyrics, animate);
        LyricEffectsSwitch.Set(Settings.LyricEffects, animate);
        SmoothLyricsTransitionSwitch.Set(Settings.SmoothLyricsTransition, animate);
        SmoothLyricsFadeInSwitch.Set(Settings.SmoothLyricsFadeIn, animate);
        SmoothLyricsTransitionLookSwitch.Set(Settings.SmoothLyricsTransition, animate);
        SmoothLyricsFadeInLookSwitch.Set(Settings.SmoothLyricsFadeIn, animate);
        RimSwitch.Set(Settings.Rim, animate);
        AppVolumeSwitch.Set(Settings.AppVolume, animate);
        NetworkSwitch.Set(Settings.Network, animate);
        ClipboardPeekSwitch.Set(Settings.ClipboardPeekEnabled, animate);
        ReactiveAuraSwitch.Set(Settings.ReactiveAuraEnabled, animate);
        FullscreenSwitch.Set(Settings.HideFullscreen, animate);
        AutostartSwitch.Set(Autostart.Enabled, animate);
        AllMonitorsSwitch.Set(Settings.AllMonitors, animate);
        MonitorsMenuSummary.Text = Settings.AllMonitors ? "Все" : Monitors.Find(Settings.SelectedMonitor).Title;
    }

    void MonitorsRow_Click(object sender, RoutedEventArgs e)
    {
        RefreshMonitorsPage();
        ShowPanel(Panel.Monitors);
    }

    void MonitorsBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Settings);

    void AllMonitors_Click(object sender, RoutedEventArgs e)
    {
        Settings.AllMonitors = !Settings.AllMonitors;
        UpdateSwitches(true);
        if (!_isSecondary)
        {
            SyncSecondaryWindows();
        }
        else if (_primaryWindow != null)
        {
            _primaryWindow.SyncSecondaryWindows();
        }
        RefreshMonitorsPage();
    }

    public void SelectMonitor(MonitorInfo target)
    {
        Settings.SelectedMonitor = target.DeviceName;
        CenterOnScreen();
        UpdateSwitches(false);
        RefreshMonitorsPage();
        if (!_isSecondary)
        {
            SyncSecondaryWindows();
        }
        else if (_primaryWindow != null)
        {
            _primaryWindow.SyncSecondaryWindows();
        }
    }

    void RefreshMonitorsPage()
    {
        if (MonitorsList == null) return;
        MonitorsList.Children.Clear();

        var all = Monitors.GetAll();
        string currentDevice = (_isSecondary && !string.IsNullOrEmpty(_assignedMonitorDevice))
            ? _assignedMonitorDevice
            : (string.IsNullOrEmpty(Settings.SelectedMonitor) ? Monitors.GetPrimary().DeviceName : Settings.SelectedMonitor);

        foreach (MonitorInfo monitor in all)
        {
            bool isCurrent = string.Equals(monitor.DeviceName, currentDevice, StringComparison.OrdinalIgnoreCase);

            var button = new Button { Style = (Style)FindResource("RowButton") };
            var grid = new Grid();

            var icon = new Icon
            {
                Style = (Style)FindResource("RowIcon"),
                Kind = Glyph.Desktop,
                Fill = isCurrent ? (Brush)FindResource("Green") : Brushes.White,
            };

            var label = new TextBlock
            {
                Style = (Style)FindResource("RowLabel"),
                Text = monitor.Title,
                Foreground = isCurrent ? (Brush)FindResource("Green") : Brushes.White,
            };

            var rightPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var resText = new TextBlock
            {
                Text = monitor.Resolution,
                Foreground = (Brush)FindResource("Dim"),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
            };
            rightPanel.Children.Add(resText);

            if (isCurrent)
            {
                var check = new Icon
                {
                    Kind = Glyph.Check,
                    Fill = (Brush)FindResource("Green"),
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                rightPanel.Children.Add(check);
            }

            grid.Children.Add(icon);
            grid.Children.Add(label);
            grid.Children.Add(rightPanel);
            button.Content = grid;

            MonitorInfo captured = monitor;
            button.Click += (_, _) => SelectMonitor(captured);

            MonitorsList.Children.Add(button);
        }

        double calculatedHeight = Math.Max(220, 145 + all.Count * 40);
        MonitorsPage.Height = calculatedHeight;
        if (_view == View.Monitors) UpdateTargets();
    }


    void SizeSlider_Changed(object? sender, EventArgs e) => SetScale((int)SizeSlider.Value);

    void GapSlider_Changed(object? sender, EventArgs e) => SetGap((int)GapSlider.Value);

    void SetScale(int percent)
    {
        int was = Settings.Scale;
        Settings.Scale = percent;
        if (Settings.Scale != was) ApplyLook();
    }

    void SetGap(int px)
    {
        int was = Settings.Gap;
        Settings.Gap = px;
        if (Settings.Gap != was) ApplyLook();
    }

    void Dots_Click(object sender, RoutedEventArgs e)
    {
        Eq.Dots = Settings.Dots = DotsSegments.PickUnderPointer() == 1;
        RefreshLookPage();
    }

    void Glass_Click(object sender, RoutedEventArgs e)
    {
        Settings.Glass = !Settings.Glass;
        RefreshLookPage();
        SyncGlass(true);
    }

    void SeekStyle_Click(object sender, RoutedEventArgs e)
    {
        Settings.LineBar = SeekStyleSegments.PickUnderPointer() == 1;
        RefreshLookPage();
        SyncSeekStyle(true);
    }

    void SeekHover_Click(object sender, RoutedEventArgs e)
    {
        Settings.SeekHover = (SeekHover)SeekHoverSegments.PickUnderPointer();
        RefreshLookPage();
    }

    void BackdropRow_Click(object sender, RoutedEventArgs e) => ToggleLookTiles(BackdropTiles);

    void LyricChangeRow_Click(object sender, RoutedEventArgs e) => ToggleLookTiles(LyricChangeTiles);

    void HoverRow_Click(object sender, RoutedEventArgs e) => ToggleLookTiles(HoverTiles);

    void BackdropTile_Click(object sender, RoutedEventArgs e)
    {
        Settings.Backdrop = (Backdrop)BackdropStrip.Children.IndexOf((UIElement)sender);
        RefreshLookPage();
        SyncBackdrop();
    }

    void LyricChangeTile_Click(object sender, RoutedEventArgs e)
    {
        Settings.LyricChange = (LyricChange)LyricChangeStrip.Children.IndexOf((UIElement)sender);
        RefreshLookPage();
    }

    void HoverTile_Click(object sender, RoutedEventArgs e)
    {
        Settings.Hover = (Hover)HoverStrip.Children.IndexOf((UIElement)sender);
        RefreshLookPage();
    }

    void ToggleLookTiles(Border tiles)
    {
        _openLookTiles = tiles == _openLookTiles ? null : tiles;
        SlideLookTiles(BackdropTiles, BackdropChevron);
        SlideLookTiles(LyricChangeTiles, LyricChangeChevron);
        SlideLookTiles(HoverTiles, HoverChevron);
    }

    void SlideLookTiles(Border tiles, Icon chevron)
    {
        bool open = tiles == _openLookTiles;
        if (!open && tiles.Visibility != Visibility.Visible) return;

        tiles.Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var slide = new DoubleAnimation(open ? LookTilesHeight : 0, Ms(open ? 320 : 240)) { EasingFunction = ease };
        slide.Completed += (_, _) =>
        {
            if (tiles != _openLookTiles) tiles.Visibility = Visibility.Collapsed;
        };
        tiles.BeginAnimation(HeightProperty, slide);
        tiles.Child.BeginAnimation(OpacityProperty, new DoubleAnimation(open ? 1 : 0, Ms(open ? 260 : 160)));
        chevron.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(open ? 90 : 0, Ms(240)) { EasingFunction = ease });
    }

    void UpdateLookHeight()
    {
        double baseHeight = Settings.ReactiveAuraEnabled ? LookHeight : LookBaseHeight;
        LookView.Height = baseHeight + BackdropTiles.ActualHeight + LyricChangeTiles.ActualHeight + HoverTiles.ActualHeight;
        if (_view == View.Look) UpdateTargets();
    }

    void LookTiles_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLookHeight();
    }

    void Accent_Click(object sender, RoutedEventArgs e)
    {
        Settings.Accent = ((RadioButton)sender).Background is SolidColorBrush picked ? picked.Color : null;
        RefreshLookPage();
        SyncAccent();
        SyncRim();
    }

    void RefreshLookPage()
    {
        SizeText.Text = Settings.Scale + "%";
        GapText.Text = Settings.Gap + " px";
        SizeSlider.Set(Settings.Scale, LookView.IsVisible);
        GapSlider.Set(Settings.Gap, LookView.IsVisible);
        DotsSegments.Set(Settings.Dots ? 1 : 0, LookView.IsVisible);
        SeekStyleSegments.Set(Settings.LineBar ? 1 : 0, LookView.IsVisible);
        SeekHoverSegments.Set((int)Settings.SeekHover, LookView.IsVisible);
        GlassSwitch.Set(Settings.Glass, LookView.IsVisible);
        ReactiveAuraSwitch.Set(Settings.ReactiveAuraEnabled, LookView.IsVisible);
        AuraIntensityRow.Visibility = Settings.ReactiveAuraEnabled ? Visibility.Visible : Visibility.Collapsed;
        AuraIntensityText.Text = Settings.AuraBassIntensityPercent + "%";
        AuraIntensitySlider.Set(Settings.AuraBassIntensityPercent, LookView.IsVisible);
        UpdateLookHeight();
        ((RadioButton)BackdropStrip.Children[(int)Settings.Backdrop]).IsChecked = true;
        ((RadioButton)LyricChangeStrip.Children[(int)Settings.LyricChange]).IsChecked = true;
        ((RadioButton)HoverStrip.Children[(int)Settings.Hover]).IsChecked = true;
        BackdropText.Text = Settings.Backdrop switch
        {
            Backdrop.Matrix => "Матрица",
            Backdrop.Stars => "Звёзды",
            Backdrop.MatrixAndStars => "Матрица и звёзды",
            _ => "Свечение",
        };
        LyricChangeText.Text = Settings.LyricChange switch
        {
            LyricChange.Wave => "Волна по буквам",
            LyricChange.Drum => "Барабан по словам",
            _ => "Плавно",
        };
        HoverText.Text = Settings.Hover switch
        {
            Hover.Magnet => "Магнит",
            Hover.Flow => "Перетекание",
            _ => "Диск",
        };
        foreach (RadioButton dot in AccentStrip.Children)
        {
            Color? color = dot.Background is SolidColorBrush own ? own.Color : null;
            if (color != Settings.Accent) continue;
            dot.IsChecked = true;
            AccentText.Text = (string)dot.Tag;
        }
    }

    void ApplyLook()
    {
        RefreshLookPage();
        _userScale.Target = Settings.Scale / 100.0;
        _topGap.Target = Settings.Gap;
        UpdateTargets();
    }

    bool _paxsenixKeyMasked = true;
    bool _updatingKeyText;

    void InitPaxsenixSettings()
    {
        string key = Settings.PaxsenixApiKey;
        _updatingKeyText = true;
        try
        {
            PaxsenixKeyText.Text = key;
            PaxsenixKeyPassword.Password = key;
        }
        finally { _updatingKeyText = false; }
        ApplyPaxsenixMasking();
        RefreshLyricsProvidersList();
        UpdatePaxsenixStatus();
    }

    void UpdatePaxsenixStatus()
    {
        string key = Settings.PaxsenixApiKey;
        bool hasKey = !string.IsNullOrWhiteSpace(key);

        string primary = Settings.LyricsProvidersOrder.FirstOrDefault() ?? "LRCLIB";
        if (string.Equals(primary, "LRCLIB", StringComparison.OrdinalIgnoreCase))
        {
            PaxsenixStatusDot.Fill = _dim;
            PaxsenixStatusText.Text = "Стандартный режим (LRCLIB)";
            LyricsSourceMenuSummary.Text = "LRCLIB";
        }
        else if (!hasKey)
        {
            PaxsenixStatusDot.Fill = _dim;
            PaxsenixStatusText.Text = $"{primary} (нужен API ключ)";
            LyricsSourceMenuSummary.Text = primary;
        }
        else if (LyricsService.IsUnauthorized)
        {
            PaxsenixStatusDot.Fill = _red;
            PaxsenixStatusText.Text = "Неверный ключ / Ошибка авторизации";
            LyricsSourceMenuSummary.Text = "Ошибка (401)";
        }
        else
        {
            PaxsenixStatusDot.Fill = _green;
            PaxsenixStatusText.Text = $"Ключ сохранен ({primary})";
            LyricsSourceMenuSummary.Text = primary;
        }

        PaxsenixPlaceholder.SetVisible(!hasKey);
    }

    Point _providerDragStart;
    bool _isProviderDragging;
    string? _draggedProvider;
    readonly Dictionary<string, Spring> _providerSprings = new();
    readonly Dictionary<string, TranslateTransform> _providerTransforms = new();
    FrameLoop? _providerLoop;

    void EnsureProviderLoop()
    {
        _providerLoop ??= new FrameLoop(AdvanceProviderSprings);
    }

    bool AdvanceProviderSprings(double dt)
    {
        bool moving = false;
        foreach (var (provider, spring) in _providerSprings)
        {
            if (spring.Advance(dt)) moving = true;
            if (_providerTransforms.TryGetValue(provider, out var transform))
            {
                transform.Y = spring.Value;
            }
        }
        return moving;
    }

    void ResetProviderSprings()
    {
        foreach (var spring in _providerSprings.Values)
        {
            spring.Target = 0;
        }
        _providerLoop?.Start();
    }

    void RefreshLyricsProvidersList()
    {
        if (LyricsProvidersList == null) return;
        LyricsProvidersList.Children.Clear();
        _providerSprings.Clear();
        _providerTransforms.Clear();
        EnsureProviderLoop();

        var order = Settings.Current.LyricsProvidersOrder;
        if (order == null || order.Count == 0)
        {
            order = ["Apple Music", "Musixmatch", "LRCLIB"];
            Settings.Current.LyricsProvidersOrder = order;
        }

        for (int i = 0; i < order.Count; i++)
        {
            string provider = order[i];
            bool isFirst = i == 0;
            bool isLast = i == order.Count - 1;

            var spring = new Spring(0, 380, 28);
            var transform = new TranslateTransform();
            _providerSprings[provider] = spring;
            _providerTransforms[provider] = transform;

            var card = CreateProviderCard(provider, i, isFirst, isLast, transform);
            LyricsProvidersList.Children.Add(card);
        }
    }

    Border CreateProviderCard(string provider, int index, bool isFirst, bool isLast, TranslateTransform transform)
    {
        var card = new Border
        {
            Height = 40,
            Margin = new Thickness(0, 2, 0, 2),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            RenderTransform = transform,
            AllowDrop = true,
            Tag = provider,
            Cursor = Cursors.Hand,
        };

        card.MouseEnter += (_, _) =>
        {
            if (!_isProviderDragging)
            {
                card.Background = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
            }
        };
        card.MouseLeave += (_, _) =>
        {
            if (!_isProviderDragging)
            {
                card.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left drag handle (six-dots badge ⋮⋮)
        var grip = new Grid
        {
            Width = 26,
            Height = 36,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var dotsPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M3,4 A1.2,1.2 0 1 1 3,6.4 A1.2,1.2 0 1 1 3,4 M7,4 A1.2,1.2 0 1 1 7,6.4 A1.2,1.2 0 1 1 7,4 M3,8.8 A1.2,1.2 0 1 1 3,11.2 A1.2,1.2 0 1 1 3,8.8 M7,8.8 A1.2,1.2 0 1 1 7,11.2 A1.2,1.2 0 1 1 7,8.8 M3,13.6 A1.2,1.2 0 1 1 3,16 A1.2,1.2 0 1 1 3,13.6 M7,13.6 A1.2,1.2 0 1 1 7,16 A1.2,1.2 0 1 1 7,13.6"),
            Fill = _dim,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grip.Children.Add(dotsPath);
        Grid.SetColumn(grip, 0);
        grid.Children.Add(grip);

        // Center info
        var infoStack = new StackPanel
        {
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var titleText = new TextBlock
        {
            Text = provider,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
        };
        string subtitle = provider switch
        {
            "Apple Music" => "Paxsenix · Apple Music",
            "Musixmatch" => "Paxsenix · Musixmatch",
            "LRCLIB" => "LRCLIB · Прямой API",
            _ => "Тексты песен",
        };
        var subText = new TextBlock
        {
            Text = subtitle,
            FontSize = 10.5,
            Foreground = _dim,
            Margin = new Thickness(0, 1, 0, 0),
        };
        infoStack.Children.Add(titleText);
        infoStack.Children.Add(subText);
        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // Right compact buttons (▲ / ▼)
        var btnStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var upBtn = new Button
        {
            Width = 24,
            Height = 24,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = isFirst ? Cursors.Arrow : Cursors.Hand,
            Opacity = isFirst ? 0.25 : 0.85,
            IsEnabled = !isFirst,
            Padding = new Thickness(0),
            ToolTip = "Переместить выше",
        };
        var upArrow = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M3,7 L7,3 L11,7"),
            Stroke = Brushes.White,
            StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        upBtn.Content = upArrow;
        upBtn.Click += (_, e) =>
        {
            e.Handled = true;
            MoveProvider(provider, -1);
        };

        var downBtn = new Button
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(2, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = isLast ? Cursors.Arrow : Cursors.Hand,
            Opacity = isLast ? 0.25 : 0.85,
            IsEnabled = !isLast,
            Padding = new Thickness(0),
            ToolTip = "Переместить ниже",
        };
        var downArrow = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M3,4 L7,8 L11,4"),
            Stroke = Brushes.White,
            StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        downBtn.Content = downArrow;
        downBtn.Click += (_, e) =>
        {
            e.Handled = true;
            MoveProvider(provider, 1);
        };

        btnStack.Children.Add(upBtn);
        btnStack.Children.Add(downBtn);
        Grid.SetColumn(btnStack, 2);
        grid.Children.Add(btnStack);

        card.Child = grid;

        // Drag & Drop
        card.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (e.OriginalSource is DependencyObject dep && FindParent<Button>(dep) != null) return;
            _providerDragStart = e.GetPosition(card);
            _draggedProvider = provider;
        };

        card.PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedProvider == provider && !_isProviderDragging)
            {
                Point current = e.GetPosition(card);
                Vector diff = current - _providerDragStart;
                if (Math.Abs(diff.Y) > 4 || Math.Abs(diff.X) > 4)
                {
                    _isProviderDragging = true;
                    card.Opacity = 0.5;
                    try
                    {
                        DragDrop.DoDragDrop(card, new DataObject("LyricsProvider", provider), DragDropEffects.Move);
                    }
                    finally
                    {
                        _isProviderDragging = false;
                        _draggedProvider = null;
                        card.Opacity = 1.0;
                        ResetProviderSprings();
                    }
                }
            }
        };

        card.DragOver += (s, e) =>
        {
            if (e.Data.GetData("LyricsProvider") is string src && src != provider)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;

                var list = Settings.LyricsProvidersOrder;
                int srcIdx = list.IndexOf(src);
                int targetIdx = list.IndexOf(provider);

                foreach (var p in list)
                {
                    if (!_providerSprings.TryGetValue(p, out var spr)) continue;
                    if (p == src)
                    {
                        spr.Target = 0;
                    }
                    else if (srcIdx < targetIdx && list.IndexOf(p) > srcIdx && list.IndexOf(p) <= targetIdx)
                    {
                        spr.Target = -36;
                    }
                    else if (srcIdx > targetIdx && list.IndexOf(p) < srcIdx && list.IndexOf(p) >= targetIdx)
                    {
                        spr.Target = 36;
                    }
                    else
                    {
                        spr.Target = 0;
                    }
                }
                _providerLoop?.Start();
            }
        };

        card.DragLeave += (s, e) =>
        {
            ResetProviderSprings();
        };

        card.Drop += (s, e) =>
        {
            ResetProviderSprings();
            if (e.Data.GetData("LyricsProvider") is string src && src != provider)
            {
                ReorderProvider(src, provider);
            }
            e.Handled = true;
        };

        return card;
    }

    static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }

    void ReorderProvider(string source, string target)
    {
        var list = new List<string>(Settings.LyricsProvidersOrder);
        int oldIndex = list.IndexOf(source);
        int newIndex = list.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

        list.RemoveAt(oldIndex);
        list.Insert(newIndex, source);
        Settings.Current.LyricsProvidersOrder = list;
        Settings.Current.Save();

        RefreshLyricsProvidersList();
        UpdatePaxsenixStatus();
        TrackLyrics(true);
    }

    void MoveProvider(string item, int offset)
    {
        var list = new List<string>(Settings.LyricsProvidersOrder);
        int index = list.IndexOf(item);
        if (index < 0) return;
        int target = index + offset;
        if (target < 0 || target >= list.Count) return;

        list.RemoveAt(index);
        list.Insert(target, item);
        Settings.Current.LyricsProvidersOrder = list;
        Settings.Current.Save();

        RefreshLyricsProvidersList();
        UpdatePaxsenixStatus();
        TrackLyrics(true);
    }

    void ApplyPaxsenixMasking()
    {
        if (_paxsenixKeyMasked)
        {
            PaxsenixKeyPassword.Password = PaxsenixKeyText.Text;
            PaxsenixKeyPassword.Visibility = Visibility.Visible;
            PaxsenixKeyText.Visibility = Visibility.Collapsed;
            PaxsenixEyeIcon.Data = Geometry.Parse("M12,4.5 C7,4.5 2.73,7.61 1,12 C2.73,16.39 7,19.5 12,19.5 C17,19.5 21.27,16.39 23,12 C21.27,7.61 17,4.5 12,4.5 Z M12,17 C9.24,17 7,14.76 7,12 C7,9.24 9.24,7 12,7 C14.76,7 17,9.24 17,12 C17,14.76 14.76,17 12,17 Z M12,9 C10.34,9 9,10.34 9,12 C9,13.66 10.34,15 12,15 C13.66,15 15,13.66 15,12 C15,10.34 13.66,9 12,9 Z");
        }
        else
        {
            PaxsenixKeyText.Text = PaxsenixKeyPassword.Password;
            PaxsenixKeyText.Visibility = Visibility.Visible;
            PaxsenixKeyPassword.Visibility = Visibility.Collapsed;
            PaxsenixEyeIcon.Data = Geometry.Parse("M12,7 c2.76,0 5,2.24 5,5 0,0.65 -0.13,1.26 -0.36,1.82 l2.92,2.92 c1.51,-1.26 2.7,-2.89 3.44,-4.74 -1.73,-4.39 -6,-7.5 -11,-7.5 -1.4,0 -2.74,0.25 -3.98,0.7 l2.16,2.16 C10.74,7.13 11.35,7 12,7 z M2,4.27 l2.28,2.28 0.46,0.46 C3.08,8.3 1.78,10.02 1,12 c1.73,4.39 6,7.5 11,7.5 1.55,0 3.03,-0.3 4.38,-0.84 l0.42,0.42 L19.73,22 21,20.73 3.27,3 2,4.27 z M7.53,9.8 l1.55,1.55 c-0.05,0.21 -0.08,0.43 -0.08,0.65 0,1.66 1.34,3 3,3 0.22,0 0.44,-0.03 0.65,-0.08 l1.55,1.55 c-0.67,0.33 -1.41,0.53 -2.2,0.53 -2.76,0 -5,-2.24 -5,-5 0,-0.79 0.2,-1.53 0.53,-2.2 z M11.84,9.02 l3.15,3.15 0.02,-0.16 c0,-1.66 -1.34,-3 -3,-3 l-0.17,0.01 z");
        }
    }

    void PaxsenixToggleVisibility_Click(object sender, RoutedEventArgs e)
    {
        _paxsenixKeyMasked = !_paxsenixKeyMasked;
        ApplyPaxsenixMasking();
    }

    void PaxsenixPaste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = Clipboard.GetText()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                _updatingKeyText = true;
                try
                {
                    PaxsenixKeyText.Text = text;
                    PaxsenixKeyPassword.Password = text;
                }
                finally { _updatingKeyText = false; }

                Settings.PaxsenixApiKey = text;
                LyricsService.ResetAuthError();
                UpdatePaxsenixStatus();
                TrackLyrics(true);
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }

    void PaxsenixKey_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingKeyText) return;
        _updatingKeyText = true;
        try
        {
            string key = PaxsenixKeyText.Text.Trim();
            PaxsenixKeyPassword.Password = key;
            Settings.PaxsenixApiKey = key;
            LyricsService.ResetAuthError();
            UpdatePaxsenixStatus();
        }
        finally { _updatingKeyText = false; }
    }

    void PaxsenixKeyPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingKeyText) return;
        _updatingKeyText = true;
        try
        {
            string key = PaxsenixKeyPassword.Password.Trim();
            PaxsenixKeyText.Text = key;
            Settings.PaxsenixApiKey = key;
            LyricsService.ResetAuthError();
            UpdatePaxsenixStatus();
        }
        finally { _updatingKeyText = false; }
    }

    void PaxsenixKey_LostFocus(object sender, RoutedEventArgs e)
    {
        string key = (_paxsenixKeyMasked ? PaxsenixKeyPassword.Password : PaxsenixKeyText.Text).Trim();
        Settings.PaxsenixApiKey = key;
        LyricsService.ResetAuthError();
        UpdatePaxsenixStatus();
        TrackLyrics(true);
    }
}
