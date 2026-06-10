using NexusEditor.Components;
using NexusEditor.GameProject;
using NexusEditor.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NexusEditor.Editors;
/// <summary>
/// GameEntityView.xaml 的交互逻辑
/// </summary>
public partial class GameEntityView : UserControl
{
    private Action _undoAction;
    private String _propertyName;
    public static GameEntityView Instance { get; private set; }
    public GameEntityView()
    {
        InitializeComponent();
        DataContext = null;
        Instance = this;
        DataContextChanged += (_, __) =>
        {
            if (DataContext != null)
            {
                (DataContext as MSEntity).PropertyChanged += (s, e) => _propertyName = e.PropertyName;
            }
        };
    }

    private Action GetRenameAction()
    {
        var vm = DataContext as MSEntity;
        var Selection = vm.SelectedEntities.Select(entity => (entity, entity.Name)).ToList();
        return new Action(() =>
        {
           Selection.ForEach(item => item.entity.Name = item.Name);
            (DataContext as MSEntity).Refresh();
        });
    }

    private Action GetIsEnabledAction()
    {
        var vm = DataContext as MSEntity;
        var Selection = vm.SelectedEntities.Select(entity => (entity, entity.IsEnabled)).ToList();
        return new Action(() =>
        {
            Selection.ForEach(item => item.entity.IsEnabled = item.IsEnabled);
            (DataContext as MSEntity).Refresh();
        });
    }

    private void OnName_TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _propertyName = string.Empty;
        _undoAction = GetRenameAction();
    }

    private void OnName_TextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if(_propertyName == nameof(MSEntity.Name) && _undoAction != null)
        {
           var redoAction = GetRenameAction();
            Project.UndoRedo.Add(new UndoRedoAction(_undoAction, redoAction, "重命名游戏实体"));
            _propertyName = null;
        }
        _undoAction = null;
    }

    private void OnIsEnabled_CheckBox_Click(object sender, RoutedEventArgs e)
    {
        var undoAction = GetIsEnabledAction();
        var vm = DataContext as MSEntity;
        vm.IsEnabled = (sender as CheckBox).IsChecked == true;
        var redoAction = GetIsEnabledAction();
        Project.UndoRedo.Add(new UndoRedoAction(undoAction, redoAction,
            vm.IsEnabled == true ? "启用游戏实体" : "禁用游戏实体"));
    }
}
