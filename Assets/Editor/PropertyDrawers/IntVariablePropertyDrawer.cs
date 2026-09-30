using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(IntVariable))]
public class IntVariablePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var obj = property.objectReferenceValue as IntVariable;

        if (obj == null)
        {
            EditorGUI.PropertyField(position, property, label);
        }
        else
        {
            float totalWidth = position.width;
            float objectWidth = totalWidth * 0.65f;

            Rect objectRect = new Rect(position.x, position.y, objectWidth, position.height);
            EditorGUI.ObjectField(objectRect, property, GUIContent.none);

            float valueX = position.x + objectWidth + 4f;
            Rect valueRect = new Rect(valueX, position.y, totalWidth - objectWidth - 8f, position.height);
            GUIStyle valueStyle = new GUIStyle(EditorStyles.boldLabel);
            valueStyle.alignment = TextAnchor.MiddleLeft;
            EditorGUI.LabelField(valueRect, $"= {obj.Value}", valueStyle);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property);
    }
}