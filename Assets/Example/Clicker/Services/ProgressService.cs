using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using Voodya.Core;

public interface IProgressService
{
    IReadOnlyReactiveProperty<int> BestScore { get; }
    int RoundsPlayed { get; }

    /// <returns>true if score is a new record</returns>
    bool SubmitScore(int score);
}

/// <summary>
/// Global example service: keeps player progress between states and app launches.
/// Lives in Global scope, so every state receives the same instance.
/// Loaded once on bootstrap as IBootableAsync.
/// </summary>
[Service(typeof(ProgressServiceInstaller))]
public class ProgressService : IProgressService, IBootableAsync
{
    private const string BEST_SCORE_KEY = "Example.Clicker.BestScore";
    private const string ROUNDS_KEY = "Example.Clicker.Rounds";

    private readonly ReactiveProperty<int> _bestScore = new();

    public IReadOnlyReactiveProperty<int> BestScore => _bestScore;
    public int RoundsPlayed { get; private set; }

    public bool IsBooted { get; set; }
    public int Priority { get; set; } = 10;

    public UniTask Boot()
    {
        _bestScore.Value = PlayerPrefs.GetInt(BEST_SCORE_KEY, 0);
        RoundsPlayed = PlayerPrefs.GetInt(ROUNDS_KEY, 0);
        return UniTask.CompletedTask;
    }

    public bool SubmitScore(int score)
    {
        RoundsPlayed++;
        bool isRecord = score > _bestScore.Value;
        if (isRecord)
            _bestScore.Value = score;

        PlayerPrefs.SetInt(BEST_SCORE_KEY, _bestScore.Value);
        PlayerPrefs.SetInt(ROUNDS_KEY, RoundsPlayed);
        PlayerPrefs.Save();
        return isRecord;
    }
}
