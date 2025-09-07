using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
#if HAS_VOODYA  
using Cysharp.Threading.Tasks;


namespace Voodya.Core
{
    [CreateAssetMenu(fileName = "SlideUiAnimation", menuName = "SO/Animations/Slide")]
    public class TopScrollAnimation : ABaseUIAnimation
    {


        public override void SetDefaultState(CanvasGroup canvasGroup, RectTransform rectTransform = null)
        {
            rectTransform.anchoredPosition = new Vector2(0f, Screen.height * _direction);
        }

        public async override UniTask PlayHideAnimation(CanvasGroup canvasGroup, bool force, CancellationToken cancelToken, RectTransform rect = null)
        {
            CancelTweens();
            if (rect != null)
            {
                //rect.anchoredPosition = Vector2.zero;
                _hideTween = rect.DOAnchorPos(new Vector2(0f, Screen.height * _direction), _animationTime);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(_animationTime), cancellationToken: cancelToken);
        }

        public async override UniTask PlayShowAnimation(CanvasGroup canvasGroup, bool force, CancellationToken cancelToken, RectTransform rect = null)
        {
            CancelTweens();
            if (rect != null)
            {
                //rect.anchoredPosition = new Vector2(0f, Screen.height * _direction);
                _showTween = rect.DOAnchorPos(Vector2.zero, _animationTime);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(_animationTime), cancellationToken: cancelToken);
        }
    }
}

#endif