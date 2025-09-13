namespace StrideEdExt.Rasterization;

public interface IGrid2dCellTraversalVisitor
{
    bool Visit(int x, int y);
}
