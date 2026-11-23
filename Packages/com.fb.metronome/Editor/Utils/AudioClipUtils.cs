using FB.Metronome.Analysis;
using FB.Metronome.Editor.Config;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FB.Metronome.Editor.Analysis
{
    public static class AudioClipUtils
    {
        /// <summary>
        /// 获取音乐片段的基础 Bpm
        /// </summary>
        /// <param name="clip"> 输入音乐 </param>
        /// <returns> bpm（初步，不一定准确） </returns>
        public static int GetClipBpm(AudioClip clip)
        {
            return UniBpmAnalyzer.AnalyzeBpm(clip);
        }

        /// <summary>
        /// 把 clip 压成一维振幅包络（峰值），归一化到 0~1。给 SpectrumTrack 用，不做 FFT。
        /// </summary>
        public static float[] GetClipEnvelope(AudioClip clip, int binCount)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0 || binCount <= 0)
            {
                return Array.Empty<float>();
            }

            if (clip.loadState != AudioDataLoadState.Loaded) return Array.Empty<float>();
            binCount = Mathf.Min(binCount, clip.samples);
            var envelope = new float[binCount];
            int channels = clip.channels;
            const int blockFrames = 16384;
            var block = new float[Mathf.Min(blockFrames, clip.samples) * channels];
            float peak = 0f;
            for (int offset = 0; offset < clip.samples; offset += blockFrames)
            {
                if (!clip.GetData(block, offset))
                {
                    Debug.LogWarning("[Metronome] AudioClip.GetData failed; use Decompress On Load.");
                    return Array.Empty<float>();
                }
                int frames = Mathf.Min(blockFrames, clip.samples - offset);
                for (int frame = 0; frame < frames; frame++)
                {
                    float amplitude = 0;
                    for (int channel = 0; channel < channels; channel++)
                        amplitude = Mathf.Max(amplitude, Mathf.Abs(block[frame * channels + channel]));
                    int bin = (int)((long)(offset + frame) * binCount / clip.samples);
                    envelope[bin] = Mathf.Max(envelope[bin], amplitude);
                    peak = Mathf.Max(peak, amplitude);
                }
            }

            if (peak > 0f)
            {
                for (int i = 0; i < binCount; i++)
                {
                    envelope[i] /= peak;
                }
            }

            return envelope;
        }

        /// <summary>
        /// 对 AudioClip 做短时傅里叶变换 (STFT)，返回各时间帧的频谱幅度序列。
        /// 结果按帧顺序展平：frame0[bin0..binN-1], frame1[bin0..binN-1], ...
        /// 其中每帧 bin 数为 windowSize / 2。
        /// </summary>
        /// <param name="clip">输入音乐</param>
        /// <param name="windowSize">FFT 窗口大小，必须为 2 的幂</param>
        /// <param name="stepSize">帧移（hop size），单位为单声道采样点</param>
        /// <returns>展平后的频谱幅度列表；参数非法时返回空列表</returns>
        public static List<float> GetClipPcmFFT(
            AudioClip clip,
            int windowSize = MetroConfig.DEFAULT_FFT_WINDOW_SIZE,
            int stepSize = MetroConfig.DEFAULT_FFT_STEP_SIZE)
        {
            if (clip == null)
            {
                Debug.LogError("[Metronome] AudioClip is null");
                return new List<float>();
            }

            if (windowSize <= 0 || (windowSize & (windowSize - 1)) != 0)
            {
                Debug.LogError("[Metronome] Window Size should be a positive power of 2");
                return new List<float>();
            }

            if (stepSize <= 0)
            {
                Debug.LogError("[Metronome] Step Size should be > 0");
                return new List<float>();
            }

            int channels = clip.channels;
            if (channels <= 0 || clip.samples <= 0)
            {
                Debug.LogError("[Metronome] Invalid AudioClip data");
                return new List<float>();
            }

            // GetData 返回交错多声道采样，需先混成单声道再做 FFT
            float[] interleaved = new float[clip.samples * channels];
            clip.GetData(interleaved, 0);
            float[] mono = ToMono(interleaved, channels);

            if (mono.Length < windowSize)
            {
                Debug.LogWarning("[Metronome] AudioClip is shorter than windowSize");
                return new List<float>();
            }

            int frameCount = (mono.Length - windowSize) / stepSize + 1;
            int binsPerFrame = windowSize / 2;
            var result = new List<float>(frameCount * binsPerFrame);

            float[] chunk = new float[windowSize];
            for (int i = 0; i <= mono.Length - windowSize; i += stepSize)
            {
                Array.Copy(mono, i, chunk, 0, windowSize);
                float[] spectrum = FFTUtils.CalculateSpectrum(chunk);
                result.AddRange(spectrum);
            }

            return result;
        }

        /// <summary>
        /// 计算绘制频谱时相邻两个节拍之间的基础间距。
        /// 单位为 STFT 帧（列）：与 <see cref="GetClipPcmFFT"/> 的 stepSize 对齐，
        /// 即假设每个 hop 对应一个横向绘制单位。实际绘制时可再乘 zoom。
        /// </summary>
        /// <param name="clip">输入音乐（取其采样率）</param>
        /// <param name="bpm">节拍速度</param>
        /// <param name="stepSize">STFT 帧移，需与频谱计算时一致</param>
        /// <returns>两拍之间的基础间距；参数非法时返回 0</returns>
        public static float GetBeatSpacing(
            AudioClip clip,
            int bpm,
            int stepSize = MetroConfig.DEFAULT_FFT_STEP_SIZE)
        {
            if (clip == null || clip.frequency <= 0 || bpm <= 0 || stepSize <= 0)
            {
                Debug.LogError("[Metronome] Invalid args for GetBeatSpacing");
                return 0f;
            }

            // 一拍秒数 = 60 / bpm
            // 一帧秒数 = stepSize / frequency
            // 间距（帧）= 一拍秒数 / 一帧秒数
            return clip.frequency * 60f / (bpm * (float)stepSize);
        }

        /// <summary>
        /// 将交错多声道 PCM 平均混成单声道
        /// </summary>
        private static float[] ToMono(float[] interleaved, int channels)
        {
            if (channels == 1)
            {
                return interleaved;
            }

            int sampleCount = interleaved.Length / channels;
            float[] mono = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float sum = 0f;
                int baseIndex = i * channels;
                for (int c = 0; c < channels; c++)
                {
                    sum += interleaved[baseIndex + c];
                }

                mono[i] = sum / channels;
            }

            return mono;
        }
    }
}
