using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

#if HAS_VOODYA  
namespace Voodya.Core
{
    public interface IApplicationScopesService
    {
        void Register<T>(T entryPoint) where T : IABaseState;
        UniTask<bool> TryChangeScope(string scope);
    }

    [Service(typeof(ApplicationScopesInstaller))]
    public class ApplicationScopesService : IApplicationScopesService
    {
        private readonly Dictionary<string, IABaseState> _scopesStr;
        private IABaseState _currentScope;

        [Inject]
        public ApplicationScopesService(IObjectResolver objectResolver)
        {
            _scopesStr = new Dictionary<string, IABaseState>();
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
                    await _currentScope.Exit();
                _currentScope = _scopesStr[scope];
                _currentScope.Enter();
                return true;
            }
            return false;
        }
    }

}

#endif