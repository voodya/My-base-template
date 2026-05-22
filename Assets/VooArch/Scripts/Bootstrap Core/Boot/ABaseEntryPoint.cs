using System;
using System.Collections.Generic;
using System.Threading;
using UniRx;
using UnityEngine;
using System.Linq;

#if HAS_VOODYA
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
 
namespace Voodya.Core
{
    public abstract class ABaseEntryPoint : IAsyncStartable, IDisposable
    {
        protected IEnumerable<IBootableAsync> _bootablesAsync;
        protected ILoadingPanelService _loadingPanelService;
        protected CompositeDisposable _compositeDisposable;
        protected string _name;

		[Inject]
        public ABaseEntryPoint(
            string Name,
            ILoadingPanelService loadingPanelService,
            IEnumerable<IBootableAsync> bootableAsyncs)
        {
            _bootablesAsync = bootableAsyncs;
            _loadingPanelService = loadingPanelService;
			_name = Name;

		}

        public void Dispose()
        {
            _compositeDisposable?.Dispose();
        }

        public virtual async UniTask StartAsync(CancellationToken cancellation = default)
        {
            _compositeDisposable = new CompositeDisposable();
			_bootablesAsync = _bootablesAsync.OrderBy(x => x.Priority);

			foreach (var item in _bootablesAsync)
            {
                if (item.IsBooted) continue;
                try
                {
                    _loadingPanelService.PrintMessage($"Load {item.GetType()}");
                    await item.Boot();
                    item.IsBooted = true;

				}
                catch (Exception e)
                {
                    Debug.LogError($"Somthing wrong in {item.GetType()} {e}");
                }
            }
        }
    }

}

#endif