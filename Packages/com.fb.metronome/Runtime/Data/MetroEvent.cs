using UnityEngine.Scripting.APIUpdating;
using System;

namespace FB.Metronome.Data
{
    /// <summary>
    /// 一条事件轨上的单个时间点。面板按钮只是它的视图，运行时按 eventId 分发。
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "MagicTavern.FrontEngine.Metronome", sourceAssembly: "MetronomeRuntime", sourceClassName: "MetroEvent")]
    public class MetroEvent
    {
        public string stableId = Guid.NewGuid().ToString("N");
        public int index;
        public float time;
        public string eventId;
        public string payload;
        public MetroEvent Copy() => (MetroEvent)MemberwiseClone();
        /// <summary>用于用户创建另一个事件；快照或存档复制仍使用 Copy 保持身份。</summary>
        public MetroEvent CopyAsNew()
        {
            var copy = Copy();
            copy.stableId = Guid.NewGuid().ToString("N");
            return copy;
        }
    }
}
