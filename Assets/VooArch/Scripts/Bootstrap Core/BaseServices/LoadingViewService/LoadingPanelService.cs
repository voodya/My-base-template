using Cysharp.Threading.Tasks;
using VContainer;

#if HAS_VOODYA  
namespace Voodya.Core
{
    public interface ILoadingPanelService : IBootableAsync
    {
        void Hide();
        void PrintMessage(string message);
        void Release();
        void Show();
    }

    [Service(typeof(LoadingPanelInstaller))]
    public class LoadingPanelService : ILoadingPanelService
    {
        private LoadingScene _view;

        public int Priority { get; set; }
        public bool IsBooted { get; set; }

        private ISceneManager _sceneManager;

        [Inject]
        public LoadingPanelService(ISceneManager sceneManager)
        {
            Priority = 0;
            _sceneManager = sceneManager;
        }

        public void PrintMessage(string message)
        {
            _view.MessageText.text = message;
        }

        public async UniTask Boot()
        {
            if (IsBooted) return;
            _view = await _sceneManager.GetScene<LoadingScene>();
            IsBooted = true;
        }

        public void Show()
        {
            _view.MessageText.text = "Load process..";
            _view.Show();
        }

        public void Hide()
        {
            _view.Hide();
        }

        public void Release()
        {
            _sceneManager.ReleaseScene<LoadingScene>();
        }
    }

}

#endif