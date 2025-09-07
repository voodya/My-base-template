using UnityEngine;
using VContainer;

namespace Voodya.Core
{
    public abstract class ScriptableInstaller : ScriptableObject
    {
        public abstract void Install(IContainerBuilder builder);
    }

}

