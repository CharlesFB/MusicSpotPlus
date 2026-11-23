/*
UniBpmAnalyzer
Copyright (c) 2016 WestHillApps (Hironari Nishioka)
This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using System;
using UnityEngine;
namespace FB.Metronome.Analysis
{
    /// <summary>Envelope-onset periodicity estimate. Ambiguous half/double tempos still require manual review.</summary>
    public static class UniBpmAnalyzer
    {
        public static int AnalyzeBpm(AudioClip clip)
        {
            if (!clip || clip.loadState != AudioDataLoadState.Loaded || clip.length < 2) return -1;
            // === 以采样帧而非交错声道元素计时；限制分析前两分钟、分块读取 ===
            int frames = Math.Max(1, clip.frequency / 100);
            int count = Math.Min(clip.samples / frames, 12000);
            var pcm = new float[frames * clip.channels];
            var onsets = new double[count];
            double previous = 0, energy = 0;
            for (int i=0;i<count;i++)
            {
                if (!clip.GetData(pcm, i * frames)) return -1;
                double sum=0;
                foreach (float v in pcm) sum += v*v;
                double volume = Math.Sqrt(sum / pcm.Length);
                onsets[i] = Math.Max(0, volume - previous);
                energy += onsets[i]; previous = volume;
            }
            if (energy < 1e-6) return -1;
            double rate = (double)clip.frequency / frames, best = 0;
            int result = -1;
            for (int bpm=60;bpm<=240;bpm++)
            {
                double sin=0,cos=0;
                for(int i=0;i<count;i++)
                { double phase=i*2*Math.PI*(bpm/60.0)/rate; sin+=onsets[i]*Math.Sin(phase); cos+=onsets[i]*Math.Cos(phase); }
                double score=(sin*sin+cos*cos)/(energy*energy);
                if (score > best + 1e-6) { best=score;result=bpm; }
            }
            return best > .01 ? result : -1;
        }
    }
}
