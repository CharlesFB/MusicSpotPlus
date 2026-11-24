using System.Collections.Generic;
using FB.Metronome.Conductor;
using FB.Metronome.Data;
using UnityEngine;

/// <summary>
/// 场景只依赖这一层。音符提前一个四分音符生成，判定仍用事件上的命中时间。
/// </summary>
public class ChartPlayback : MonoBehaviour
{
    public static ChartPlayback Instance { get; private set; }

    MetroConductor conductor;

    public double SongTime => conductor != null ? conductor.SongTime : 0d;

    public bool HasEnded =>
        conductor != null && conductor.State == MetroConductor.PlaybackState.Ended;

    public bool SongActive
    {
        get
        {
            if (conductor == null)
                return false;
            var state = conductor.State;
            return state == MetroConductor.PlaybackState.Scheduled
                || state == MetroConductor.PlaybackState.Playing
                || state == MetroConductor.PlaybackState.Paused
                || state == MetroConductor.PlaybackState.Ended;
        }
    }

    public static float Since(float origin)
    {
        var playback = Instance;
        if (playback == null)
            return 0f;
        return Mathf.Max(0f, (float)(playback.SongTime - origin));
    }

    void Awake()
    {
        Instance = this;
        conductor = GetComponent<MetroConductor>();
        if (conductor == null)
            conductor = gameObject.AddComponent<MetroConductor>();
        PrepareClock();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Load(MusicChart chart, AudioSource output)
    {
        if (conductor == null)
            return;
        if (chart == null || chart.source == null)
        {
            Debug.LogWarning("[ChartPlayback] 没有可播放的谱面。");
            return;
        }
        if (output == null)
            output = GetComponent<AudioSource>();

        var lanes = new List<EventLane>();
        if (chart.lanes != null)
        {
            foreach (var lane in chart.lanes)
            {
                if (lane != null)
                    lanes.Add(lane);
            }
        }

        PrepareClock();
        conductor.Configure(output, chart.source, lanes);
    }

    public void Play()
    {
        if (conductor != null)
            conductor.Play();
    }

    public void Pause()
    {
        if (conductor != null)
            conductor.Pause();
    }

    public void Restart()
    {
        if (conductor == null)
            return;
        conductor.Seek(0f);
        if (!conductor.IsPlaying)
            conductor.Play();
    }

    public void Subscribe(string eventId, MetroConductor.MetroEventHandler handler)
    {
        if (conductor != null)
            conductor.Subscribe(eventId, handler);
    }

    void PrepareClock()
    {
        conductor.LookAheadSeconds = 0f;
        conductor.ahead = 0.05f;
    }
}
