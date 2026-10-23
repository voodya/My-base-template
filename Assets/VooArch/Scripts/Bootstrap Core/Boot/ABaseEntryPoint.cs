using System;
using System.Collections.Generic;
using System.Threading;
using UniRx;
using System.Linq;

#if HAS_VOODYA
using Cysharp.Threading.Tasks;
using VContainer;

namespace Voodya.Core
{
    /// <summary>
    /// Base class of application states. The state machine starts it (not VContainer),
    /// so the start can be awaited, is cancelled when the state exits and its errors are handled.
    /// </summary>
    public abstract class ABaseEntryPoint : IDisposable
    {
        protected IEnumerable<IBootableAsync> _bootablesAsync;
        protected ILoadingPanelService _loadingPanelService;
        protected CompositeDisposable _compositeDisposable;
        protected string _name;

        /// <summary>Cancelled when the state exits</summary>
        protected CancellationToken LifetimeToken { get; private set; }

        /// <summary>
        /// When false (default) an exception in IBootableAsync.Boot fails the start of the state.
        /// Override to true to only log it and continue.
        /// </summary>
        protected virtual bool ContinueOnBootError => false;

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

        public virtual void Dispose()
        {
            _compositeDisposable?.Dispose();
        }

        public virtual async UniTask StartAsync(CancellationToken cancellation = default)
        {
            LifetimeToken = cancellation;
            _compositeDisposable = new CompositeDisposable();

			foreach (var item in _bootablesAsync.OrderBy(x => x.Priority))
            {
                cancellation.ThrowIfCancellationRequested();
                if (item.IsBooted) continue;
                try
                {
                    _loadingPanelService.PrintMessage($"Load {item.GetType().Name}");
                    await item.Boot();
                    item.IsBooted = true;
				}
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    if (!ContinueOnBootError)
                        throw new InvalidOperationException($"Boot of {item.GetType().Name} failed in {_name}", e);
                    VooLog.Error($"Boot of {item.GetType().Name} failed in {_name}: {e}");
                }
            }
        }
    }

}

#endif
