using System;
using UniRx;
using UnityEngine;
#if HAS_VOODYA
using VContainer.Unity;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    /// <summary>
    /// Application pause, focus and quit events for services, which are not MonoBehaviours.
    /// Use OnPause to save data on mobile: an app in background can be killed without OnQuit.
    /// </summary>
    public interface IApplicationLifecycle
    {
        /// <summary>True when the app goes to background, false when it returns</summary>
        IObservable<bool> OnPause { get; }
        IObservable<bool> OnFocus { get; }
        /// <summary>Raised before the app quits (not guaranteed on mobile)</summary>
        IObservable<Unit> OnQuit { get; }
        bool IsPaused { get; }
    }

    [Service(typeof(ApplicationLifecycleInstaller))]
    public class ApplicationLifecycleService : IApplicationLifecycle, IInitializable, IDisposable
    {
        private readonly Subject<bool> _onPause = new();
        private readonly Subject<bool> _onFocus = new();
        private readonly Subject<Unit> _onQuit = new();
        private LifecycleListener _listener;

        public IObservable<bool> OnPause => _onPause;
        public IObservable<bool> OnFocus => _onFocus;
        public IObservable<Unit> OnQuit => _onQuit;
        public bool IsPaused { get; private set; }

        // Registered as entry point, so the listener exists from the start even if nobody resolved the service yet
        public void Initialize()
        {
            if (_listener != null) return;
            var gameObject = new GameObject("[VooArch] Application Lifecycle");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            _listener = gameObject.AddComponent<LifecycleListener>();
            _listener.Paused += paused =>
            {
                IsPaused = paused;
                _onPause.OnNext(paused);
            };
            _listener.Focused += focused => _onFocus.OnNext(focused);
            _listener.Quitting += () => _onQuit.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            if (_listener != null)
                UnityEngine.Object.Destroy(_listener.gameObject);
            _listener = null;
            _onPause.OnCompleted();
            _onFocus.OnCompleted();
            _onQuit.OnCompleted();
        }

        private class LifecycleListener : MonoBehaviour
        {
            public event Action<bool> Paused;
            public event Action<bool> Focused;
            public event Action Quitting;

            private void OnApplicationPause(bool pauseStatus) => Paused?.Invoke(pauseStatus);
            private void OnApplicationFocus(bool hasFocus) => Focused?.Invoke(hasFocus);
            private void OnApplicationQuit() => Quitting?.Invoke();
        }
    }
}
#endif
