using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LeanCloud.Play.Test
{
    public class LogTest
    {
        [Test]
        public void LogError() {
            //Debug.LogError("log error");
            //Debug.Log("log debug");
            Debug.LogWarning("log warning");
        }
    }
}
