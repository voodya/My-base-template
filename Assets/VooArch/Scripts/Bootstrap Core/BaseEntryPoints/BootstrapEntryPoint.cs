using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#if HAS_VOODYA
using Voodya.Core;
using Cysharp.Threading.Tasks;
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    [EntryPoint(true)]
    public class BootstrapEntryPoint : ABaseEntryPoint
    {
        private IApplicationScopesService _stateMachineService;
        private IObjectResolver _objectResolver;
        private ScriptableStates _scriptableStates;

        [Inject]
        public BootstrapEntryPoint(
            ILoadingPanelService loadingPanelService,
            IEnumerable<IBootableAsync> bootableAsyncs,
            IApplicationScopesService stateMachineService,
            IObjectResolver objectResolver,
            ScriptableStates scriptableStates)
            : base(loadingPanelService, bootableAsyncs)
        {
            _stateMachineService = stateMachineService;
            _objectResolver = objectResolver;
            _scriptableStates = scriptableStates;
        }

        public override async UniTask StartAsync(CancellationToken cancellation = default)
        {
            await _loadingPanelService.Boot();
            _loadingPanelService.Show();
            _scriptableStates.OnRegister(_stateMachineService, _objectResolver);

            await base.StartAsync(cancellation);

            if (!await _stateMachineService.TryChangeScope(_scriptableStates.FirstState.Name))
            {
                throw new NotImplementedException($"Somthing wrong in {_scriptableStates.FirstState.Name}");
            }
            else
            {
                Debug.Log($"Load {_scriptableStates.FirstState.Name}");
                _loadingPanelService.Hide();
            }

        }
    }
}
/// <summary>
/// Class for register project entry points (EP)
/// </summary>
public abstract class ScriptableStates : ScriptableObject
{
    protected Type _firstStateType;
    public Type FirstState => _firstStateType;

    /// <summary>
    /// Action method for declarete your entry points (EP)
    /// Require add first EP to _firstStateType (ex. _firstStateType = typeof(YOUR_ENTRY_POINT))
    /// </summary>
    /// <param name="applicationScopesService"> Use this to register EP (ex. applicationScopesService.Register(new DefaultState<YOUR_ENTRY_POINT>(objectResolver)))</param>
    /// <param name="objectResolver"></param>
    public abstract void OnRegister(IApplicationScopesService applicationScopesService, IObjectResolver objectResolver);
}


#endif