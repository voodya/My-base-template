using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Voodya.Core
{
    public abstract class ABaseEntryPoint : IAsyncStartable, IDisposable
    {
        protected IEnumerable<IBootableAsync> _bootablesAsync;
        protected ILoadingPanelService _loadingPanelService;
        protected CompositeDisposable _compositeDisposable;

        [Inject]
        public ABaseEntryPoint(
            ILoadingPanelService loadingPanelService,
            IEnumerable<IBootableAsync> bootableAsyncs)
        {
            _bootablesAsync = bootableAsyncs;
            _loadingPanelService = loadingPanelService;
        }

        public void Dispose()
        {
            _compositeDisposable?.Dispose();
        }

        public virtual async UniTask StartAsync(CancellationToken cancellation = default)
        {

            _compositeDisposable = new CompositeDisposable();
            foreach (var item in _bootablesAsync)
            {
                try
                {
                    _loadingPanelService.PrintMessage($"Load {item.GetType()}");
                    await item.Boot();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Somthing wrong in {item.GetType()} {e}");
                }
            }
        }
    }

}

