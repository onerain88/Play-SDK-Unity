using System;
using UnityEngine;

namespace LeanCloud.Play.Test {
    internal static class Utils {
        internal static Client NewClient(string userId) {
            var appId = "Eohx7L4EMfe4xmairXeT7q1w-gzGzoHsz";
            var appKey = "GSBSGpYH9FsRdCss8TGQed0F";
            return new Client(appId, appKey, userId);
        }

        internal static void Log(LogLevel level, string info) { 
            switch (level) {
                case LogLevel.Debug:
                    Debug.Log(info);
                    break;
                case LogLevel.Warn:
                    Debug.LogWarning(info);
                    break;
                case LogLevel.Error:
                    Debug.LogError(info);
                    break;
                default:
                    Debug.Log(info);
                    break;
            }
        }
    }
}
