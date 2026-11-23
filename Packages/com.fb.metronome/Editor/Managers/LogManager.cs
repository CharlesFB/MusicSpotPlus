// ==================================================
// Copyright (c) Magic Tavern. All rights reserved.
// @Author: 陈梓霄
// @Maintainer: 陈梓霄
// @Description: MTP 日志
// ==================================================

using FB.Metronome.Editor.Config;
using System;
using System.IO;
using UnityEngine;

namespace FB.Metronome.Editor.Diagnostics
{
    public sealed class LogManager
    {
        // 日志写锁
        private static readonly object s_fileLock = new object();

        private static LogManager s_instance;
#if UNITY_2020_2_OR_NEWER
        public static LogManager Instance => s_instance ??= new LogManager();
#else
        public static LogManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new LogManager();
                }

                return s_instance;
            }
        }
#endif

        private LogManager()
        {
        }

        public static void TimeLog(string msg)
        {
            msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Metronome]:{msg}";
            if (ShouldLogOnConsole())
            {
                Debug.Log(msg);
            }

            WriteToFile("INFO", msg);
        }

        public static void Log(string msg)
        {
            msg = "[Metronome]:" + msg;
            if (ShouldLogOnConsole())
            {
                Debug.Log(msg);
            }

            WriteToFile("INFO", msg);
        }

        public static void LogError(string msg)
        {
            msg = "[Metronome]:" + msg;
            Debug.LogError(msg);
            WriteToFile("ERROR", msg);
        }

        public static void LogWarning(string msg)
        {
            msg = "[Metronome]:" + msg;
            Debug.LogWarning(msg);
            WriteToFile("WARN", msg);
        }

        /// <summary>
        /// 优先读已缓存 Config 避免循环
        /// 尚未加载时默认打 Console。
        /// </summary>
        private static bool ShouldLogOnConsole()
        {
            return MetroConfig.LogOnConsole;
        }

        private static void WriteToFile(string level, string msg)
        {
            try
            {
                string path = MetroPath.LOG_PATH;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string line = $"[{level}] {msg}{Environment.NewLine}";

                lock (s_fileLock)
                {
                    File.AppendAllText(path, line);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LogManager] 写入日志文件失败: {ex.Message}");
            }
        }
    }
}
