using VContainer;

namespace Voodya.Core
{
    public class MenuState : DefaultState<MenuEntryPoint>
    {
        public MenuState(IObjectResolver objectResolver) : base(objectResolver)
        {
            //_currentType = typeof(MenuEntryPoint);
        }
    }
}

