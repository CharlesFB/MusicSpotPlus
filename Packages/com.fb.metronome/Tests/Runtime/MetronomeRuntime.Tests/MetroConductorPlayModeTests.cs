using FB.Metronome.Conductor;
using FB.Metronome.Data;
using FB.Metronome.Playback;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FB.Metronome.Tests.Runtime
{
    public class MetroConductorPlayModeTests
    {
        private const double TimeEpsilon = 0.05;

        private GameObject _go;
        private AudioSource _audio;
        private MetroSource _source;
        private MetroConductor _conductor;
        private AudioClip _clip;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _clip = AudioClip.Create("Test pulse", 44100 * 5, 1, 44100, false);
            yield return null;

            _go = new GameObject("MetroConductorTest");
            _audio = _go.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _conductor = _go.AddComponent<MetroConductor>();
            _conductor.ahead = 0.08f;

            _source = ScriptableObject.CreateInstance<MetroSource>();
            _source.clip = _clip;
            _source.duration = _clip.length;
            Inject(_conductor, _audio, _source);
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
        }

        [UnityTest]
        public IEnumerator SongTime_BeforePlay_IsZeroNotDspTime()
        {
            yield return null;
            Assert.AreEqual(0d, _conductor.SongTime, TimeEpsilon);
            Assert.IsFalse(_conductor.IsPlaying);
            Assert.Less(_conductor.SongTime, AudioSettings.dspTime * 0.5);
        }

        [UnityTest]
        public IEnumerator Play_AfterAhead_SongTimeAdvances()
        {
            _conductor.Play();
            Assert.IsTrue(_conductor.IsPlaying);
            Assert.AreEqual(0d, _conductor.SongTime, TimeEpsilon);

            yield return WaitDsp(_conductor.ahead + 0.35);

            Assert.IsTrue(_conductor.IsPlaying);
            Assert.Greater(_conductor.SongTime, 0.15);
            Assert.Less(_conductor.SongTime, 1.5);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesSongTime_PlayResumes()
        {
            _conductor.Play();
            yield return WaitDsp(_conductor.ahead + 0.4);

            double pausedAt = _conductor.SongTime;
            Assert.Greater(pausedAt, 0.1);

            _conductor.Pause();
            Assert.IsFalse(_conductor.IsPlaying);
            yield return WaitDsp(0.3);
            Assert.AreEqual(pausedAt, _conductor.SongTime, TimeEpsilon);

            _conductor.Play();
            yield return WaitDsp(_conductor.ahead + 0.25);
            Assert.IsTrue(_conductor.IsPlaying);
            Assert.Greater(_conductor.SongTime, pausedAt + 0.05);
        }

        [UnityTest]
        public IEnumerator Seek_WhileStopped_UpdatesFrozenSongTime()
        {
            _conductor.Seek(2.5f);
            yield return null;
            Assert.IsFalse(_conductor.IsPlaying);
            Assert.AreEqual(2.5d, _conductor.SongTime, TimeEpsilon);
        }

        [UnityTest]
        public IEnumerator Seek_WhilePlaying_JumpsPlayhead()
        {
            _conductor.Play();
            yield return WaitDsp(_conductor.ahead + 0.2);

            _conductor.Seek(3f);
            yield return WaitDsp(_conductor.ahead + 0.25);

            Assert.IsTrue(_conductor.IsPlaying);
            Assert.Greater(_conductor.SongTime, 2.9);
            Assert.Less(_conductor.SongTime, 4.0);
        }

        [UnityTest]
        public IEnumerator Seek_PastDuration_Clamps()
        {
            _conductor.Seek(_clip.length + 10f);
            yield return null;
            Assert.AreEqual(_clip.length, _conductor.SongTime, 0.02);
        }

        [UnityTest]
        public IEnumerator Stop_ResetsSongTime()
        {
            _conductor.Play();
            yield return WaitDsp(_conductor.ahead + 0.25);
            _conductor.Stop();
            yield return null;
            Assert.IsFalse(_conductor.IsPlaying);
            Assert.AreEqual(0d, _conductor.SongTime, TimeEpsilon);
        }

        [Test]
        public void GetLatency_ReportsMixerEstimateAndCalibration()
        {
            _conductor.calibrationMs = 12.5;
            AudioLatency latency = _conductor.GetLatency();
            Assert.Greater(latency.OutputMs, 0d);
            Assert.AreEqual(12.5d, latency.CalibrationMs);
            Assert.AreEqual(latency.OutputMs + latency.CalibrationMs, latency.TotalMs);
        }

        [UnityTest]
        public IEnumerator Play_WithoutClip_DoesNotStart()
        {
            _source.clip = null;
            LogAssert.Expect(LogType.Warning, "[Metronome] Conductor 检测到脚本存在未装载资源，请仔细检查 Inspector 中的配置。");
            _conductor.Play();
            yield return null;
            Assert.IsFalse(_conductor.IsPlaying);
            Assert.AreEqual(0d, _conductor.SongTime, TimeEpsilon);
        }

        private static void Inject(MetroConductor conductor, AudioSource audio, MetroSource source)
        {
            conductor.Configure(audio, source, new EventLane[0]);
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
