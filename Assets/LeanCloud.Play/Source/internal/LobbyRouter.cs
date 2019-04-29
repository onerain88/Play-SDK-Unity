using System;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;

namespace LeanCloud.Play {
    internal class LobbyRouter {
        readonly string appId;
        readonly bool insecure;
        readonly string feature;

        long nextConnectTimestamp;
        long serverValidTimestamp;
        int connectFailedCount;

        string primaryServer;
        string secondaryServer;

        internal LobbyRouter(string appId, bool insecure, string feature) {
            this.appId = appId;
            this.insecure = insecure;
            this.feature = feature;
            nextConnectTimestamp = 0;
            serverValidTimestamp = 0;
            connectFailedCount = 0;
        }

        internal string Fetch(string url) {
            var now = DateTimeUtils.Now;
            if (now < serverValidTimestamp) {
                return primaryServer;
            }
            if (now < nextConnectTimestamp) {
                var delay = nextConnectTimestamp - now;
                return DelayFetch(delay, url);
            }
            return FetchFromServer(url);
        }

        string DelayFetch(long delay, string url) {
            return "";
        }

        string FetchFromServer(string url) {
            try {
                var client = new WebClient();
                client.QueryString.Add("appId", appId);
                client.QueryString.Add("sdkVersion", "0.18.0-beta.1");
                client.QueryString.Add("protocolVersion", "0");
                if (feature != null) {
                    client.QueryString.Add("feature", feature);
                }
                if (insecure) {
                    client.QueryString.Add("insecure", "true");
                }
                var content = client.DownloadString(url);
                Logger.Debug(content);
                var response = Json.Parse(content) as Dictionary<string, object>;
                connectFailedCount = 0;
                nextConnectTimestamp = 0;
                if (response.TryGetValue("server", out object serverObj)) {
                    primaryServer = serverObj.ToString();
                }
                if (response.TryGetValue("secondary", out object secondaryObj)) {
                    secondaryServer = secondaryObj.ToString();
                }
                if (response.TryGetValue("ttl", out object ttlObj)) {
                    var ttl = int.Parse(ttlObj.ToString());
                    serverValidTimestamp = DateTimeUtils.Now + ttl * 1000;
                }
                return primaryServer;
            } catch (Exception e) {
                Logger.Error(e.Message);
                connectFailedCount++;
                nextConnectTimestamp = DateTimeUtils.Now + 2 * connectFailedCount * 1000;
                throw e;
            }
        }
    }
}
