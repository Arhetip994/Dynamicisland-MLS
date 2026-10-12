using System.Windows.Media;
using Microsoft.Win32;

namespace DynamicIsland;

[Flags]
enum Backdrop
{
    Glow = 0,
    Matrix = 1,
    Stars = 2,
    MatrixAndStars = Matrix | Stars,
}

enum LyricChange
{
    Smooth,
    Wave,
    Drum,
}

enum Hover
{
    Disc,
    Magnet,
    Flow,
}

enum SeekHover
{
    Magnifier,
    Lift,
    Wave,
}

static class Settings
{
    const string Key = @"Software\DynamicIsland";
    const int MinScale = 85, MaxScale = 130, MaxGap = 24;
    const int DefaultScale = 100, DefaultGap = 8;

    static bool _lyrics = ReadSwitch(nameof(Lyrics)), _lyricEffects = ReadSwitch(nameof(LyricEffects));
    static bool _network = ReadSwitch(nameof(Network)), _hideFullscreen = ReadSwitch(nameof(HideFullscreen));
    static bool _rim = ReadSwitch(nameof(Rim)), _appVolume = ReadSwitch(nameof(AppVolume));
    static bool _dots = ReadSwitch(nameof(Dots), false);
    static bool _lineBar = ReadSwitch(nameof(LineBar));
    static bool _glass = ReadSwitch(nameof(Glass), false);
    static Backdrop _backdrop = (Backdrop)Math.Clamp(Read(nameof(Backdrop), 0), 0, (int)Backdrop.MatrixAndStars);
    static LyricChange _lyricChange = (LyricChange)Math.Clamp(Read(nameof(LyricChange), (int)LyricChange.Wave), 0, (int)LyricChange.Drum);
    static Hover _hover = (Hover)Math.Clamp(Read(nameof(Hover), 0), 0, (int)Hover.Flow);
    static SeekHover _seekHover = (SeekHover)Math.Clamp(Read(nameof(SeekHover), 0), 0, (int)SeekHover.Wave);
    static int _scale = Math.Clamp(Read(nameof(Scale), DefaultScale), MinScale, MaxScale);
    static int _gap = Math.Clamp(Read(nameof(Gap), DefaultGap), 0, MaxGap);
    static int _accent = Read(nameof(Accent), 0);
    static string _paxsenixApiKey = Read(nameof(PaxsenixApiKey), string.Empty);
    static readonly string[] DefaultProviders = ["Apple Music", "Musixmatch", "LRCLIB"];
    static List<string> _lyricsProvidersOrder = ReadProviders();
    static bool _smoothLyricsTransition = ReadSwitch(nameof(SmoothLyricsTransition), true);
    static bool _smoothLyricsFadeIn = ReadSwitch(nameof(SmoothLyricsFadeIn), true);
    static bool _reactiveAuraEnabled = ReadSwitch(nameof(ReactiveAuraEnabled), true);
    static int _auraBassIntensityPercent = Math.Clamp(Read(nameof(AuraBassIntensity), 35), 0, 100);
    static bool _clipboardPeekEnabled = ReadSwitch(nameof(ClipboardPeekEnabled), true);

    public static List<string> LyricsProvidersOrder
    {
        get => _lyricsProvidersOrder;
        set
        {
            _lyricsProvidersOrder = value ?? [.. DefaultProviders];
            Write(nameof(LyricsProvidersOrder), _lyricsProvidersOrder.ToArray(), RegistryValueKind.MultiString);
        }
    }

    public static bool SmoothLyricsTransition
    {
        get => _smoothLyricsTransition;
        set => Write(nameof(SmoothLyricsTransition), _smoothLyricsTransition = value);
    }

    public static bool SmoothLyricsFadeIn
    {
        get => _smoothLyricsFadeIn;
        set => Write(nameof(SmoothLyricsFadeIn), _smoothLyricsFadeIn = value);
    }

    public static bool ReactiveAuraEnabled
    {
        get => _reactiveAuraEnabled;
        set => Write(nameof(ReactiveAuraEnabled), _reactiveAuraEnabled = value);
    }

    public static double AuraBassIntensity
    {
        get => _auraBassIntensityPercent / 100.0;
        set => Write(nameof(AuraBassIntensity), _auraBassIntensityPercent = Math.Clamp((int)Math.Round(value * 100), 0, 100));
    }

    public static int AuraBassIntensityPercent
    {
        get => _auraBassIntensityPercent;
        set => Write(nameof(AuraBassIntensity), _auraBassIntensityPercent = Math.Clamp(value, 0, 100));
    }

    public static bool ClipboardPeekEnabled
    {
        get => _clipboardPeekEnabled;
        set => Write(nameof(ClipboardPeekEnabled), _clipboardPeekEnabled = value);
    }

    public static void Save()
    {
        Write(nameof(LyricsProvidersOrder), _lyricsProvidersOrder.ToArray(), RegistryValueKind.MultiString);
        Write(nameof(SmoothLyricsTransition), _smoothLyricsTransition);
        Write(nameof(SmoothLyricsFadeIn), _smoothLyricsFadeIn);
        Write(nameof(ReactiveAuraEnabled), _reactiveAuraEnabled);
        Write(nameof(AuraBassIntensity), _auraBassIntensityPercent);
        Write(nameof(ClipboardPeekEnabled), _clipboardPeekEnabled);
    }

