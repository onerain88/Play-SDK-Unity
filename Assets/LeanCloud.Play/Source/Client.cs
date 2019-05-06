using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LeanCloud.Play {
    public class Client {
        // 事件
        public event Action<List<LobbyRoom>> OnLobbyRoomListUpdated;
        public event Action<Player> OnPlayerRoomJoined;
        public event Action<Player> OnPlayerRoomLeft;
        public event Action<Player> OnMasterSwitched;
        public event Action<bool> OnRoomOpenChanged;
        public event Action<bool> OnRoomVisibleChanged;
        public event Action<Dictionary<string, object>> OnRoomCustomPropertiesChanged;
        public event Action<Player, Dictionary<string, object>> OnPlayerCustomPropertiesChanged;
        public event Action<Player> OnPlayerActivityChanged;
        public event Action<string, Dictionary<string, object>, int> OnCustomEvent;
        public event Action<int, string> OnRoomKicked;
        public event Action OnDisconnected;
        public event Action<int, string> OnError;

        readonly SynchronizationContext context;

        public string AppId {
            get; private set;
        }

        public string AppKey {
            get; private set;
        }

        public string UserId {
            get; private set;
        }

        public bool Ssl {
            get; private set;
        }

        public string GameVersion {
            get; private set;
        }

        PlayRouter playRouter;
        LobbyRouter lobbyRouter;
        LobbyConnection lobbyConn;
        GameConnection gameConn;

        PlayState state;

        public List<LobbyRoom> LobbyRoomList;

        public Room Room {
            get; private set;
        }

        public Player Player {
            get; internal set;
        }

        public Client(string appId, string appKey, string userId, bool ssl = true, string gameVersion = "0.0.1", string playServer = null) {
            AppId = appId;
            AppKey = appKey;
            UserId = userId;
            Ssl = ssl;
            GameVersion = gameVersion;

            state = PlayState.INIT;
            Logger.Debug("start at {0}", Thread.CurrentThread.ManagedThreadId);
            context = SynchronizationContext.Current ?? new PlaySynchronizationContext();

            playRouter = new PlayRouter(appId, playServer);
            lobbyRouter = new LobbyRouter(appId, false, null);
            lobbyConn = new LobbyConnection();
            gameConn = new GameConnection();
        }

        public Task<Client> Connect() {
            var tcs = new TaskCompletionSource<Client>();
            context.Post(_ => {
                Logger.Debug("connecting at {0}", Thread.CurrentThread.ManagedThreadId);
                // 判断状态
                if (state == PlayState.CONNECTING) {
                    // 
                    Logger.Debug("it is connecting...");
                    return;
                }
                if (state != PlayState.INIT) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call Connect() on {0} state", state.ToString())));
                    return;
                }
                state = PlayState.CONNECTING;
                // 建立连接
                ConnectLobby().ContinueWith(t => {
                    context.Post(__ => {
                        if (t.IsFaulted) {
                            state = PlayState.INIT;
                            tcs.SetException(t.Exception.InnerException);
                        } else {
                            state = PlayState.LOBBY;
                            Logger.Debug("connected at: {0}", Thread.CurrentThread.ManagedThreadId);
                            lobbyConn = t.Result;
                            lobbyConn.OnMessage += OnLobbyConnMessage;
                            lobbyConn.OnClose += OnLobbyConnClose;
                            tcs.SetResult(this);
                        }
                    }, null);
                });
            }, null);
            return tcs.Task;
        }

        public Task JoinLobby() {
            var tcs = new TaskCompletionSource<bool>();
            context.Post(_ => {
                if (state != PlayState.LOBBY) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call JoinLobby() on {0} state", state.ToString())));
                    return;
                }
                lobbyConn.JoinLobby().ContinueWith(t => {
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        tcs.SetResult(true);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task<Room> CreateRoom(string roomName = null, RoomOptions roomOptions = null, List<string> expectedUserIds = null) {
            var tcs = new TaskCompletionSource<Room>();
            context.Post(_ => {
                // 判断状态
                if (state != PlayState.LOBBY) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call CreateRoom() on {0} state", state.ToString())));
                    return;
                }
                state = PlayState.LOBBY_TO_GAME;
                string roomId = null;
                GameConnection gc = null;
                // 发送创建房间消息
                lobbyConn.CreateRoom(roomName, roomOptions, expectedUserIds).ContinueWith(t => {
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    // 将 GameConnection 中的 Connect 和 Create 合并在一起返回
                    roomId = t.Result.RoomId;
                    var server = t.Result.PrimaryUrl;
                    return GameConnection.Connect(AppId, server, UserId, GameVersion);
                }).Unwrap().ContinueWith(t => {
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    gc = t.Result;
                    return gc.CreateRoom(roomId, roomOptions, expectedUserIds);
                }).Unwrap().ContinueWith(t => {
                    context.Post(__ => {
                        if (t.IsFaulted) {
                            state = PlayState.INIT;
                            tcs.SetException(t.Exception.InnerException);
                        } else {
                            LobbyToGame(gc, t.Result);
                            tcs.SetResult(Room);
                        }
                    }, null);
                });
            }, null);
            return tcs.Task;
        }

        public Task<Room> JoinRoom(string roomName, List<string> expectedUserIds = null) {
            var tcs = new TaskCompletionSource<Room>();
            context.Post(_ => {
                // 判断状态
                if (state != PlayState.LOBBY) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError, 
                        string.Format("You cannot call JoinRoom() on {0} state", state.ToString())));
                    return;
                }
                state = PlayState.LOBBY_TO_GAME;
                string roomId = null;
                GameConnection gc = null;
                lobbyConn.JoinRoom(roomName, expectedUserIds).ContinueWith(t => {
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    roomId = t.Result.RoomId;
                    var server = t.Result.PrimaryUrl;
                    return GameConnection.Connect(AppId, server, UserId, GameVersion);
                }).Unwrap().ContinueWith(t => {
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    gc = t.Result;
                    return gc.JoinRoom(roomId, expectedUserIds);
                }).Unwrap().ContinueWith(t => {
                    context.Post(__ => { 
                        if (t.IsFaulted) {
                            state = PlayState.INIT;
                            tcs.SetException(t.Exception.InnerException);
                        } else {
                            LobbyToGame(gc, t.Result);
                            tcs.SetResult(Room);
                        }
                    }, null);
                });
            }, null);
            return tcs.Task;
        }

        public Task<Room> RejoinRoom(string roomName) {
            var tcs = new TaskCompletionSource<Room>();
            context.Post(_ => {
                if (state != PlayState.LOBBY) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError, 
                        string.Format("You cannot call RejoinRoom() on {0} state", state.ToString())));
                    return;
                }
                state = PlayState.LOBBY_TO_GAME;
                string roomId = null;
                GameConnection gc = null;
                lobbyConn.RejoinRoom(roomName).ContinueWith(t => {
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    roomId = t.Result.RoomId;
                    var server = t.Result.PrimaryUrl;
                    return GameConnection.Connect(AppId, server, UserId, GameVersion);
                }).Unwrap().ContinueWith(t => { 
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    gc = t.Result;
                    return gc.JoinRoom(roomId, null);
                }).Unwrap().ContinueWith(t => {
                    context.Post(__ => { 
                        if (t.IsFaulted) {
                            state = PlayState.INIT;
                            tcs.SetException(t.Exception.InnerException);
                        } else {
                            LobbyToGame(gc, t.Result);
                            tcs.SetResult(Room);
                        }
                    }, null);
                });
            }, null);
            return tcs.Task;
        }

        public Task<Room> JoinOrCreateRoom(string roomName, RoomOptions roomOptions = null, List<string> expectedUserIds = null) {
            var tcs = new TaskCompletionSource<Room>();
            context.Post(_ => { 
                if (state != PlayState.LOBBY) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call JoinOrCreateRoom() on {0} state", state.ToString())));
                    return;
                }
                state = PlayState.LOBBY_TO_GAME;
                bool create = false;
                string roomId = null;
                GameConnection gc = null;
                lobbyConn.JoinOrCreateRoom(roomName, roomOptions, expectedUserIds).ContinueWith(t => { 
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    create = t.Result.Create;
                    roomId = t.Result.RoomId;
                    var server = t.Result.PrimaryUrl;
                    return GameConnection.Connect(AppId, server, UserId, GameVersion);
                }).Unwrap().ContinueWith(t => { 
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    gc = t.Result;
                    if (create) {
                        return gc.CreateRoom(roomId, roomOptions, expectedUserIds);
                    }
                    return gc.JoinRoom(roomId, expectedUserIds);
                }).Unwrap().ContinueWith(t => {
                    context.Post(__ => { 
                        if (t.IsFaulted) {
                            state = PlayState.INIT;
                            tcs.SetException(t.Exception.InnerException);
                        } else {
                            LobbyToGame(gc, t.Result);
                            tcs.SetResult(Room);
                        }
                    }, null);
                });
            }, null);
            return tcs.Task;
        }

        public Task<Room> JoinRandomRoom(Dictionary<string, object> matchProperties = null, List<string> expectedUserIds = null) {
            var tcs = new TaskCompletionSource<Room>();
            context.Post(_ => { 
                if (state != PlayState.LOBBY) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError, 
                        string.Format("You cannot call JoinRandomRoom() on {0} state", state.ToString())));
                    return;
                }
                state = PlayState.LOBBY_TO_GAME;
                string roomId = null;
                GameConnection gc = null;
                lobbyConn.JoinRandomRoom(matchProperties, expectedUserIds).ContinueWith(t => {
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    roomId = t.Result.RoomId;
                    var server = t.Result.PrimaryUrl;
                    return GameConnection.Connect(AppId, server, UserId, GameVersion);
                }).Unwrap().ContinueWith(t => { 
                    if (t.IsFaulted) {
                        throw t.Exception.InnerException;
                    }
                    gc = t.Result;
                    return gc.JoinRoom(roomId, expectedUserIds);
                }).Unwrap().ContinueWith(t => {
                    context.Post(__ => {
                        if (t.IsFaulted) {
                            state = PlayState.INIT;
                            tcs.SetException(t.Exception.InnerException);
                        } else {
                            LobbyToGame(gc, t.Result);
                            tcs.SetResult(Room);
                        }
                    }, null);
                });
            }, null);
            return tcs.Task;
        }

        public Task<LobbyRoom> MatchRandom(Dictionary<string, object> matchProperties = null, List<string> expectedUserIds = null) {
            var tcs = new TaskCompletionSource<LobbyRoom>();
            context.Post(_ => { 
                if (state != PlayState.LOBBY) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call MatchRandom() on {0} state", state.ToString())));
                    return;
                }
                lobbyConn.MatchRandom(matchProperties, expectedUserIds).ContinueWith(t => {
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        tcs.SetResult(t.Result);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task LeaveRoom() {
            var tcs = new TaskCompletionSource<bool>();
            context.Post(_ => {
                //  判断状态
                if (state != PlayState.GAME) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError, 
                        string.Format("You cannot call LeaveRoom() on {0} state", state.ToString())));
                    return;
                }
                gameConn.LeaveRoom().ContinueWith(t => { 
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        context.Post(__ => {
                            gameConn.Close();
                            // 建立连接
                            ConnectLobby().ContinueWith(st => {
                                context.Post(___ => {
                                    if (t.IsFaulted) {
                                        state = PlayState.INIT;
                                        throw t.Exception.InnerException;
                                    }
                                    GameToLobby(st.Result);
                                    tcs.SetResult(true);
                                }, null);
                            });
                        }, null);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task<bool> SetRoomOpened(bool opened) {
            var tcs = new TaskCompletionSource<bool>();
            context.Post(_ => {
                // 判断状态
                if (state != PlayState.GAME) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError, 
                        string.Format("You cannot call SetRoomOpened() on {0} state", state.ToString())));
                    return;
                }
                gameConn.SetRoomOpened(opened).ContinueWith(t => { 
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        context.Post(__ => {
                            Room.Opened = t.Result;
                            tcs.SetResult(t.Result);
                        }, null);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task<bool> SetRoomVisible(bool visible) {
            var tcs = new TaskCompletionSource<bool>();
            context.Post(_ => { 
                // 判断状态
                if (state != PlayState.GAME) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError, 
                        string.Format("You cannot call SetRoomVisible() on {0} state", state.ToString())));
                    return;
                }
                gameConn.SetRoomVisible(visible).ContinueWith(t => {
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        context.Post(__ => {
                            Room.Visible = t.Result;
                            tcs.SetResult(t.Result);
                        }, null);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task<Player> SetMaster(int newMasterId) {
            var tcs = new TaskCompletionSource<Player>();
            context.Post(_ => {
                if (state != PlayState.GAME) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call SetMaster() on {0} state", state.ToString())));
                    return;
                }
                gameConn.SetMaster(newMasterId).ContinueWith(t => { 
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        context.Post(__ => {
                            Room.MasterActorId = t.Result;
                            tcs.SetResult(Room.Master);
                        }, null);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task KickPlayer(int actorId, int code = 0, string reason = null) {
            var tcs = new TaskCompletionSource<bool>();
            context.Post(_ => { 
                if (state != PlayState.GAME) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call KickPlayer() on {0} state", state.ToString())));
                    return;
                }
                gameConn.KickPlayer(actorId, code, reason).ContinueWith(t => {
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        context.Post(__ => {
                            try {
                                Room.RemovePlayer(t.Result);
                            } catch (Exception e) {
                                Logger.Error(e.Message);
                            }
                            tcs.SetResult(true);
                        }, null);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task SendEvent(string eventId, Dictionary<string, object> eventData = null, SendEventOptions options = null) {
            var tcs = new TaskCompletionSource<bool>();
            context.Post(_ => {
                if (state != PlayState.GAME) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call SendEvent() on {0} state", state.ToString())));
                    return;
                }
                var opts = options;
                if (opts == null) {
                    opts = new SendEventOptions { 
                        ReceiverGroup = ReceiverGroup.All
                    };
                }
                gameConn.SendEvent(eventId, eventData, opts).ContinueWith(t => {
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        context.Post(__ => {
                            tcs.SetResult(true);
                        }, null);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task SetRoomCustomProperties(Dictionary<string, object> properties, Dictionary<string, object> expectedValues = null) {
            var tcs = new TaskCompletionSource<bool>();
            context.Post(_ => { 
                if (state != PlayState.GAME) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call SetRoomCustomProperties() on {0} state", state.ToString())));
                    return;
                }
                gameConn.SetRoomCustomProperties(properties, expectedValues).ContinueWith(t => {
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        context.Post(__ => {
                            if (t.Result != null) {
                                Room.MergeProperties(t.Result);
                            }
                            tcs.SetResult(true);
                        }, null);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public Task SetPlayerCustomProperties(int actorId, Dictionary<string, object> properties, Dictionary<string, object> expectedValues = null) {
            var tcs = new TaskCompletionSource<bool>();
            context.Post(_ => {
                if (state != PlayState.GAME) {
                    tcs.SetException(new PlayException(PlayExceptionCode.StateError,
                        string.Format("You cannot call SetPlayerCustomProperties() on {0} state", state.ToString())));
                    return;
                }
                gameConn.SetPlayerCustomProperties(actorId, properties, expectedValues).ContinueWith(t => { 
                    if (t.IsFaulted) {
                        tcs.SetException(t.Exception.InnerException);
                    } else {
                        context.Post(__ => { 
                            if (t.Result != null) {
                                var aId = int.Parse(t.Result["actorId"].ToString());
                                var player = Room.GetPlayer(aId);
                                player.MergeProperties(t.Result["changedProps"] as Dictionary<string, object>);
                            }
                            tcs.SetResult(true);
                        }, null);
                    }
                });
            }, null);
            return tcs.Task;
        }

        public void PauseMessageQueue() { 
            if (state == PlayState.LOBBY) {
                lobbyConn.PauseMessageQueue();
            } else if (state == PlayState.GAME) {
                gameConn.PauseMessageQueue();
            }
        }

        public void ResumeMessageQueue() {
            if (state == PlayState.LOBBY) {
                lobbyConn.ResumeMessageQueue();
            } else if (state == PlayState.GAME) {
                gameConn.ResumeMessageQueue();
            }
        }

        public void Close() {
            if (state == PlayState.LOBBY) {
                lobbyConn.Close();
            } else if (state == PlayState.GAME) {
                gameConn.Close();
            }
        }

        void OnLobbyConnMessage(Message msg) {
            context.Post((m) => { 
                switch (msg.Cmd) {
                    case "lobby":
                        switch (msg.Op) {
                            case "room-list":
                                HandleRoomListMsg(msg);
                                break;
                            default:
                                HandleUnknownMsg(msg);
                                break;
                        }
                        break;
                    case "events":
                        break;
                    case "statistic":
                        break;
                    case "conv":
                        break;
                    case "error":
                        HandleErrorMsg(msg);
                        break;
                    default:
                        HandleUnknownMsg(msg);
                        break;
                }
            }, msg);
        }

        void HandleRoomListMsg(Message msg) {
            LobbyRoomList = new List<LobbyRoom>();
            if (msg.Data.TryGetValue("list", out object roomsObj)) {
                List<object> rooms = roomsObj as List<object>;
                foreach (Dictionary<string, object> room in rooms) {
                    var lobbyRoom = LobbyRoom.NewFromDictionary(room);
                    LobbyRoomList.Add(lobbyRoom);
                }
            }
            OnLobbyRoomListUpdated?.Invoke(LobbyRoomList);
        }

        void HandleErrorMsg(Message msg) {
            Logger.Error("error msg: {0}", msg.ToJson());
            if (msg.TryGetValue("reasonCode", out object codeObj) &&
                int.TryParse(codeObj.ToString(), out int code)) {
                var detail = msg["detail"] as string;
                OnError?.Invoke(code, detail); 
            }
        }

        void HandleUnknownMsg(Message msg) {
            try {
                Logger.Error("unknown msg: {0}", msg);
            } catch (Exception e) {
                Logger.Error(e.Message);
            }
        }

        void OnLobbyConnClose(int code, string reason) {
            OnDisconnected?.Invoke();
        }

        void OnGameConnMessage(Message msg) {
            try {
                context.Post(state => {
                    var message = state as Message;
                    Logger.Debug($"On Game Message: {message.ToJson()}");
                    switch (message.Cmd) {
                        case "conv":
                            switch (message.Op) {
                                case "members-joined":
                                    HandlePlayerJoinedRoom(msg);
                                    break;
                                case "members-left":
                                    HandlePlayerLeftRoom(msg);
                                    break;
                                case "master-client-changed":
                                    HandleMasterChanged(msg);
                                    break;
                                case "opened-notify":
                                    HandleRoomOpenChanged(msg);
                                    break;
                                case "visible-notify":
                                    HandleRoomVisibleChanged(msg);
                                    break;
                                case "updated-notify":
                                    HandleRoomCustomPropertiesChanged(msg);
                                    break;
                                case "player-props":
                                    HandlePlayerCustomPropertiesChanged(msg);
                                    break;
                                case "members-offline":
                                    HandlePlayerOffline(msg);
                                    break;
                                case "members-online":
                                    HandlePlayerOnline(msg);
                                    break;
                                case "kicked-notice":
                                    HandleRoomKicked(msg);
                                    break;
                                default:
                                    HandleUnknownMsg(msg);
                                    break;
                            }
                            break;
                        case "events":
                            break;
                        case "direct":
                            HandleSendEvent(msg);
                            break;
                        case "error":
                            HandleErrorMsg(msg);
                            break;
                        default:
                            HandleUnknownMsg(msg);
                            break;
                    }
                }, msg);
            } catch (Exception e) {
                Logger.Error(e.Message);
            }
        }   

        void OnGameConnClose(int code, string reason) {
            OnDisconnected?.Invoke();
        }

        void HandlePlayerJoinedRoom(Message msg) { 
            if (msg.TryGetValue("member", out object playerObj)) {
                var player = Player.NewFromDictionary(playerObj as Dictionary<string, object>);
                player.Client = this;
                Room.AddPlayer(player);
                OnPlayerRoomJoined?.Invoke(player);
            } else {
                Logger.Error("Handle player joined room error: {0}", msg.ToJson());
            }
        }

        void HandlePlayerLeftRoom(Message msg) { 
            if (msg.TryGetValue("actorId", out object playerIdObj) &&
                int.TryParse(playerIdObj.ToString(), out int playerId)) {
                try {
                    var leftPlayer = Room.GetPlayer(playerId);
                    Room.RemovePlayer(playerId);
                    OnPlayerRoomLeft?.Invoke(leftPlayer);
                } catch (Exception e) {
                    Logger.Error(e.Message);
                }
            } else {
                Logger.Error("Handle player left room error: {0}", msg.ToJson());
            }
        }

        void HandleMasterChanged(Message msg) {
            if (msg.Data.ContainsKey("masterActorId")) {
                if (msg.Data["masterActorId"] != null &&
                    msg.TryGetValue("masterActorId", out object newMasterIdObj) &&
                    int.TryParse(newMasterIdObj.ToString(), out int newMasterId)) {
                    Room.MasterActorId = newMasterId;
                    var newMaster = Room.GetPlayer(newMasterId);
                    OnMasterSwitched?.Invoke(newMaster);
                } else {
                    Room.MasterActorId = -1;
                    OnMasterSwitched?.Invoke(null);
                }
            } else {
                Logger.Error("Handle room open changed error: {0}", msg.ToJson());
            }
        }

        void HandleRoomOpenChanged(Message msg) { 
            if (msg.TryGetValue("toggle", out object openedObj) &&
                bool.TryParse(openedObj.ToString(), out bool opened)) {
                Room.Opened = opened;
                OnRoomOpenChanged?.Invoke(opened);
            } else {
                Logger.Error("Handle room open changed error: {0}", msg.ToJson());
            }
        }

        void HandleRoomVisibleChanged(Message msg) { 
            if (msg.TryGetValue("toggle", out object visibleObj) &&
                bool.TryParse(visibleObj.ToString(), out bool visible)) {
                Room.Visible = visible;
                OnRoomVisibleChanged?.Invoke(visible);
            } else {
                Logger.Error("Handle room visible changed error: {0}", msg.ToJson());
            }
        }

        void HandleRoomCustomPropertiesChanged(Message msg) { 
            if (msg.TryGetValue("attr", out object attrObj)) {
                var changedProps = attrObj as Dictionary<string, object>;
                Room.MergeProperties(changedProps);
                OnRoomCustomPropertiesChanged?.Invoke(changedProps);
            } else {
                Logger.Error("Handle room custom properties changed error: {0}", msg.ToJson());
            }
        }

        void HandlePlayerCustomPropertiesChanged(Message msg) { 
            if (msg.TryGetValue("actorId", out object playerIdObj) && 
                int.TryParse(playerIdObj.ToString(), out int playerId) &&
                msg.TryGetValue("attr", out object attrObj)) {
                var player = Room.GetPlayer(playerId);
                if (player == null) {
                    Logger.Error("No player id: {0} when player properties changed", msg.ToJson());
                    return;
                }
                var changedProps = attrObj as Dictionary<string, object>;
                player.MergeProperties(changedProps);
                OnPlayerCustomPropertiesChanged?.Invoke(player, changedProps);
            } else {
                Logger.Error("Handle player custom properties changed error: {0}", msg.ToJson());
            }
        }

        void HandlePlayerOffline(Message msg) {
            if (msg.TryGetValue("initByActor", out object playerIdObj) &&
                int.TryParse(playerIdObj.ToString(), out int playerId)) {
                var player = Room.GetPlayer(playerId);
                if (player == null) {
                    Logger.Error("No player id: {0} when player is offline");
                    return;
                }
                player.IsActive = false;
                OnPlayerActivityChanged?.Invoke(player);
            } else {
                Logger.Error("Handle player offline error: {0}", msg.ToJson());
            }
        }

        void HandlePlayerOnline(Message msg) { 
            if (msg.TryGetValue("member", out object memberObj)) {
                var member = memberObj as Dictionary<string, object>;
                if (member.TryGetValue("actorId", out object playerIdObj) &&
                    int.TryParse(playerIdObj.ToString(), out int playerId)) {
                    var player = Player.NewFromDictionary(member);
                    player.Client = this;
                    OnPlayerActivityChanged?.Invoke(player);
                } else {
                    Logger.Error("Handle player online error: {0}", msg.ToJson());
                }
            } else {
                Logger.Error("Handle player online error: {0}", msg.ToJson());
            }
        }

        void HandleSendEvent(Message msg) { 
            if (msg.TryGetValue("eventId", out object eventIdObj)) {
                var eventId = eventIdObj.ToString();
                var senderId = -1;
                if (msg.TryGetValue("fromActorId", out object senderIdObj)) {
                    int.TryParse(senderIdObj.ToString(), out senderId);
                }
                Dictionary<string, object> eventData = null;
                if (msg.TryGetValue("msg", out object eventDataObj)) {
                    eventData = eventDataObj as Dictionary<string, object>;
                }
                OnCustomEvent?.Invoke(eventId, eventData, senderId);
            } else {
                Logger.Error("Handle custom event error: {0}", msg.ToJson());
            }
        }

        void HandleRoomKicked(Message msg) {
            // 建立连接
            ConnectLobby().ContinueWith(t => {
                context.Post(_ => {
                    if (t.IsFaulted) {
                        state = PlayState.INIT;
                        throw t.Exception.InnerException;
                    }
                    GameToLobby(t.Result);
                    int code = -1;
                    string reason = string.Empty;
                    if (msg.TryGetValue("appCode", out object codeObj) &&
                        int.TryParse(codeObj.ToString(), out code)) { }
                    if (msg.TryGetValue("appMsg", out object reasonObj)) {
                        reason = reasonObj.ToString();
                    }
                    OnRoomKicked?.Invoke(code, reason);
                }, null);
            });
        }

        Task<LobbyConnection> ConnectLobby() {
            return playRouter.Fetch().OnSuccess(t => {
                var serverUrl = t.Result;
                Logger.Debug("play server: {0} at {1}", serverUrl, Thread.CurrentThread.ManagedThreadId);
                return lobbyRouter.Fetch(serverUrl);
            }).Unwrap().OnSuccess(t => {
                var lobbyUrl = t.Result;
                Logger.Debug("wss server: {0} at {1}", lobbyUrl, Thread.CurrentThread.ManagedThreadId);
                return LobbyConnection.Connect(AppId, lobbyUrl, UserId, GameVersion);
            }).Unwrap();
        }

        void LobbyToGame(GameConnection gc, Room room) {
            state = PlayState.GAME;
            lobbyConn.OnMessage -= OnLobbyConnMessage;
            lobbyConn.OnClose -= OnLobbyConnClose;
            lobbyConn.Close();
            gameConn = gc;
            gameConn.OnMessage += OnGameConnMessage;
            gameConn.OnClose += OnGameConnClose;
            Room = room;
            Room.Client = this;
            foreach (var player in Room.PlayerList) { 
                if (player.UserId == UserId) {
                    Player = player;
                }
                player.Client = this;
            }
        }

        void GameToLobby(LobbyConnection lc) {
            state = PlayState.LOBBY;
            gameConn.OnMessage -= OnGameConnMessage;
            gameConn.OnClose -= OnGameConnClose;
            gameConn.Close();
            Logger.Debug("connected at: {0}", Thread.CurrentThread.ManagedThreadId);
            lobbyConn = lc;
            lobbyConn.OnMessage += OnLobbyConnMessage;
            lobbyConn.OnClose += OnLobbyConnClose;
        }

        public void _Disconnet() { 
            if (state == PlayState.LOBBY) {
                lobbyConn.Close();
            } else if (state == PlayState.GAME) {
                gameConn.Close();
            }
        }
    }
}
