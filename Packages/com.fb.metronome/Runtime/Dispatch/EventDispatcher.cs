using System;
using System.Collections.Generic;
using FB.Metronome.Data;

namespace FB.Metronome.Dispatch
{
    /// <summary>有序播放快照。时间回退不重发；Seek 跳过目标点；Reset 包含零秒。</summary>
    public sealed class EventDispatcher
    {
        private sealed class Entry { public EventLane Lane; public MetroEvent Event; public int Order; }
        private readonly List<Entry> entries = new List<Entry>();
        private int cursor, version;
        private double waterline = double.NegativeInfinity;
        public void Load(IEnumerable<EventLane> lanes)
        {
            entries.Clear();
            int order = 0;
            foreach (var lane in lanes)
                foreach (var e in lane.events)
                    entries.Add(new Entry { Lane = lane, Order = order++, Event = e.Copy() });
            entries.Sort((a,b) => { int c = a.Event.time.CompareTo(b.Event.time); return c != 0 ? c : a.Order.CompareTo(b.Order); });
            Reset();
        }
        public void Reset() { cursor = 0; waterline = double.NegativeInfinity; version++; }
        public void Invalidate() { version++; }
        public void Seek(double time)
        {
            version++; waterline = time;
            int lo = 0, hi = entries.Count;
            while (lo < hi) { int mid = (lo + hi) / 2; if (entries[mid].Event.time <= time) lo = mid + 1; else hi = mid; }
            cursor = lo;
        }
        public void Tick(double time, Action<EventLane, MetroEvent> emit, Action<Exception> onError = null)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < waterline) return;
            int stamp = version;
            // === 先推进游标，再进入用户代码；控制命令使旧扫描失效 ===
            while (cursor < entries.Count && entries[cursor].Event.time <= time)
            {
                var entry = entries[cursor++];
                try { emit(entry.Lane, entry.Event); } catch (Exception ex) { onError?.Invoke(ex); }
                if (stamp != version) return;
            }
            waterline = time;
        }
    }
}
