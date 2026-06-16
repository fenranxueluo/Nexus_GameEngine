using System.Windows;
using System.Windows.Controls;

namespace NexusEditor.Utilities;
/// <summary>
/// RenderSurfaceView.xaml 的交互逻辑
/// </summary>
public partial class RenderSurfaceView : UserControl, IDisposable
{
    private RenderSurfaceHost _host = null;

    public RenderSurfaceView()
    {
        InitializeComponent();
        Loaded += OnRenderSurfaceViewLoaded;
    }

    private void OnRenderSurfaceViewLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnRenderSurfaceViewLoaded;

        _host = new RenderSurfaceHost(ActualWidth, ActualHeight);
        Content = _host;
    }

    #region IDisposable Support

    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _host.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
