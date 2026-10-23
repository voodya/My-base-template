using UnityEngine;
using VContainer;
using Voodya.Core;

[CreateAssetMenu(fileName = "ClickerServiceInstaller", menuName = "Example/Clicker Service Installer")]
public class ClickerServiceInstaller : ScriptableInstaller
{
    [SerializeField] private ClickerSettings _defaultSettings = new();

    public override void Install(IContainerBuilder builder)
    {
        // Skipped when TryChangeScope<ClickerEntryPoint>(settings) passed its own settings as payload
        builder.RegisterDefaultInstance(_defaultSettings);
        builder.Register<ClickerService>(Lifetime.Scoped).AsImplementedInterfaces();
    }
}
