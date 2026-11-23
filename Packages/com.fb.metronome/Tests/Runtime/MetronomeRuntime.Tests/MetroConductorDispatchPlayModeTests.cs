using FB.Metronome.Conductor;
using FB.Metronome.Data;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FB.Metronome.Tests.Runtime
{
    public class MetroConductorDispatchPlayModeTests
    {
        private const float EventTime = 0.4f;

        private GameObject _go;
        private AudioSource _audio;
        private MetroSource _source;
        private EventLane _lane;
        private MetroConductor _conductor;
        private AudioClip _clip;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _clip = AudioClip.Create("Test pulse", 44100 * 5, 1, 44100, false);
            yield return null;

            _go = new GameObject("MetroConductorDispatchTest");
            _audio = _go.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _conductor = _go.AddComponent<MetroConductor>();
            _conductor.ahead = 0.08f;

            _source = ScriptableObject.CreateInstance<MetroSource>();
            _source.clip = _clip;
            _source.duration = _clip.length;

            _lane = ScriptableObject.CreateInstance<EventLane>();
            _lane.laneId = "tap";
            _lane.source = _source;
            _lane.AddEvent(EventTime, "tap", snap: false, payload: "dispatch-test");

            Inject(_conductor, _audio, _source, _lane);
        }

        [TearDown]
        public void TearDown()
        {
            if (_conductor != null && _conductor.IsPlaying)
                _conductor.Stop();

            if (_go != null)
                Object.DestroyImmediate(_go);
            if (_source != null)
                Object.DestroyImmediate(_source);
            if (_clip != null) Object.DestroyImmediate(_clip);
            if (_lane != null)
                Object.DestroyImmediate(_lane);
        }

        [UnityTest]
        public IEnumerator Dispatch_WhenSongTimePassesEvent_FiresOnce()
        {
            int fires = 0;
            void OnTap(EventLane lane, MetroEvent e)
            {
                fires++;
                Debug.Log(
                    $"[MetronomeTest] FIRE #{fires} eventId={e.eventId} eventTime={e.time:F3} " +
                    $"songTime={_conductor.SongTime:F3} lane={lane.laneId} payload={e.payload}");
            }
        
            _conductor.Subscribe("tap", OnTap);
            Debug.Log(
                $"[MetronomeTest] Play ahead={_conductor.ahead:F3} eventTime={EventTime:F3} " +
                $"lanes={_conductor.Lanes.Count} events={_conductor.Lanes[0].events.Count}");
            _conductor.Play();
        
            double lastLogged = -1d;
            double wait = _conductor.ahead + EventTime + 0.35;
            double start = AudioSettings.dspTime;
            float deadline = Time.realtimeSinceStartup + 10;
            while (AudioSettings.dspTime - start < wait && Time.realtimeSinceStartup < deadline)
            {
                double songTime = _conductor.SongTime;
                if (songTime - lastLogged >= 0.1)
                {
                    Debug.Log(
                        $"[MetronomeTest] progress songTime={songTime:F3} dspWait={AudioSettings.dspTime - start:F3} " +
                        $"fires={fires} playing={_conductor.IsPlaying} lanes={_conductor.Lanes.Count}");
                    lastLogged = songTime;
                }
        
                yield return null;
            }
        
            Debug.Log($"[MetronomeTest] done songTime={_conductor.SongTime:F3} fires={fires}");
            Assert.AreEqual(1, fires, "Event should fire exactly once when SongTime crosses it");
            Assert.Greater(_conductor.SongTime, EventTime);
            _conductor.Unsubscribe("tap", OnTap);
        }

        [UnityTest]
        public IEnumerator Dispatch_SeekPastEvent_DoesNotFireThatEvent()
        {
            int fires = 0;
            void OnTap(EventLane lane, MetroEvent e)
            {
                fires++;
                Debug.Log(
                    $"[MetronomeTest] FIRE #{fires} eventTime={e.time:F3} songTime={_conductor.SongTime:F3}");
            }

            _conductor.Subscribe("tap", OnTap);
            Debug.Log("[MetronomeTest] Seek to 1.0 (past 0.4 event) then Play");
            _conductor.Seek(1f);
            _conductor.Play();

            yield return WaitDsp(_conductor.ahead + 0.35);

            Debug.Log($"[MetronomeTest] after seek-play songTime={_conductor.SongTime:F3} fires={fires}");
            Assert.AreEqual(0, fires);
            Assert.Greater(_conductor.SongTime, 0.9);
            _conductor.Unsubscribe("tap", OnTap);
        }

        [UnityTest]
        public IEnumerator ZeroAndTailFireOnceBeforeEnded()
        {
            _lane.events.Clear(); _lane.AddEvent(0,"tap",false); _lane.AddEvent(_clip.length,"tap",false);
            _conductor.RefreshEvents();int n=0;_conductor.Subscribe("tap",(l,e)=>n++);
            _conductor.Play();yield return WaitDsp(.2);Assert.AreEqual(1,n);
            _conductor.Seek(_clip.length-.15f);yield return WaitDsp(.4);
            Assert.AreEqual(2,n);Assert.AreEqual(MetroConductor.PlaybackState.Ended,_conductor.State);
        }
        [UnityTest]
        public IEnumerator CallbackStopCancelsOtherDueEvents()
        {
            _lane.events.Clear();_lane.AddEvent(0,"tap",false);_lane.AddEvent(0,"tap",false);
            _conductor.RefreshEvents();int n=0;
            _conductor.Subscribe("tap",(l,e)=>{n++;_conductor.Stop();});
            _conductor.Play();yield return WaitDsp(.25);
            Assert.AreEqual(1,n);Assert.IsFalse(_conductor.IsPlaying);Assert.AreEqual(0,_conductor.SongTime);
        }
        [UnityTest]
        public IEnumerator DisableStopsAudioAndClock()
        {
            _conductor.Play();yield return WaitDsp(.2);_conductor.enabled=false;
            yield return null;Assert.IsFalse(_audio.isPlaying);Assert.IsFalse(_conductor.IsPlaying);Assert.AreEqual(0,_conductor.SongTime);
        }

        private static void Inject(
            MetroConductor conductor, AudioSource audio, MetroSource source, EventLane lane)
        {
            conductor.Configure(audio, source, new[] { lane });
        }

        private static IEnumerator WaitDsp(double seconds)
        {
            double start = AudioSettings.dspTime;
            float deadline = Time.realtimeSinceStartup + 10;
            while (AudioSettings.dspTime - start < seconds && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Less(Time.realtimeSinceStartup, deadline, "DSP clock did not progress");
        }
    }
}
