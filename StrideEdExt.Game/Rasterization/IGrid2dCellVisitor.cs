namespace StrideEdExt.Rasterization;

public interface IGrid2dCellVisitor
{
    void Visit(int x, int y);
}
