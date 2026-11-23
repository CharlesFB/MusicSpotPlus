using System;
using System.Collections.Generic;
using System.Linq;
using FB.Metronome.Data;
using UnityEngine;
namespace FB.Metronome.Serialization
{
    [Serializable] public sealed class ChartDocument
    {
        public int version = 1;
        public string audioPath;
        public string audioId;
        public string audioHash;
        public float bpm = 120, beatOffset, duration;
        public int beatsPerMeasure = 4, beatUnit = 4;
        public List<LaneDocument> lanes = new List<LaneDocument>();
    }
    [Serializable] public sealed class LaneDocument
    {
        public string laneId;
        public int subdivision = 4;
        public List<MetroEvent> events = new List<MetroEvent>();
    }
    /// <summary>运行时副本及其所有权。不会修改原始 ScriptableObject，Dispose 不销毁调用方的 clip。</summary>
    public sealed class ChartSession : IDisposable
    {
        public MetroSource Source { get; private set; }
        public List<EventLane> Lanes { get; } = new List<EventLane>();
        public static ChartSession Clone(MetroSource source, IEnumerable<EventLane> lanes)
            => FromJson(ToJson(source, lanes), source.clip);
        public static string ToJson(MetroSource source, IEnumerable<EventLane> lanes)
        {
            var snapshot = new List<EventLane>(lanes ?? throw new ArgumentNullException(nameof(lanes)));
            MetroValidation.Validate(source,snapshot);
            var doc = new ChartDocument { audioPath=source.clipPath, audioId=source.audioId, audioHash=source.audioHash, bpm=source.bpm, beatOffset=source.beatOffset,
                duration=source.clip ? source.clip.length : source.duration, beatsPerMeasure=source.beatsPerMeasure, beatUnit=source.beatUnit };
            foreach (var lane in snapshot) doc.lanes.Add(new LaneDocument { laneId=lane.laneId, subdivision=lane.snapSubdivision, events=lane.events });
            return JsonUtility.ToJson(doc,true);
        }
        /// <summary>先验证存档并获取音频路径，不创建 Unity 资源或启动加载。</summary>
        public static ChartDocument ReadDocument(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Empty chart document");
            var doc = JsonUtility.FromJson<ChartDocument>(json);
            ValidateDocument(doc);
            return doc;
        }
        private static void ValidateDocument(ChartDocument doc)
        {
            if (doc == null || doc.version != 1 || doc.lanes == null) throw new ArgumentException("Unsupported or invalid chart document");
            MetroValidation.ValidateTiming(doc.bpm, doc.beatOffset, doc.duration, doc.beatsPerMeasure, doc.beatUnit);
            var laneIds = new HashSet<string>();
            var eventIds = new HashSet<string>();
            foreach (var lane in doc.lanes)
            {
                if (lane == null) throw new ArgumentException("Null lane document");
                // === 仅为旧存档补齐缺失身份；重复身份必须显式修复，不能悄悄更改引用 ===
                if (lane.events != null)
                    foreach (var e in lane.events)
                        if (e != null && string.IsNullOrWhiteSpace(e.stableId)) e.stableId = Guid.NewGuid().ToString("N");
                MetroValidation.ValidateLane(lane.laneId, lane.subdivision, lane.events, doc.duration, laneIds, eventIds);
            }
        }
        public static ChartSession FromJson(string json, AudioClip clip = null)
            => FromDocument(ReadDocument(json), clip);

        public static ChartSession FromDocument(ChartDocument doc, AudioClip clip = null)
        {
            ValidateDocument(doc);
            var session = new ChartSession();
            try
            {
                session.Source = ScriptableObject.CreateInstance<MetroSource>();
                var s=session.Source;s.clip=clip;s.clipPath=doc.audioPath;s.bpm=doc.bpm;s.beatOffset=doc.beatOffset;
                s.audioId=doc.audioId;s.audioHash=doc.audioHash;
                s.duration=clip?clip.length:doc.duration;s.beatsPerMeasure=doc.beatsPerMeasure;s.beatUnit=doc.beatUnit;
                foreach(var data in doc.lanes)
                {
                    if(data==null || data.events==null || data.subdivision<1 || data.subdivision>64) throw new ArgumentException("Invalid lane");
                    var lane=ScriptableObject.CreateInstance<EventLane>();session.Lanes.Add(lane);
                    lane.source=s;lane.laneId=data.laneId;lane.snapSubdivision=data.subdivision;lane.events=data.events.Select(e => e.Copy()).ToList();
                }
                MetroValidation.Validate(s,session.Lanes);
                foreach(var lane in session.Lanes) lane.Renumber();
                return session;
            }
            catch { session.Dispose();throw; }
        }
        public void Dispose()
        {
            foreach(var lane in Lanes) Destroy(lane);
            Lanes.Clear();Destroy(Source);Source=null;
        }
        private static void Destroy(UnityEngine.Object obj)
        {
            if (!obj) return;
            if(Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
