using System;

namespace LeanCloud.Play.Test {
    internal static class Utils {
        internal static Client NewClient(string userId) {
            var appId = "Eohx7L4EMfe4xmairXeT7q1w-gzGzoHsz";
            var appKey = "GSBSGpYH9FsRdCss8TGQed0F";
            return new Client(appId, appKey, userId);
        }
    }
}
