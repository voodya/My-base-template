using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "FadeUiAnimation", menuName = "SO/Animations/Fade")]
    public class FadeUiAnimation : ABaseUIAnimation
    {
        public override async UniTask PlayHideAnimation(CanvasGroup canvasGroup, bool force, CancellationToken cancelToken, RectTransform rect = null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            CancelTweens();
            var time = _animationTime;
            //if (!TimeValidator.IsFinished)
            //    time = 0.01f;
            //canvasGroup.alpha = 1;
            if (force)
            {
                canvasGroup.alpha = 0;
            }
            else
            {

                _hideTween = canvasGroup.DOFade(0f, time);
                await CheckBuzy();
                _isHiding = true;
                await UniTask.Delay(TimeSpan.FromSeconds(time), cancellationToken: cancelToken);
                _isHiding = false;
            }
        }

        private async UniTask CheckBuzy()
        {
            await UniTask.Yield();
            //while (_controller.IsBuzy)
            //{
            //    Debug.LogError("IS BUZY");
            //    await UniTask.Yield();
            //}
        }

        public override async UniTask PlayShowAnimation(CanvasGroup canvasGroup, bool force, CancellationToken cancelToken, RectTransform rect = null)
        {
            var time = _animationTime;
            //if (!TimeValidator.IsFinished)
            //    time = 0.01f;
            CancelTweens();
            canvasGroup.alpha = 0;
            if (force)
            {
                canvasGroup.alpha = 1;
            }
            else
            {

                _showTween = canvasGroup.DOFade(1f, time);
                await CheckBuzy();
                _isShowing = true;
                await UniTask.Delay(TimeSpan.FromSeconds(time), cancellationToken: cancelToken);
                _isShowing = false;
            }
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }
}


