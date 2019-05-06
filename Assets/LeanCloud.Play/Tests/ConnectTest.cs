using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Threading.Tasks;
using System.Threading;

namespace LeanCloud.Play.Test
{
    public class ConnectTest
    {
        [Test]
        public async void Connect() {
            var c = Utils.NewClient("ct0");
            await c.Connect();
            Debug.Log($"{c.UserId} connected.");
            c.Close();
        }

        [UnityTest]
        public IEnumerator ConnectWithSameId() {
            Logger.LogDelegate += Utils.Log;

            var flag = false;
            var c0 = Utils.NewClient("ct1");
            var c1 = Utils.NewClient("ct1");
            c0.OnError += (code, detail) => {
                Debug.Log($"on error at {Thread.CurrentThread.ManagedThreadId}");
                Assert.AreEqual(code, 4102);
                Debug.Log(detail);
                flag = true;
            };
            c0.Connect().OnSuccess(_ => {
                return c1.Connect();
            }).Unwrap().OnSuccess(_ => {
                Debug.Log($"{c1.UserId} connected at {Thread.CurrentThread.ManagedThreadId}");
            });

            while (!flag) {
                yield return null;
            }
            c0.Close();
            c1.Close();
        }

        [Test]
        public async void CloseFromLobby() {
            var c = Utils.NewClient("ct2");
            await c.Connect();
            c.Close();
            c = Utils.NewClient("ct2");
            await c.Connect();
            c.Close();
        }

        [Test]
        public async void CloseFromGame() {
            var c = Utils.NewClient("ct3");
            await c.Connect();
            await c.CreateRoom();
            c.Close();
            c = Utils.NewClient("ct3");
            await c.Connect();
            await c.CreateRoom();
            c.Close();
        }

        [Test]
        public async void ConnectFailed() {
            var c = Utils.NewClient("ct4 ");
            try {
                await c.Connect();
            } catch (PlayException e) {
                Assert.AreEqual(e.Code, 4104);
                Debug.Log(e.Message);
                c.Close();
            }
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator KeepAlive() {
            Logger.LogDelegate += Utils.Log;

            var f = false;
            var roomName = "ct5_r";
            var c = Utils.NewClient("ct5");

            c.Connect().OnSuccess(_ => {
                return c.CreateRoom(roomName);
            }).Unwrap().OnSuccess(_ => {
                Task.Delay(30000).OnSuccess(__ => {
                    Debug.Log("delay 30s done");
                    f = true;
                });
            });

            while (!f) {
                yield return null;
            }
            c.Close();
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator SendOnly() {
            Logger.LogDelegate += Utils.Log;

            var f = false;
            var c = Utils.NewClient("ct6");
            c.Connect().OnSuccess(_ => {
                return c.CreateRoom();
            }).Unwrap().OnSuccess(_ => {
                Task.Run(() => {
                    var count = 6;
                    while (count > 0 && !f) {
                        var options = new SendEventOptions { 
                            ReceiverGroup = ReceiverGroup.Others
                        };
                        c.SendEvent("hi", null, options);
                        Thread.Sleep(5000);
                    }
                });
                Task.Delay(30000).OnSuccess(__ => {
                    Debug.Log("delay 30s done");
                    f = true;
                });
            });

            while (!f) {
                yield return null;
            }
            c.Close();
        }
    }
}
