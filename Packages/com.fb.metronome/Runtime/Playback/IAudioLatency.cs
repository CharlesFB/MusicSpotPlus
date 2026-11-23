namespace FB.Metronome.Playback
{
    public readonly struct AudioLatency
    {
        // 估算的混音缓冲延迟
        public readonly double OutputMs;
        // 用户校准的额外残差（毫秒）；正值表示可听/输入比谱面时钟更晚。不是硬件测量值。
        public readonly double CalibrationMs;

        public AudioLatency(double outputMs, double calibrationMs)
        {
            if (!FB.Metronome.Data.MetroValidation.IsFinite(outputMs) || outputMs < 0)
                throw new System.ArgumentOutOfRangeException(nameof(outputMs));
            if (!FB.Metronome.Data.MetroValidation.IsFinite(calibrationMs) || !FB.Metronome.Data.MetroValidation.IsFinite(outputMs + calibrationMs))
                throw new System.ArgumentOutOfRangeException(nameof(calibrationMs));
            OutputMs = outputMs;
            CalibrationMs = calibrationMs;
        }

        public double TotalMs => OutputMs + CalibrationMs;
        /// <summary>把输入时刻换算到听到的谱面位置。不要再重复扣减校准。</summary>
        public double ToAudibleSongTime(double songTime) => songTime - TotalMs / 1000.0;
        public static AudioLatency FromMeasuredTotal(double outputMs, double measuredTotalMs)
            => new AudioLatency(outputMs, measuredTotalMs - outputMs);
    }

    /// <summary>
    /// 玩家侧延迟查询。P2 由 Conductor 根据 DSP buffer + 校准填充。
    /// </summary>
    public interface IAudioLatency
    {
        AudioLatency GetLatency();
    }
}
