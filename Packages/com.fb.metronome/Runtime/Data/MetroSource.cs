using FB.Metronome.Analysis;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine;

namespace FB.Metronome.Data
{
    /// <summary>
    /// 每音源一份：解析与时间轴（BPM / 偏移 / 时长）。不存放玩法事件。
    /// </summary>
    [CreateAssetMenu(fileName = "MetroSource", menuName = "Metronome/Metro Source")]
    [MovedFrom(true, sourceNamespace: "MagicTavern.FrontEngine.Metronome", sourceAssembly: "MetronomeRuntime", sourceClassName: "MetroSource")]
    public class MetroSource : ScriptableObject
    {
        [Header("Clip")]
        public AudioClip clip;
        public string clipAddress;
        public string clipPath;
        public string audioId;
        public string audioHash;

        [Header("Timeline")]
        public float bpm = 120f;
        public float beatOffset;
        public float duration;
        public int beatsPerMeasure = 4;
        [Tooltip("拍号分母。BPM 以此音符为一拍；每拍秒数始终为 60/BPM。")]
        public int beatUnit = 4;

        /// <summary>
        /// 从 Direct Clip 填充时长并估算 BPM。Addressables / 外部路径加载属于后续阶段。
        /// </summary>
        public void AnalyzeFromClip()
        {
            if (clip == null)
            {
                Debug.LogWarning("[Metronome] MetroSource.clip is null");
                return;
            }

            duration = clip.length;
            int analyzed = UniBpmAnalyzer.AnalyzeBpm(clip);
            if (analyzed > 0)
            {
                bpm = analyzed;
            }
        }

        /// <summary>
        /// 将时间吸附到拍网格。subdivision=4 表示 1/4 拍。
        /// </summary>
        public float Snap(float timeSeconds, int subdivision)
        {
            if (bpm <= 0f || subdivision <= 0)
            {
                return timeSeconds;
            }

            float grid = 60f / bpm / subdivision;
            float shifted = timeSeconds - beatOffset;
            return Mathf.Round(shifted / grid) * grid + beatOffset;
        }

        public float BeatDuration => bpm > 0f ? 60f / bpm : 0f;
    }
}
