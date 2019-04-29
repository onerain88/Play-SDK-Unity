﻿using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Threading;
using System.Threading.Tasks;

namespace LeanCloud.Play.Test
{
    public class CreateRoomTest {
        [Test]
        public async void CreateNullNameRoom() {
            var c = Utils.NewClient("crt0");
            await c.Connect();
            var room = await c.CreateRoom();
            Debug.Log(room.Name);
        }

        [Test]
        public async void CreateSimpleRoom() {
            var roomName = "crt1_r";
            var c = Utils.NewClient("crt1");
            await c.Connect();
            var room = await c.CreateRoom(roomName);
            Assert.AreEqual(room.Name, roomName);
        }

        [Test]
        public async void CreateCustomRoom() {
            var roomName = "crt2_r";
            var roomTitle = "LeanCloud Room";
            var c = Utils.NewClient(roomName);
            await c.Connect();
            var roomOptions = new RoomOptions {
                Visible = false,
                EmptyRoomTtl = 10000,
                MaxPlayerCount = 2,
                PlayerTtl = 600,
                CustomRoomProperties = new Dictionary<string, object> {
                    { "title", roomTitle },
                    { "level", 2 },
                },
                CustoRoomPropertyKeysForLobby = new List<string> { "level" }
            };
            var expectedUserIds = new List<string> { "world" };
            var room = await c.CreateRoom(roomName, roomOptions, expectedUserIds);
            Assert.AreEqual(room.Name, roomName);
            Assert.AreEqual(room.Visible, false);
            var props = room.CustomProperties;
            Assert.AreEqual(props["title"].ToString(), roomTitle);
            Assert.AreEqual(int.Parse(props["level"].ToString()), 2);
        }

        [UnityTest]
        public IEnumerator MasterAndLocal() {
            var flag = false;
            var roomName = "crt3_r";
            var c0 = Utils.NewClient("crt3_0");
            var c1 = Utils.NewClient("crt3_1");
            c0.Connect().OnSuccess(_ => {
                return c0.CreateRoom(roomName);
            }).Unwrap().OnSuccess(_ => {
                c0.OnPlayerRoomJoined += (newPlayer) => {
                    Assert.AreEqual(c0.Player.IsMaster, true);
                    Assert.AreEqual(c0.Player.IsLocal, true);
                    Debug.Log($"new player joined at {Thread.CurrentThread.ManagedThreadId}");
                    flag = true;
                };
                return c1.Connect();
            }).Unwrap().OnSuccess(_ => {
                return c1.JoinRoom(roomName);
            }).Unwrap().OnSuccess(_ => {
                Assert.AreEqual(c1.Player.IsLocal, true);
            });

            while (!flag) {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator OpenAndVisible() {
            var flag = false;
            var c = Utils.NewClient("crt4");
            Room room = null;
            c.Connect().OnSuccess(_ => {
                return c.CreateRoom();
            }).Unwrap().OnSuccess(t => {
                room = t.Result;
                c.OnRoomOpenChanged += opened => {
                    Assert.AreEqual(opened, false);
                    Debug.Log($"opened: {opened} at {Thread.CurrentThread.ManagedThreadId}");
                };
                c.OnRoomVisibleChanged += visible => {
                    Assert.AreEqual(visible, false);
                    Debug.Log($"visible: {visible} at {Thread.CurrentThread.ManagedThreadId}");
                    flag = true;
                };
                room.SetOpened(false);
                room.SetVisible(false);
            });
            while (!flag) {
                yield return null;
            }
        }
    }
}
