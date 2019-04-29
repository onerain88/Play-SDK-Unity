﻿using System;
using System.Threading.Tasks;
using System.Threading;
using WebSocketSharp;
using System.Collections.Generic;

namespace LeanCloud.Play {
    internal abstract class Connection {
        static readonly string PING = "{}";

        protected WebSocket ws;
        readonly Dictionary<int, TaskCompletionSource<Message>> requests;

        internal event Action<Message> OnMessage;
        internal event Action<int, string> OnClose;

        readonly Queue<Message> messageQueue;
        bool isMessageQueueRunning;

        CancellationTokenSource pingTokenSource;
        CancellationTokenSource pongTokenSource;

        internal Connection() {
            requests = new Dictionary<int, TaskCompletionSource<Message>>();
            messageQueue = new Queue<Message>();
            pingTokenSource = new CancellationTokenSource();
            pongTokenSource = new CancellationTokenSource();
        }

        protected Task Connect(string server, string userId) {
            Logger.Debug("connect at {0}", Thread.CurrentThread.ManagedThreadId);
            var tcs = new TaskCompletionSource<bool>();
            ws = new WebSocket(server);
            void onOpen(object sender, EventArgs args) {
                Logger.Debug("wss on open at {0}", Thread.CurrentThread.ManagedThreadId);
                Connected();
                ws.OnOpen -= onOpen;
                ws.OnClose -= onClose;
                ws.OnError -= onError;
                tcs.SetResult(true);
            }
            void onClose(object sender, CloseEventArgs args) {
                Logger.Debug("wss on close at {0}", Thread.CurrentThread.ManagedThreadId);
                ws.OnOpen -= onOpen;
                ws.OnClose -= onClose;
                ws.OnError -= onError;
                tcs.SetException(new Exception());
            }
            void onError(object sender, ErrorEventArgs args) {
                Logger.Debug("wss on error at {0}", Thread.CurrentThread.ManagedThreadId);
                ws.OnOpen -= onOpen;
                ws.OnClose -= onClose;
                ws.OnError -= onError;
                tcs.SetException(new Exception());
            }
            ws.OnOpen += onOpen;
            ws.OnClose += onClose;
            ws.OnError += onError;
            ws.Connect();
            ws.ConnectAsync();
            return tcs.Task;
        }

        void Connected() {
            isMessageQueueRunning = true;
            ws.OnMessage += OnWebSocketMessage;
            ws.OnClose += OnWebSocketClose;
            ws.OnError += OnWebSocketError;
            Ping();
        }

        protected Task OpenSession(string appId, string userId, string gameVersion) {
            var msg = Message.NewRequest("session", "open");
            msg["appId"] = appId;
            msg["peerId"] = userId;
            msg["sdkVersion"] = Config.PlayVersion;
            msg["gameVersion"] = gameVersion;
            return Send(msg);
        }

        protected Task<Message> Send(Message msg) {
            var tcs = new TaskCompletionSource<Message>();
            if (msg.HasI) {
                lock (requests) {
                    requests.Add(msg.I, tcs);
                }
            }
            Send(msg.ToJson());
            return tcs.Task;
        }

        void Send(string msg) {
            Logger.Debug("=> {0} at {1}", msg, Thread.CurrentThread.ManagedThreadId);
            ws.Send(msg);
            Ping();
        }

        internal void Close() {
            StopKeepAlive();
            ws.OnMessage -= OnWebSocketMessage;
            ws.OnClose -= OnWebSocketClose;
            ws.OnError -= OnWebSocketError;
            ws.CloseAsync();
        }

        // Websocket 事件
        void OnWebSocketMessage(object sender, MessageEventArgs eventArgs) {
            Logger.Debug("<= {0} at {1}", eventArgs.Data, Thread.CurrentThread.ManagedThreadId);
            Pong();
            if (PING.Equals(eventArgs.Data)) {
                return;
            }
            var message = Message.FromJson(eventArgs.Data);
            if (isMessageQueueRunning) {
                HandleMessage(message);
            } else {
                lock (messageQueue) {
                    messageQueue.Enqueue(message);
                }
            }
        }

        void HandleMessage(Message message) {
            if (message.HasI) {
                TaskCompletionSource<Message> tcs = null;
                lock (requests) {
                    if (!requests.TryGetValue(message.I, out tcs)) {
                        Logger.Error("no requests for {0}", message.I);
                    }
                }
                if (message.IsError) {
                    tcs.SetException(new PlayException(message.ReasonCode, message.Detail));
                } else {
                    tcs.SetResult(message);
                }
            } else {
                Logger.Debug($"handle message: {message.ToJson()}");
                // 推送消息
                OnMessage?.Invoke(message);
            }
        }

        void OnWebSocketClose(object sender, CloseEventArgs eventArgs) {
            StopKeepAlive();
            OnClose?.Invoke(eventArgs.Code, eventArgs.Reason);
        }

        void OnWebSocketError(object sender, ErrorEventArgs e) {
            Logger.Error(e.Message);
            ws.CloseAsync();
        }

        internal void PauseMessageQueue() {
            isMessageQueueRunning = false;
        }

        internal void ResumeMessageQueue() {
            if (messageQueue.Count > 0) {
                lock (messageQueue) { 
                    while (messageQueue.Count > 0) {
                        var msg = messageQueue.Dequeue();
                        HandleMessage(msg);
                    }
                }
            }
            isMessageQueueRunning = true;
        }

        void Ping() {
            lock (pingTokenSource) {
                if (pingTokenSource != null) {
                    pingTokenSource.Cancel();
                }
                pingTokenSource = new CancellationTokenSource();
                Task.Delay(TimeSpan.FromSeconds(GetPingDuration())).ContinueWith(t => {
                    Send(PING);
                }, pingTokenSource.Token);
            }
        }

        void Pong() { 
            lock (pongTokenSource) { 
                if (pongTokenSource != null) {
                    pongTokenSource.Cancel();
                }
                pongTokenSource = new CancellationTokenSource();
                Task.Delay(TimeSpan.FromSeconds(GetPingDuration() * 2)).ContinueWith(t => {
                    Logger.Debug("It's time for closing ws.");
                    lock (ws) {
                        try {
                            ws.Close();
                        } catch (Exception e) {
                            Logger.Error(e.Message);
                        }
                    }
                }, pongTokenSource.Token);
            }
        }

        void StopKeepAlive() { 
            if (pingTokenSource != null) {
                pingTokenSource.Cancel();
            }
            if (pongTokenSource != null) {
                pongTokenSource.Cancel();
            }
        }

        protected abstract int GetPingDuration();
    }
}
