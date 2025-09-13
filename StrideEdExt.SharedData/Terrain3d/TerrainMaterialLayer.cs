using Stride.Core;
using Stride.Graphics;

namespace StrideEdExt.SharedData.Terrain3d;

[DataContract]
public class TerrainMaterialLayer
{
    public string? MaterialName { get; set; }
    public Texture? DiffuseMap {get;set;}
    public Texture? NormalMap {get;set;}
}
