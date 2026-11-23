using FB.Metronome.Data;
using System;
using System.Globalization;

namespace FB.Metronome.Data
{
    // === 所有窗口输入与事件写入共用同一套秒数边界 ===
    public static class MetroTime
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static bool TrySeconds(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && IsFinite(value);
        }

        public static float Clamp(double time, double duration)
        {
            if (!IsFinite(time) || !IsFinite(duration) || duration <= 0) return 0;
            return (float)Math.Max(0, Math.Min(time, duration));
        }

        public static float EventTime(double time, double duration, MetroSource source, int subdivision, bool snap)
        {
            float result = Clamp(time, duration);
            if (snap && source != null && IsFinite(source.bpm) && IsFinite(source.beatOffset))
                result = source.Snap(result, subdivision);
            // 吸附到最后一个网格时也可能越过片尾。
            return Clamp(result, duration);
        }
    }
}
