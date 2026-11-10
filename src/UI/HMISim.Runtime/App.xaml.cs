using System.IO;
using System.Windows;
using HMISim.ProjectModel;

namespace HMISim.Runtime;

/// <summary>
/// 应用入口：带工程文件参数时以「工程模式」打开画面窗口（双击工程/组态端预览的正式通道）；
/// 无参数时打开阶段 0 的演示列表窗口。
/// </summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            try
            {
                var document = ProjectFile.Load(e.Args[0]);
                new ScreenWindow(document).Show();
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工程失败：{ex.Message}", "HMISim 运行端", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        new MainWindow().Show();
    }
}
