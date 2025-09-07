#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
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

#endif