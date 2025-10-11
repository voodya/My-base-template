using System;
using System.Threading;
using UniRx;
using UnityEngine;
#if HAS_VOODYA  
using Cysharp.Threading.Tasks;

namespace Voodya.Core
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class ABaseScene : MonoBehaviour, IDisposable
    {
        [SerializeField] protected CanvasGroup _canvasGroup;
        [SerializeField] protected ABaseUIAnimation _animation;
        [SerializeField] protected RectTransform _customAnimRect;

        private CancellationTokenSource _cancellationTokenSource;
        private CompositeDisposable _onceDisposables;
        private ABaseUIAnimation _instanceAnim;

        private void OnValidate()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
        }

        public ABaseUIAnimation Animation => _animation;
        public GameObject DisposeObject => gameObject;
        public CompositeDisposable OnceSubscribe => _onceDisposables;

        private void ValidateAnim()
        {
            if (_instanceAnim == null)
            {
                _instanceAnim = (ABaseUIAnimation)ScriptableObject.CreateInstance(_animation.GetType());
                _instanceAnim._animationTime = _animation._animationTime;
                _instanceAnim._direction = _animation._direction;
            }
        }

        public void SetDefaultState()
        {
            ValidateAnim();
            _instanceAnim.SetDefaultState(_canvasGroup, _customAnimRect);
        }

        public void SetAnimationController(SyncAnimationController syncAnimation)
        {
            ValidateAnim();
            _instanceAnim.SetAnimationController(syncAnimation);
        }

        public virtual async UniTask Hide(bool force = false)
        {
            ValidateAnim();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            await _instanceAnim.PlayHideAnimation(_canvasGroup, force, _cancellationTokenSource.Token, _customAnimRect);
            _onceDisposables?.Dispose();
            _onceDisposables = new CompositeDisposable();
        }

        public virtual async UniTask Show(bool force = false)
        {
            ValidateAnim();
            _onceDisposables?.Dispose();
            _onceDisposables = new CompositeDisposable();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            await _instanceAnim.PlayShowAnimation(_canvasGroup, force, _cancellationTokenSource.Token, _customAnimRect);
        }

        public virtual void Lock(bool isLock)
        {
            _canvasGroup.blocksRaycasts = !isLock;
        }

        public virtual void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _onceDisposables?.Dispose();
        }
    }
}


#endif