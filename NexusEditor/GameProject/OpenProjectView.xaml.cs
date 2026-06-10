using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NexusEditor.GameProject;

public partial class OpenProjectView : UserControl
{
    public OpenProjectView()
    {
        InitializeComponent();

        Loaded += (s, e) =>
        {
            var item = projectsListBox.ItemContainerGenerator.ContainerFromIndex(projectsListBox.SelectedIndex) as ListBoxItem;
            item?.Focus();
        };
    }
    
    private void OnOpen_Button_Click(object sender, RoutedEventArgs e)
    {
        OpenSelectedProject();
    }

    private void OpenListBoxItem_Mouse_DoubleClick(object sender,MouseButtonEventArgs e)
    {
        OpenSelectedProject();
    }

    private void OpenSelectedProject()
    {
        var project = OpenProject.Open(projectsListBox.SelectedItem as ProjectData);
        bool dialogResult = false;
        var win = Window.GetWindow(this);
        if (project != null)
        {
            dialogResult = true;
            win.DataContext = project;
        }
        
        win.DialogResult = dialogResult;
        win.Close();
    }

}


