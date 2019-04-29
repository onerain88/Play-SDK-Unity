using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace LeanCloud.Play {
    internal class LobbyConnection : Connection {
        internal LobbyConnection() {

        }

        internal static Task<LobbyConnection> Connect(string appId, string server, string userId, string gameVersion) {
            var tcs = new TaskCompletionSource<LobbyConnection>();
            LobbyConnection connection = new LobbyConnection();
            connection.Connect(server, userId).ContinueWith(t => {
                if (t.IsFaulted) {
                    throw t.Exception.InnerException;
                }
                return connection.OpenSession(appId, userId, gameVersion);
            }).Unwrap().ContinueWith(t => {
                if (t.IsFaulted) {
                    tcs.SetException(t.Exception.InnerException);
                } else {
                    tcs.SetResult(connection);
                }
            });
            return tcs.Task;
        }

        internal Task<LobbyRoomResult> CreateRoom(string roomName, RoomOptions roomOptions, List<string> expectedUserIds) {
            var tcs = new TaskCompletionSource<LobbyRoomResult>();
            var msg = Message.NewRequest("conv", "start");
            if (!string.IsNullOrEmpty(roomName)) {
                msg["cid"] = roomName;
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
                return new LobbyRoomResult {
                    RoomId = res["cid"].ToString(),
                    PrimaryUrl = res["addr"].ToString(),
                    SecondaryUrl = res["secureAddr"].ToString()
                };
            });
        }

        internal Task<LobbyRoomResult> JoinRoom(string roomName) {
            var msg = Message.NewRequest("conv", "add");
            msg["cid"] = roomName;
            // TODO 其他参数

            return Send(msg).OnSuccess(t => {
                var res = t.Result;
                return new LobbyRoomResult {
                    RoomId = res["cid"].ToString(),
                    PrimaryUrl = res["addr"].ToString(),
                    SecondaryUrl = res["secureAddr"].ToString()
                };
            });
        }

        internal Task<LobbyRoomResult> RejoinRoom(string roomName) {
            var msg = Message.NewRequest("conv", "add");
            msg["cid"] = roomName;
            msg["rejoin"] = true;
            return Send(msg).OnSuccess(t => {
                var res = t.Result;
                return new LobbyRoomResult {
                    RoomId = res["cid"].ToString(),
                    PrimaryUrl = res["addr"].ToString(),
                    SecondaryUrl = res["secureAddr"].ToString()
                };
            });
        }

        internal Task<LobbyRoomResult> JoinRandomRoom(Dictionary<string, object> matchProperties, List<string> expectedUserIds) {
            var msg = Message.NewRequest("conv", "add-random");
            if (matchProperties != null) {
                msg["expectAttr"] = matchProperties;
            }
            if (expectedUserIds != null) {
                msg["expectMembers"] = expectedUserIds;
            }
            return Send(msg).OnSuccess(t => {
                var res = t.Result;
                return new LobbyRoomResult {
                    RoomId = res["cid"].ToString(),
                    PrimaryUrl = res["addr"].ToString(),
                    SecondaryUrl = res["secureAddr"].ToString()
                };
            });
        }

        internal Task<LobbyRoomResult> JoinOrCreateRoom(string roomName, RoomOptions roomOptions, List<string> expectedUserIds)  {
            var msg = Message.NewRequest("conv", "add");
            msg["cid"] = roomName;
            msg["createOnNotFound"] = true;
            if (roomOptions != null) {
                var roomOptionsDict = roomOptions.ToDictionary();
                foreach (var entry in roomOptionsDict) {
                    msg[entry.Key] = entry.Value;
                }
            }
            if (expectedUserIds != null) {
                List<object> expecteds = expectedUserIds.Cast<object>().ToList();
                msg["expectMembers"] = expecteds;
            }
            return Send(msg).OnSuccess(t => {
                var res = t.Result;
                return new LobbyRoomResult {
                    Create = res.Op == "started",
                    RoomId = res["cid"].ToString(),
                    PrimaryUrl = res["addr"].ToString(),
                    SecondaryUrl = res["secureAddr"].ToString()
                };
            });
        }

        internal Task<LobbyRoom> MatchRandom(Dictionary<string, object> matchProperties, List<string> expectedUserIds) {
            var msg = Message.NewRequest("conv", "match-random");
            if (matchProperties != null) {
                msg["expectAttr"] = matchProperties;
            }
            if (expectedUserIds != null) {
                msg["expectMembers"] = expectedUserIds;
            }
            return Send(msg).OnSuccess(t => {
                var res = t.Result;
                return LobbyRoom.NewFromDictionary(res.Data);
            });
        }

        protected override int GetPingDuration() {
            return 20;
        }
    }
}