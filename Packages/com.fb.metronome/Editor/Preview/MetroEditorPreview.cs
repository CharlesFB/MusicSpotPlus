using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FB.Metronome.Editor.Preview
{
    /// <summary>
    /// Editor-only AudioUtil 适配器。预览通道是 Unity 全局共享的，不触发 Conductor 事件。
    /// 内部 API 不可用时禁用预览，编谱仍然可用。
    /// </summary>
    public sealed class MetroEditorPreview : IDisposable
    {
        private static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static MethodInfo Find(string name, params Type[] args) => AudioUtil?.GetMethod(name,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
        private static readonly MethodInfo PlayMethod = Find("PlayPreviewClip", typeof(AudioClip), typeof(int), typeof(bool));
        private static readonly MethodInfo StopMethod = Find("StopAllPreviewClips");
        private static readonly MethodInfo PlayingMethod = Find("IsPreviewClipPlaying");
        private static readonly MethodInfo PositionMethod = Find("GetPreviewClipSamplePosition");
        private static MetroEditorPreview _owner;
        private AudioClip _clip;
        private bool _failed;
        private double _started;
        private float _startPosition;
        public bool LostOwnership { get; private set; }
        // Unity 2022 没有公开的预览 clip 身份查询。检测不连续跳转后只放弃所有权，绝不停止新的试听。
        private bool OwnsChannel()
        {
            if (_owner != this || !Available) return false;
            if (!_clip) { _owner = null; return false; }
            if ((bool)PlayingMethod.Invoke(null, null))
            {
                float actual = (int)PositionMethod.Invoke(null, null) / (float)_clip.frequency;
                double expected = _startPosition + EditorApplication.timeSinceStartup - _started;
                if (Math.Abs(actual - expected) > .5)
                { LostOwnership = true; _owner = null; return false; }
            }
            return true;
        }

        public bool Available => !_failed && PlayMethod != null && StopMethod != null && PlayingMethod != null && PositionMethod != null;
        public bool IsPlaying => OwnsChannel() && (bool)PlayingMethod.Invoke(null, null);
        public float Position => _clip != null && OwnsChannel()
            ? (int)PositionMethod.Invoke(null, null) / (float)_clip.frequency : 0;

        public bool Play(AudioClip clip, float seconds)
        {
            if (!Available || clip == null || clip.samples <= 0) return false;
            try
            {
                // AudioUtil 只有一个共享预览通道，开始本窗口试听会替换 Inspector 的试听。
                StopMethod.Invoke(null, null);
                _owner = this;
                _clip = clip; LostOwnership = false; _started = EditorApplication.timeSinceStartup; _startPosition = seconds;
                int sample = (int)Math.Min(clip.samples - 1L, Math.Max(0, (double)seconds * clip.frequency));
                PlayMethod.Invoke(null, new object[] { clip, sample, false });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Metronome] Editor preview unavailable: " + e.GetBaseException().Message);
                Stop();
                _failed = true;
                return false;
            }
        }

        // 暂停由窗口先读取位置，再停止；再次 Play 从保存的采样位置开始。
        public void Stop()
        {
            if (!OwnsChannel()) return;
            try { StopMethod?.Invoke(null, null); }
            catch (Exception e) { Debug.LogWarning("[Metronome] Preview stop: " + e.GetBaseException().Message); }
            finally { _owner = null; _clip = null; }
        }

        public void Dispose() => Stop();
    }
}
