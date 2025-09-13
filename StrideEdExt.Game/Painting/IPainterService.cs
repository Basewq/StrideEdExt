using Stride.Core.Mathematics;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.Painting;

public interface IPainterService
{
    /// <summary>
    /// Creates a painting session, allocating memory for textures to read/write to.
    /// </summary>
    /// <param name="editorEntityId">The editor entity this session belongs to.</param>
    /// <returns>The <see cref="PaintSessionId"/> for this session.</returns>
    PaintSessionId BeginSession(Guid editorEntityId);
    /// <summary>
    /// Ends the session and deallocates all memory used in this session.
    /// </summary>
    void EndSession(PaintSessionId paintSessionId);

    bool TryGetActiveSessionId(out PaintSessionId paintSessionId);
    void SetActiveSessionId(PaintSessionId paintSessionId);

    bool TryGetActiveTool(PaintSessionId paintSessionId, [NotNullWhen(true)] out IPainterTool? painterTool);
    void SetActiveTool(PaintSessionId paintSessionId, IPainterTool painterTool);

    //void UpdateBrush(PaintSessionId paintSessionId, PaintBrushSettings brushSettings);

    /// <summary>
    /// Begins a brushstroke action. This usually corresponds to the mouse button pressed on the object.
    /// </summary>
    PaintBrushstrokeHandle BeginBrushstroke(PaintSessionId paintSessionId, Vector2 brushScreenPositionNormalized);
    /// <summary>
    /// Add the brush to the current mouse location.
    /// </summary>
    void AddBrushstrokePoint(PaintBrushstrokeHandle brushstrokeHandle, Vector2 brushScreenPositionNormalized);
    /// <summary>
    /// Ends a brushstroke action. This usually corresponds to the mouse button released.
    /// </summary>
    void EndBrushstroke(PaintBrushstrokeHandle brushstrokeHandle);


    // THREE PHASES
    // Picking
    // Brush map/Stroke map
    // Composite
}
