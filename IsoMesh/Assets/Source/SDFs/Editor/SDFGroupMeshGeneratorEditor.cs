using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    [CustomEditor(typeof(SDFGroupMeshGenerator))]
    [CanEditMultipleObjects]
    public class SDFGroupMeshGeneratorEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent ComputeShader = new GUIContent("Compute Shader", "This compute shader contains the GPU side of this class.");
            public static GUIContent SDFGroup = new GUIContent("SDF Group", "An SDF group is a collection of sdf primitives, meshes, and operations which mutually interact.");
            public static GUIContent AutoUpdate = new GUIContent("Auto Update", "Whether the mesh will automatically be regenerated when any setting, on this component or the SDF Group, changes.");
            public static GUIContent OutputMode = new GUIContent("Output Mode", "This mesh can be passed directly to a material as a triangle and index buffer in 'Procedural' mode, or transfered to the CPU and sent to a MeshFilter in 'Mesh' mode.");
            public static GUIContent IsAsynchronous = new GUIContent("Is Asynchronous", "If true, the main thread won't wait for the GPU to finish generating the mesh.");
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
        }

        private class SerializedProperties
        {
            public SerializedProperty ComputeShader { get; }
            public SerializedProperty SDFGroup { get; }
            public SerializedProperty AutoUpdate { get; }
            public SerializedProperty OutputMode { get; }
            public SerializedProperty IsAsynchronous { get; }
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

            public SerializedProperties(SerializedObject serializedObject)
            {
                ComputeShader = serializedObject.FindProperty("m_computeShader");
                SDFGroup = serializedObject.FindProperty("m_group");
                AutoUpdate = serializedObject.FindProperty("m_autoUpdate");
                OutputMode = serializedObject.FindProperty("m_outputMode");
                IsAsynchronous = serializedObject.FindProperty("m_isAsynchronous");
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
            }
        }


        private SDFGroupMeshGenerator m_sdfGroupMeshGen;

        private SerializedProperties m_serializedProperties;
        private SerializedPropertySetter m_setter;
        private bool m_isVoxelSettingsOpen = true;
        private bool m_isAlgorithmSettingsOpen = true;
        private bool m_isDebugSettingsOpen = true;

        private void OnEnable()
        {
            m_sdfGroupMeshGen = target as SDFGroupMeshGenerator;
            m_serializedProperties = new SerializedProperties(serializedObject);
            m_setter = new SerializedPropertySetter(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            m_setter.Clear();

            serializedObject.DrawScript();

            GUI.enabled = false;
            EditorGUILayout.PropertyField(m_serializedProperties.ComputeShader, Labels.ComputeShader);
            GUI.enabled = true;

            GUI.enabled = false;
            EditorGUILayout.PropertyField(m_serializedProperties.SDFGroup, Labels.SDFGroup);
            GUI.enabled = true;
            
            m_setter.DrawProperty(Labels.AutoUpdate, m_serializedProperties.AutoUpdate);

            m_setter.DrawEnumSetting<OutputMode>(Labels.OutputMode, m_serializedProperties.OutputMode, onValueChangedCallback: m_sdfGroupMeshGen.OnOutputModeChanged);

            if (m_sdfGroupMeshGen.OutputMode == OutputMode.Procedural)
            {
                m_setter.DrawProperty(Labels.ProceduralMaterial, m_serializedProperties.ProceduralMaterial);
            }
            else if (m_sdfGroupMeshGen.OutputMode == OutputMode.MeshFilter)
            {
                m_setter.DrawProperty(Labels.IsAsynchronous, m_serializedProperties.IsAsynchronous);
            }

            if (m_isVoxelSettingsOpen = EditorGUILayout.Foldout(m_isVoxelSettingsOpen, Labels.VoxelSettings, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        m_setter.DrawProperty(Labels.CellSizeMode, m_serializedProperties.CellSizeMode);

                        if (m_sdfGroupMeshGen.CellSizeMode == CellSizeMode.Fixed)
                        {
                            m_setter.DrawFloatSetting(Labels.CellSize, m_serializedProperties.CellSize, min: 0.005f, onValueChangedCallback: m_sdfGroupMeshGen.OnCellSizeChanged);
                            m_setter.DrawIntSetting(Labels.CellCount, m_serializedProperties.CellCount, min: 2, max: 200, onValueChangedCallback: m_sdfGroupMeshGen.OnCellCountChanged);
                        }
                        else if (m_sdfGroupMeshGen.CellSizeMode == CellSizeMode.Density)
                        {
                            m_setter.DrawFloatSetting(Labels.VolumeSize, m_serializedProperties.VolumeSize, min: 0.05f, onValueChangedCallback: m_sdfGroupMeshGen.OnDensitySettingChanged);
                            m_setter.DrawFloatSetting(Labels.CellDensity, m_serializedProperties.CellDensity, min: 0.05f, onValueChangedCallback: m_sdfGroupMeshGen.OnDensitySettingChanged);
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
                        m_setter.DrawEnumSetting<IsosurfaceExtractionType>(Labels.IsosurfaceExtractionType, m_serializedProperties.IsosurfaceExtractionType, onValueChangedCallback: m_sdfGroupMeshGen.OnIsosurfaceExtractionTypeChanged);

                        if (m_sdfGroupMeshGen.IsosurfaceExtractionType == IsosurfaceExtractionType.DualContouring)
                            m_setter.DrawFloatSetting(Labels.ConstrainToCellUnits, m_serializedProperties.ConstrainToCellUnits, min: 0f, onValueChangedCallback: m_sdfGroupMeshGen.OnConstrainToCellUnitsChanged);

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Normal Settings", EditorStyles.boldLabel);

                        m_setter.DrawFloatSetting(Labels.MaxAngleTolerance, m_serializedProperties.MaxAngleTolerance, min: 0f, max: 180f, onValueChangedCallback: m_sdfGroupMeshGen.OnMaxAngleToleranceChanged);
                        m_setter.DrawFloatSetting(Labels.VisualNormalSmoothing, m_serializedProperties.VisualNormalSmoothing, min: 1e-5f, max: 10f, onValueChangedCallback: m_sdfGroupMeshGen.OnVisualNormalSmoothingChanged);

                        if (m_sdfGroupMeshGen.IsosurfaceExtractionType == IsosurfaceExtractionType.DualContouring)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("QEF Settings", EditorStyles.boldLabel);

                            m_setter.DrawBoolSetting(Labels.OverrideQEFSettings, m_serializedProperties.OverrideQEFSettings, onValueChangedCallback: m_sdfGroupMeshGen.OnQEFSettingsOverrideChanged);

                            if (m_sdfGroupMeshGen.OverrideQEFSettings)
                            {
                                m_setter.DrawIntSetting(Labels.QEFSweeps, m_serializedProperties.QEFSweeps, min: 1, onValueChangedCallback: m_sdfGroupMeshGen.OnQEFSettingsOverrideChanged);
                                m_setter.DrawFloatSetting(Labels.QEFPseudoInverseThreshold, m_serializedProperties.QEFPseudoInverseThreshold, min: 1e-7f, onValueChangedCallback: m_sdfGroupMeshGen.OnQEFSettingsOverrideChanged);
                            }

                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Nudge Settings", EditorStyles.boldLabel);

                            m_setter.DrawFloatSetting(Labels.NudgeVerticesToAverageNormalScalar, m_serializedProperties.NudgeVerticesToAverageNormalScalar, min: 0f, onValueChangedCallback: m_sdfGroupMeshGen.OnNudgeSettingsChanged);
                            m_setter.DrawFloatSetting(Labels.NudgeMaxMagnitude, m_serializedProperties.NudgeMaxMagnitude, min: 0f, onValueChangedCallback: m_sdfGroupMeshGen.OnNudgeSettingsChanged);
                        }

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Edge Intersection Settings", EditorStyles.boldLabel);

                        m_setter.DrawEnumSetting<EdgeIntersectionType>(Labels.EdgeIntersectionType, m_serializedProperties.EdgeIntersectionType, onValueChangedCallback: m_sdfGroupMeshGen.OnEdgeIntersectionTypeChanged);

                        if (m_sdfGroupMeshGen.EdgeIntersectionType == EdgeIntersectionType.BinarySearch)
                            m_setter.DrawIntSetting(Labels.BinarySearchIterations, m_serializedProperties.BinarySearchIterations, min: 1, onValueChangedCallback: m_sdfGroupMeshGen.OnBinarySearchIterationsChanged);

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Gradient Descent Settings", EditorStyles.boldLabel);

                        m_setter.DrawBoolSetting(Labels.ApplyGradientDescent, m_serializedProperties.ApplyGradientDescent, onValueChangedCallback: m_sdfGroupMeshGen.OnApplyGradientDescentChanged);

                        if (m_sdfGroupMeshGen.ApplyGradientDescent)
                            m_setter.DrawIntSetting(Labels.GradientDescentIterations, m_serializedProperties.GradientDescentIterations, min: 1, onValueChangedCallback: m_sdfGroupMeshGen.OnGradientDescentIterationsChanged);
                    }
                }
            }

            if (m_isDebugSettingsOpen = EditorGUILayout.Foldout(m_isDebugSettingsOpen, Labels.DebugSettings, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        m_setter.DrawProperty(Labels.ShowGrid, m_serializedProperties.ShowGrid);
                    }
                }
            }

            m_setter.Update();
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
}