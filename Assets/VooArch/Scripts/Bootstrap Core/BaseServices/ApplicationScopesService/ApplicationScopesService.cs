using System;
using System.Collections.Generic;
using System.Linq;

#if HAS_VOODYA
using Cysharp.Threading.Tasks;
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    public interface IApplicationScopesService
    {
        /// <summary>Type of the current main state, null before the first state is entered</summary>
        Type CurrentState { get; }

        /// <summary>True while a state is being exited or entered; new transitions are rejected</summary>
        bool IsTransitioning { get; }

        /// <summary>True while transitioning or while the main state is starting</summary>
        bool IsBusy { get; }

        IReadOnlyCollection<Type> RegisteredStates { get; }

        /// <summary>Raised when a state fails to build or start</summary>
        event Action<Type, Exception> OnStateFailed;

        void Register<T>(T state) where T : IABaseState;

        /// <summary>Exits the current state with its additional states and starts the given one.</summary>
        /// <param name="payload">Optional data available in the new state scope by its runtime type</param>
        /// <returns>True when the new state has started</returns>
        UniTask<bool> TryChangeScope(string scope, object payload = null);
        UniTask<bool> TryChangeScope<TEntryPoint>(object payload = null) where TEntryPoint : ABaseEntryPoint;

        /// <summary>Starts a state on top of the current one; it is closed together with the current state.</summary>
        UniTask<bool> TryAddScope(string scope, object payload = null);
        UniTask<bool> TryAddScope<TEntryPoint>(object payload = null) where TEntryPoint : ABaseEntryPoint;

        UniTask<bool> CloseAdditionalScope(string scope);
        UniTask<bool> CloseAdditionalScope<TEntryPoint>() where TEntryPoint : ABaseEntryPoint;

        [Obsolete("Use CloseAdditionalScope")]
        UniTask DirectrlyCloseAdittionalScope(string scope);
    }

    [Service(typeof(ApplicationScopesInstaller))]
    public class ApplicationScopesService : IApplicationScopesService
    {
        private readonly Dictionary<string, IABaseState> _states = new();
        private readonly Dictionary<IABaseState, List<IABaseState>> _additionalScopes = new();
        private IABaseState _currentScope;
        // First successfully started main state, used as fallback when another state fails
        private IABaseState _homeScope;
        private bool _isTransitioning;
        private int _startingMainStates;

        public Type CurrentState => _currentScope?.CurrentType;
        public bool IsTransitioning => _isTransitioning;
        public bool IsBusy => _isTransitioning || _startingMainStates > 0;
        public IReadOnlyCollection<Type> RegisteredStates => _states.Values.Select(s => s.CurrentType).ToList();

        public event Action<Type, Exception> OnStateFailed;

        [Inject]
        public ApplicationScopesService()
        {
        }

        public void Register<T>(T state) where T : IABaseState
        {
            string name = state.CurrentType.Name;
            if (_states.ContainsKey(name))
            {
                VooLog.Error($"State {name} already registered");
                return;
            }
            _states.Add(name, state);
        }

        public UniTask<bool> TryChangeScope<TEntryPoint>(object payload = null) where TEntryPoint : ABaseEntryPoint
            => TryChangeScope(typeof(TEntryPoint).Name, payload);

        public UniTask<bool> TryAddScope<TEntryPoint>(object payload = null) where TEntryPoint : ABaseEntryPoint
            => TryAddScope(typeof(TEntryPoint).Name, payload);

        public UniTask<bool> CloseAdditionalScope<TEntryPoint>() where TEntryPoint : ABaseEntryPoint
            => CloseAdditionalScope(typeof(TEntryPoint).Name);

        public async UniTask<bool> TryChangeScope(string scope, object payload = null)
        {
            if (!TryGetState(scope, out var next)) return false;
            if (!TryBeginTransition($"Change to {scope}")) return false;

            bool entered;
            try
            {
                if (_currentScope != null)
                {
                    var previous = _currentScope;
                    _currentScope = null;
                    await ExitAdditionalScopes(previous);
                    await SafeExit(previous);
                }
                entered = await TryEnter(next, payload);
                if (entered)
                    _currentScope = next;
            }
            finally
            {
                _isTransitioning = false;
            }

            bool started = false;
            if (entered)
            {
                // The lock is released before the start, so the starting state can open additional states
                _startingMainStates++;
                try
                {
                    started = await TryStart(next);
                }
                finally
                {
                    _startingMainStates--;
                }
            }

            if (started)
            {
                _homeScope ??= next;
                return true;
            }

            // The previous state is already closed, so a failed state is replaced with the home one.
            // Cancelled start means another transition already replaced this state, nothing to do then
            bool failed = !entered || _currentScope == next;
            if (failed && _homeScope != null && _homeScope != next)
            {
                VooLog.Warning($"Returning to {_homeScope.CurrentType.Name} after {next.CurrentType.Name} failure");
                await TryChangeScope(_homeScope.CurrentType.Name);
            }
            return false;
        }

        public async UniTask<bool> TryAddScope(string scope, object payload = null)
        {
            if (!TryGetState(scope, out var additional)) return false;
            if (_currentScope == null)
            {
                VooLog.Error($"Can't add additional state {scope}: no current state");
                return false;
            }
            if (additional.IsActive)
            {
                VooLog.Error($"State {scope} is already active");
                return false;
            }
            if (!TryBeginTransition($"Add {scope}")) return false;

            var owner = _currentScope;
            try
            {
                if (!await TryEnter(additional, payload)) return false;
                if (!_additionalScopes.TryGetValue(owner, out var scopes))
                    _additionalScopes[owner] = scopes = new List<IABaseState>();
                scopes.Add(additional);
            }
            finally
            {
                _isTransitioning = false;
            }

            if (await TryStart(additional)) return true;

            // Failed additional state is closed, the main state keeps working
            if (RemoveAdditional(owner, additional))
                await SafeExit(additional);
            return false;
        }

        public async UniTask<bool> CloseAdditionalScope(string scope)
        {
            if (!TryGetState(scope, out var additional)) return false;
            if (_currentScope == null || !RemoveAdditional(_currentScope, additional))
            {
                VooLog.Warning($"State {scope} is not opened as additional state");
                return false;
            }
            await SafeExit(additional);
            return true;
        }

        [Obsolete("Use CloseAdditionalScope")]
        public async UniTask DirectrlyCloseAdittionalScope(string scope)
        {
            await CloseAdditionalScope(scope);
        }

        private bool TryGetState(string scope, out IABaseState state)
        {
            if (_states.TryGetValue(scope, out state)) return true;
            VooLog.Error($"State {scope} is not registered");
            return false;
        }

        private bool TryBeginTransition(string transition)
        {
            if (_isTransitioning)
            {
                VooLog.Warning($"{transition} ignored: another transition is in progress");
                return false;
            }
            _isTransitioning = true;
            return true;
        }

        private bool RemoveAdditional(IABaseState owner, IABaseState additional)
        {
            if (!_additionalScopes.TryGetValue(owner, out var scopes) || !scopes.Remove(additional))
                return false;
            if (scopes.Count == 0)
                _additionalScopes.Remove(owner);
            return true;
        }

        private async UniTask ExitAdditionalScopes(IABaseState owner)
        {
            if (!_additionalScopes.Remove(owner, out var scopes)) return;
            foreach (var scope in scopes)
                await SafeExit(scope);
        }

        private async UniTask<bool> TryEnter(IABaseState state, object payload)
        {
            try
            {
                await state.Enter(payload);
                return true;
            }
            catch (Exception e)
            {
                VooLog.Exception(e);
                await SafeExit(state);
                OnStateFailed?.Invoke(state.CurrentType, e);
                return false;
            }
        }

        private async UniTask<bool> TryStart(IABaseState state)
        {
            try
            {
                await state.Start();
                return true;
            }
            catch (OperationCanceledException)
            {
                VooLog.Info($"Start of {state.CurrentType.Name} was cancelled");
                return false;
            }
            catch (Exception e)
            {
                VooLog.Exception(e);
                OnStateFailed?.Invoke(state.CurrentType, e);
                return false;
            }
        }

        private static async UniTask SafeExit(IABaseState state)
        {
            try
            {
                await state.Exit();
            }
            catch (Exception e)
            {
                VooLog.Exception(e);
            }
        }
    }

}

#endif
