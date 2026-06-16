using System.IO;
using System.Windows;

namespace NexusEditor
{
    /// <summary>
    /// Interaction logic for EnginePathDialog.xaml
    /// </summary>
    public partial class EnginePathDialog : Window
    {
        public string NexusPath { get; private set; }

        public EnginePathDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void OnOk_ButtonClick(object sender, RoutedEventArgs e)
        {
            var path = pathTextBox.Text.Trim();
            messageTextBlock.Text = string.Empty;

            if (string.IsNullOrEmpty(path))
            {
                messageTextBlock.Text = "请输入有效路径";
            }
            else if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                messageTextBlock.Text = "路径中使用了无效字符";
            }
            else if (!Directory.Exists(Path.Combine(path, @"Engine\EngineAPI")))
            {
                messageTextBlock.Text = "无法在指定位置找到引擎";
            }
            if (string.IsNullOrEmpty(messageTextBlock.Text))
            {
                if (!Path.EndsInDirectorySeparator(path)) path += @"\";
                NexusPath = path;
                DialogResult = true;
                Close();
            }
        }
    }
}