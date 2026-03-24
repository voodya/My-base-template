using System.Collections.Generic;
using UnityEngine;
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
        void Register<T>(T entryPoint) where T : IABaseState;
		UniTask<bool> TryAddScope(string scope);
		void DirectrlyCloseAdittionalScope(string scope);
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
                _currentScope = _scopesStr[scope];
                _currentScope.Enter();
                return true;
            }
            return false;
        }

		public async UniTask<bool> TryAddScope(string scope)
		{
			if (_scopesStr.ContainsKey(scope))
			{
                var additionalScope = _scopesStr[scope];
				if (_additionalScopes.ContainsKey(_currentScope))
                {
                    if(_additionalScopes[_currentScope].Contains(additionalScope))
                    {
                        Debug.LogError($"State {additionalScope.CurrentType.Name} already added to {_currentScope.CurrentType.Name}");
                        return false;
					}
                    _additionalScopes[_currentScope].Add(additionalScope);
				}
                else
                {
                    _additionalScopes.Add(_currentScope, new List<IABaseState>() { additionalScope });
                }
                additionalScope.Enter();
				return true;
			}
			return false;
		}

		public void DirectrlyCloseAdittionalScope(string scope)
		{
			var additionalScope = _scopesStr[scope];
			if (_additionalScopes.ContainsKey(_currentScope))
			{
				if (_additionalScopes[_currentScope].Contains(additionalScope))
				{
                    _additionalScopes[_currentScope].First(x => x == additionalScope).Exit();
				}
			}
		}
	}

}

#endif