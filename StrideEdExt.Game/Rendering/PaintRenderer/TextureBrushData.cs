using Stride.Core.Mathematics;
using System.Runtime.InteropServices;

namespace StrideEdExt.Rendering;

// The generated shader key will be public, so this struct must also be public.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TextureBrushData
{
    /// <summary>
    /// Brush's position in the mesh's local space.
    /// </summary>
    public Vector3 WorldPosition;
    /// <summary>
    /// Brush's normal vector in the mesh's local space.
    /// </summary>
    public Vector3 WorldNormal;
    public Matrix BrushWorldInverse;
    public float Strength;
}
