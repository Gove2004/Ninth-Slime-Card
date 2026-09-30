using UnityEditor;
using UnityEngine;

/// <summary>
/// GameEventSO 的 Inspector 绘制器：左侧对象引用，右侧显示当前监听器数量。
/// 注意：Unity 不支持为 open generic 类型（GameEventSO&lt;T&gt;）注册 PropertyDrawer，
/// 因此泛型事件通道在 Inspector 中使用 Unity 默认绘制（其字段均为私有，不会暴露）。
/// </summary>
[CustomPropertyDrawer(typeof(GameEventSO))]
public class GameEventSOPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float totalWidth = position.width;
        float objectWidth = totalWidth * 0.65f;

        // 左侧：对象引用字段
        Rect objectRect = new Rect(position.x, position.y, objectWidth, position.height);
        EditorGUI.ObjectField(objectRect, property, GUIContent.none);

        // 右侧：监听器数量（只读调试信息）
        var gameEvent = property.objectReferenceValue as GameEventSO;
        string info = gameEvent != null ? $"Listeners:{gameEvent.ListenerCount}" : "none";
        Rect infoRect = new Rect(position.x + objectWidth + 4f, position.y, totalWidth - objectWidth - 8f, position.height);
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
        style.fontSize = 9;
        style.alignment = TextAnchor.MiddleLeft;
        EditorGUI.LabelField(infoRect, info, style);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property);
    }
}
