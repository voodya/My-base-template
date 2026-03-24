using UnityEngine;
#if HAS_VOODYA
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{

	public class AdditionalState<T> : DefaultState<T> where T : ABaseEntryPoint
	{
		public AdditionalState(IObjectResolver objectResolver) : base(objectResolver)
		{
		}

	}
}
#endif
