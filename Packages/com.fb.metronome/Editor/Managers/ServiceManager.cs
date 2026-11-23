namespace FB.Metronome.Editor.Services
{
    /// <summary>
    /// 编辑器域重载后的服务注册入口。运行时播放不要走这里，用场景里的 MetroConductor。
    /// </summary>
    public sealed class ServiceManager
    {
        private static ServiceManager s_instance;
#if UNITY_2020_2_OR_NEWER
        public static ServiceManager Instance => s_instance ??= new ServiceManager();
#else
        public static ServiceManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new ServiceManager();
                }

                return s_instance;
            }
        }
#endif
        ServiceManager()
        {
        }

        public static void Initialize()
        {
            // 服务注册前(如果这是域重载后执行的)先清理掉之前注册的服务
            ServiceLocator.Clear();
            
            // 添加注册服务接口与实现
            // Sample : ServiceLocator.Register<IBusinessService>(new BusinessManager);
        }
    }
}
