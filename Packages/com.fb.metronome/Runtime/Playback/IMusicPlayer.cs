namespace FB.Metronome.Playback
{
    /// <summary>
    /// 播放控制。P2 由 MetroConductor / AudioSource 实现，此处只定雏形。
    /// </summary>
    public interface IMusicPlayer
    {
        bool IsPlaying { get; }
        void Play();
        void Pause();
        void Stop();
        void Seek(float timeSeconds);
    }
}
