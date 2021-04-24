using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SDFGroupMeshGenerator))]
public class SDFGroupMeshGeneratorEditor : Editor
{
    private static class Labels
    {
        public static GUIContent ComputeShader = new GUIContent("Compute Shader", "This compute shader contains the GPU side of this class.");
        public static GUIContent SDFGroup = new GUIContent("SDF Group", "An SDF group is a collection of sdf primitives, meshes, and operations which mutually interact.");
        public static GUIContent AutoUpdate = new GUIContent("Auto Update", "Whether the mesh will automatically be regenerated when any setting, on this component or the SDF Group, changes.");
        public static GUIContent OutputMode = new GUIContent("Output Mode", "This mesh can be passed directly to a material as a triangle and index buffer in 'Procedural' mode, or transfered to the CPU and sent to a MeshFilter in 'Mesh' mode.");
        public static GUIContent ProceduralMaterial = new GUIContent("Procedural Material", "Mesh data will be passed directly to this material as vertex and index buffers.");
        public static GUIContent VoxelSettings = new GUIContent("Voxel Settings", "These settings control the size/amount/density of voxels.");
        public static GUIContent CellSizeMode = new GUIContent("Cell Size Mode", "Fixed = the number of cells doesn't change. Density = the size of the volume doesn't change.");
        public static GUIContent CellSize = new GUIContent("Cell Size", "The size of an indidual cell (or 'voxel').");
        public static GUIContent CellCount = new GUIContent("Cell Count", "The number of cells (or 'voxels') on each side.");
        public static GUIContent VolumeSize = new GUIContent("Volume Size", "The size of each side of the whole volume in which a mesh will be generated.");
        public static GUIContent CellDensity = new GUIContent("Cell Density", "The number of cells per side. (Rounded.)");
        public static GUIContent AlgorithmSettings = new GUIContent("Algorithm Settings", "These settings control how the mesh vertices and normals are calculated from the sdf data");
        public static GUIContent MaxAngleTolerance = new GUIContent("Max Angle Tolerance", "If the angle between the vertex normal and the triangle normal exceeds this value (degrees), the vertex will be split off and given the triangle normal. This is important for sharp edges.");
        public static GUIContent VisualNormalSmoothing = new GUIContent("Visual Normal Smoothing", "The sample size for determining the surface normals of the mesh. Higher values produce smoother normals.");
        public static GUIContent IsosurfaceExtractionType = new GUIContent("Isosurface Extraction Type", "What algorithm is used to convert the SDF data to a mesh.\nSurface Nets = cheap but bad at sharp edges and corners.\nDual Contouring = similar to surface nets but uses a more advanced technique for positioning the vertices, which is more expensive but produces nice sharp edges and corners.");
        public static GUIContent ConstrainToCellUnits = new GUIContent("Constrain to Cell Units", "Dual contouring can sometimes produce vertex positions outside of their cells. This value defines the max of how far outside the cell the vertex can be before it falls back to the surface nets solution.");
        public static GUIContent OverrideQEFSettings = new GUIContent("Override QEF Settings", "Advanced controls for dual contouring's technique for finding the vertex position.");
        public static GUIContent QEFSweeps = new GUIContent("QEF Sweeps");
        public static GUIContent QEFPseudoInverseThreshold = new GUIContent("QEF Pseudo Inverse Threshold");
        public static GUIContent EdgeIntersectionType = new GUIContent("Edge Intersection Type", "Part of the isosurface extraction algorithm involves finding the intersection between each voxel edge and the underlying isosurface.\nInterpolation = a cheap approximate solution.\nBinary Search = Iteratively search for the point of intersection.");
        public static GUIContent BinarySearchIterations = new GUIContent("Binary Search Iterations", "The number of iterations for the binary search for the edge intersection. Higher values are more expensive and accurate.");
        public static GUIContent ApplyGradientDescent = new GUIContent("Apply Gradient Descent", "The found vertex position can sometimes be slightly off the true 0-isosurface. This final step will nudge it back towards the surface.");
        public static GUIContent GradientDescentIterations = new GUIContent("Gradient Descent Iterations", "The number of times to iteratively apply the gradient descent step. 1 is usually enough.");
        public static GUIContent NudgeVerticesToAverageNormalScalar = new GUIContent("Nudge Vertices to Average Normal Scalar", "Giving vertices a further nudge in the direction of the average normal of each of the voxels edge intersections can improve edges and corners but also can produce artefacts at interior angles. This scalar value is simply multiplied by the sum of these normals. Best used at very small values and alongside gradient descent.");
        public static GUIContent NudgeMaxMagnitude = new GUIContent("Nudge Max", "Limits the magnitude of the nudge vector. (See above.)");
        public static GUIContent DebugSettings = new GUIContent("Debug Settings", "Controls for gizmos.");
        public static GUIContent ShowGrid = new GUIContent("Show Grid", "Show the voxel grid as a gizmo. Not recommended for high voxel counts.");
        //public static GUIContent ShowSamplePoints = new GUIContent("Show Sample Points", "Show the samples of the underlying sdf as a gizmo. Not recommended for high voxel counts.");
    }

