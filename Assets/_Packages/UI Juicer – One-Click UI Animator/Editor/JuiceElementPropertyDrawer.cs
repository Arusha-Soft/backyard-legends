using UnityEditor;
using UnityEngine;

namespace JuiceUp.Editor
{
    [CustomPropertyDrawer(typeof(JuiceUp.UiJuiceAnimator.JuiceElement))]
    public class JuiceElementPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty ignoreProp = property.FindPropertyRelative("ignore");
            SerializedProperty targetProp = property.FindPropertyRelative("target");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;
            float indent = EditorGUI.indentLevel * 15f;
            
            // Get object name for display
            string objectName = "None (RectTransform)";
            if (targetProp.objectReferenceValue != null)
            {
                RectTransform target = targetProp.objectReferenceValue as RectTransform;
                if (target != null)
                {
                    objectName = target.name;
                }
            }

            // Create header with ignore toggle and object name
            Rect headerRect = new Rect(position.x, position.y, position.width, lineHeight);
            
            // Draw background box for better visibility
            Color bgColor = ignoreProp.boolValue ? new Color(0.4f, 0.3f, 0.3f, 0.4f) : new Color(0.3f, 0.3f, 0.35f, 0.3f);
            EditorGUI.DrawRect(headerRect, bgColor);

            // Draw foldout arrow first
            Rect foldoutRect = new Rect(position.x + indent, position.y, 15f, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, "", false);

            // Draw ignore toggle
            Rect ignoreLabelRect = new Rect(position.x + indent + 18f, position.y, 50f, lineHeight);
            EditorGUI.LabelField(ignoreLabelRect, "Ignore", EditorStyles.miniLabel);
            Rect toggleRect = new Rect(position.x + indent + 65f, position.y + 1f, 15f, lineHeight - 2f);
            ignoreProp.boolValue = EditorGUI.Toggle(toggleRect, ignoreProp.boolValue);

            // Draw object name (bold, prominent)
            Rect labelRect = new Rect(position.x + indent + 85f, position.y, 150f, lineHeight);
            Color originalColor = GUI.color;
            if (ignoreProp.boolValue)
            {
                GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);
            }
            EditorGUI.LabelField(labelRect, objectName, EditorStyles.boldLabel);
            GUI.color = originalColor;

            // Draw object field on the right
            Rect objectRect = new Rect(position.x + indent + 240f, position.y, position.width - indent - 240f, lineHeight);
            EditorGUI.PropertyField(objectRect, targetProp, GUIContent.none);
            
            // Draw expanded properties
            if (property.isExpanded)
            {
                float yOffset = lineHeight + spacing;
                EditorGUI.indentLevel++;
                
                SerializedProperty childProp = property.Copy();
                SerializedProperty endProp = property.GetEndProperty();
                bool enterChildren = true;
                
                while (childProp.NextVisible(enterChildren) && !SerializedProperty.EqualContents(childProp, endProp))
                {
                    if (childProp.name != "ignore" && childProp.name != "target")
                    {
                        Rect propRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUI.GetPropertyHeight(childProp, false));
                        EditorGUI.PropertyField(propRect, childProp, true);
                        yOffset += EditorGUI.GetPropertyHeight(childProp, true) + spacing;
                    }
                    enterChildren = false;
                }
                
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight + 2f;

            float height = EditorGUIUtility.singleLineHeight + 4f;
            SerializedProperty childProp = property.Copy();
            SerializedProperty endProp = property.GetEndProperty();
            bool enterChildren = true;

            while (childProp.NextVisible(enterChildren) && !SerializedProperty.EqualContents(childProp, endProp))
            {
                if (childProp.name != "ignore" && childProp.name != "target")
                {
                    height += EditorGUI.GetPropertyHeight(childProp, true) + 2f;
                }
                enterChildren = false;
            }

            return height;
        }
    }
}

