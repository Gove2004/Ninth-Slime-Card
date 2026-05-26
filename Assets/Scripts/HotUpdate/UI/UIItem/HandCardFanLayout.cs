using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor; // 引入 Editor 命名空间，用于编辑器模式下的刷新
#endif

[RequireComponent(typeof(HorizontalLayoutGroup))] // 确保挂载了横向布局组
public class HandCardFanLayout : MonoBehaviour
{
    [Header("扇形参数")]
    [Tooltip("扇形总角度（例如：30度表示左边-15度，右边+15度）")]
    public float fanAngle = 30f;

    [Tooltip("扇形半径（影响卡牌的间距感，通常配合 Layout Group 的 Spacing 使用）")]
    public float radius = 0f; // 0表示只旋转不位移，非0会微调位置

    [Tooltip("是否反转方向（如果卡牌面朝下的话勾选这个）")]
    public bool invertDirection = false;

    // 缓存 Layout Group 组件
    private HorizontalLayoutGroup _layoutGroup;

    void Awake()
    {
        _layoutGroup = GetComponent<HorizontalLayoutGroup>();
    }

    void Start()
    {
        // 游戏开始时强制刷新一次
        ApplyFanShape();
    }

    /// <summary>
    /// 应用扇形布局
    /// </summary>
    public void ApplyFanShape()
    {
        int childCount = transform.childCount;
        if (childCount == 0) return;

        // 确保 Layout Group 先计算完位置（如果需要的话）
        // 实际上我们不需要 Layout Group 来计算旋转，只需要它排好X位置
        // 如果不需要 Layout Group 的 X 位置，可以注释掉下面这行，完全手写位置
        // LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform); 

        float angleStep = (childCount > 1) ? fanAngle / (childCount - 1) : 0;
        float startAngle = -fanAngle / 2;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            
            // 1. 计算旋转角度
            float currentAngle = startAngle + (angleStep * i);
            if (invertDirection) currentAngle = -currentAngle;

            // 设置旋转（只改 Z 轴）
            child.localEulerAngles = new Vector3(0, 0, -currentAngle); // 负号是为了符合视觉习惯：左边正转，右边负转

            // 2. （可选）根据半径微调位置，让扇形更立体
            // 如果不设置 radius，卡牌会挤在一起，设置后会根据旋转角度往外扩一点
            if (radius > 0)
            {
                float rad = currentAngle * Mathf.Deg2Rad;
                // 这里的 Y 轴偏移是为了让扇形弧度更自然（近大远小/高低差）
                // 简单处理：根据旋转角度给一点 Y 轴的偏移
                Vector3 pos = child.localPosition;
                pos.y = -Mathf.Cos(Mathf.Abs(currentAngle) * Mathf.Deg2Rad) * radius * 0.1f; // 轻微下沉
                child.localPosition = pos;
            }
        }
    }

#if UNITY_EDITOR
    // 在编辑器模式下，只要Inspector的值发生变化，就重新计算
    private void OnValidate()
    {
        // 延迟一帧执行，避免在编辑模式下频繁报错或卡顿
        EditorApplication.delayCall += () =>
        {
            if (this == null) return; // 防止对象已被销毁
            ApplyFanShape();
        };
    }
#endif

    // 如果你在代码中动态添加了卡牌，记得调用这个方法刷新
    public void RebuildLayout()
    {
        // 等待一帧让 Layout Group 先排版
        StartCoroutine(RebuildNextFrame());
    }

    System.Collections.IEnumerator RebuildNextFrame()
    {
        yield return null; // 等待一帧
        ApplyFanShape();
    }
}