using System.Collections.Generic;
#if HAS_VOODYA
using Cysharp.Threading.Tasks;
#endif
using System.Threading;
using UnityEngine;

#if HAS_VOODYA
namespace Voodya.Core
{
    [EntryPoint(0)]
    public class MenuEntryPointExample : ABaseEntryPoint
    {
        private ISceneManager _sceneManager;
        private IApplicationScopesService _applicationScopesService;

        public MenuEntryPointExample(
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
            Debug.LogError("Load Example State");
            _loadingPanelService.Show();
            await base.StartAsync(cancellation);
            _loadingPanelService.PrintMessage("Voodya arch example!");
            //await _sceneManager.GetScene<MenuScene>();

            //_loadingPanelService.Hide();
        }

    }
}

#endif