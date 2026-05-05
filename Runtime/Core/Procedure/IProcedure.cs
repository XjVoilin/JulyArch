using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 长流程编排原语。可 await / cancel / 嵌套；实例一次性，每次 new。
    /// </summary>
    public interface IProcedure : IArchitectureSettable
    {
        UniTask ExecuteAsync(CancellationToken ct);
    }
}