    public static class Current
    {
        public static List<string> LyricsProvidersOrder
        {
            get => Settings.LyricsProvidersOrder;
            set => Settings.LyricsProvidersOrder = value;
        }

        public static bool SmoothLyricsTransition
        {
            get => Settings.SmoothLyricsTransition;
            set => Settings.SmoothLyricsTransition = value;
        }

        public static bool SmoothLyricsFadeIn
        {
            get => Settings.SmoothLyricsFadeIn;
            set => Settings.SmoothLyricsFadeIn = value;
        }

        public static bool ReactiveAuraEnabled
        {
            get => Settings.ReactiveAuraEnabled;
            set => Settings.ReactiveAuraEnabled = value;
        }

        public static double AuraBassIntensity
        {
            get => Settings.AuraBassIntensity;
            set => Settings.AuraBassIntensity = value;
        }

        public static int AuraBassIntensityPercent
        {
            get => Settings.AuraBassIntensityPercent;
            set => Settings.AuraBassIntensityPercent = value;
        }

        public static bool ClipboardPeekEnabled
        {
            get => Settings.ClipboardPeekEnabled;
            set => Settings.ClipboardPeekEnabled = value;
        }

        public static void Save() => Settings.Save();
    }

    static List<string> ReadProviders()
    {
        string[] read = Read<string[]>(nameof(LyricsProvidersOrder), []);
        if (read == null || read.Length == 0) return [.. DefaultProviders];
        var list = new List<string>();
        foreach (string s in read)
        {
            string? match = DefaultProviders.FirstOrDefault(d => string.Equals(d, s, StringComparison.OrdinalIgnoreCase));
            if (match != null && !list.Contains(match))
                list.Add(match);
        }
        foreach (string s in DefaultProviders)
        {
            if (!list.Contains(s))
                list.Add(s);
        }
        return list;
    }

    public static string PaxsenixApiKey
    {
        get => _paxsenixApiKey;
        set => Write(nameof(PaxsenixApiKey), _paxsenixApiKey = value?.Trim() ?? string.Empty, RegistryValueKind.String);
    }

    public static bool Dots
    {
        get => _dots;
        set => Write(nameof(Dots), _dots = value);
    }

    public static bool LineBar
    {
        get => _lineBar;
        set => Write(nameof(LineBar), _lineBar = value);
    }

    public static bool Glass
    {
        get => _glass;
        set => Write(nameof(Glass), _glass = value);
    }

    public static Backdrop Backdrop
    {
        get => _backdrop;
        set => Write(nameof(Backdrop), (int)(_backdrop = value));
    }

    public static LyricChange LyricChange
    {
        get => _lyricChange;
        set => Write(nameof(LyricChange), (int)(_lyricChange = value));
    }

    public static Hover Hover
    {
        get => _hover;
        set => Write(nameof(Hover), (int)(_hover = value));
    }

    public static SeekHover SeekHover
    {
        get => _seekHover;
        set => Write(nameof(SeekHover), (int)(_seekHover = value));
    }

    public static bool Lyrics
    {
        get => _lyrics;
        set => Write(nameof(Lyrics), _lyrics = value);
    }

    public static bool LyricEffects
    {
        get => _lyricEffects;
        set => Write(nameof(LyricEffects), _lyricEffects = value);
    }

    public static bool Rim
    {
        get => _rim;
        set => Write(nameof(Rim), _rim = value);
    }

    public static bool AppVolume
    {
        get => _appVolume;
        set => Write(nameof(AppVolume), _appVolume = value);
    }

    public static bool Network
    {
        get => _network;
        set => Write(nameof(Network), _network = value);
    }

    public static bool HideFullscreen
    {
        get => _hideFullscreen;
        set => Write(nameof(HideFullscreen), _hideFullscreen = value);
    }

    public static int Scale
    {
        get => _scale;
        set => Write(nameof(Scale), _scale = Math.Clamp(value, MinScale, MaxScale));
    }

    public static int Gap
    {
        get => _gap;
        set => Write(nameof(Gap), _gap = Math.Clamp(value, 0, MaxGap));
    }

    public static Color? Accent
    {
        get => _accent == 0 ? null : Color.FromRgb((byte)(_accent >> 16), (byte)(_accent >> 8), (byte)_accent);
        set => Write(nameof(Accent), _accent = value is { } c ? c.R << 16 | c.G << 8 | c.B : 0);
    }

    public static string[] Shelf
    {
        get => Read<string[]>(nameof(Shelf), []);
        set => Write(nameof(Shelf), value, RegistryValueKind.MultiString);
    }

    static bool ReadSwitch(string name, bool fallback = true) => Read(name, fallback ? 1 : 0) != 0;

    static T Read<T>(string name, T fallback)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(Key);
            return key?.GetValue(name) is T value ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    static void Write(string name, bool value) => Write(name, value ? 1 : 0);

    static void Write(string name, int value) => Write(name, value, RegistryValueKind.DWord);

    static void Write(string name, object value, RegistryValueKind kind)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(Key);
            key.SetValue(name, value, kind);
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }
}
