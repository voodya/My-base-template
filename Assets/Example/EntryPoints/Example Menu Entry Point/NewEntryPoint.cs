using System.Collections.Generic;
using UnityEngine;
using Voodya.Core;

[EntryPoint]
public class NewEntryPoint : ABaseEntryPoint
{
    public NewEntryPoint(ILoadingPanelService loadingPanelService, IEnumerable<IBootableAsync> bootableAsyncs) : base(loadingPanelService, bootableAsyncs)
    {
    }


}
