using UnityEngine;
using VContainer;
using Voodya.Core;

[CreateAssetMenu(fileName = "ProgressServiceInstaller", menuName = "Example/Progress Service Installer")]
public class ProgressServiceInstaller : ScriptableInstaller
{
    public override void Install(IContainerBuilder builder)
    {
        builder.Register<ProgressService>(Lifetime.Singleton).AsImplementedInterfaces();
    }
}
