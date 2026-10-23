#if HAS_VOODYA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Voodya.Core;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Voodya.Core.Tests
{
    /// <summary>
    /// Boots the app from the Bootstrap scene and enters every registered state.
    /// Fails on any error or exception logged on the way.
    /// </summary>
    public class BootstrapSmokeTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        [UnityTest]
        public IEnumerator Bootstrap_StartsAndEntersEveryState() => UniTask.ToCoroutine(async () =>
        {
#if UNITY_EDITOR
            string path = AssetDatabase.FindAssets("Bootstrap t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "Bootstrap");
            if (path == null)
                Assert.Ignore("No Bootstrap scene in the project");
            await EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single)).ToUniTask();
#else
            await SceneManager.LoadSceneAsync("Bootstrap").ToUniTask();
#endif

            var root = UnityEngine.Object.FindFirstObjectByType<BootstrapInstaller>();
            Assert.IsNotNull(root, "BootstrapInstaller not found in Bootstrap scene");
            var states = root.Container.Resolve<IApplicationScopesService>();

            var failures = new List<string>();
            states.OnStateFailed += (type, exception) => failures.Add($"{type.Name}: {exception.Message}");

            await UniTask.WaitUntil(() => states.CurrentState != null && !states.IsBusy).Timeout(Timeout);
            CollectionAssert.IsEmpty(failures, "First state failed");

            foreach (var state in states.RegisteredStates.ToList())
            {
                bool started = await states.TryChangeScope(state.Name).Timeout(Timeout);
                Assert.IsTrue(started, $"{state.Name} has not started");
                Assert.AreEqual(state, states.CurrentState);
            }
            CollectionAssert.IsEmpty(failures);
        });
    }
}
#endif
