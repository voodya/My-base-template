using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Voodya.Core;

public class NewScene : ABaseScene
{
    [SerializeField] private Button _close;

    public IObservable<Unit> OnClose => _close.OnClickAsObservable();
}
