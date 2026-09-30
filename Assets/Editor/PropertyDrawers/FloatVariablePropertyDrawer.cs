using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FloatVariable))]
public class FloatVariablePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var obj = property.objectReferenceValue as FloatVariable;

        if (obj == null)
        {
            // 未赋值时显示完整的对象字段
            EditorGUI.PropertyField(position, property, label);
        }
        else
        {
            // 已赋值：左侧对象引用，右侧只读显示当前值
            float totalWidth = position.width;
            float objectWidth = totalWidth * 0.65f;

            // Object field
            Rect objectRect = new Rect(position.x, position.y, objectWidth, position.height);
            EditorGUI.ObjectField(objectRect, property, GUIContent.none);

            // Value display
            float valueX = position.x + objectWidth + 4f;
            Rect valueRect = new Rect(valueX, position.y, totalWidth - objectWidth - 8f, position.height);
            GUIStyle valueStyle = new GUIStyle(EditorStyles.boldLabel);
            valueStyle.alignment = TextAnchor.MiddleLeft;
            EditorGUI.LabelField(valueRect, $"= {obj.Value:F2}", valueStyle);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 确保高度与正常属性字段一致
        return EditorGUI.GetPropertyHeight(property);
    }
}