using Loongdum.SceneFlow;
using UnityEditor;
using UnityEngine;

namespace Loongdum.EditorTools
{
    [CustomEditor(typeof(LevelCatalog))]
    public sealed class LevelCatalogEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawSceneField(serializedObject.FindProperty("selectionScenePath"), "Selection Scene");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("levels"), true);
            serializedObject.ApplyModifiedProperties();

            var catalog = (LevelCatalog)target;
            if (!catalog.Validate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("The list order controls selection order and the next-level button. Each playable scene needs one LevelGoal.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Sync Catalog Scenes To Build"))
                    SceneFlowSetup.SyncBuildScenes(catalog);
            }
        }

        private static void DrawSceneField(SerializedProperty property, string label)
        {
            SceneAsset current = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
            EditorGUI.BeginChangeCheck();
            var selected = (SceneAsset)EditorGUILayout.ObjectField(label, current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = selected != null ? AssetDatabase.GetAssetPath(selected) : string.Empty;
        }
    }

    [CustomPropertyDrawer(typeof(LevelEntry))]
    public sealed class LevelEntryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return line * 5f + EditorGUIUtility.standardVerticalSpacing * 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float gap = EditorGUIUtility.standardVerticalSpacing;
            var row = new Rect(position.x, position.y, position.width, line);
            EditorGUI.PropertyField(row, property.FindPropertyRelative("id"));
            row.y += line + gap;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("title"));
            row.y += line + gap;
            SerializedProperty scene = property.FindPropertyRelative("scenePath");
            SceneAsset current = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.stringValue);
            EditorGUI.BeginChangeCheck();
            var selected = (SceneAsset)EditorGUI.ObjectField(row, "Scene", current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                scene.stringValue = selected != null ? AssetDatabase.GetAssetPath(selected) : string.Empty;
            row.y += line + gap;
            row.height = line * 2f + gap;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("description"));
            EditorGUI.EndProperty();
        }
    }
}
