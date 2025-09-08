// ФАЙЛ СГЕНЕРИРОВАН АВТОМАТИЧЕСКИ. НЕ РЕДАКТИРУЙТЕ ЕГО ВРУЧНУЮ.
using System;
using UnityEngine;
using VContainer;
using Voodya.Core;

[CreateAssetMenu(fileName = "EntrysMapping", menuName = "Voodya/Entry Points Mapping")]
public class AUTOGEN_EP : ScriptableStates
{
    public override void OnRegister(IApplicationScopesService applicationScopesService, IObjectResolver objectResolver)
    {
        _firstStateType = typeof(MenuEntryPointExample);

        applicationScopesService.Register(new DefaultState<MenuEntryPointExample>(objectResolver));
        applicationScopesService.Register(new DefaultState<BootstrapEntryPoint>(objectResolver));
    }
}
