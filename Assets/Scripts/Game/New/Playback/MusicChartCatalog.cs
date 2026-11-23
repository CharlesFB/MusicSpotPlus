using System;
using FB.Metronome.Data;
using UnityEngine;

[CreateAssetMenu(menuName = "MusicSpot/Chart Catalog", fileName = "MusicChartCatalog")]
public class MusicChartCatalog : ScriptableObject
{
    public MusicChart[] byMusicId = new MusicChart[0];
    public MusicChart[] extras = new MusicChart[0];

    public MusicChart ByMusicId(int musicId)
    {
        if (byMusicId == null || musicId < 0 || musicId >= byMusicId.Length)
            return null;
        return byMusicId[musicId];
    }

    public MusicChart Find(string key)
    {
        MusicChart found = Search(byMusicId, key);
        return found != null ? found : Search(extras, key);
    }

    static MusicChart Search(MusicChart[] charts, string key)
    {
        if (charts == null || string.IsNullOrEmpty(key))
            return null;
        foreach (var chart in charts)
        {
            if (chart != null && chart.key == key)
                return chart;
        }
        return null;
    }
}

[Serializable]
public class MusicChart
{
    public string key;
    public MetroSource source;
    public EventLane[] lanes = new EventLane[0];
}
