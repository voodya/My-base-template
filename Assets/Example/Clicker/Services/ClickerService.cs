using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using Voodya.Core;

public interface IClickerService
{
    IReadOnlyReactiveProperty<int> Score { get; }
    IReadOnlyReactiveProperty<float> TimeLeft { get; }
    IReadOnlyReactiveProperty<bool> IsPlaying { get; }
    IObservable<RoundResult> OnRoundFinished { get; }

    void StartRound();
    void Click();
}

public readonly struct RoundResult
{
    public readonly int Score;
    public readonly bool IsRecord;

    public RoundResult(int score, bool isRecord)
    {
        Score = score;
        IsRecord = isRecord;
    }
}

/// <summary>
/// Scoped example service: created when ClickerEntryPoint state starts and disposed when it exits.
/// Uses global IProgressService to store the result of each round.
/// </summary>
[Service(typeof(ClickerServiceInstaller))]
public class ClickerService : IClickerService, IDisposable
{
    public const float ROUND_DURATION = 10f;

    private readonly IProgressService _progress;
    private readonly ReactiveProperty<int> _score = new();
    private readonly ReactiveProperty<float> _timeLeft = new(ROUND_DURATION);
    private readonly ReactiveProperty<bool> _isPlaying = new();
    private readonly Subject<RoundResult> _onRoundFinished = new();
    private CancellationTokenSource _roundCts;
    private bool _disposed;

    public IReadOnlyReactiveProperty<int> Score => _score;
    public IReadOnlyReactiveProperty<float> TimeLeft => _timeLeft;
    public IReadOnlyReactiveProperty<bool> IsPlaying => _isPlaying;
    public IObservable<RoundResult> OnRoundFinished => _onRoundFinished;

    public ClickerService(IProgressService progress)
    {
        _progress = progress;
    }

    public void StartRound()
    {
        if (_disposed || _isPlaying.Value) return;

        _score.Value = 0;
        _roundCts?.Dispose();
        _roundCts = new CancellationTokenSource();
        RunRound(_roundCts.Token).Forget();
    }

    public void Click()
    {
        if (_isPlaying.Value)
            _score.Value++;
    }

    private async UniTaskVoid RunRound(CancellationToken token)
    {
        _isPlaying.Value = true;
        float endTime = Time.realtimeSinceStartup + ROUND_DURATION;
        try
        {
            while (Time.realtimeSinceStartup < endTime)
            {
                _timeLeft.Value = endTime - Time.realtimeSinceStartup;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
            // State was closed in the middle of the round, result is discarded
            return;
        }

        _timeLeft.Value = 0f;
        _isPlaying.Value = false;
        bool isRecord = _progress.SubmitScore(_score.Value);
        _onRoundFinished.OnNext(new RoundResult(_score.Value, isRecord));
    }

    public void Dispose()
    {
        // DefaultState disposes scope disposables twice, so Dispose must be idempotent
        if (_disposed) return;
        _disposed = true;

        _roundCts?.Cancel();
        _roundCts?.Dispose();
        _onRoundFinished.OnCompleted();
        _onRoundFinished.Dispose();
        _score.Dispose();
        _timeLeft.Dispose();
        _isPlaying.Dispose();
    }
}
