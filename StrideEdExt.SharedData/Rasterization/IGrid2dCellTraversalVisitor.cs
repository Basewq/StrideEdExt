namespace StrideEdExt.SharedData.Rasterization;

public interface IGrid2dCellTraversalVisitor
{
    /// <summary>
    /// Returns <c>true</c> if this visitor should continue visiting.
    /// </summary>
    bool Visit(int x, int y);
}
