using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Voodya.Core;

public class NewScene : ABaseScene
{
    [SerializeField] private Button _close;
    [SerializeField] private Button _openClicker;

    public IObservable<Unit> OnClose => _close.OnClickAsObservable();
    public IObservable<Unit> OnOpenClicker => _openClicker.OnClickAsObservable();
}
