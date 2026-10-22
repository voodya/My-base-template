using UnityEngine;
using VContainer;
using Voodya.Core;

[CreateAssetMenu(fileName = "ClickerServiceInstaller", menuName = "Example/Clicker Service Installer")]
public class ClickerServiceInstaller : ScriptableInstaller
{
    public override void Install(IContainerBuilder builder)
    {
        builder.Register<ClickerService>(Lifetime.Scoped).AsImplementedInterfaces();
    }
}
