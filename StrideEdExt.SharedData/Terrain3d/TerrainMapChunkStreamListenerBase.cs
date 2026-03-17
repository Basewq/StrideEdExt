using Stride.Core;
using Stride.Engine;

namespace StrideEdExt.SharedData.Terrain3d;

[DataContract(Inherited = true)]
public abstract class TerrainMapChunkStreamListenerBase : EntityComponent
{
    //public virtual void OnChunkLoading(TerrainChunkIndex2d chunkIndex) { }
    //public virtual void OnChunkUnloading(TerrainChunkIndex2d chunkIndex, TerrainChunk chunk) {}
    public virtual void OnChunkVisible(TerrainMap terrainMap, TerrainChunkIndex2d chunkIndex, TerrainChunk chunk) {}
    public virtual void OnChunkNotVisible(TerrainMap terrainMap, TerrainChunkIndex2d chunkIndex) { }
}
