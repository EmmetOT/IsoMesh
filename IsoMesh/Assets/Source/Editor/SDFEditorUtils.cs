using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class SDFEditorUtils
{
    public static void DrawScript(this SerializedObject obj)
    {
        GUI.enabled = false;
        EditorGUILayout.PropertyField(obj.FindProperty("m_Script"));
        GUI.enabled = true;
    }

    public static bool DrawFloatField(this Object obj, string label, ref float val, float? min = null, float? max = null) =>
        DrawFloatField(obj, new GUIContent(label), ref val, min, max);

    public static bool DrawFloatField(this Object obj, GUIContent label, ref float val, float? min = null, float? max = null)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            float newVal = EditorGUILayout.FloatField(label, val);

            if (min.HasValue)
                newVal = Mathf.Max(newVal, min.Value);

            if (max.HasValue)
                newVal = Mathf.Min(newVal, max.Value);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                val = newVal;
                EditorUtility.SetDirty(obj);
                return true;
            }
        }

        return false;
    }

    public static bool DrawFloatField(this Object obj, string label, float val, out float newVal, float? min = null, float? max = null) =>
        DrawFloatField(obj, new GUIContent(label), val, out newVal, min, max);

    public static bool DrawFloatField(this Object obj, GUIContent label, float val, out float newVal, float? min = null, float? max = null)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            newVal = EditorGUILayout.FloatField(label, val);

            if (min.HasValue)
                newVal = Mathf.Max(newVal, min.Value);

            if (max.HasValue)
                newVal = Mathf.Min(newVal, max.Value);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                return true;
            }
        }

        return false;
    }

    public static bool DrawIntField(this Object obj, string label, ref int val, int? min = null, int? max = null) =>
        DrawIntField(obj, new GUIContent(label), ref val, min, max);

    public static bool DrawIntField(this Object obj, GUIContent label, ref int val, int? min = null, int? max = null)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            int newVal = EditorGUILayout.IntField(label, val);

            if (min.HasValue)
                newVal = Mathf.Max(newVal, min.Value);

            if (max.HasValue)
                newVal = Mathf.Min(newVal, max.Value);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                val = newVal;
                EditorUtility.SetDirty(obj);
                return true;
            }
        }

        return false;
    }

    public static bool DrawIntField(this Object obj, string label, int val, out int newVal, int? min = null, int? max = null) =>
        DrawIntField(obj, new GUIContent(label), val, out newVal, min, max);

    public static bool DrawIntField(this Object obj, GUIContent label, int val, out int newVal, int? min = null, int? max = null)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            newVal = EditorGUILayout.IntField(label, val);

            if (min.HasValue)
                newVal = Mathf.Max(newVal, min.Value);

            if (max.HasValue)
                newVal = Mathf.Min(newVal, max.Value);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                return true;
            }
        }

        return false;
    }

    public static bool DrawEnumField<T>(this Object obj, GUIContent label, T val, out T newVal) where T : System.Enum
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            newVal = (T)EditorGUILayout.EnumPopup(label, val);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                return true;
            }
        }

        return false;
    }

    public static bool DrawVector3Field(this Object obj, string label, ref Vector3 val) =>
        DrawVector3Field(obj, new GUIContent(label), ref val);

    public static bool DrawVector3Field(this Object obj, GUIContent label, ref Vector3 val)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            Vector3 newVal = EditorGUILayout.Vector3Field(label, val);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                val = newVal;
                EditorUtility.SetDirty(obj);
                return true;
            }
        }

        return false;
    }

    public static bool DrawVector3Field(this Object obj, string label, Vector3 val, out Vector3 newVal) =>
        DrawVector3Field(obj, new GUIContent(label), val, out newVal);

    public static bool DrawVector3Field(this Object obj, GUIContent label, Vector3 val, out Vector3 newVal)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            newVal = EditorGUILayout.Vector3Field(label, val);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                return true;
            }
        }

        return false;
    }

    public static bool DrawColourField(this Object obj, string label, Color val, out Color newVal) =>
        DrawColourField(obj, new GUIContent(label), val, out newVal);

    public static bool DrawColourField(this Object obj, GUIContent label, Color val, out Color newVal)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            newVal = EditorGUILayout.ColorField(label, val);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                return true;
            }
        }

        return false;
    }


    public static bool DrawBoolField(this Object obj, string label, ref bool val) =>
        DrawBoolField(obj, new GUIContent(label), ref val);

    public static bool DrawBoolField(this Object obj, GUIContent label, ref bool val)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            bool newVal = EditorGUILayout.Toggle(label, val);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                val = newVal;
                EditorUtility.SetDirty(obj);
                return true;
            }
        }

        return false;
    }

    public static bool DrawBoolField(this Object obj, string label, bool val, out bool newVal) =>
        DrawBoolField(obj, new GUIContent(label), val, out newVal);

    public static bool DrawBoolField(this Object obj, GUIContent label, bool val, out bool newVal)
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            newVal = EditorGUILayout.Toggle(label, val);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                val = newVal;
                return true;
            }
        }

        return false;
    }

    public static bool DrawObjectField<T>(this Object obj, string label, ref T val) where T : UnityEngine.Object =>
        DrawObjectField<T>(obj, new GUIContent(label), ref val);

    public static bool DrawObjectField<T>(this Object obj, GUIContent label, ref T val) where T : UnityEngine.Object
    {
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            T newVal = (T)EditorGUILayout.ObjectField(label, val, typeof(T), allowSceneObjects: false);

            if (check.changed)
            {
                Undo.RecordObject(obj, "Changed " + label);
                val = newVal;
                EditorUtility.SetDirty(obj);
                return true;
            }
        }

        return false;
    }

}
