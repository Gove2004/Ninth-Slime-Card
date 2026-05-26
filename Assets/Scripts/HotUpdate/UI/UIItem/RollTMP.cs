using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class RollTMP : MonoBehaviour
{
    private TextMeshProUGUI tmp;
    private Tween currentTween;
    
    private void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }


    public void RollTMPText(string formatString, float from, float to, float duration)
    {
        // 停止当前正在进行的动画
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }
        
        float currentValue = from;
        
        // 立即显示起始值
        tmp.text = string.Format(formatString, currentValue);
        
        // 创建数字滚动动画
        currentTween = DOTween.To(() => currentValue, x =>
        {
            currentValue = x;
            tmp.text = string.Format(formatString, currentValue);
        }, to, duration)
        .SetEase(Ease.OutExpo) // 使用指数缓动，开始快结束慢
        .OnComplete(() =>
        {
            // 确保最终值正确显示
            tmp.text = string.Format(formatString, to);
            currentTween = null;
        });
    }


    public void RollTMPText(string formatString, int from, int to, float duration)
    {
        // 停止当前正在进行的动画
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }
        
        int currentValue = from;
        
        // 立即显示起始值
        tmp.text = string.Format(formatString, currentValue);
        
        // 创建数字滚动动画
        currentTween = DOTween.To(() => currentValue, x =>
        {
            currentValue = x;
            tmp.text = string.Format(formatString, currentValue);
        }, to, duration)
        .SetEase(Ease.OutExpo) // 使用指数缓动，开始快结束慢
        .OnComplete(() =>
        {
            // 确保最终值正确显示
            tmp.text = string.Format(formatString, to);
            currentTween = null;
        });
    }
    
    /// <summary>
    /// 数字滚动效果
    /// </summary>
    /// <param name="formatString">格式化字符串</param>
    /// <param name="from">起始数字</param>
    /// <param name="to">目标数字</param>
    /// <param name="duration">持续时间</param>
    public void RollTMPText(string formatString, long from, long to, float duration)
    {
        // 停止当前正在进行的动画
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }
        
        long currentValue = from;
        
        // 立即显示起始值
        tmp.text = string.Format(formatString, currentValue);
        
        // 创建数字滚动动画
        currentTween = DOTween.To(() => currentValue, x =>
        {
            currentValue = x;
            tmp.text = string.Format(formatString, currentValue);
        }, to, duration)
        .SetEase(Ease.OutExpo) // 使用指数缓动，开始快结束慢
        .OnComplete(() =>
        {
            // 确保最终值正确显示
            tmp.text = string.Format(formatString, to);
            currentTween = null;
        });
    }
    
    
    /// <summary>
    /// 立即停止当前动画并设置最终值
    /// </summary>
    public void StopAndSetFinalValue(string formatString, int finalValue)
    {
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
            currentTween = null;
        }
        tmp.text = string.Format(formatString, finalValue);
    }
    
    private void OnDestroy()
    {
        // 清理动画
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }
    }
}