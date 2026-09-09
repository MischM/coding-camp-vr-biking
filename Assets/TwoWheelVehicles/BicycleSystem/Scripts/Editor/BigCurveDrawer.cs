using UnityEngine;
using UnityEditor;

namespace rayzngames
{
    // Now it listens to [BigCurve] instead of all AnimationCurves
    [CustomPropertyDrawer(typeof(CurveSizeAttribute))]
    public class BigCurveDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CurveSizeAttribute bigCurve = (CurveSizeAttribute)attribute;

            if (property.propertyType == SerializedPropertyType.AnimationCurve)
            {
                Rect curveRect = new Rect(position.x, position.y, position.width, bigCurve.height);
                EditorGUI.PropertyField(curveRect, property, label);
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use [CurveSize] only on AnimationCurve");
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            CurveSizeAttribute bigCurve = (CurveSizeAttribute)attribute;
            return property.propertyType == SerializedPropertyType.AnimationCurve
                ? bigCurve.height + 5f
                : base.GetPropertyHeight(property, label);
        }
    }
}

