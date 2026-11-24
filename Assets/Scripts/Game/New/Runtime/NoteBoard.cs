using System.Collections.Generic;
using UnityEngine;

public static class GameServices
{
    public static InfoManager Info;
    public static ResultManager Results;
    public static EffectController Effects;
    public static HitSound Hits;
    public static NoteBoard Board;
    public static Camera Camera;
}

[DefaultExecutionOrder(-50)]
public class NoteBoard : MonoBehaviour
{
    readonly List<Spot> active = new List<Spot>();

    public List<Spot> Active => active;

    public void Add(Spot spot)
    {
        if (spot != null && !active.Contains(spot))
            active.Add(spot);
    }

    public void Remove(Spot spot)
    {
        active.Remove(spot);
    }

    void Update()
    {
        ChartPlayback playback = ChartPlayback.Instance;
        if (playback == null || active.Count == 0)
            return;
        float songTime = (float)playback.SongTime;
        for (int i = active.Count - 1; i >= 0; i--)
            active[i].Tick(songTime);
    }
}
