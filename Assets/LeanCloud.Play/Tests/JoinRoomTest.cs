using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Threading.Tasks;

namespace LeanCloud.Play.Test
{
    public class JoinRoomTest
    {
        [Test]
        public async void JoinRoomByName() {
            var roomName = "jrt0_r";
            var c0 = Utils.NewClient("jrt0_0");
            var c1 = Utils.NewClient("jrt1_0");
            await c0.Connect();
            await c0.CreateRoom(roomName);
            await c1.Connect();
            var room = await c1.JoinRoom(roomName);
            Assert.AreEqual(room.Name, roomName);
        }

        [Test]
        public async void JoinRandomRoom() {
            var c0 = Utils.NewClient("jrt1_0");
            var c1 = Utils.NewClient("jrt1_1");
            await c0.Connect();
            await c0.CreateRoom();

            await c1.Connect();
            var room = await c1.JoinRandomRoom();
            Debug.Log($"join random: {room.Name}");
        }

        [Test]
        public async void JoinWithExpectedUserIds() {
            var roomName = "jrt2_r";
            var c0 = Utils.NewClient("jrt2_0");
            var c1 = Utils.NewClient("jrt2_1");
            var c2 = Utils.NewClient("jrt2_2");
            await c0.Connect();
            var roomOptions = new RoomOptions { 
                MaxPlayerCount = 2
            };
            await c0.CreateRoom(roomName, roomOptions, new List<string> { "jrt2_2" });

            await c1.Connect();
            try {
                await c1.JoinRoom(roomName);
            } catch (PlayException e) {
                Assert.AreEqual(e.Code, 4302);
                Debug.Log(e.Detail);
            }

            await c2.Connect();
            var room = await c2.JoinRoom(roomName);
            Assert.AreEqual(room.Name, roomName);
        }

        [UnityTest]
        public IEnumerator LeaveRoom() {
            var flag = false;

            var roomName = "jrt3_r";
            var c0 = Utils.NewClient("jrt3_0");
            var c1 = Utils.NewClient("jrt3_1");

            c0.Connect().OnSuccess(_ => {
                var roomOptions = new RoomOptions {
                    PlayerTtl = 600
                };
                return c0.CreateRoom(roomName, roomOptions);
            }).Unwrap().OnSuccess(_ => {
                c0.OnPlayerActivityChanged += player => {
                    Assert.AreEqual(player.IsActive, false);
                    flag = true;
                };
                return c1.Connect();
            }).Unwrap().OnSuccess(_ => {
                return c1.JoinRoom(roomName);
            }).Unwrap().OnSuccess(_ => {
                Debug.Log($"{c1.UserId} joined room");
                c1._Disconnet();
            });

            while (!flag) {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RejoinRoom() {
            var roomName = "jrt4_r";
            var c0 = Utils.NewClient("jrt4_0");
            var c1 = Utils.NewClient("jrt4_1");

            c0.Connect().OnSuccess(_ => {
                var roomOptions = new RoomOptions {
                    PlayerTtl = 600
                };
                return c0.CreateRoom(roomName, roomOptions);
            });

            yield return null;
        }

        [Test]
        public async void JoinRoomFailed() {
            var roomName = "jrt6_r";
            var c = Utils.NewClient("jrt6");

            await c.Connect();
            try {
                await c.JoinRoom(roomName);
            } catch (PlayException e) {
                Assert.AreEqual(e.Code, 4301);
                Debug.Log(e.Detail);
            }
        }

        [Test]
        public async void JoinRandomWithMatchProperties() {
            var roomName = "jrt7_r";
            var c0 = Utils.NewClient("jrt7_0");
            var c1 = Utils.NewClient("jrt7_1");
            var c2 = Utils.NewClient("jrt7_2");

            await c0.Connect();
            var roomOptions = new RoomOptions {
                CustomRoomProperties = new Dictionary<string, object> {
                    { "lv", 2 }
                },
                CustoRoomPropertyKeysForLobby = new List<string> { "lv" }
            };
            await c0.CreateRoom(roomName, roomOptions);

            await c1.Connect();
            await c1.JoinRandomRoom(new Dictionary<string, object> {
                { "lv", 2 }
            });

            await c2.Connect();
            try {
                await c2.JoinRandomRoom(new Dictionary<string, object> {
                    { "lv", 3 }
                });
            } catch (PlayException e) {
                Assert.AreEqual(e.Code, 4301);
                Debug.Log(e.Detail);
            }
        }

        [Test]
        public async void MatchRandom() {
            var roomName = "jr8_r";
            var c0 = Utils.NewClient("jr8_0");
            var c1 = Utils.NewClient("jr8_1");

            await c0.Connect();
            var roomOptions = new RoomOptions {
                CustomRoomProperties = new Dictionary<string, object> {
                    { "lv", 5 }
                },
                CustoRoomPropertyKeysForLobby = new List<string> { "lv" }
            };
            await c0.CreateRoom(roomName, roomOptions);

            await c1.Connect();
            var lobbyRoom = await c1.MatchRandom(new Dictionary<string, object> {
                { "lv", 5 }
            });
            Assert.AreEqual(lobbyRoom.RoomName, roomName);
            await c1.JoinRoom(lobbyRoom.RoomName);
        }

        [Test]
        public async void TestMatch() {
            var roomName = "jr9_r";
            var c0 = Utils.NewClient("jr9_0");
            var c1 = Utils.NewClient("jr9_1");

            await c0.Connect();
            await c0.CreateRoom(roomName, expectedUserIds: new List<string> { "jr9_1" });
            await c1.Connect();
            await c1.JoinRoom(roomName, new List<string> { "jr9_0" });
        }
    }
}
