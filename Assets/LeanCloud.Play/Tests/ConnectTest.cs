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
            Logger.LogDelegate += (level, info) => {
                Debug.Log(info);
            };

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
        }

        [Test]
        public async void ConnectFailed() {
            var c = Utils.NewClient("ct2 ");
            try {
                await c.Connect();
            } catch (PlayException e) {
                Assert.AreEqual(e.Code, 4104);
                Debug.Log(e.Message);
            }
        }
    }
}
