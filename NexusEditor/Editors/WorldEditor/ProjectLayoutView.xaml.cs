using NexusEditor.Components;
using NexusEditor.GameProject;
using NexusEditor.Utilities;
using System.Windows;
using System.Windows.Controls;

namespace NexusEditor.Editors;

public partial class ProjectLayoutView : UserControl
{
    public ProjectLayoutView()
    {
        InitializeComponent();
    }

    private void OnAddGameEntity_Button_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        var vm = btn.DataContext as Scene;
        vm.AddGameEntityCommand.Execute(new GameEntity(vm) { Name = "空游戏实体" });
    }

    private void OnGameEntities_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        var newSelection = listBox.SelectedItems.Cast<GameEntity>().ToList();
        var previousSelection = newSelection.Except(e.AddedItems.Cast<GameEntity>()).Concat(e.RemovedItems.Cast<GameEntity>()).ToList();

        Project.UndoRedo.Add(new UndoRedoAction(
            () =>
            {
                listBox.UnselectAll();
                previousSelection.ForEach(x => (listBox.ItemContainerGenerator.ContainerFromItem(x) as ListBoxItem).IsSelected = true);
            },

            () =>
            {
                listBox.UnselectAll();
                newSelection.ForEach(x => (listBox.ItemContainerGenerator.ContainerFromItem(x) as ListBoxItem).IsSelected = true);
            },
            "选择更改"
            ));

        MSGameEntity msEntity = null;
        if (newSelection.Any())
        {
            msEntity = new MSGameEntity(newSelection);
        }
        GameEntityView.Instance.DataContext = msEntity;
    }
}
