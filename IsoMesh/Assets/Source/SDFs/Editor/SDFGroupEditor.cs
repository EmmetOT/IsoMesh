using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    [CustomEditor(typeof(SDFGroup))]
    [CanEditMultipleObjects]
    public class SDFGroupEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent IsRunning = new GUIContent("Is Running", "Whether this group is actively updating.");
            public static GUIContent Settings = new GUIContent("Settings", "Additional controls for the entire group.");
            //public static GUIContent Smoothing = new GUIContent("Smoothing", "The global smoothing setting for combining signed distance fields. It's fun!");
            public static GUIContent NormalSmoothing = new GUIContent("Normal Smoothing", "The sample size for determining the normals of the resulting combined SDF. Higher values produce smoother normals.");
        }

        private class SerializedProperties
        {
            public SerializedProperty IsRunning { get; }
            public SerializedProperty Smoothing { get; }
            public SerializedProperty NormalSmoothing { get; }

            public SerializedProperties(SerializedObject serializedObject)
            {
                IsRunning = serializedObject.FindProperty("m_isRunning");
                //Smoothing = serializedObject.FindProperty("m_smoothing");
                NormalSmoothing = serializedObject.FindProperty("m_normalSmoothing");
            }
        }

        private SerializedProperties m_serializedProperties;
        private SDFGroup m_sdfGroup;
        private bool m_isSettingsOpen = true;

        private void OnEnable()
        {
            m_serializedProperties = new SerializedProperties(serializedObject);
            m_sdfGroup = target as SDFGroup;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.DrawScript();

            EditorGUILayout.PropertyField(m_serializedProperties.IsRunning, Labels.IsRunning);

            if (m_isSettingsOpen = EditorGUILayout.Foldout(m_isSettingsOpen, Labels.Settings, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        //if (this.DrawFloatField(Labels.Smoothing, m_serializedProperties.Smoothing, out float val, min: SDFGroup.MIN_SMOOTHING))
                        //    m_sdfGroup.SetSmoothing(val);

                        if (this.DrawFloatField(Labels.NormalSmoothing, m_serializedProperties.NormalSmoothing, out float val, min: SDFGroup.MIN_SMOOTHING))
                            m_sdfGroup.SetNormalSmoothing(val);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}