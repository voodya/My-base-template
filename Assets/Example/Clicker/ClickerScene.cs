using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Voodya.Core;

public class ClickerScene : ABaseScene
{
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _timerText;
    [SerializeField] private TextMeshProUGUI _bestText;
    [SerializeField] private Button _clickButton;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _backButton;

    public IObservable<Unit> OnClick => _clickButton.OnClickAsObservable();
    public IObservable<Unit> OnStart => _startButton.OnClickAsObservable();
    public IObservable<Unit> OnBack => _backButton.OnClickAsObservable();

    public void SetStatus(string text) => _statusText.text = text;
    public void SetScore(int score) => _scoreText.text = score.ToString();
    public void SetTimeLeft(float seconds) => _timerText.text = $"{seconds:0.0}s";
    public void SetBest(int best) => _bestText.text = $"Best: {best}";

    public void SetPlaying(bool isPlaying)
    {
        _clickButton.interactable = isPlaying;
        _startButton.gameObject.SetActive(!isPlaying);
        _backButton.interactable = !isPlaying;
    }
}
