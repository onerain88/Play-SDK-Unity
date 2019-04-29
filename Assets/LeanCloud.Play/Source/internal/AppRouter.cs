using System;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;

namespace LeanCloud.Play {
    internal class AppRouter {
        string appId;
        string url;
        long serverValidTimestamp;

        internal AppRouter(string appId) {
            this.appId = appId;
            url = null;
            serverValidTimestamp = 0;
        }

        internal string Fetch() {
            var now = DateTimeUtils.Now;
            if (now < serverValidTimestamp) {
                Logger.Debug("Get server from cache");
                return url;
            }
            return FetchFromServer();
        }

        string FetchFromServer() {
            try {
                var client = new WebClient();
                client.QueryString.Add("appId", appId);
                var content = client.DownloadString("https://app-router.leancloud.cn/2/route");
                Logger.Debug(content);
                var response = Json.Parse(content) as Dictionary<string, object>;
                string primaryServer = null;
                string secondaryServer = null;
                if (response.TryGetValue("multiplayer_router_server", out object primaryServerObj)) {
                    primaryServer = primaryServerObj.ToString();
                }
                if (response.TryGetValue("play_server", out object secondaryServerObj)) {
                    secondaryServer = secondaryServerObj.ToString();
                }
                var routerServer = primaryServer ?? secondaryServer;
                if (routerServer == null) {
                    throw new ArgumentNullException(nameof(routerServer));
                }
                if (response.TryGetValue("ttl", out object ttlObj)) {
                    var ttl = int.Parse(response["ttl"].ToString());
                    serverValidTimestamp = DateTimeUtils.Now + ttl * 1000;
                }
                return string.Format("https://{0}/1/multiplayer/router/router", routerServer);
            } catch (Exception e) {
                Logger.Error(e.Message);
                throw e;
            }
        }
    }
}
