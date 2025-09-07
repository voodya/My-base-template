using UnityEngine;
using VContainer;

#if HAS_VOODYA  
namespace Voodya.Core
{
    public abstract class ScriptableInstaller : ScriptableObject
    {
        public abstract void Install(IContainerBuilder builder);
    }

}

#endif