using TMPro;
using UnityEngine;

#if HAS_VOODYA 
namespace Voodya.Core
{
    public class LoadingScene : ABaseScene
    {
        [field: SerializeField] public TextMeshProUGUI MessageText { get; set; }

        public override void Dispose()
        {
            Debug.LogError("Dispose Loading View (nothing to dispose)");
            base.Dispose();
        }
    }

}
#endif