    private class SerializedProperties
    {
        public SerializedProperty ComputeShader { get; }
        public SerializedProperty SDFGroup { get; }
        public SerializedProperty AutoUpdate { get; }
        public SerializedProperty OutputMode { get; }
        public SerializedProperty ProceduralMaterial { get; }
        public SerializedProperty CellSizeMode { get; }
        public SerializedProperty CellSize { get; }
        public SerializedProperty CellCount { get; }
        public SerializedProperty VolumeSize { get; }
        public SerializedProperty CellDensity { get; }
        public SerializedProperty MaxAngleTolerance { get; }
        public SerializedProperty VisualNormalSmoothing { get; }
        public SerializedProperty IsosurfaceExtractionType { get; }
        public SerializedProperty ConstrainToCellUnits { get; }
        public SerializedProperty OverrideQEFSettings { get; }
        public SerializedProperty QEFSweeps { get; }
        public SerializedProperty QEFPseudoInverseThreshold { get; }
        public SerializedProperty EdgeIntersectionType { get; }
        public SerializedProperty BinarySearchIterations { get; }
        public SerializedProperty ApplyGradientDescent { get; }
        public SerializedProperty GradientDescentIterations { get; }
        public SerializedProperty NudgeVerticesToAverageNormalScalar { get; }
        public SerializedProperty NudgeMaxMagnitude { get; }
        public SerializedProperty ShowGrid { get; }
        //public SerializedProperty ShowSamplePoints { get; }

        public SerializedProperties(SerializedObject serializedObject)
        {
            ComputeShader = serializedObject.FindProperty("m_computeShader");
            SDFGroup = serializedObject.FindProperty("m_group");
            AutoUpdate = serializedObject.FindProperty("m_autoUpdate");
            OutputMode = serializedObject.FindProperty("m_outputMode");
            ProceduralMaterial = serializedObject.FindProperty("m_proceduralMaterial");
            CellSizeMode = serializedObject.FindProperty("m_cellSizeMode");
            CellSize = serializedObject.FindProperty("m_cellSize");
            CellCount = serializedObject.FindProperty("m_cellCount");
            VolumeSize = serializedObject.FindProperty("m_volumeSize");
            CellDensity = serializedObject.FindProperty("m_cellDensity");
            MaxAngleTolerance = serializedObject.FindProperty("m_maxAngleTolerance");
            VisualNormalSmoothing = serializedObject.FindProperty("m_visualNormalSmoothing");
            IsosurfaceExtractionType = serializedObject.FindProperty("m_isosurfaceExtractionType");
            ConstrainToCellUnits = serializedObject.FindProperty("m_constrainToCellUnits");
            OverrideQEFSettings = serializedObject.FindProperty("m_overrideQEFSettings");
            QEFSweeps = serializedObject.FindProperty("m_qefSweeps");
            QEFPseudoInverseThreshold = serializedObject.FindProperty("m_qefPseudoInverseThreshold");
            EdgeIntersectionType = serializedObject.FindProperty("m_edgeIntersectionType");
            BinarySearchIterations = serializedObject.FindProperty("m_binarySearchIterations");
            ApplyGradientDescent = serializedObject.FindProperty("m_applyGradientDescent");
            GradientDescentIterations = serializedObject.FindProperty("m_gradientDescentIterations");
            NudgeVerticesToAverageNormalScalar = serializedObject.FindProperty("m_nudgeVerticesToAverageNormalScalar");
            NudgeMaxMagnitude = serializedObject.FindProperty("m_nudgeMaxMagnitude");
            ShowGrid = serializedObject.FindProperty("m_showGrid");
            //ShowSamplePoints = serializedObject.FindProperty("m_showSamplePoints");
        }
    }


