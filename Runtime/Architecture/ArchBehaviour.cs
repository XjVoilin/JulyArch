using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 通用 MonoBehaviour 框架成员基类。
    /// 继承后可通过 this.GetSystem&lt;T&gt;() / this.Subscribe&lt;T&gt;() 等扩展方法访问框架能力。
    /// </summary>
    public abstract class ArchBehaviour : MonoBehaviour,
        ICanGetSystem, ICanEvent, ICanGetView
    {
    }
}
