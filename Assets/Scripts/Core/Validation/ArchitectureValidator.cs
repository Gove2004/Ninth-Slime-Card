using UnityEngine;

namespace NinthsSlime.Architecture
{
    /// <summary>
    /// 运行时架构验证器（MonoBehaviour 版）：挂到场景任意 GameObject 上，
    /// 进入 Play 模式时 Awake 自动执行，通过 Debug.Log 输出新架构核心模块的功能验证结果。
    /// 结果可在 Unity Console 窗口查看。
    /// </summary>
    public class ArchitectureValidator : MonoBehaviour
    {
        private void Awake()
        {
            RunValidation();
        }

        [ContextMenu("Run Architecture Validation")]
        public static void RunValidation()
        {
            Debug.Log("[ArchValidator] === 开始运行时架构验证 ===");

            bool intOk = ValidateIntVariable();
            bool floatOk = ValidateFloatVariable();
            bool eventOk = ValidateGameEventSO();

            bool allPassed = intOk && floatOk && eventOk;
            Debug.Log(allPassed
                ? "[ArchValidator] === 全部验证通过 ✅ ==="
                : "[ArchValidator] === 验证存在失败项 ❌ ===");
        }

        private static bool ValidateIntVariable()
        {
            var v = ScriptableObject.CreateInstance<IntVariable>();
            v.Value = 0;

            int received = -1;
            v.OnValueChanged += (oldV, newV) => received = newV;

            v.Apply(5);
            bool ok = received == 5;
            Debug.Log($"[ArchValidator] IntVariable: Add(5) -> 收到回调值={received} {(ok ? "✅" : "❌")}");
            return ok;
        }

        private static bool ValidateFloatVariable()
        {
            var v = ScriptableObject.CreateInstance<FloatVariable>();
            v.Value = 0f;

            float received = float.NaN;
            v.OnValueChanged += (oldV, newV) => received = newV;

            v.Apply(3.14f);
            bool ok = Mathf.Abs(received - 3.14f) < 0.001f;
            Debug.Log($"[ArchValidator] FloatVariable: Apply(3.14) -> 收到回调值={received:F2} {(ok ? "✅" : "❌")}");
            return ok;
        }

        private static bool ValidateGameEventSO()
        {
            var e = ScriptableObject.CreateInstance<GameEventSO>();
            int triggerCount = 0;
            object lastData = null;

            e.Register(data =>
            {
                triggerCount++;
                lastData = data;
            });

            e.Publish("battle-started");

            bool ok = triggerCount == 1 && ReferenceEquals(lastData, "battle-started");
            Debug.Log($"[ArchValidator] GameEventSO: Publish -> 触发次数={triggerCount}, 数据={lastData} {(ok ? "✅" : "❌")}");
            return ok;
        }
    }
}
