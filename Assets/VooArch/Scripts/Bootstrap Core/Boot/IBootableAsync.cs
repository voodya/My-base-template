using Cysharp.Threading.Tasks;

#if HAS_VOODYA  
namespace Voodya.Core
{
    public interface IBootableAsync
    {
        public bool IsBooted { get; set; }
        public int Priority { get; set; }
        public UniTask Boot();
    }

}
#endif