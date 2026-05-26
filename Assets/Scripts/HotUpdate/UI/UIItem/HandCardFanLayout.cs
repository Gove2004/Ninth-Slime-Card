using UnityEngine;
using System.Collections.Generic;

public class HandCardFanLayout : MonoBehaviour
{
    [Header("扇形参数")]
    [Tooltip("扇形总角度（例如：30度表示左边-15度，右边+15度）")]
    public float fanAngle = 30f;

    [Tooltip("扇形半径（决定卡牌分布的松散程度）")]
    public float radius = 200f; 

    [Tooltip("是否反转方向")]
    public bool invertDirection = false;

    [Tooltip("卡牌之间的重叠偏移（正值重叠，负值间距）")]
    public float cardSpacingOffset = -30f;

    void Start()
    {
        ApplyFanShape();
    }

    /// <summary>
    /// 应用扇形布局
    /// </summary>
    public void ApplyFanShape()
    {
        int childCount = transform.childCount;
        if (childCount == 0) return;

        // 特殊情况：只有一张牌，直接居中且不旋转
        if (childCount == 1)
        {
            Transform singleChild = transform.GetChild(0);
            singleChild.localPosition = Vector3.zero;
            singleChild.localRotation = Quaternion.identity;
            return;
        }

        // 计算角度步长
        // 如果是两张牌，我们希望它们分别处于 -half 和 +half 的位置
        // 如果是三张牌，则是 -half, 0, +half
        float halfAngle = fanAngle / 2;
        float angleStep = childCount > 1 ? fanAngle / (childCount - 1) : 0;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            
            // 1. 计算当前角度
            float currentAngle = -halfAngle + (angleStep * i);
            if (invertDirection) currentAngle = -currentAngle;

            // 2. 计算位置 (极坐标转直角坐标)
            // 假设圆心在 (0, 0)，卡牌从圆心向外延伸
            float rad = currentAngle * Mathf.Deg2Rad;
            
            // X: 基于半径和角度的正弦值（左右分布）
            // Y: 基于半径和角度的余弦值（上下弧度，可选）
            float x = Mathf.Sin(rad) * radius;
            float y = (1 - Mathf.Cos(rad)) * radius * 0.1f; // 轻微的高度变化，让扇形更自然

            // 3. 处理卡牌重叠 (Z轴排序 & X轴微调)
            // 为了让中间的牌显示在前面，且左右两边的牌稍微往里缩一点
            // 这里简单处理：根据索引调整X位置，模拟重叠效果
            // 更进阶的做法是用 SortingOrder 或 Z轴位置
            float overlapFactor = (i - (childCount - 1) / 2.0f) * cardSpacingOffset;
            Vector3 finalPos = new Vector3(x + overlapFactor, y, 0);

            child.localPosition = finalPos;

            // 4. 设置旋转
            // 负号是因为Unity的2D旋转是顺时针为正，我们希望左边牌逆时针转（正角），右边牌顺时针转（负角）
            child.localRotation = Quaternion.Euler(0, 0, -currentAngle);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 编辑器模式下实时刷新
        if (!Application.isPlaying)
        {
            ApplyFanShape();
        }
    }
#endif

    public void RebuildLayout()
    {
        StartCoroutine(RebuildNextFrame());
    }

    System.Collections.IEnumerator RebuildNextFrame()
    {
        yield return null;
        ApplyFanShape();
    }
}