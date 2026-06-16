using NexusEditor.GameProject;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace NexusEditor;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public static string NexusPath { get; private set; }
    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnMainWindowLoaded;
        Closing += OnMainWindowClosing;
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnMainWindowLoaded;
        GetEnginePath();
        OpenProjectBrowserDialog();
    }

    private void GetEnginePath()
    {
        var nexusPath = Environment.GetEnvironmentVariable("NEXUS_ENGINE", EnvironmentVariableTarget.User);
        if (nexusPath == null || !Directory.Exists(Path.Combine(nexusPath, @"Engine\EngineAPI")))
        {
            var dlg = new EnginePathDialog();
            if (dlg.ShowDialog() == true)
            {
                NexusPath = dlg.NexusPath;
                Environment.SetEnvironmentVariable("NEXUS_ENGINE", NexusPath.ToUpper(), EnvironmentVariableTarget.User);
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
        else
        {
            NexusPath = nexusPath;
        }
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        Closing -= OnMainWindowClosing;
        Project.Current?.Unload();
    }

    private void OpenProjectBrowserDialog()
    {
        var projectBrowser = new ProjectBrowserDialog();
        if (projectBrowser.ShowDialog() == false || projectBrowser.DataContext == null)
        {
            Application.Current.Shutdown();
        }
        else
        {
            Project.Current?.Unload();
            DataContext = projectBrowser.DataContext;
        }
    }
}