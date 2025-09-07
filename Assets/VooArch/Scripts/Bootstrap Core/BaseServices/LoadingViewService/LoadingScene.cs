using TMPro;
using UnityEngine;

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
