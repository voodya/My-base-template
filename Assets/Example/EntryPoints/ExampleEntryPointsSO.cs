using UnityEngine;
using VContainer;
using Voodya.Core;

[CreateAssetMenu(fileName = "ExampleEntryPointsSO", menuName = "Scriptable Objects/ExampleEntryPointsSO")]
public class ExampleEntryPointsSO : ScriptableStates
{
    public override void OnRegister(IApplicationScopesService applicationScopesService, IObjectResolver objectResolver)
    {

        _firstStateType = typeof(MenuEntryPointExample);
        applicationScopesService.Register(new DefaultState<MenuEntryPointExample>(objectResolver));
    }
}
