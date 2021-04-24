using UnityEngine;

public interface ISDFGroupComponent
{
    // update methods are only called when buffer is created or size changes
    void UpdatePrimitivesDataBuffer(ComputeBuffer computeBuffer, int count);
    void UpdateSettingsBuffer(ComputeBuffer computeBuffer);
    void UpdateMeshSamplesBuffer(ComputeBuffer samplesBuffer, ComputeBuffer packedUVsBuffer);
    void UpdateMeshMetadataBuffer(ComputeBuffer computeBuffer, int count);

    void Run();

    void OnEmpty();
    void OnNotEmpty();
}