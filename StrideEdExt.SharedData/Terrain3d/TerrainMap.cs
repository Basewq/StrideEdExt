using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SceneEditorExtensionExample.SharedData.Terrain3d;

/**
 * This is the custom data as seen at run-time.
 */
[DataContract]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<TerrainMap>), Profile = "Content")]
[ContentSerializer(typeof(DataContentSerializer<TerrainMap>))]
//[ContentSerializer(typeof(TerrainMapContentSerializer))]
//[DataSerializer(typeof(TerrainMapDataSerializer))]
public class TerrainMap
{
    private readonly Dictionary<TerrainChunkIndex2d, TerrainChunk> _chunkIndexToChunkMap = [];

    [DataMemberIgnore]
    public bool IsInitialized { get; private set; }

    [DataMemberIgnore]
    public int LoadedChunkCount => _chunkIndexToChunkMap.Count;

    //public Dictionary<string, Prefab> TileSetNameToTileSetPrefabMap { get; } = [];

    /// <summary>
    /// The maximum map size in terms of quad count.<br/>
    /// Note that the size of <see cref="MapSize"/> is always (<see cref="HeightmapData"/> - <see cref="Int2.One"/>).<br/>
    /// Value should try to be a value of (2^x - 1) or less to minimize unused terrain texture spaces, 
    /// eg. a map size of 15x15 requires a 16x16px texture because each pixel on a texture represents a vertex point.
    /// </summary>
    public Int2 MapSize { get; set; }
    /// <summary>
    /// The number of quads per terrain mesh.
    /// </summary>
    public Int2 QuadPerMesh { get; set; }
    /// <summary>
    /// The world size of a single terrain mesh quad.
    /// </summary>
    public Vector2 MeshQuadSize { get; set; }

    /// <summary>
    /// The number of meshes contained in a single chunk.
    /// </summary>
    public TerrainMeshPerChunk MeshPerChunk { get; set; }

    public Vector2 HeightRange { get; set; }

    /// <summary>
    /// The height values of each vertices that form the quad.<br/>
    /// Note that the size of <see cref="HeightmapData"/> is always (<see cref="MapSize"/> + <see cref="Int2.One"/>).
    /// </summary>
    public Array2d<float>? HeightmapData { get; set; }
    //public Texture? HeightmapTexture { get; set; }

    //public Material? TerrainMaterial { get; set; }

    /// <summary>
    /// The total width/height quad count contained in a single chunk.
    /// </summary>
    public Int2 QuadPerChunk => QuadPerMesh * MeshPerChunk.GetSingleAxisLength();

    public int MaxChunkX => (int)Math.Ceiling(MapSize.X / (float)QuadPerChunk.X);
    public int MaxChunkY => (int)Math.Ceiling(MapSize.Y / (float)QuadPerChunk.Y);

    /// <summary>
    /// Returns the world size of a single chunk.
    /// </summary>
    public Vector2 ChunkWorldSizeVec2
    {
        get
        {
            var chunkSize = MeshQuadSize * (Vector2)QuadPerChunk;
            return chunkSize;
        }
    }

    /// <summary>
    /// Returns the world size of a single mesh within a single chunk.
    /// </summary>
    public Vector2 ChunkMeshWorldSizeVec2
    {
        get
        {
            var chunkSize = MeshQuadSize * (Vector2)QuadPerMesh;
            return chunkSize;
        }
    }

    public void Initialize()
    {
        RebuildChunks();
        IsInitialized = true;
    }

    public void RebuildChunks()
    {
        int maxChunkX = MaxChunkX;
        int maxChunkY = MaxChunkY;
        _chunkIndexToChunkMap.Clear();
        for (int y = 0; y < maxChunkY; y++)
        {
            for (int x = 0; x < maxChunkX; x++)
            {
                var chunkIndex = new TerrainChunkIndex2d(x, y);
                var chunk = new TerrainChunk(this, chunkIndex);
                AddChunk(chunk);
            }
        }
    }

    public IEnumerable<TerrainChunk> GetAllChunks()
    {
        return _chunkIndexToChunkMap.Values;
    }

    public bool TryGetChunk(TerrainChunkIndex2d chunkIndex, [NotNullWhen(true)] out TerrainChunk? chunk)
    {
        if (_chunkIndexToChunkMap.TryGetValue(chunkIndex, out chunk))
        {
            return true;
        }
        chunk = null;
        return false;
    }

    public void AddChunk(TerrainChunk chunk)
    {
        bool wasAdded = _chunkIndexToChunkMap.TryAdd(chunk.ChunkIndex, chunk);
        Debug.Assert(wasAdded);
    }

    public bool TryRemoveChunk(TerrainChunkIndex2d chunkIndex)
    {
        bool wasRemoved = _chunkIndexToChunkMap.Remove(chunkIndex);
        return wasRemoved;
    }

    public Vector3 ToChunkMinimumWorldPosition(TerrainChunkIndex2d chunkIndex, float height = 0)
    {
        var chunkWorldSize = ChunkWorldSizeVec2;
        float x = chunkIndex.X * chunkWorldSize.X;
        float z = chunkIndex.Z * chunkWorldSize.Y;

        return new Vector3(x, height, z);
    }

    public Vector3 ToChunkSubCellMinimumWorldPosition(TerrainChunkIndex2d chunkIndex, TerrainChunkSubCellIndex2d chunkSubCellIndex, float height = 0)
    {
        var chunkMinPos = ToChunkMinimumWorldPosition(chunkIndex, height);
        var meshWorldSize = ChunkMeshWorldSizeVec2;
        float offsetX = chunkSubCellIndex.X * meshWorldSize.X;
        float offsetZ = chunkSubCellIndex.Z * meshWorldSize.Y;

        return new Vector3(chunkMinPos.X + offsetX, height, chunkMinPos.Z + offsetZ);
    }

    public void InvalidateMeshes()
    {
        foreach (var chunk in _chunkIndexToChunkMap.Values)
        {
            int meshPerChunkSingleAxisLength = MeshPerChunk.GetSingleAxisLength();
            for (int chunkSubCellY = 0; chunkSubCellY < meshPerChunkSingleAxisLength; chunkSubCellY++)
            {
                for (int chunkSubCellX = 0; chunkSubCellX < meshPerChunkSingleAxisLength; chunkSubCellX++)
                {
                    var chunkSubCellIndex = new TerrainChunkSubCellIndex2d(chunkSubCellX, chunkSubCellY);
                    var subChunk = chunk.GetSubChunk(chunkSubCellIndex);
                    subChunk.Mesh = null;
                }
            }
        }
    }
}
