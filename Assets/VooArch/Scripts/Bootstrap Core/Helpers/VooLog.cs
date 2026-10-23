using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

#if HAS_VOODYA
namespace Voodya.Core
{
    /// <summary>
    /// Framework logger. Info messages are kept only in the editor and development builds
    /// (define VOOARCH_LOGS to keep them in release), warnings and errors are always logged.
    /// </summary>
    public static class VooLog
    {
        private const string PREFIX = "[VooArch] ";

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("VOOARCH_LOGS")]
        public static void Info(string message) => Debug.Log(PREFIX + message);

        public static void Warning(string message) => Debug.LogWarning(PREFIX + message);

        public static void Error(string message) => Debug.LogError(PREFIX + message);

        public static void Exception(Exception exception) => Debug.LogException(exception);
    }
}
#endif
