using System;
using System.Collections.Generic;
namespace FB.Metronome.Data
{
    public static class MetroValidation
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // === 播放、保存和恢复使用同一套规则，避免生成无法读回的存档 ===
        internal static void ValidateTiming(float bpm, float offset, double duration, int beats, int unit)
        {
            if (!IsFinite(bpm) || bpm <= 0 || !IsFinite(offset) ||
                !IsFinite(duration) || duration < 0 || beats < 1 || unit < 1)
                throw new ArgumentException("Invalid timing: BPM must be positive; time values finite; meter values positive");
        }

        internal static void ValidateLane(string id, int subdivision, IEnumerable<MetroEvent> events,
            double duration, HashSet<string> laneIds, HashSet<string> eventIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !laneIds.Add(id))
                throw new ArgumentException("Lane IDs must be nonempty and unique: " + id);
            if (subdivision < 1 || subdivision > 64) throw new ArgumentException("Lane subdivision must be 1..64: " + id);
            if (events == null) throw new ArgumentException("Missing events collection: " + id);
            foreach (var e in events)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.eventId) || !IsFinite(e.time) || e.time < 0 || e.time > duration)
                    throw new ArgumentException("Events require an ID and finite time inside the source duration: " + id);
                if (string.IsNullOrWhiteSpace(e.stableId) || !eventIds.Add(e.stableId))
                    throw new ArgumentException("Event stable IDs must be nonempty and unique across the chart: " + id + "/" + e.stableId);
            }
        }

        public static void Validate(MetroSource source, IEnumerable<EventLane> lanes)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            if (lanes == null) throw new ArgumentNullException(nameof(lanes));
            double duration = source.clip ? source.clip.length : source.duration;
            ValidateTiming(source.bpm, source.beatOffset, duration, source.beatsPerMeasure, source.beatUnit);
            var laneIds = new HashSet<string>();
            var eventIds = new HashSet<string>();
            foreach (var lane in lanes)
            {
                if (!lane || lane.source != source) throw new ArgumentException("Lane must reference the configured source");
                ValidateLane(lane.laneId, lane.snapSubdivision, lane.events, duration, laneIds, eventIds);
            }
        }
    }
}
