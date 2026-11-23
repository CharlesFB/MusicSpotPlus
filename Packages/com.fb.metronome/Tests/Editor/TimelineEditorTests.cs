using FB.Metronome.Data;
using FB.Metronome.Editor.Analysis;
using FB.Metronome.Editor.Preview;
using FB.Metronome.Editor.UI;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FB.Metronome.Tests.Editor
{
    public class TimelineEditorTests
    {
        private MetroSource _source;
        private EventLane _lane;
        private MetronomeGrapherWindow _window;

        [SetUp]
        public void SetUp()
        {
            _source = ScriptableObject.CreateInstance<MetroSource>();
            _source.duration = 10;
            _source.bpm = 137;
            _lane = ScriptableObject.CreateInstance<EventLane>();
            _lane.source = _source;
            _lane.laneId = "tap";
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
            Undo.ClearUndo(_lane);
            Undo.ClearUndo(_source);
            Object.DestroyImmediate(_lane);
            Object.DestroyImmediate(_source);
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("abc")]
        public void TimeInput_RejectsNonFiniteValues(string value)
        {
            Assert.IsFalse(TimelineMath.TrySeconds(value, out _));
        }

        [Test]
        public void Snap_ClampsAfterRounding_AndSupportsTriplets()
        {
            _source.bpm = 120;
            Assert.AreEqual(0.49f, TimelineMath.EventTime(0.49, 0.49, _source, 4, true));
            Assert.AreEqual(0, TimelineMath.EventTime(-1, 10, _source, 4, true));
            Assert.AreEqual(1f / 6, TimelineMath.EventTime(0.17, 10, _source, 3, true), 0.00001);
            Assert.AreEqual(0.17f, TimelineMath.EventTime(0.17, 10, _source, 3, false));
        }

        [Test]
        public void MarkerHitTest_UsesTimeAndVerticalBand_CyclesOverlaps()
        {
            var a = _lane.AddEvent(1, "a", false);
            var b = _lane.AddEvent(1, "b", false);
            var track = new SpectrumTrack();
            track.InitData(new float[0], 10);
            track.SetEvents(_lane.events, null);
            Assert.AreSame(a, track.HitTest(new Vector2(100, 13)));
            Assert.IsNull(track.HitTest(new Vector2(100, 100)));
            track.SetSelected(a);
            Assert.AreSame(b, track.HitTest(new Vector2(100, 13)));
            track.SetSelected(b);
            Assert.AreSame(a, track.HitTest(new Vector2(100, 13)));
        }

        [Test]
        public void Envelope_PreservesOppositePhaseStereo()
        {
            var clip = AudioClip.Create("opposite-phase", 64, 2, 44100, false);
            try
            {
                var pcm = new float[128];
                for (int i = 0; i < 128; i += 2) { pcm[i] = 0.5f; pcm[i + 1] = -0.5f; }
                clip.SetData(pcm, 0);
                var envelope = AudioClipUtils.GetClipEnvelope(clip, 8);
                Assert.AreEqual(8, envelope.Length);
                foreach (float value in envelope) Assert.AreEqual(1, value);
            }
            finally { Object.DestroyImmediate(clip); }
        }

        private IEnumerator Open()
        {
            MetronomeGrapherWindow.ShowLane(_lane);
            _window = EditorWindow.GetWindow<MetronomeGrapherWindow>();
            yield return null;
            Assert.IsNotNull(_window.rootVisualElement.Q<TextField>("tfSeek"));
        }

        [UnityTest]
        public IEnumerator Window_SeekAddDeleteUndoRedo_UpdatesAssetAndCards()
        {
            yield return Open();
            var root = _window.rootVisualElement;
            Assert.AreEqual(137, _source.bpm, "Opening must not overwrite manually set BPM");
            root.Q<TextField>("tfSeek").value = "100";
            Assert.AreEqual("10", root.Q<TextField>("tfSeek").value);
            Invoke("AddEvent");
            Assert.AreEqual(1, _lane.events.Count);
            Assert.LessOrEqual(_lane.events[0].time, 10);
            Assert.AreEqual(1, root.Q<ScrollView>("lvEventContainer").contentContainer.childCount);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Invoke("DeleteEvent");
            Undo.FlushUndoRecordObjects();
            Assert.AreEqual(0, _lane.events.Count);
            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(1, _lane.events.Count);
            Assert.AreEqual(1, root.Q<ScrollView>("lvEventContainer").contentContainer.childCount);
            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(0, _lane.events.Count);
            Assert.AreEqual(0, root.Q<ScrollView>("lvEventContainer").contentContainer.childCount);
        }

        [UnityTest]
        public IEnumerator Apply_RejectsInvalidTimeAtomically_AndPreservesFreeTimeForPayloadEdit()
        {
            var e = _lane.AddEvent(0.1234567f, "tap", false);
            yield return Open();
            Invoke("SelectEvent", e, false);
            var root = _window.rootVisualElement;
            root.Q<TextField>("tfTime").value = "NaN";
            root.Q<TextField>("tfEventId").value = "changed";
            Invoke("ApplyEventChange");
            Assert.AreEqual("tap", e.eventId);
            Assert.AreEqual(0.1234567f, e.time);
            Invoke("SelectEvent", e, false);
            Assert.IsFalse(root.Q<Button>("btnApply").enabledSelf);
            root.Q<TextField>("tfPayload").value = "left";
            Invoke("ApplyEventChange");
            Assert.AreEqual("left", e.payload);
            Assert.AreEqual(0.1234567f, e.time, "Payload-only editing must not snap the existing time");
        }

        [UnityTest]
        public IEnumerator RecreateGui_RestoresLaneSelectionAndInformation()
        {
            _lane.AddEvent(1, "tap", false);
            yield return Open();
            _window.CreateGUI();
            yield return null;
            var root = _window.rootVisualElement;
            Assert.AreEqual("137", root.Q<TextField>("tfBpm").value);
            Assert.AreEqual(1, root.Q<ScrollView>("lvEventContainer").contentContainer.childCount);
            root.Q<TextField>("tfBpm").value = "150";
            Assert.AreEqual(150, _source.bpm);
        }

        [UnityTest]
        public IEnumerator EditorPreview_StartsAtSeek_Advances_AndStops()
        {
            var clip = AudioClip.Create("Preview test", 44100 * 5, 1, 44100, false);
            Assert.IsNotNull(clip);
            using (var preview = new MetroEditorPreview())
            {
                Assert.IsTrue(preview.Available, "AudioUtil adapter should support this Unity version");
                Assert.IsTrue(preview.Play(clip, 1));
                double deadline = EditorApplication.timeSinceStartup + 3;
                while (EditorApplication.timeSinceStartup < deadline && (!preview.IsPlaying || preview.Position <= 1.05f))
                    yield return null;
                Assert.IsTrue(preview.IsPlaying);
                Assert.Greater(preview.Position, 1.05f, "Preview should advance from the requested clip position");
                preview.Stop();
                Assert.IsFalse(preview.IsPlaying);
                Assert.IsTrue(preview.Play(clip, 2));
                deadline = EditorApplication.timeSinceStartup + 3;
                while (EditorApplication.timeSinceStartup < deadline && (!preview.IsPlaying || preview.Position <= 2.05f))
                    yield return null;
                Assert.Greater(preview.Position, 2.05f, "Resume should begin at the stored position");
            }
        }

        [UnityTest]
        public IEnumerator LargeLane_UsesBoundedCards_PreservesDraft_AndZoomHitTest()
        {
            _source.duration = 3000;
            for (int i=0;i<10000;i++) _lane.events.Add(new MetroEvent { index=i,time=i*.25f,eventId="tap" });
            var watch=System.Diagnostics.Stopwatch.StartNew();
            yield return Open();
            yield return null;
            watch.Stop();
            var root=_window.rootVisualElement;
            int cards=root.Q<ScrollView>("lvEventContainer").contentContainer.childCount;
            Debug.Log($"[MetronomePerf] 10000 events: first layout {watch.ElapsedMilliseconds}ms, live cards={cards}");
            Assert.Less(cards,100,"Card count must follow viewport size, not event count");
            Invoke("SelectEvent",_lane.events[1],false);
            root.Q<TextField>("tfPayload").value="unsaved draft";
            Invoke("OnProjectChanged");
            Assert.AreEqual("unsaved draft",root.Q<TextField>("tfPayload").value);
            var track=root.Q<SpectrumTrack>();track.SetZoom(200);track.SetSelected(null);
            Assert.AreSame(_lane.events[1],track.HitTest(new Vector2(50,13)));
        }

        private void Invoke(string method, params object[] args)
        {
            typeof(MetronomeGrapherWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_window, args);
        }
    }
}
