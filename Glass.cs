using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace DynamicIsland;

sealed class Glass
{
    [ComImport, Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICompositorDesktopInterop
    {
        void CreateDesktopWindowTarget(IntPtr hwnd, bool topmost, out IntPtr target);
    }

    sealed class Pane
    {
        public readonly SpriteVisual Visual;
        readonly CompositionRoundedRectangleGeometry _outline;

        public Pane(Compositor compositor, CompositionBrush backdrop)
        {
            _outline = compositor.CreateRoundedRectangleGeometry();
            Visual = compositor.CreateSpriteVisual();
            Visual.Brush = backdrop;
            Visual.Clip = compositor.CreateGeometricClip(_outline);
            Visual.IsVisible = false;
        }

        public void Fit((Rect Box, double Radius) part, Matrix toPixels)
        {
            if (part.Box.IsEmpty)
            {
                Visual.IsVisible = false;
                return;
            }
            Point from = toPixels.Transform(part.Box.TopLeft), to = toPixels.Transform(part.Box.BottomRight);
            float w = (float)(to.X - from.X), h = (float)(to.Y - from.Y);
            if (w <= 0 || h <= 0)
            {
                Visual.IsVisible = false;
                return;
            }
            Visual.Offset = new Vector3((float)from.X, (float)from.Y, 0);
            Visual.Size = new Vector2(w, h);
            _outline.Offset = Vector2.Zero;
            _outline.Size = new Vector2(w, h);
            _outline.CornerRadius = new Vector2(
                Math.Max(0, (float)(part.Radius * toPixels.M11)),
                Math.Max(0, (float)(part.Radius * toPixels.M22)));
            Visual.IsVisible = true;
        }
    }

    static bool _queueStarted;

    readonly Window _over;
    Compositor? _compositor;
    DesktopWindowTarget? _target;
    IntPtr _hwnd;
    Pane? _pill, _bubble;

    public Glass(Window over) => _over = over;

    public bool IsOn => _target != null;

    public void Show()
    {
        if (_target != null) return;
        try
        {
            _queueStarted = _queueStarted || Native.StartDispatcherQueue();
            _hwnd = Native.CreateBackdropWindow();
            _compositor = new Compositor();
            _compositor.As<ICompositorDesktopInterop>().CreateDesktopWindowTarget(_hwnd, true, out IntPtr target);
            _target = DesktopWindowTarget.FromAbi(target);
            Marshal.Release(target);

            CompositionBrush backdrop = _compositor.CreateHostBackdropBrush();
            Windows.UI.Composition.ContainerVisual root = _compositor.CreateContainerVisual();
            _pill = new Pane(_compositor, backdrop);
            _bubble = new Pane(_compositor, backdrop);
            root.Children.InsertAtTop(_pill.Visual);
            root.Children.InsertAtTop(_bubble.Visual);
            _target.Root = root;
            Place();
            App.Log("Glass.Show initialized successfully");
        }
        catch (Exception ex)
        {
            App.Log(ex);
            Hide();
        }
    }

    public void Hide()
    {
        _target?.Dispose();
        _compositor?.Dispose();
        if (_hwnd != IntPtr.Zero) Native.Destroy(_hwnd);
        _target = null;
        _compositor = null;
        _hwnd = IntPtr.Zero;
        _pill = _bubble = null;
    }

    public void Place()
    {
        if (_hwnd != IntPtr.Zero) Native.PlaceBehind(_hwnd, new WindowInteropHelper(_over).Handle);
    }

    public void Cut((Rect Box, double Radius) pill, (Rect Box, double Radius) bubble, Matrix toPixels)
    {
        _pill?.Fit(pill, toPixels);
        _bubble?.Fit(bubble, toPixels);
    }
}
