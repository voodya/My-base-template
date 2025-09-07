using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Voodya.Core
{
    public abstract class ABaseUIAnimation : ScriptableObject
    {
        [SerializeField] public float _animationTime;
        [SerializeField] public float _direction = -2;
        protected Tween _showTween;
        protected Tween _hideTween;
        protected SyncAnimationController _controller;

        protected bool _isHiding;
        protected bool _isShowing;


        public bool IsTweened => ValidateTweened();

        private bool ValidateTweened()
        {
            if (_isHiding)
                return _hideTween.active;
            if (_isShowing)
                return _showTween.active;
            return false;
        }

        public abstract UniTask PlayHideAnimation(CanvasGroup canvasGroup, bool force, CancellationToken cancelToken, RectTransform rect = null);
        public abstract UniTask PlayShowAnimation(CanvasGroup canvasGroup, bool force, CancellationToken cancelToken, RectTransform rect = null);
        public virtual void SetDefaultState(CanvasGroup canvasGroup, RectTransform rectTransform = null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        public void CancelTweens()
        {
            _showTween?.Kill(true);
            _hideTween?.Kill(true);
        }

        internal void SetAnimationController(SyncAnimationController syncAnimation)
        {
            _controller = syncAnimation;
        }
    }
}


