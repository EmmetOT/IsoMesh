using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IsoMesh
{
    [System.Serializable]
    public struct SDFMaterial
    {
        public const float MIN_SMOOTHING = 0.0001f;

        [SerializeField]
        [ColorUsage(showAlpha: false)]
        private Color m_colour;
        public Color Colour => m_colour;

        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        private Color m_emission;
        public Color Emission => m_emission;

        //[SerializeField]
        //private float m_materialSmoothing;
        //public float MaterialSmoothing => m_materialSmoothing;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_metallic;
        public float Metallic => m_metallic;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_smoothness;
        public float Smoothness => m_smoothness;

        public SDFMaterial(Color mainCol, Color emission, float metallic, float smoothness)
        {
            m_colour = mainCol;
            m_emission = emission;
            m_metallic = metallic;
            m_smoothness = smoothness;
        }
    }
    
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SDFMaterialGPU
    {
        public static int Stride => sizeof(float) * 8;

        public Vector3 Colour;
        public Vector3 Emission;
        //public float MaterialSmoothing;
        public float Metallic;
        public float Smoothness;

        public SDFMaterialGPU(SDFMaterial material)
        {
            Colour = (Vector4)material.Colour;
            Emission = (Vector4)material.Emission;
            //MaterialSmoothing = material.MaterialSmoothing;//Mathf.Max(SDFMaterial.MIN_SMOOTHING, material.MaterialSmoothing);
            Metallic = Mathf.Clamp01(material.Metallic);
            Smoothness = Mathf.Clamp01(material.Smoothness);
        }
    }
}