using System;

namespace SceneEditorExtensionExample.Rendering.Meshes;

[Flags]
public enum MeshFace
{
    Unknown             = 0,

    North               = 1 << 1,
    East                = 1 << 2,
    South               = 1 << 3,
    West                = 1 << 4,
    Top                 = 1 << 5,
    Bottom              = 1 << 6,

    HorizontalSides     = North | East | South | West,
    AnySide             = North | East | South | West | Top | Bottom,
}
