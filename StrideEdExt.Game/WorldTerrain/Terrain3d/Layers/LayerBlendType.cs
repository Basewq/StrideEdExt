using System.Runtime.Serialization;

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d.Layers;

[DataContract]
public enum LayerBlendType
{
    Average,
    Minimum,
    Maximum
}
