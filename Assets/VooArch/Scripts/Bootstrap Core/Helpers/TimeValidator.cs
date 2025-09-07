using System;
using UnityEngine;

namespace Voodya.Core
{
    public static class TimeValidator
    {
        private static TimeSpan _delta;
        private static DateTime _satrtTime;
        public static bool IsFinished;

        [RuntimeInitializeOnLoadMethod]
        public static void Start()
        {
            _delta = TimeSpan.Zero;
            _satrtTime = DateTime.Now;
        }

        public static void Finish()
        {
            IsFinished = true;
            _delta = DateTime.Now - _satrtTime;
            Debug.LogError($"Booted at {_delta.TotalMilliseconds}");
        }
    }
}


