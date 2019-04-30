using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace LeanCloud.Play {
    internal class GameConnection : Connection {
        internal Room Room {
            get; private set;
        }

        internal GameConnection() {
        
        }

        internal static Task<GameConnection> Connect(string appId, string server, string userId, string gameVersion) {
            var tcs = new TaskCompletionSource<GameConnection>();
            var connection = new GameConnection();
            return connection.Connect(server, userId).OnSuccess(t => {
                return connection.OpenSession(appId, userId, gameVersion);
            }).Unwrap().OnSuccess(_ => {
                return connection;
            });
        }

        internal Task<Room> CreateRoom(string roomId, RoomOptions roomOptions, List<string> expectedUserIds) {
            var msg = Message.NewRequest("conv", "start");
            if (roomId != null) {
                msg["cid"] = roomId;
            }
            if (roomOptions != null) {
                var roomOptionsDict = roomOptions.ToDictionary();
                foreach (var entry in roomOptionsDict) {
                    msg[entry.Key] = entry.Value;
                }
            }
            if (expectedUserIds != null) {
                var expecteds = expectedUserIds.Cast<object>().ToList();
                msg["expectMembers"] = expecteds;
            }
            return Send(msg).OnSuccess(t => {
                var res = t.Result;
                return Room.NewFromDictionary(res.Data);
            });
        }

        internal Task<Room> JoinRoom(string roomId) {
            var msg = Message.NewRequest("conv", "add");
            msg["cid"] = roomId;
            return Send(msg).OnSuccess(t => {
                var res = t.Result;
                return Room.NewFromDictionary(res.Data);
            });
        }

        internal Task LeaveRoom() {
            var msg = Message.NewRequest("conv", "remove");
            return Send(msg);
        }

        internal Task<bool> SetRoomOpened(bool opened) {
            var msg = Message.NewRequest("conv", "open");
            msg["toggle"] = opened;
            return Send(msg).OnSuccess(t => {
                if (t.Result.TryGetValue("toggle", out object openedObj) &&
                    bool.TryParse(openedObj.ToString(), out bool open)) {
                    return open;
                }
                return opened;
            });
        }

        internal Task<bool> SetRoomVisible(bool visible) {
            var msg = Message.NewRequest("conv", "visible");
            msg["toggle"] = visible;
            return Send(msg).OnSuccess(t => {
                if (t.Result.TryGetValue("toggle", out object visibleObj) &&
                    bool.TryParse(visibleObj.ToString(), out bool v)) {
                    return v;
                }
                // TODO 异常
                return visible;
            });
        }

        internal Task<int> SetMaster(int newMasterId) {
            var msg = Message.NewRequest("conv", "update-master-client");
            msg["masterActorId"] = newMasterId;
            return Send(msg).OnSuccess(t => { 
                if (t.Result.TryGetValue("masterActorId", out object masterIdObj) &&
                    int.TryParse(masterIdObj.ToString(), out int masterId)) {
                    return masterId;
                }
                // TODO 异常
                return -1;
            });
        }

        internal Task<int> KickPlayer(int actorId, int code, string reason) {
            var msg = Message.NewRequest("conv", "kick");
            msg["targetActorId"] = actorId;
            msg["appCode"] = code;
            msg["appMsg"] = reason;
            return Send(msg).OnSuccess(t => { 
                if (t.Result.TryGetValue("targetActorId", out object actorIdObj) &&
                    int.TryParse(actorIdObj.ToString(), out int kickedActorId)) {
                    return kickedActorId; 
                }
                // TODO 异常
                return actorId;
            });
        }

        internal Task SendEvent(string eventId, Dictionary<string, object> eventData, SendEventOptions options) {
            var msg = Message.NewRequest("direct", null);
            msg["eventId"] = eventId;
            msg["msg"] = eventData;
            msg["receiverGroup"] = (int) options.ReceiverGroup;
            msg["toActorIds"] = options.targetActorIds.Cast<object>().ToList();
            return Send(msg);
        }

        internal Task<Dictionary<string, object>> SetRoomCustomProperties(Dictionary<string, object> properties, Dictionary<string, object> expectedValues) {
            var msg = Message.NewRequest("conv", "update");
            msg["attr"] = properties;
            if (expectedValues != null) {
                msg["expectAttr"] = expectedValues;
            }
            return Send(msg).OnSuccess(t => { 
                if (t.Result.TryGetValue("attr", out object attrObj)) {
                    return attrObj as Dictionary<string, object>;
                }
                return null;
            });
        }

        internal Task<Dictionary<string, object>> SetPlayerCustomProperties(int playerId, Dictionary<string, object> properties, Dictionary<string, object> expectedValues) {
            var msg = Message.NewRequest("conv", "update-player-prop");
            msg["targetActorId"] = playerId;
            msg["attr"] = properties;
            if (expectedValues != null) {
                msg["expectAttr"] = expectedValues;
            }
            return Send(msg).OnSuccess(t => {
                if (t.Result.TryGetValue("actorId", out object actorIdObj) &&
                    int.TryParse(actorIdObj.ToString(), out int actorId) &&
                    t.Result.TryGetValue("attr", out object attrObj)) {
                    return new Dictionary<string, object> {
                        { "actorId", actorId },
                        { "changedProps", attrObj },
                    };
                }
                return null;
            });
        }

        protected override int GetPingDuration() {
            return 7;
        }
    }
}
