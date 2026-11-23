using FB.Metronome.Data;
namespace FB.Metronome.Editor.UI
{
    public static class TimelineMath
    {
        public static bool IsFinite(double value) => MetroTime.IsFinite(value);
        public static bool TrySeconds(string text, out float value) => MetroTime.TrySeconds(text, out value);
        public static float Clamp(double time, double duration) => MetroTime.Clamp(time, duration);
        public static float EventTime(double time, double duration, MetroSource source, int subdivision, bool snap)
            => MetroTime.EventTime(time, duration, source, subdivision, snap);
    }
}
