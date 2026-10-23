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
            string Name,
			ILoadingPanelService loadingPanelService,
            IEnumerable<IBootableAsync> bootableAsyncs,
            ISceneManager sceneManager,
            IApplicationScopesService applicationScopesService)
            : base(Name, loadingPanelService, bootableAsyncs)
        {
            _sceneManager = sceneManager;
            _applicationScopesService = applicationScopesService;
        }

        public override async UniTask StartAsync(CancellationToken cancellation = default)
        {
            VooLog.Info("Load Example State");
            _loadingPanelService.Show();
            await base.StartAsync(cancellation);
            _loadingPanelService.PrintMessage("Voodya arch example!");

            // Additional state lives on top of this one and is closed together with it
            await _applicationScopesService.TryAddScope<NewEntryPoint>();
            _loadingPanelService.Hide();
		}

    }
}

#endif