    private SDFGroupMeshGenerator m_sdfGroupMeshGen;

    private SerializedProperties m_serializedProperties;
    private bool m_isVoxelSettingsOpen = true;
    private bool m_isAlgorithmSettingsOpen = true;
    private bool m_isDebugSettingsOpen = true;

    private void OnEnable()
    {
        m_sdfGroupMeshGen = target as SDFGroupMeshGenerator;
        m_serializedProperties = new SerializedProperties(serializedObject);
    }


    public override void OnInspectorGUI()
    {
        serializedObject.DrawScript();

        GUI.enabled = false;
        EditorGUILayout.PropertyField(m_serializedProperties.ComputeShader, Labels.ComputeShader);
        GUI.enabled = true;

        GUI.enabled = false;
        EditorGUILayout.PropertyField(m_serializedProperties.SDFGroup, Labels.SDFGroup);
        GUI.enabled = true;

        EditorGUILayout.PropertyField(m_serializedProperties.AutoUpdate, Labels.AutoUpdate);

        if (this.DrawEnumField(Labels.OutputMode, m_sdfGroupMeshGen.OutputMode, out OutputMode newOutputMode))
        {
            m_sdfGroupMeshGen.SetOutputMode(newOutputMode);
            EditorUtility.SetDirty(m_sdfGroupMeshGen);
        }

        if (m_sdfGroupMeshGen.OutputMode == OutputMode.Procedural)
            EditorGUILayout.PropertyField(m_serializedProperties.ProceduralMaterial, Labels.ProceduralMaterial);

        if (m_isVoxelSettingsOpen = EditorGUILayout.Foldout(m_isVoxelSettingsOpen, Labels.VoxelSettings, true))
        {
            using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(m_serializedProperties.CellSizeMode, Labels.CellSizeMode);

                    if (m_sdfGroupMeshGen.CellSizeMode == CellSizeMode.Fixed)
                    {
                        if (this.DrawFloatField(Labels.CellSize, m_sdfGroupMeshGen.CellSize, out float newCellSize, min: 0.005f))
                        {
                            m_sdfGroupMeshGen.SetCellSize(newCellSize);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }

                        if (this.DrawIntField(Labels.CellCount, m_sdfGroupMeshGen.CellCount, out int newCellCount, min: 2, max: 200))
                        {
                            m_sdfGroupMeshGen.SetCellCount(newCellCount);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }
                    }
                    else if (m_sdfGroupMeshGen.CellSizeMode == CellSizeMode.Density)
                    {
                        if (this.DrawFloatField(Labels.VolumeSize, m_sdfGroupMeshGen.VolumeSize, out float newVolumeSize, min: 0.05f))
                        {
                            m_sdfGroupMeshGen.SetDensity(newVolumeSize, m_sdfGroupMeshGen.CellDensity);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }

                        if (this.DrawFloatField(Labels.CellDensity, m_sdfGroupMeshGen.CellDensity, out float newCellDensity, min: 0.05f))
                        {
                            m_sdfGroupMeshGen.SetDensity(m_sdfGroupMeshGen.VolumeSize, newCellDensity);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }
                    }
                }
            }
        }

        if (m_isAlgorithmSettingsOpen = EditorGUILayout.Foldout(m_isAlgorithmSettingsOpen, Labels.AlgorithmSettings, true))
        {
            using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                {
                    if (this.DrawEnumField(Labels.IsosurfaceExtractionType, m_sdfGroupMeshGen.IsosurfaceExtractionType, out IsosurfaceExtractionType newIsosurfaceExtractionType))
                    {
                        m_sdfGroupMeshGen.SetIsosurfaceExtractionType(newIsosurfaceExtractionType);
                        EditorUtility.SetDirty(m_sdfGroupMeshGen);
                    }

                    if (m_sdfGroupMeshGen.IsosurfaceExtractionType == IsosurfaceExtractionType.DualContouring)
                    {
                        if (this.DrawFloatField(Labels.ConstrainToCellUnits, m_sdfGroupMeshGen.ConstrainToCellUnits, out float newConstrainToCellUnits, min: 0f))
                        {
                            m_sdfGroupMeshGen.SetConstrainToCellUnits(newConstrainToCellUnits);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Normal Settings", EditorStyles.boldLabel);

                    if (this.DrawFloatField(Labels.MaxAngleTolerance, m_sdfGroupMeshGen.MaxAngleTolerance, out float newMaxAngleTolerance, min: 0f, max: 180f))
                    {
                        m_sdfGroupMeshGen.SetMaxAngleTolerance(newMaxAngleTolerance);
                        EditorUtility.SetDirty(m_sdfGroupMeshGen);
                    }

                    if (this.DrawFloatField(Labels.VisualNormalSmoothing, m_sdfGroupMeshGen.VisualNormalSmoothing, out float newVisualNormalSmoothing, min: 1e-5f, max: 10f))
                    {
                        m_sdfGroupMeshGen.SetVisualNormalSmoothing(newVisualNormalSmoothing);
                        EditorUtility.SetDirty(m_sdfGroupMeshGen);
                    }

                    if (m_sdfGroupMeshGen.IsosurfaceExtractionType == IsosurfaceExtractionType.DualContouring)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("QEF Settings", EditorStyles.boldLabel);

                        if (this.DrawBoolField(Labels.OverrideQEFSettings, m_sdfGroupMeshGen.OverrideQEFSettings, out bool newOverideQEFSettings))
                        {
                            if (newOverideQEFSettings)
                                m_sdfGroupMeshGen.SetQefOverrideSettings(m_sdfGroupMeshGen.QefSweeps, m_sdfGroupMeshGen.QefPseudoInverseThreshold);
                            else
                                m_sdfGroupMeshGen.DisableQEFOverride();

                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }

                        if (m_sdfGroupMeshGen.OverrideQEFSettings)
                        {
                            if (this.DrawIntField(Labels.QEFSweeps, m_sdfGroupMeshGen.QefSweeps, out int newQEFSweeps, min: 1))
                            {
                                m_sdfGroupMeshGen.SetQefOverrideSettings(newQEFSweeps, m_sdfGroupMeshGen.QefPseudoInverseThreshold);
                                EditorUtility.SetDirty(m_sdfGroupMeshGen);
                            }

                            if (this.DrawFloatField(Labels.QEFPseudoInverseThreshold, m_sdfGroupMeshGen.QefPseudoInverseThreshold, out float newQEFPseudoInverseThreshold, min: 1e-9f))
                            {
                                m_sdfGroupMeshGen.SetQefOverrideSettings(m_sdfGroupMeshGen.QefSweeps, newQEFPseudoInverseThreshold);
                                EditorUtility.SetDirty(m_sdfGroupMeshGen);
                            }
                        }

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Nudge Settings", EditorStyles.boldLabel);

                        if (this.DrawFloatField(Labels.NudgeVerticesToAverageNormalScalar, m_sdfGroupMeshGen.NudgeVerticesToAverageNormalScalar, out float newNudgeVerticesToAverageNormalScalar, min: 0f))
                        {
                            m_sdfGroupMeshGen.SetNudgeSettings(newNudgeVerticesToAverageNormalScalar, m_sdfGroupMeshGen.NudgeMaxMagnitude);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }

                        if (this.DrawFloatField(Labels.NudgeMaxMagnitude, m_sdfGroupMeshGen.NudgeMaxMagnitude, out float newNudgeMaxMagnitude, min: 0f))
                        {
                            m_sdfGroupMeshGen.SetNudgeSettings(m_sdfGroupMeshGen.NudgeVerticesToAverageNormalScalar, newNudgeMaxMagnitude);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }
                    }


                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Edge Intersection Settings", EditorStyles.boldLabel);

                    if (this.DrawEnumField(Labels.EdgeIntersectionType, m_sdfGroupMeshGen.EdgeIntersectionType, out EdgeIntersectionType newEdgeIntersectionType))
                    {
                        m_sdfGroupMeshGen.SetEdgeIntersectionType(newEdgeIntersectionType);
                        EditorUtility.SetDirty(m_sdfGroupMeshGen);
                    }

                    if (m_sdfGroupMeshGen.EdgeIntersectionType == EdgeIntersectionType.BinarySearch)
                    {
                        if (this.DrawIntField(Labels.BinarySearchIterations, m_sdfGroupMeshGen.BinarySearchIterations, out int newBinarySearchIterations, min: 1))
                        {
                            m_sdfGroupMeshGen.SetBinarySearchIterations(newBinarySearchIterations);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Gradient Descent Settings", EditorStyles.boldLabel);

                    if (this.DrawBoolField(Labels.ApplyGradientDescent, m_sdfGroupMeshGen.ApplyGradientDescent, out bool newApplyGradientDescent))
                    {
                        m_sdfGroupMeshGen.SetApplyGradientDescent(newApplyGradientDescent);
                        EditorUtility.SetDirty(m_sdfGroupMeshGen);
                    }

                    if (m_sdfGroupMeshGen.ApplyGradientDescent)
                    {
                        if (this.DrawIntField(Labels.GradientDescentIterations, m_sdfGroupMeshGen.GradientDescentIterations, out int newGradientDescentIterations, min: 1))
                        {
                            m_sdfGroupMeshGen.SetGradientDescentIterations(newGradientDescentIterations);
                            EditorUtility.SetDirty(m_sdfGroupMeshGen);
                        }
                    }
                }
            }
        }


        if (m_isDebugSettingsOpen = EditorGUILayout.Foldout(m_isDebugSettingsOpen, Labels.DebugSettings, true))
        {
            using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(m_serializedProperties.ShowGrid, Labels.ShowGrid);
                    //EditorGUILayout.PropertyField(m_serializedProperties.ShowSamplePoints, Labels.ShowSamplePoints);
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        Handles.matrix = m_sdfGroupMeshGen.transform.localToWorldMatrix;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        Handles.color = Color.black;
        Handles.DrawWireCube(Vector3.zero, Vector3.one * m_sdfGroupMeshGen.CellCount * m_sdfGroupMeshGen.CellSize);

        if (m_sdfGroupMeshGen.ShowGrid)
        {
            int cellCount = m_sdfGroupMeshGen.CellCount;

            const float lineThickness = 1f;

            for (int i = 0; i < m_sdfGroupMeshGen.SamplesPerSide; i++)
            {
                for (int j = 0; j < m_sdfGroupMeshGen.SamplesPerSide; j++)
                {
                    Vector3 xVertexStart = m_sdfGroupMeshGen.CellCoordinateToVertex(0, i, j);
                    Vector3 yVertexStart = m_sdfGroupMeshGen.CellCoordinateToVertex(i, 0, j);
                    Vector3 zVertexStart = m_sdfGroupMeshGen.CellCoordinateToVertex(i, j, 0);

                    Vector3 xVertexEnd = m_sdfGroupMeshGen.CellCoordinateToVertex(cellCount, i, j);
                    Vector3 yVertexEnd = m_sdfGroupMeshGen.CellCoordinateToVertex(i, cellCount, j);
                    Vector3 zVertexEnd = m_sdfGroupMeshGen.CellCoordinateToVertex(i, j, cellCount);

                    Handles.DrawAAPolyLine(lineThickness, xVertexStart, xVertexEnd);
                    Handles.DrawAAPolyLine(lineThickness, yVertexStart, yVertexEnd);
                    Handles.DrawAAPolyLine(lineThickness, zVertexStart, zVertexEnd);
                }
            }
        }
    }
}
