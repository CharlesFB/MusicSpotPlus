using UnityEngine.Scripting.APIUpdating;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace FB.Metronome.Data
{
    /// <summary>
    /// 控制器资产：引用同一个 MetroSource，持有一类事件。一音源可建多条 Lane。
    /// </summary>
    [CreateAssetMenu(fileName = "EventLane", menuName = "Metronome/Event Lane")]
    [MovedFrom(true, sourceNamespace: "MagicTavern.FrontEngine.Metronome", sourceAssembly: "MetronomeRuntime", sourceClassName: "EventLane")]
    public class EventLane : ScriptableObject
    {
        public MetroSource source;
        public string laneId = "default";
        public int snapSubdivision = 4;
        public List<MetroEvent> events = new List<MetroEvent>();

        public MetroEvent AddEvent(float time, string eventId, bool snap = true, string payload = "")
        {
            if (!MetroValidation.IsFinite(time) || time < 0 || string.IsNullOrWhiteSpace(eventId))
                throw new ArgumentException("Event requires a nonnegative finite time and an ID");
            if (events == null)
            {
                events = new List<MetroEvent>();
            }
            float t = time;
            if (snap && source != null)
            {
                t = source.Snap(time, snapSubdivision);
            }

            if (source) t = UnityEngine.Mathf.Clamp(t, 0, source.clip ? source.clip.length : source.duration);
            var metroEvent = new MetroEvent
            {
                time = t,
                eventId = eventId,
                payload = payload
            };
            events.Add(metroEvent);
            Renumber();
            return metroEvent;
        }

        public bool RemoveEvent(MetroEvent metroEvent)
        {
            if (events == null || metroEvent == null)
            {
                return false;
            }

            bool removed = events.Remove(metroEvent);
            if (removed)
            {
                Renumber();
            }

            return removed;
        }

        public void Renumber()
        {
            if (events == null)
            {
                return;
            }

            events = events.Where(e => e != null).OrderBy(e => e.time).ToList();
            for (int i = 0; i < events.Count; i++)
            {
                events[i].index = i;
                if (string.IsNullOrEmpty(events[i].stableId)) events[i].stableId = Guid.NewGuid().ToString("N");
            }
        }
    }
}
