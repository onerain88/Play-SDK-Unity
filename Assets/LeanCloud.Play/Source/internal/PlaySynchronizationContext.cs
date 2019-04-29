using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LeanCloud.Play {
    internal class PlaySynchronizationContext: SynchronizationContext {
        readonly List<WorkRequest> asyncWorkQueue;
        readonly object lockObj = new object();
        readonly List<WorkRequest> currentWorkQueue;

        readonly AutoResetEvent autoReset;

        internal PlaySynchronizationContext() {
            asyncWorkQueue = new List<WorkRequest>();
            currentWorkQueue = new List<WorkRequest>();
            autoReset = new AutoResetEvent(false);
            Task.Run(() => {
                while (true) {
                    lock (lockObj) {
                        currentWorkQueue.AddRange(asyncWorkQueue);
                        asyncWorkQueue.Clear();
                    }
                    Logger.Debug($"invoke count: {currentWorkQueue.Count}");
                    foreach (var work in currentWorkQueue) {
                        work.Invoke();
                    }
                    currentWorkQueue.Clear();
                    //autoReset.WaitOne();
                    Thread.Sleep(100);
                }
            });
        }

        public override void Post(SendOrPostCallback d, object state) {
            lock (lockObj) {
                if (state != null)
                    Logger.Debug($"post: {state.ToString()}");
                try {
                    Logger.Debug($"before add work request: {asyncWorkQueue.Count}");
                    asyncWorkQueue.Add(new WorkRequest(d, state));
                    Logger.Debug($"add work request: {asyncWorkQueue.Count}");
                } catch (Exception e) {
                    Logger.Error(e.Message);
                }
            }
            //autoReset.Set();
        }

        struct WorkRequest {
            readonly SendOrPostCallback callback;
            readonly object state;
            readonly ManualResetEvent waitHandle;

            internal WorkRequest(SendOrPostCallback callback, object state, ManualResetEvent waitHandle = null) {
                Logger.Debug("new WorkRequest");
                this.callback = callback;
                this.state = state;
                this.waitHandle = waitHandle;
            }

            internal void Invoke() {
                try {
                    if (state == null) {
                        Logger.Debug("invoke");
                    } else {
                        Logger.Debug($"invoke: {state.ToString()}");
                    }
                    callback.Invoke(state);
                } catch (Exception e) {
                    Logger.Error(e.Message);
                }
                if (waitHandle != null) {
                    waitHandle.Set();
                }
            }
        }
    }
}
