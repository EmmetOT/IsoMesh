using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SDFGroupRaymarcher))]
public class SDFGroupRaymarcherEditor : Editor
{
    private static class Labels
    {
        public static GUIContent VisualSettings = new GUIContent("Visual Settings");
        public static GUIContent Material = new GUIContent("Material", "This is the material the sdf data is sent to.");
        public static GUIContent SDFGroup = new GUIContent("SDF Group", "An SDF group is a collection of sdf primitives, meshes, and operations which mutually interact.");
        public static GUIContent Size = new GUIContent("Size", "The size and shape of the raymarching volume.");
        public static GUIContent DiffuseColour = new GUIContent("Diffuse Colour", "The diffuse colour of the raymarched shapes.");
        public static GUIContent AmbientColour = new GUIContent("Ambient Colour", "The ambient, or 'base' colour of the raymarched shapes.");
        public static GUIContent GlossPower = new GUIContent("Gloss Power", "The gloss/specular power of the raymarched shapes.");
        public static GUIContent GlossMultiplier = new GUIContent("Gloss Multiplier", "A multiplier for the contribution of the glossiness.");
    }

    private class SerializedProperties
    {
        public SerializedProperty SDFGroup { get; }
        public SerializedProperty Material { get; }
        public SerializedProperty Size { get; }
        public SerializedProperty DiffuseColour { get; }
        public SerializedProperty AmbientColour { get; }
        public SerializedProperty GlossPower { get; }
        public SerializedProperty GlossMultiplier { get; }

        public SerializedProperties(SerializedObject serializedObject)
        {
            SDFGroup = serializedObject.FindProperty("m_group");
            Material = serializedObject.FindProperty("m_material");
            Size = serializedObject.FindProperty("m_size");
            DiffuseColour = serializedObject.FindProperty("m_diffuseColour");
            AmbientColour = serializedObject.FindProperty("m_ambientColour");
            GlossPower = serializedObject.FindProperty("m_glossPower");
            GlossMultiplier = serializedObject.FindProperty("m_glossMultiplier");
        }
    }

    private SDFGroupRaymarcher m_raymarcher;

    private SerializedProperties m_serializedProperties;
    private bool m_isVisualSettingsOpen = true;

    private void OnEnable()
    {
        m_raymarcher = target as SDFGroupRaymarcher;
        m_serializedProperties = new SerializedProperties(serializedObject);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.DrawScript();

        GUI.enabled = false;
        EditorGUILayout.PropertyField(m_serializedProperties.SDFGroup, Labels.SDFGroup);
        EditorGUILayout.PropertyField(m_serializedProperties.Material, Labels.Material);
        GUI.enabled = true;

        if (m_isVisualSettingsOpen = EditorGUILayout.Foldout(m_isVisualSettingsOpen, Labels.VisualSettings, true))
        {
            using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                {
                    if (this.DrawVector3Field(Labels.Size, m_raymarcher.Size, out Vector3 newSize))
                    {
                        m_raymarcher.SetSize(Vector3.Max(newSize, Vector3.zero));
                        EditorUtility.SetDirty(m_raymarcher);
                    }

                    if (this.DrawColourField(Labels.DiffuseColour, m_raymarcher.DiffuseColour, out Color newDiffuseColour))
                    {
                        m_raymarcher.SetDiffuseColour(newDiffuseColour);
                        EditorUtility.SetDirty(m_raymarcher);
                    }

                    if (this.DrawColourField(Labels.AmbientColour, m_raymarcher.AmbientColour, out Color newAmbientColour))
                    {
                        m_raymarcher.SetAmbientColour(newAmbientColour);
                        EditorUtility.SetDirty(m_raymarcher);
                    }

                    if (this.DrawFloatField(Labels.GlossPower, m_raymarcher.GlossPower, out float newGlossPower, min: 0f))
                    {
                        m_raymarcher.SetGlossPower(newGlossPower);
                        EditorUtility.SetDirty(m_raymarcher);
                    }

                    if (this.DrawFloatField(Labels.GlossMultiplier, m_raymarcher.GlossMultiplier, out float newGlossMultiplier, min: 0f))
                    {
                        m_raymarcher.SetGlossMultiplier(newGlossMultiplier);
                        EditorUtility.SetDirty(m_raymarcher);
                    }
                }
            }
        }
    }

    private void OnSceneGUI()
    {
        Handles.color = Color.white;
        Handles.matrix = m_raymarcher.transform.localToWorldMatrix;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        Handles.DrawWireCube(Vector3.zero, m_raymarcher.Size);
    }
}
