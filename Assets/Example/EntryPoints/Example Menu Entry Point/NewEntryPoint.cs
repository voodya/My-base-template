using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
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
				_applicationScopesService.CloseAdditionalScope<NewEntryPoint>().Forget();
				VooLog.Info("Close additional state");
			}).AddTo(_compositeDisposable);

			// Switch main state: this additional state and the menu are closed, clicker state starts.
			// Round settings come from ClickerServiceInstaller; to override them for one transition pass a payload:
			// TryChangeScope<ClickerEntryPoint>(new ClickerSettings { RoundDuration = 5f })
			scene.OnOpenClicker
				.Subscribe(_ => _applicationScopesService.TryChangeScope<ClickerEntryPoint>().Forget())
				.AddTo(_compositeDisposable);
		});
	}

}
