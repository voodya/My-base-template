using TMPro;
using UnityEngine;

#if HAS_VOODYA 
namespace Voodya.Core
{
    public class LoadingScene : ABaseScene
    {
        [field: SerializeField] public TextMeshProUGUI MessageText { get; set; }

    }

}
#endif