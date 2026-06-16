using System.Windows;
using System.Windows.Controls;

namespace NexusEditor.Utilities;

public partial class LoggerView : UserControl
{
    public LoggerView()
    {
        InitializeComponent();
    }

    private void OnClear_Button_Click(object sender, RoutedEventArgs e)
    {
        Logger.Clear();
    }

    private void OnMessageFilter_Button_Click(object sender, RoutedEventArgs e)
    {
        var filtet = 0x0;
        if(toggleInfo.IsChecked == true) filtet |= (int)MessageType.Info;
        if(toggleWarnings.IsChecked == true) filtet |= (int)MessageType.Warning;
        if(toggleErrors.IsChecked == true) filtet |= (int)MessageType.Error;
        Logger.SetMessageFilter(filtet);
    }
}
