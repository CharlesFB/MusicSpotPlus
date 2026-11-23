using FB.Metronome.Editor.Services;
using UnityEditor;

namespace FB.Metronome.Editor.Entry
{
    [InitializeOnLoad]
    public static class ApplicationStater
    {
        static ApplicationStater()
        {
            // 仅初始化编辑器服务。运行时入口是 MetroConductor。
            ServiceManager.Initialize();
        }
    }
}