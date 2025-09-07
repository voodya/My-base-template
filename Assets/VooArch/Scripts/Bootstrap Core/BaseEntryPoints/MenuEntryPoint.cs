using Cysharp.Threading.Tasks;
using System.Collections.Generic;

using System.Threading;
using UniRx;
using UnityEngine;

#if HAS_VOODYA  
namespace Voodya.Core
{
    public class MenuEntryPoint : ABaseEntryPoint
    {
        private ISceneManager _sceneManager;
        private IApplicationScopesService _applicationScopesService;

        public MenuEntryPoint(
            ILoadingPanelService loadingPanelService,
            IEnumerable<IBootableAsync> bootableAsyncs,
            ISceneManager sceneManager,
            IApplicationScopesService applicationScopesService)
            : base(loadingPanelService, bootableAsyncs)
        {
            _sceneManager = sceneManager;
            _applicationScopesService = applicationScopesService;
        }

        public override async UniTask StartAsync(CancellationToken cancellation = default)
        {
            Debug.LogError("Load MenuEntryPoint");
            _loadingPanelService.Show();
            await base.StartAsync(cancellation);
            //await _sceneManager.GetScene<MenuScene>();

            _loadingPanelService.Hide();
        }

    }
}

#endif