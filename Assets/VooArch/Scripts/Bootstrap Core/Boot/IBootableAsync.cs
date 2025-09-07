using Cysharp.Threading.Tasks;

namespace Voodya.Core
{
    public interface IBootableAsync
    {
        public bool IsBooted { get; set; }
        public int Priority { get; set; }
        public UniTask Boot();
    }

}
