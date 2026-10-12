using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynamicIsland;

public enum ClipboardKind
{
    Text,
    Link,
    Image,
    File,
}

public sealed record ClipboardItem(
    ClipboardKind Kind,
    string PreviewText,
    string FullText,
    ImageSource? Thumbnail,
    string Badge
);

sealed class ClipboardService
{
    public event Action<ClipboardItem>? Copied;

    readonly Dispatcher _dispatcher;
    string _lastText = "";
    DateTime _lastCopiedAt = DateTime.MinValue;
    int _debounceToken;

    public ClipboardService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void OnClipboardUpdate()
    {
        if (!Settings.ClipboardPeekEnabled) return;

        int token = ++_debounceToken;
        // Small delay (80ms) to ensure the source application releases the clipboard lock
        _ = Task.Delay(80).ContinueWith(_ =>
        {
            _dispatcher.InvokeAsync(() => ProcessClipboard(token));
        });
    }

    void ProcessClipboard(int token)
    {
        if (token != _debounceToken || !Settings.ClipboardPeekEnabled) return;

        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(text)) return;

                // Ignore immediate duplicates within 3 seconds
                if (text == _lastText && DateTime.UtcNow - _lastCopiedAt < TimeSpan.FromSeconds(3)) return;

                _lastText = text;
                _lastCopiedAt = DateTime.UtcNow;

                bool isUrl = IsWebUrl(text, out string urlDisplay);
                if (isUrl)
                {
                    Copied?.Invoke(new ClipboardItem(
                        ClipboardKind.Link,
                        urlDisplay,
                        text,
                        null,
                        "Ссылка"
                    ));
                }
                else
                {
                    string singleLine = Regex.Replace(text, @"\s+", " ");
                    string preview = singleLine.Length > 45 ? singleLine[..45] + "…" : singleLine;
                    Copied?.Invoke(new ClipboardItem(
                        ClipboardKind.Text,
                        preview,
                        text,
                        null,
                        "Текст"
                    ));
                }
            }
            else if (Clipboard.ContainsImage())
            {
                BitmapSource? img = Clipboard.GetImage();
                if (img == null) return;

                string imgKey = $"{img.PixelWidth}x{img.PixelHeight}";
                if (imgKey == _lastText && DateTime.UtcNow - _lastCopiedAt < TimeSpan.FromSeconds(3)) return;
                _lastText = imgKey;
                _lastCopiedAt = DateTime.UtcNow;

                Copied?.Invoke(new ClipboardItem(
                    ClipboardKind.Image,
                    $"{img.PixelWidth} × {img.PixelHeight}",
                    "",
                    img,
                    "PNG"
                ));
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                if (files == null || files.Count == 0) return;

                string first = files[0] is { } f ? Path.GetFileName(f) ?? f : "Файл";
                string text = files.Count == 1 ? first : $"{first} (+{files.Count - 1})";
                if (text == _lastText && DateTime.UtcNow - _lastCopiedAt < TimeSpan.FromSeconds(3)) return;
                _lastText = text;
                _lastCopiedAt = DateTime.UtcNow;

                Copied?.Invoke(new ClipboardItem(
                    ClipboardKind.File,
                    text,
                    files[0] ?? "",
                    null,
                    "Файл"
                ));
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }

    static bool IsWebUrl(string text, out string display)
    {
        display = text;
        if (Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            display = uri.Host + (uri.PathAndQuery != "/" ? uri.PathAndQuery : "");
            if (display.Length > 45) display = display[..45] + "…";
            return true;
        }

        if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            display = text.Length > 45 ? text[..45] + "…" : text;
            return true;
        }

        return false;
    }
}
