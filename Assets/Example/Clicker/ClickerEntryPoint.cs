using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using Voodya.Core;

/// <summary>
/// Example state: a 10 second clicker round.
/// Shows how a state combines a global service (IProgressService, shared by all states),
/// a scoped one (IClickerService, created and disposed together with this state)
/// and its own addressable scene, which is unloaded when the state exits.
/// </summary>
[EntryPoint(10)]
public class ClickerEntryPoint : ABaseEntryPoint
{
    private readonly ISceneManager _sceneManager;
    private readonly IApplicationScopesService _applicationScopesService;
    private readonly IClickerService _clicker;
    private readonly IProgressService _progress;

    public ClickerEntryPoint(
        string Name,
        ILoadingPanelService loadingPanelService,
        IEnumerable<IBootableAsync> bootableAsyncs,
        ISceneManager sceneManager,
        IApplicationScopesService applicationScopesService,
        IClickerService clicker,
        IProgressService progress)
        : base(Name, loadingPanelService, bootableAsyncs)
    {
        _sceneManager = sceneManager;
        _applicationScopesService = applicationScopesService;
        _clicker = clicker;
        _progress = progress;
    }

    public override async UniTask StartAsync(CancellationToken cancellation = default)
    {
        _loadingPanelService.Show();
        await base.StartAsync(cancellation);

        var scene = await _sceneManager.GetScene<ClickerScene>();
        Bind(scene);
        _loadingPanelService.Hide();
    }

    private void Bind(ClickerScene scene)
    {
        scene.SetStatus($"Click as many times as you can in {_clicker.RoundDuration:0} seconds");

        _clicker.Score.Subscribe(scene.SetScore).AddTo(_compositeDisposable);
        _clicker.TimeLeft.Subscribe(scene.SetTimeLeft).AddTo(_compositeDisposable);
        _clicker.IsPlaying.Subscribe(scene.SetPlaying).AddTo(_compositeDisposable);
        _progress.BestScore.Subscribe(scene.SetBest).AddTo(_compositeDisposable);
        _clicker.OnRoundFinished
            .Subscribe(result => scene.SetStatus(result.IsRecord
                ? $"New record: {result.Score}!"
                : $"Score: {result.Score}. Rounds played: {_progress.RoundsPlayed}"))
            .AddTo(_compositeDisposable);

        scene.OnStart.Subscribe(_ => _clicker.StartRound()).AddTo(_compositeDisposable);
        scene.OnClick.Subscribe(_ => _clicker.Click()).AddTo(_compositeDisposable);
        scene.OnBack
            .Subscribe(_ => _applicationScopesService.TryChangeScope<MenuEntryPointExample>().Forget())
            .AddTo(_compositeDisposable);
    }
}
