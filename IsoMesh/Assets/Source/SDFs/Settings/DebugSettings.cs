using UnityEngine;

namespace IsoMesh
{
    [System.Serializable]
    public class DebugSettings
    {
        [SerializeField]
        private int m_showIterations = 0;
        public bool ShowIteration(int iteration) => ((1 << iteration) & m_showIterations) != 0;

        [SerializeField]
        private bool m_showDistances = false;
        public bool ShowDistances => m_showDistances;

        public void CopySettings(DebugSettings source)
        {
            m_showIterations = source.m_showIterations;
            m_showDistances = source.m_showDistances;
        }

    }
}