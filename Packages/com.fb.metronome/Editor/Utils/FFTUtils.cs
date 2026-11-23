using System;
using UnityEngine;

namespace FB.Metronome.Editor.Analysis
{
    public class FFTUtils
    {
        /// <summary>
        /// 计算音频时域片段的频谱大小 (Magnitude Spectrum)
        /// </summary>
        /// <param name="timeDomainData">输入波形数据片段。数组长度必须是 2 的 n 次方（如 1024, 2048）</param>
        /// <returns>返回频率振幅数组，长度为输入的一半（根据奈奎斯特定理，后一半是镜像）</returns>
        public static float[] CalculateSpectrum(float[] timeDomainData)
        {
            int n = timeDomainData.Length;

            // 确保数组长度是 2 的幂
            if ((n & (n - 1)) != 0)
            {
                throw new ArgumentException("输入数据长度必须是 2 的 n 次方");
            }

            float[] real = new float[n];
            float[] imag = new float[n];

            // 1. 应用 Hanning 窗函数并拷贝数据
            for (int i = 0; i < n; i++)
            {
                // Hanning window 公式: 0.5 * (1 - cos(2 * PI * i / (N - 1)))
                float multiplier = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (n - 1)));
                real[i] = timeDomainData[i] * multiplier;
            }

            // 2. 位反转置换 (Bit-reversal permutation)
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    float temp = real[i];
                    real[i] = real[j];
                    real[j] = temp;
                }

                int m = n / 2;
                while (m >= 1 && j >= m)
                {
                    j -= m;
                    m /= 2;
                }

                j += m;
            }

            // 3. Cooley-Tukey FFT
            for (int step = 1; step < n; step *= 2)
            {
                float angle = -1f * Mathf.PI / step;
                float wReal = Mathf.Cos(angle);
                float wImag = Mathf.Sin(angle);

                for (int i = 0; i < n; i += 2 * step)
                {
                    float uReal = 1f;
                    float uImag = 0f;

                    for (int k = 0; k < step; k++)
                    {
                        int even = i + k;
                        int odd = i + k + step;

                        // 蝶形运算 (Butterfly operation)
                        float tReal = uReal * real[odd] - uImag * imag[odd];
                        float tImag = uReal * imag[odd] + uImag * real[odd];

                        real[odd] = real[even] - tReal;
                        imag[odd] = imag[even] - tImag;
                        real[even] += tReal;
                        imag[even] += tImag;

                        // 旋转因子复数相乘
                        float nextUReal = uReal * wReal - uImag * wImag;
                        float nextUImag = uReal * wImag + uImag * wReal;
                        uReal = nextUReal;
                        uImag = nextUImag;
                    }
                }
            }

            // 4. 计算振幅 (Magnitude)
            // 只需要前一半的数据，因为真实信号的 FFT 结果是对称的
            int halfN = n / 2;
            float[] spectrum = new float[halfN];
            for (int i = 0; i < halfN; i++)
            {
                // 振幅 = sqrt(real^2 + imag^2)
                // 乘以 2.0 / N 进行归一化
                spectrum[i] = Mathf.Sqrt(real[i] * real[i] + imag[i] * imag[i]) * 2f / n;
            }

            return spectrum;
        }
    }
}