using System;
using FB.Metronome.Data;
using FB.Metronome.Serialization;
using NUnit.Framework;
using UnityEngine;
namespace FB.Metronome.Tests.Editor
{
    public class ChartDocumentTests
    {
        [Test] public void RoundTripPreservesIdentityAndCloneDoesNotEditAsset()
        {
            var source=ScriptableObject.CreateInstance<MetroSource>();source.duration=10;
            var lane=ScriptableObject.CreateInstance<EventLane>();lane.source=source;
            var original=lane.AddEvent(1,"tap",false);
            try
            {
                string json=ChartSession.ToJson(source,new[]{lane});
                using(var copy=ChartSession.FromJson(json))
                {
                    Assert.AreEqual(original.stableId,copy.Lanes[0].events[0].stableId);
                    copy.Lanes[0].events[0].time=2;Assert.AreEqual(1,original.time);
                    Assert.Throws<ArgumentException>(()=>ChartSession.FromJson(json.Replace("\"version\": 1","\"version\": 999")));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(lane);UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test] public void DuplicateLaneAndInvalidTimeAreRejected()
        {
            var source=ScriptableObject.CreateInstance<MetroSource>();source.duration=10;
            var lane=ScriptableObject.CreateInstance<EventLane>();lane.source=source;
            try
            {
                Assert.Throws<ArgumentException>(()=>lane.AddEvent(float.NaN,"tap"));
                Assert.Throws<ArgumentException>(()=>MetroValidation.Validate(source,new[]{lane,lane}));
            }
            finally{UnityEngine.Object.DestroyImmediate(lane);UnityEngine.Object.DestroyImmediate(source);}
        }
        [TestCase(0, 4)]
        [TestCase(120, 0)]
        [TestCase(120, 65)]
        public void SaveAndReadRejectTheSameInvalidTiming(float bpm, int subdivision)
        {
            var source=ScriptableObject.CreateInstance<MetroSource>();source.duration=10;source.bpm=bpm;
            var lane=ScriptableObject.CreateInstance<EventLane>();lane.source=source;lane.snapSubdivision=subdivision;
            try
            {
                Assert.Throws<ArgumentException>(()=>ChartSession.ToJson(source,new[]{lane}));
                var doc=new ChartDocument {bpm=bpm,duration=10};
                doc.lanes.Add(new LaneDocument {laneId=lane.laneId,subdivision=subdivision});
                Assert.Throws<ArgumentException>(()=>ChartSession.ReadDocument(JsonUtility.ToJson(doc)));
            }
            finally { UnityEngine.Object.DestroyImmediate(lane);UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test] public void DuplicateIdentityRejectedAndExplicitDuplicationGetsNewIdentity()
        {
            var doc=new ChartDocument {duration=10};
            var e=new MetroEvent {eventId="tap",time=1};
            doc.lanes.Add(new LaneDocument {laneId="a",events=new System.Collections.Generic.List<MetroEvent>{e}});
            doc.lanes.Add(new LaneDocument {laneId="b",events=new System.Collections.Generic.List<MetroEvent>{e.Copy()}});
            Assert.Throws<ArgumentException>(()=>ChartSession.FromDocument(doc));
            Assert.AreEqual(e.stableId,doc.lanes[1].events[0].stableId);
            doc.lanes[1].events[0]=e.CopyAsNew();
            Assert.AreNotEqual(e.stableId,doc.lanes[1].events[0].stableId);
            using(var session=ChartSession.FromDocument(doc))
            {
                session.Lanes[0].events[0].time=3;
                Assert.AreEqual(1,e.time);
            }
        }
        [Test] public void ActualAudioTooShortRejectsWithoutTrimmingDocument()
        {
            var doc=new ChartDocument {duration=10};
            doc.lanes.Add(new LaneDocument {laneId="notes",events=new System.Collections.Generic.List<MetroEvent>{new MetroEvent {eventId="tap",time=5}}});
            var clip=AudioClip.Create("short",44100,1,44100,false);
            try
            {
                Assert.Throws<ArgumentException>(()=>ChartSession.FromDocument(doc,clip));
                Assert.AreEqual(1,doc.lanes[0].events.Count);
                Assert.AreEqual(5,doc.lanes[0].events[0].time);
            }
            finally {UnityEngine.Object.DestroyImmediate(clip);}
        }
    }
}
