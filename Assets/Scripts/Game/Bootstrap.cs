using UnityEngine;

namespace Slime.Game
{
    /// <summary>场景入口。场景中只需一个挂着本脚本的物体，其余全部由代码构建。</summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (GameApp.I == null)
            {
                var host = new GameObject("GameApp");
                host.AddComponent<GameApp>();
            }
        }
    }
}
