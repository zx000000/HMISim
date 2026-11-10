using System.Windows;
using HMISim.ProjectModel;

namespace HMISim.Runtime;

/// <summary>工程模式窗口：只承载 ScreenPlayer（播放逻辑全在播放器内）。</summary>
public partial class ScreenWindow : Window
{
    /// <summary>以指定工程文档创建窗口。</summary>
    public ScreenWindow(ProjectDocument document)
    {
        InitializeComponent();
        Title = $"HMISim 运行端 — {document.Name}";
        Player.Project = document;
    }
}
