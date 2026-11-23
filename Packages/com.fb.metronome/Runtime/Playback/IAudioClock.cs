namespace FB.Metronome.Playback
{
    /// <summary>
    /// 歌曲时钟。P2 以 AudioSettings.dspTime 为主钟。
    /// </summary>
    public interface IAudioClock
    {
        double SongTime { get; }
        long SongTimeSamples { get; }
        double DspTime { get; }
    }
}
