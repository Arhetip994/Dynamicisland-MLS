using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DynamicIsland;

sealed class LyricsService
{
    public readonly record struct Line(TimeSpan Time, string Text);

    sealed record Candidate(double Duration, double End, Line[] Lines);

    const string SearchUrl = "https://lrclib.net/api/search?";
    const string TitleSeparator = " - ";
    const double DurationTolerance = 4;
    const double EndSlack = 1;
    const double MinTimeline = 1;
    const double RestretchAfter = 0.5;
    const double MinUsualDuration = 30;
    const double MinStretch = 0.5, MaxStretch = 2;
    const int Attempts = 2;
    const int RetryDelayMs = 1500;
    const int MaxMinutes = 24 * 60;

    static readonly Line[] None = [];
    static readonly HttpClient Http = CreateClient();
    const string Rework = @"remix|rmx|sped\s*up|speed\s*up|slowed|reverb|nightcore|hardstyle|phonk|bootleg|mashup|ремикс|hoodtrap|drill|trap|edit";

    static readonly Regex Noise = new(
        @"\s*[\(\[][^\)\]]*\b(official|video|audio|lyrics?|visuali[sz]er|remaster(ed)?|hd|hq|4k|mv|feat|ft|prod|edit|mix|version|cover|клип|премьера|"
        + Rework + @")\b[^\)\]]*[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex Reworked = new(@"\b(" + Rework + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex ReworkTail = new(@"\s*[-–—+xX]?\s*\b(" + Rework + @")\b.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex Pipes = new(@"\s*\|[^|]*\|\s*|\s+\|\s.*$", RegexOptions.Compiled);
    static readonly Regex Channel = new(@"\s*-\s*Topic$|\s*VEVO$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex Stamped = new(@"^((?:\[\d+:\d+(?:\.\d+)?\])+)(.*)$", RegexOptions.Compiled);
    static readonly Regex Stamp = new(@"\[(\d+):(\d+(?:\.\d+)?)\]", RegexOptions.Compiled);

    public static event Action? StatusChanged;
    public static bool IsUnauthorized { get; private set; }

    public static void ResetAuthError()
    {
        if (IsUnauthorized)
        {
            IsUnauthorized = false;
            StatusChanged?.Invoke();
        }
    }

    static void SetUnauthorized(bool value)
    {
        if (IsUnauthorized != value)
        {
            IsUnauthorized = value;
            StatusChanged?.Invoke();
        }
    }

    Candidate[] _candidates = [];
    Line[] _stretched = None;
    double _stretchedFor;
    bool _reworked;
    string _trackKey = "";
    int _requestVersion;

    public event Action? Changed;

    public bool IsPending { get; private set; }

    public void Track(string title, string artist, double duration = 0, bool force = false)
    {
        string key = title + "\n" + artist;
        if (!force && key == _trackKey) return;

        _trackKey = key;
        _candidates = [];
        _stretchedFor = 0;
        _reworked = Reworked.IsMatch(title);
        int version = ++_requestVersion;
        IsPending = title.Length > 0;
        if (IsPending) _ = LoadAsync(title, artist, duration, version);
    }

    public Line[] LinesFor(TimeSpan duration)
    {
        double seconds = duration.TotalSeconds;
        if (seconds < MinTimeline) return None;

        Line[] best = None;
        double bestGap = DurationTolerance;
        foreach (Candidate candidate in _candidates)
        {
            if (candidate.End > seconds + EndSlack) continue;
            double gap = candidate.Duration <= 0 ? 0 : Math.Abs(candidate.Duration - seconds);
            if (gap > bestGap || (gap == bestGap && best.Length > 0)) continue;
            best = candidate.Lines;
            bestGap = gap;
        }
        if (best.Length > 0 || !_reworked) return best;

        if (Math.Abs(seconds - _stretchedFor) > RestretchAfter)
        {
            _stretched = Stretch(seconds);
            _stretchedFor = seconds;
        }
        return _stretched;
    }

    public TimeSpan UsualLength => _reworked ? TimeSpan.Zero : TimeSpan.FromSeconds(UsualCandidate() is { } c ? (c.Duration > 0 ? c.Duration : c.End) : 0);

    Candidate? UsualCandidate() => _candidates.Where(c => (c.Duration >= MinUsualDuration || c.End >= MinUsualDuration) && (c.Duration <= 0 || c.End <= c.Duration + EndSlack))
        .GroupBy(c => Math.Round(c.Duration > 0 ? c.Duration : c.End)).OrderByDescending(g => g.Count()).FirstOrDefault()?.First();

    Line[] Stretch(double seconds)
    {
        Candidate? usual = UsualCandidate();
        if (usual == null) return None;

        double duration = usual.Duration > 0 ? usual.Duration : usual.End;
        if (duration <= 0) return None;

        double ratio = seconds / duration;
        if (ratio is < MinStretch or > MaxStretch) return None;
        return Array.ConvertAll(usual.Lines, line => new Line(line.Time * ratio, line.Text));
    }

    async Task LoadAsync(string title, string artist, double duration, int version)
    {
        Candidate[] found = [];
        try { found = await Task.Run(() => FetchAsync(title, artist, duration)); }
        catch { }
        if (version != _requestVersion) return;

        _candidates = found;
        _stretchedFor = 0;
        IsPending = false;
        Changed?.Invoke();
    }

    static async Task<Candidate[]> FetchAsync(string title, string artist, double duration = 0)
    {
        string apiKey = Settings.PaxsenixApiKey;
        string song = ReworkTail.Replace(Pipes.Replace(Noise.Replace(title, ""), " "), "").Replace('—', '-').Replace('–', '-').Trim();
        string by = Channel.Replace(artist, "").Trim();
        if (song.Length == 0) song = title;

        var order = Settings.Current.LyricsProvidersOrder;
        if (order == null || order.Count == 0)
            order = ["Apple Music", "Musixmatch", "LRCLIB"];

        foreach (string provider in order)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
            Candidate[] found = [];
            try
            {
                if (string.Equals(provider, "Apple Music", StringComparison.OrdinalIgnoreCase))
                {
                    found = await FetchAppleMusicAsync(song, by, apiKey, cts.Token);
                }
                else if (string.Equals(provider, "Musixmatch", StringComparison.OrdinalIgnoreCase))
                {
                    found = await FetchMusixmatchAsync(song, by, duration, apiKey, cts.Token);
                }
                else if (string.Equals(provider, "LRCLIB", StringComparison.OrdinalIgnoreCase))
                {
                    found = await FetchLrclibAsync(title, artist, cts.Token);
                }

                if (found.Length > 0)
                {
                    return found;
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
            {
                // Fallback to next provider
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        }

        return [];
    }

    static async Task<Candidate[]> FetchAppleMusicAsync(string song, string by, string apiKey, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return [];

        try
        {
            var queries = new List<string>();
            string qFull = by.Length > 0 ? song + " " + by : song;
            queries.Add(qFull);

            string cleanSong = ReworkTail.Replace(song, "").Trim();
            if (cleanSong.Length == 0) cleanSong = song;
            string primaryBy = by.Split([',', '&', ';', '/'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? by;

            if (cleanSong != song)
            {
                queries.Add(primaryBy.Length > 0 ? cleanSong + " " + primaryBy : cleanSong);
                queries.Add(cleanSong);
            }
            if (primaryBy.Length > 0 && primaryBy != by)
            {
                queries.Add(song + " " + primaryBy);
            }

            foreach (string q in queries.Distinct())
            {
                if (token.IsCancellationRequested) break;
                string amSearchUrl = $"https://api.paxsenix.org/apple-music/search?q={Uri.EscapeDataString(q)}";
                string? amSearchJson = await FetchPaxsenixStringAsync(amSearchUrl, apiKey, token);
                if (!string.IsNullOrEmpty(amSearchJson))
                {
                    List<string> trackIds = ExtractAppleTrackIds(amSearchJson);
                    foreach (string trackId in trackIds.Take(4))
                    {
                        if (token.IsCancellationRequested) break;
                        string amLyricsUrl = $"https://api.paxsenix.org/lyrics/applemusic?id={Uri.EscapeDataString(trackId)}";
                        Candidate[]? c = await FetchPaxsenixUrlAsync(amLyricsUrl, apiKey, token);
                        if (c != null && c.Length > 0) return c;
                    }
                }
            }

            // Direct Paxsenix source parameter "apple"
            string directUrl = $"https://api.paxsenix.org/lyrics?source=apple&t={Uri.EscapeDataString(cleanSong)}&a={Uri.EscapeDataString(primaryBy)}";
            Candidate[]? direct = await FetchPaxsenixUrlAsync(directUrl, apiKey, token);
            if (direct != null && direct.Length > 0) return direct;

            string directUrl2 = $"https://api.paxsenix.org/lyrics/apple?t={Uri.EscapeDataString(cleanSong)}&a={Uri.EscapeDataString(primaryBy)}";
            Candidate[]? direct2 = await FetchPaxsenixUrlAsync(directUrl2, apiKey, token);
            if (direct2 != null && direct2.Length > 0) return direct2;
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException) { }
        catch (Exception ex) { App.Log(ex); }

        return [];
    }

    static async Task<Candidate[]> FetchMusixmatchAsync(string song, string by, double duration, string apiKey, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return [];

        try
        {
            int durSec = duration > 0 ? (int)Math.Round(duration) : 180;
            string cleanSong = ReworkTail.Replace(song, "").Trim();
            if (cleanSong.Length == 0) cleanSong = song;
            string primaryBy = by.Split([',', '&', ';', '/'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? by;

            var variations = new List<(string S, string A)>
            {
                (cleanSong, primaryBy),
                (song, by)
            };

            foreach (var (s, a) in variations.Distinct())
            {
                if (token.IsCancellationRequested) break;

                // 1. Musixmatch endpoint via Paxsenix with t, a and required d (duration in seconds)
                string mmUrl = $"https://api.paxsenix.org/lyrics/musixmatch?t={Uri.EscapeDataString(s)}&a={Uri.EscapeDataString(a)}&d={durSec}";
                Candidate[]? c = await FetchPaxsenixUrlAsync(mmUrl, apiKey, token);
                if (c != null && c.Length > 0) return c;

                // 2. High-precision fallback via Paxsenix LRC search (lrcget)
                string q = a.Length > 0 ? s + " " + a : s;
                string lrcgetUrl = $"https://api.paxsenix.org/lyrics/lrcget?q={Uri.EscapeDataString(q)}";
                Candidate[]? cLrc = await FetchPaxsenixUrlAsync(lrcgetUrl, apiKey, token);
                if (cLrc != null && cLrc.Length > 0) return cLrc;

                // 3. Search Musixmatch track ID and fetch lyrics
                string mmSearchUrl = $"https://api.paxsenix.org/musixmatch/search?q={Uri.EscapeDataString(q)}";
                string? searchJson = await FetchPaxsenixStringAsync(mmSearchUrl, apiKey, token);
                if (!string.IsNullOrEmpty(searchJson))
                {
                    string? trackId = ExtractMusixmatchTrackId(searchJson);
                    if (!string.IsNullOrEmpty(trackId))
                    {
                        string mmLyricsUrl = $"https://api.paxsenix.org/musixmatch/tracks/{Uri.EscapeDataString(trackId)}/lyrics";
                        Candidate[]? c2 = await FetchPaxsenixUrlAsync(mmLyricsUrl, apiKey, token);
                        if (c2 != null && c2.Length > 0) return c2;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException) { }
        catch (Exception ex) { App.Log(ex); }

        return [];
    }

    static string? ExtractMusixmatchTrackId(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("commontrack_id", out JsonElement cid))
                        return cid.GetString() ?? cid.GetRawText().Trim('"');
                    if (item.TryGetProperty("track_id", out JsonElement tid))
                        return tid.GetString() ?? tid.GetRawText().Trim('"');
                    if (item.TryGetProperty("id", out JsonElement id))
                        return id.GetString() ?? id.GetRawText().Trim('"');
                }
            }
        }
        catch { }
        return null;
    }

    static async Task<Candidate[]?> FetchPaxsenixUrlAsync(string url, string apiKey, CancellationToken token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var resp = await Http.SendAsync(req, token);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                SetUnauthorized(true);
                return null;
            }
            if (!resp.IsSuccessStatusCode) return null;

            string body = await resp.Content.ReadAsStringAsync(token);
            string? lrc = ExtractLrc(body);
            if (!string.IsNullOrWhiteSpace(lrc))
            {
                Line[] lines = Parse(lrc);
                if (lines.Length > 0)
                {
                    SetUnauthorized(false);
                    double end = lines.LastOrDefault(line => line.Text.Length > 0).Time.TotalSeconds;
                    double duration = ExtractDuration(body, end);
                    return [new Candidate(duration, end, lines)];
                }
            }
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            return null;
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return null;
        }
        return null;
    }

    static async Task<string?> FetchPaxsenixStringAsync(string url, string apiKey, CancellationToken token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var resp = await Http.SendAsync(req, token);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                SetUnauthorized(true);
                return null;
            }
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(token);
        }
        catch
        {
            return null;
        }
    }

    static List<string> ExtractAppleTrackIds(string json)
    {
        var ids = new List<string>();
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            JsonElement? list = null;
            if (root.ValueKind == JsonValueKind.Array) list = root;
            else if (root.TryGetProperty("results", out JsonElement res))
            {
                if (res.ValueKind == JsonValueKind.Array) list = res;
                else if (res.TryGetProperty("songs", out JsonElement songs) && songs.TryGetProperty("data", out JsonElement sd) && sd.ValueKind == JsonValueKind.Array) list = sd;
            }
            else if (root.TryGetProperty("data", out JsonElement d) && d.ValueKind == JsonValueKind.Array) list = d;

            if (list.HasValue)
            {
                // First pass: add tracks that explicitly advertise hasTimeSyncedLyrics
                foreach (JsonElement item in list.Value.EnumerateArray())
                {
                    bool hasSync = item.TryGetProperty("hasTimeSyncedLyrics", out JsonElement syncEl) && syncEl.ValueKind == JsonValueKind.True;
                    if (hasSync && TryGetTrackId(item, out string? id) && !string.IsNullOrEmpty(id) && !ids.Contains(id))
                    {
                        ids.Add(id);
                    }
                }
                // Second pass: add all other candidate tracks
                foreach (JsonElement item in list.Value.EnumerateArray())
                {
                    if (TryGetTrackId(item, out string? id) && !string.IsNullOrEmpty(id) && !ids.Contains(id))
                    {
                        ids.Add(id);
                    }
                }
            }
        }
        catch { }
        return ids;
    }

    static bool TryGetTrackId(JsonElement item, out string? id)
    {
        if (item.TryGetProperty("playParams", out JsonElement pp) && pp.TryGetProperty("id", out JsonElement pid))
        {
            id = pid.GetString() ?? pid.GetRawText().Trim('"');
            return true;
        }
        if (item.TryGetProperty("id", out JsonElement idEl))
        {
            id = idEl.GetString() ?? idEl.GetRawText().Trim('"');
            return true;
        }
        id = null;
        return false;
    }

    static string? ExtractLrc(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        string trimmed = content.Trim();
        if (Stamp.IsMatch(trimmed) && trimmed.Contains('\n')) return trimmed;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(trimmed);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("ok", out JsonElement ok) && ok.ValueKind == JsonValueKind.False)
                return null;

            string[] props = ["syncedLyrics", "lrc", "lyrics", "result", "data", "subtitle"];
            foreach (string p in props)
            {
                if (root.TryGetProperty(p, out JsonElement el))
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        string str = el.GetString() ?? "";
                        if (Stamp.IsMatch(str)) return str;
                    }
                    else if (el.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in el.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                string str = item.GetString() ?? "";
                                if (Stamp.IsMatch(str)) return str;
                            }
                        }
                    }
                    else if (el.ValueKind == JsonValueKind.Object)
                    {
                        foreach (string sub in props)
                        {
                            if (el.TryGetProperty(sub, out JsonElement subEl))
                            {
                                if (subEl.ValueKind == JsonValueKind.String)
                                {
                                    string str = subEl.GetString() ?? "";
                                    if (Stamp.IsMatch(str)) return str;
                                }
                                else if (subEl.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (JsonElement item in subEl.EnumerateArray())
                                    {
                                        if (item.ValueKind == JsonValueKind.String)
                                        {
                                            string str = item.GetString() ?? "";
                                            if (Stamp.IsMatch(str)) return str;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (root.TryGetProperty("content", out JsonElement contentArr) && contentArr.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (JsonElement item in contentArr.EnumerateArray())
                {
                    int ts = item.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.TryGetInt32(out int t) ? t : -1;
                    if (ts >= 0 && item.TryGetProperty("text", out JsonElement textEl))
                    {
                        string text = "";
                        if (textEl.ValueKind == JsonValueKind.String) text = textEl.GetString() ?? "";
                        else if (textEl.ValueKind == JsonValueKind.Array)
                        {
                            var parts = new List<string>();
                            foreach (JsonElement part in textEl.EnumerateArray())
                            {
                                if (part.TryGetProperty("text", out JsonElement pt))
                                    parts.Add(pt.GetString() ?? "");
                            }
                            text = string.Join(" ", parts);
                        }
                        TimeSpan span = TimeSpan.FromMilliseconds(ts);
                        sb.AppendLine($"[{(int)span.TotalMinutes:D2}:{span.Seconds:D2}.{span.Milliseconds / 10:D2}]{text.Trim()}");
                    }
                }
                if (sb.Length > 0) return sb.ToString();
            }
        }
        catch
        {
            if (Stamp.IsMatch(trimmed)) return trimmed;
        }

        return null;
    }

    static double ExtractDuration(string body, double fallbackEnd)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("duration", out JsonElement dur) && dur.TryGetDouble(out double d) && d > 0)
                return d;
            if (doc.RootElement.TryGetProperty("data", out JsonElement data) && data.TryGetProperty("duration", out JsonElement d2) && d2.TryGetDouble(out double dd) && dd > 0)
                return dd;
        }
        catch { }
        return 0;
    }

    static async Task<Candidate[]> FetchLrclibAsync(string title, string artist, CancellationToken token = default)
    {
        foreach (string query in Queries(title, artist))
        {
            if (token.IsCancellationRequested) break;
            using JsonDocument? json = await GetAsync(SearchUrl + query, token);
            if (json == null || json.RootElement.ValueKind != JsonValueKind.Array) continue;

            Candidate[] found = json.RootElement.EnumerateArray().Select(ToCandidate).OfType<Candidate>().ToArray();
            if (found.Length > 0) return found;
        }
        return [];
    }

    static Candidate? ToCandidate(JsonElement item)
    {
        if (!item.TryGetProperty("syncedLyrics", out JsonElement synced) || synced.ValueKind != JsonValueKind.String) return null;
        if (!item.TryGetProperty("duration", out JsonElement duration) || duration.ValueKind != JsonValueKind.Number) return null;
        Line[] lines = Parse(synced.GetString()!);
        if (lines.Length == 0) return null;
        double end = lines.LastOrDefault(line => line.Text.Length > 0).Time.TotalSeconds;
        return new Candidate(duration.GetDouble(), end, lines);
    }

    static async Task<JsonDocument?> GetAsync(string url, CancellationToken token = default)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode) return null;
                using var stream = await response.Content.ReadAsStreamAsync(token);
                return await JsonDocument.ParseAsync(stream, cancellationToken: token);
            }
            catch (HttpRequestException)
            {
                if (attempt + 1 < Attempts && !token.IsCancellationRequested) await Task.Delay(RetryDelayMs, token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or JsonException)
            {
                return null;
            }
        }
        return null;
    }

    static IEnumerable<string> Queries(string title, string artist)
    {
        string song = ReworkTail.Replace(Pipes.Replace(Noise.Replace(title, ""), " "), "").Replace('—', '-').Replace('–', '-').Trim();
        string by = Channel.Replace(artist, "").Trim();
        if (song.Length == 0) song = title;

        string cleanSong = ReworkTail.Replace(song, "").Trim();
        string primaryBy = by.Split([',', '&', ';', '/'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? by;

        if (by.Length > 0) yield return $"track_name={Uri.EscapeDataString(song)}&artist_name={Uri.EscapeDataString(by)}";
        if (cleanSong.Length > 0 && cleanSong != song)
        {
            if (primaryBy.Length > 0) yield return $"track_name={Uri.EscapeDataString(cleanSong)}&artist_name={Uri.EscapeDataString(primaryBy)}";
            yield return "q=" + Uri.EscapeDataString(cleanSong + (primaryBy.Length > 0 ? " " + primaryBy : ""));
        }

        int dash = song.IndexOf(TitleSeparator, StringComparison.Ordinal);
        if (dash > 0)
        {
            yield return $"track_name={Uri.EscapeDataString(song[(dash + TitleSeparator.Length)..])}&artist_name={Uri.EscapeDataString(song[..dash])}";
            yield return "q=" + Uri.EscapeDataString(song.Replace(TitleSeparator, " "));
        }
        else
        {
            yield return "q=" + Uri.EscapeDataString(by.Length > 0 ? by + " " + song : song);
        }
    }

    static Line[] Parse(string lrc)
    {
        var lines = new List<Line>();
        foreach (string raw in lrc.Split('\n'))
        {
            Match stamped = Stamped.Match(raw.Trim());
            if (!stamped.Success) continue;

            string text = stamped.Groups[2].Value.Trim();
            foreach (Match stamp in Stamp.Matches(stamped.Groups[1].Value))
            {
                if (!int.TryParse(stamp.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int minutes)
                    || !double.TryParse(stamp.Groups[2].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double rest)
                    || minutes > MaxMinutes) continue;
                lines.Add(new Line(TimeSpan.FromSeconds(minutes * 60 + rest), text));
            }
        }
        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines.ToArray();
    }

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIsland/1.0");
        return http;
    }
}
