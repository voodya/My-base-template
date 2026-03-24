using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Voodya.Core;
using UniRx;

[EntryPoint]
public class NewEntryPoint : ABaseEntryPoint
{
	private readonly ISceneManager _sceneManager;
	private readonly IApplicationScopesService _applicationScopesService;
    public NewEntryPoint(
		string Name,
		ILoadingPanelService loadingPanelService,
		IEnumerable<IBootableAsync> bootableAsyncs,
		ISceneManager sceneManager,
		IApplicationScopesService applicationScopesService
		) : base(Name, loadingPanelService, bootableAsyncs)
    {
		_sceneManager = sceneManager;
		_applicationScopesService = applicationScopesService;

	}

	public override async UniTask StartAsync(CancellationToken cancellation = default)
	{
		_loadingPanelService.Show();
		await base.StartAsync(cancellation);
		_loadingPanelService.PrintMessage("Additional state!");

		await _sceneManager.GetSceneWithAfterAction<NewScene>(scene => 
		{
			// Do something with scene
			scene.OnClose.Subscribe(_ =>
			{
				_applicationScopesService.DirectrlyCloseAdittionalScope(_name);
				Debug.Log("Close additional state");
			});

		});
	}

}
