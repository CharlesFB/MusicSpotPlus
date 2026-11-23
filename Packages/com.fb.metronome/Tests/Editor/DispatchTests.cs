using System;
using System.Collections.Generic;
using FB.Metronome.Data;
using FB.Metronome.Dispatch;
using NUnit.Framework;
using UnityEngine;
namespace FB.Metronome.Tests.Editor
{
    public class DispatchTests
    {
        EventLane lane;
        EventDispatcher scan;
        [SetUp] public void Setup()
        {
            lane = ScriptableObject.CreateInstance<EventLane>();
            lane.events = new List<MetroEvent> { new MetroEvent { time=0, eventId="zero" }, new MetroEvent { time=1, eventId="one" }, new MetroEvent { time=1, eventId="two" }, new MetroEvent { time=2, eventId="end" } };
            scan = new EventDispatcher(); scan.Load(new[] {lane});
        }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(lane);
        [Test] public void ZeroTailRollbackAndSameTime()
        {
            var seen = new List<string>();
            void Emit(EventLane l, MetroEvent e) => seen.Add(e.eventId);
            scan.Tick(0,Emit); scan.Tick(1,Emit); scan.Tick(.9,Emit); scan.Tick(1,Emit); scan.Tick(2,Emit);
            CollectionAssert.AreEqual(new[]{"zero","one","two","end"},seen);
        }
        [Test] public void SeekSkipsTargetAndBackwardAllowsReplay()
        {
            int n=0; scan.Seek(1); scan.Tick(1,(l,e)=>n++); Assert.AreEqual(0,n);
            scan.Seek(0); scan.Tick(1,(l,e)=>n++); Assert.AreEqual(2,n);
        }
        [Test] public void CallbackSeekInvalidatesOldScan()
        {
            int n=0; scan.Tick(2,(l,e)=>{n++;scan.Seek(2);}); scan.Tick(2,(l,e)=>n++); Assert.AreEqual(1,n);
        }
        [Test] public void PauseInvalidationPreservesRemainingEvents()
        {
            int n=0;scan.Tick(2,(l,e)=>{n++;scan.Invalidate();}); Assert.AreEqual(1,n);
            scan.Tick(2,(l,e)=>n++); Assert.AreEqual(4,n);
        }
        [Test] public void ExceptionDoesNotReplayAndSnapshotIgnoresEdits()
        {
            int errors=0,n=0;lane.events.Clear();scan.Tick(2,(l,e)=>{n++;throw new Exception();},ex=>errors++);
            scan.Tick(2,(l,e)=>n++);Assert.AreEqual(4,n);Assert.AreEqual(4,errors);
        }
    }
}
