using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
#if HAS_VOODYA
using Cysharp.Threading.Tasks;
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    public class BootstrapEntryPoint : ABaseEntryPoint
    {
        private IApplicationScopesService _stateMachineService;
        private IObjectResolver _objectResolver;

        [Inject]
        public BootstrapEntryPoint(
            ILoadingPanelService loadingPanelService,
            IEnumerable<IBootableAsync> bootableAsyncs,
            IApplicationScopesService stateMachineService,
            IObjectResolver objectResolver)
            : base(loadingPanelService, bootableAsyncs)
        {
            _stateMachineService = stateMachineService;
            _objectResolver = objectResolver;
        }

        public override async UniTask StartAsync(CancellationToken cancellation = default)
        {
            await _loadingPanelService.Boot();
            _loadingPanelService.Show();

            //await UniTask.Delay(2000); //fake delay


            //add states
            _stateMachineService.Register(new DefaultState<MenuEntryPoint>(_objectResolver));

            await base.StartAsync(cancellation);

            if (!await _stateMachineService.TryChangeScope(typeof(MenuEntryPoint).Name))
            {
                throw new NotImplementedException("Somthing wrong in MenuEntryPoint");
            }
            else
            {
                Debug.Log("Load MenuEntryPoint");
                _loadingPanelService.Hide();
            }

        }
    }
}

#endif