using System;
using System.Collections.Generic;
using UnityEngine;

#if HAS_VOODYA
using Cysharp.Threading.Tasks;
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    public interface IApplicationScopesService
    {
        void Register<T>(T entryPoint) where T : IABaseState;
		UniTask<bool> TryAddScope(string scope);
		UniTask DirectrlyCloseAdittionalScope(string scope);
		UniTask<bool> TryChangeScope(string scope);
    }

    [Service(typeof(ApplicationScopesInstaller))]
    public class ApplicationScopesService : IApplicationScopesService
    {
        private readonly Dictionary<string, IABaseState> _scopesStr;
        private IABaseState _currentScope;
        private Dictionary<IABaseState, List<IABaseState>> _additionalScopes = new();

		[Inject]
        public ApplicationScopesService(IObjectResolver objectResolver)
        {
            _scopesStr = new Dictionary<string, IABaseState>();
			_additionalScopes = new Dictionary<IABaseState, List<IABaseState>>();

		}


        public void Register<T>(T entryPoint) where T : IABaseState
        {
            if (!_scopesStr.ContainsKey(entryPoint.CurrentType.Name))
            {
                _scopesStr.Add(entryPoint.CurrentType.Name, entryPoint);
            }
            else
                Debug.LogError($"State {entryPoint.CurrentType.Name} already register");
        }

        public async UniTask<bool> TryChangeScope(string scope)
        {
            if (_scopesStr.ContainsKey(scope))
            {
                if (_currentScope != null)
                {
                    if(_additionalScopes.ContainsKey(_currentScope))  
                    {
                        foreach (var item in _additionalScopes[_currentScope])
                        {
                            await item.Exit();
                        }
                        _additionalScopes.Remove(_currentScope);
					}
					await _currentScope.Exit();
                }
                _currentScope = null;
                var newScope = _scopesStr[scope];
                if (!TryEnter(newScope))
                    return false;
                _currentScope = newScope;
                return true;
            }
            return false;
        }

        private bool TryEnter(IABaseState state)
        {
            try
            {
                state.Enter();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

		public async UniTask<bool> TryAddScope(string scope)
		{
			if (_currentScope == null)
			{
				Debug.LogError($"Can't add additional state {scope}: no current state");
				return false;
			}
			if (_scopesStr.ContainsKey(scope))
			{
                var additionalScope = _scopesStr[scope];
				if (_additionalScopes.TryGetValue(_currentScope, out var scopes) && scopes.Contains(additionalScope))
                {
                    Debug.LogError($"State {additionalScope.CurrentType.Name} already added to {_currentScope.CurrentType.Name}");
                    return false;
				}
                if (!TryEnter(additionalScope))
                    return false;

                if (scopes == null)
                {
                    scopes = new List<IABaseState>();
                    _additionalScopes.Add(_currentScope, scopes);
                }
                scopes.Add(additionalScope);
				return true;
			}
			return false;
		}

		public async UniTask DirectrlyCloseAdittionalScope(string scope)
		{
			if (_currentScope == null || !_scopesStr.TryGetValue(scope, out var additionalScope))
			{
				Debug.LogError($"State {scope} not registered or no current state");
				return;
			}
			if (!_additionalScopes.TryGetValue(_currentScope, out var scopes) || !scopes.Remove(additionalScope))
			{
				Debug.LogError($"State {scope} is not added to {_currentScope.CurrentType.Name}");
				return;
			}
			if (scopes.Count == 0)
				_additionalScopes.Remove(_currentScope);

			await additionalScope.Exit();
		}
	}

}

#endif