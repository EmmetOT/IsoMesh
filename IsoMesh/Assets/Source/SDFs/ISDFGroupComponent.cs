using UnityEngine;

public interface ISDFGroupComponent
{
    // update methods are only called when buffer is created or size changes
    void UpdateSettingsBuffer(ComputeBuffer computeBuffer);
    void UpdateGlobalMeshDataBuffers(ComputeBuffer samplesBuffer, ComputeBuffer packedUVsBuffer);

    void UpdateDataBuffer(ComputeBuffer computeBuffer, int count);
    
    //void UpdatePrimitivesDataBuffer(ComputeBuffer computeBuffer, int count);
    //void UpdateMeshMetadataBuffer(ComputeBuffer computeBuffer, int count);

    void Run();

    void OnEmpty();
    void OnNotEmpty();
}