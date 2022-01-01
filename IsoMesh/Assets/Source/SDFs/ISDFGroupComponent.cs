using UnityEngine;

namespace IsoMesh
{
    public interface ISDFGroupComponent
    {
        void UpdateSettingsBuffer(ComputeBuffer computeBuffer);
        void UpdateDataBuffers(ComputeBuffer computeBuffer, ComputeBuffer materialBuffer, int count);
        
        void Run();

        void OnEmpty();
        void OnNotEmpty();
    }
}