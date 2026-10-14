using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static DynamicIsland.Motion;

namespace DynamicIsland;

public partial class MainWindow
{
    const double UpdatePagePadding = 14;
    const int MegabyteShift = 20;
    const double LookHeight = 644, LookBaseHeight = 604, LookAuraRowHeight = 40, LookTilesHeight = 72;
    const int LargestScale = 130, ScaleStep = 5;
    const int LargestGap = 24, GapStep = 2;

    const int FirstSourceRow = 1;

    static readonly string[] SourceNames = ["LRCLIB", "Deezer", "NetEase", "QQ Music", "Kugou", "Paxsenix"];

    Border? _openLookTiles;

    void SettingsRow_Click(object sender, RoutedEventArgs e) => ShowPanelAndCheckUpdate(Panel.Settings);

    void UpdateRow_Click(object sender, RoutedEventArgs e) => ShowPanelAndCheckUpdate(Panel.Update);

    void UpdateBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Settings);

    void SourcesRow_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Sources);

    void SourcesBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Settings);

    void ShowPanelAndCheckUpdate(Panel panel)
    {
        ShowPanel(panel);
        _ = _updater.CheckAsync();
    }

    void Source_Click(object sender, RoutedEventArgs e)
    {
        var picked = (LyricSource)(1 << SourceRows.Children.IndexOf((UIElement)sender) - FirstSourceRow);
        LyricSource left = Settings.LyricSources ^ picked;
        if (left == 0) return;

        Settings.LyricSources = left;
        UpdateSwitches(true);
        TrackLyrics(true);
    }

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
        WeatherSwitch.Set(Settings.Weather, animate);
        ClipboardPeekSwitch.Set(Settings.ClipboardPeekEnabled, animate);
        ReactiveAuraSwitch.Set(Settings.ReactiveAuraEnabled, animate);
        FullscreenSwitch.Set(Settings.HideFullscreen, animate);
        AutostartSwitch.Set(Autostart.Enabled, animate);
        AllMonitorsSwitch.Set(Settings.AllMonitors, animate);
        MonitorsMenuSummary.Text = Settings.AllMonitors ? "Все" : Monitors.Find(Settings.SelectedMonitor).Title;

        Toggle[] sources = [LrclibSwitch, DeezerSwitch, NetEaseSwitch, QqMusicSwitch, KugouSwitch, PaxsenixSwitch];
        string[] chosen = SourceNames.Where((_, i) => Settings.LyricSources.HasFlag((LyricSource)(1 << i))).ToArray();
        for (int i = 0; i < sources.Length; i++) sources[i].Set(Settings.LyricSources.HasFlag((LyricSource)(1 << i)), animate);
        SourcesText.Text = chosen.Length == sources.Length ? "Все" : chosen.Length == 1 ? chosen[0] : $"{chosen.Length} из {sources.Length}";
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

    async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_updater.State != Updater.Stage.Available) await _updater.CheckAsync(true);
        else if (await _updater.InstallAsync()) Exit();
    }

    void RefreshUpdatePage()
    {
        Updater.Stage stage = _updater.State;
        bool loading = stage == Updater.Stage.Loading, found = loading || stage == Updater.Stage.Available;
        Version version = found ? _updater.LatestVersion! : Updater.CurrentVersion;

        UpdateText.Foreground = found ? _orange : _dim;
        UpdateText.Text = loading ? _updater.Percent + "%" : "v" + version;

        UpdateVersion.Text = version.ToString();
        UpdateFrom.Text = stage switch
        {
            Updater.Stage.Checking => "проверяю…",
            Updater.Stage.Latest => "последняя версия",
            Updater.Stage.Failed => "не получилось",
            _ => found ? "вместо " + Updater.CurrentVersion : "",
        };
        UpdateNotes.SetVisible(found && _updater.Notes.Length > 0);
        UpdateNotesText.Text = string.Join('\n', _updater.Notes.Select(note => "·  " + note));

        UpdateButton.SetVisible(!loading);
        UpdateLoad.SetVisible(loading);
        UpdateButton.Content = stage switch
        {
            Updater.Stage.Available => "Обновить и перезапустить",
            Updater.Stage.Checking => "Проверяю…",
            Updater.Stage.Failed => "Попробовать ещё раз",
            _ => "Проверить ещё раз",
        };
        if (found) UpdateButton.Background = _orange;
        else UpdateButton.ClearValue(BackgroundProperty);
        UpdateButton.Foreground = found ? Brushes.Black : Brushes.White;

        if (loading) ShowUpdateProgress();
        FitUpdatePage();
        UpdateView();
    }

    void ShowUpdateProgress()
    {
        UpdateBytes.Text = $"{_updater.DownloadedBytes >> MegabyteShift} из {_updater.TotalBytes >> MegabyteShift} МБ";
        LoadingText.Text = _updater.Percent + "%";
        var fill = new DoubleAnimation(_updater.Percent / 100.0, Ms(200));
        UpdateRing.BeginAnimation(Ring.ProgressProperty, fill);
        LoadingRing.BeginAnimation(Ring.ProgressProperty, fill);
    }

    void FitUpdatePage()
    {
        UpdateBody.Measure(new Size(UpdatePage.Width, double.PositiveInfinity));
        double height = Math.Ceiling(UpdateBody.DesiredSize.Height) + UpdatePagePadding;
        if (height == UpdatePage.Height) return;

        UpdatePage.Height = height;
        if (_view == View.Update) UpdateTargets();
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
        UpdatePaxsenixStatus();
    }

    void UpdatePaxsenixStatus()
    {
        string key = Settings.PaxsenixApiKey;
        bool hasKey = !string.IsNullOrWhiteSpace(key);

        if (hasKey)
        {
            PaxsenixStatusDot.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89));
            PaxsenixStatusText.Text = "Ключ сохранён (Apple Music + Musixmatch)";
        }
        else
        {
            PaxsenixStatusDot.Fill = (SolidColorBrush)FindResource("Dim");
            PaxsenixStatusText.Text = "Без ключа (только открытый Apple Music)";
        }
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
