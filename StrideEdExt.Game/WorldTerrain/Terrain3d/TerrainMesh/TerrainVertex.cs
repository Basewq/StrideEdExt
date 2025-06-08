using System.Runtime.InteropServices;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace SceneEditorExtensionExample.WorldTerrain.TerrainMesh;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TerrainVertex : IVertex
{
    public static readonly VertexDeclaration Layout = CreateVertexDeclaration();
    private static VertexDeclaration CreateVertexDeclaration()
    {
        var vertElems = new VertexElement[]
        {
            VertexElement.Position<Vector3>(),
            VertexElement.Normal<Vector3>(),
            //VertexElement.BiTangent<Vector3>(),
            //VertexElement.Tangent<Vector3>(),
            
            // Colors
            VertexElement.Color<Color>(0),

            // Texture Coordinates
            VertexElement.TextureCoordinate<Vector2>(0),
            //VertexElement.TextureCoordinate<Vector2>(1),
            //VertexElement.TextureCoordinate<Vector2>(2),

            //// Alpha Mask Coordinates
            //VertexElement.TextureCoordinate<Vector2>(3),
            //VertexElement.TextureCoordinate<Vector2>(4),

            //// Normal Map Coordinates
            //VertexElement.TextureCoordinate<Vector2>(6),
            //VertexElement.TextureCoordinate<Vector2>(7),
            //VertexElement.TextureCoordinate<Vector2>(8),
        };
        return new VertexDeclaration(vertElems);
    }

    private static readonly Vector2 UnsetVector2 = new Vector2(-1);

    public Vector3 Position;
    public Vector3 Normal;
    //public Vector3 Binormal;
    //public Vector3 Tangent;

    public Color Color;

    public Vector2 TextureCoords0;
    //public Vector2 TextureCoords1;
    //public Vector2 TextureCoords2;

    //public Vector2 AlphaMaskCoords0;
    //public Vector2 AlphaMaskCoords1;

    //public Vector2 NormalMapCoords0;
    //public Vector2 NormalMapCoords1;
    //public Vector2 NormalMapCoords2;

    public readonly VertexDeclaration GetLayout() => Layout;

    public void FlipWinding()
    {
        TextureCoords0.X = (1.0f - TextureCoords0.X);
        //TextureCoords1.X = (1.0f - TextureCoords1.X);
        //TextureCoords2.X = (1.0f - TextureCoords2.X);
    }
}
