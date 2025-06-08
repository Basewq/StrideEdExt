using Stride.Core;
using Stride.Core.Mathematics;
using System.Collections;
using System.Diagnostics;
using System.Text;

namespace SceneEditorExtensionExample.SharedData;

[DataContract]
[DebuggerDisplay("{DebugDisplayString,nq}")]
public class Array2d<T> : IEnumerable<KeyValuePair<Int2, T>>
{
    public delegate bool Array2dPredicate(in T item);

    private T[][] _array2d;

    public int LengthX { get; private set; }
    public int LengthY { get; private set; }

    public Int2 Length2d => new Int2(LengthX, LengthY);

    public Array2d()
    {
        _array2d = [];
    }

    public Array2d(Int2 size) : this(size.X, size.Y) { }

    public Array2d(int lengthX, int lengthY)
    {
        _array2d = new T[lengthY][];
        for (int y = 0; y < lengthY; y++)
        {
            _array2d[y] = new T[lengthX];
        }
        LengthX = lengthX;
        LengthY = lengthY;
    }

    public Array2d(int lengthX, int lengthY, T initialValue)
        : this(lengthX, lengthY)
    {
        for (int y = 0; y < lengthY; y++)
        {
            for (int x = 0; x < lengthX; x++)
            {
                _array2d[y][x] = initialValue;
            }
        }
    }

    public Array2d(int lengthX, int lengthY, Func<T> initialValueFunc)
        : this(lengthX, lengthY)
    {
        for (int y = 0; y < lengthY; y++)
        {
            for (int x = 0; x < lengthX; x++)
            {
                _array2d[y][x] = initialValueFunc();
            }
        }
    }

    public ref T this[in Int2 index]
    {
        get
        {
            return ref _array2d[index.Y][index.X];
        }
    }

    public ref T this[int x, int y]
    {
        get
        {
            return ref _array2d[y][x];
        }
    }

    public void Resize(Int2 size) => Resize(size.X, size.Y);

    public void Resize(int lengthX, int lengthY)
    {
        var prevArray2d = _array2d;
        int prevLengthX = LengthX;
        int prevLengthY = LengthY;

        // Copy existing data, if possible, any excess data is effectively truncated.
        int copyLengthX = Math.Min(lengthX, prevLengthX);
        int copyLengthY = Math.Min(lengthY, prevLengthY);

        _array2d = new T[lengthY][];
        for (int y = 0; y < lengthY; y++)
        {
            _array2d[y] = new T[lengthX];

            if (y < copyLengthY)
            {
                for (int x = 0; x < copyLengthX; x++)
                {
                    _array2d[y][x] = prevArray2d[y][x];
                }
                prevArray2d[y] = null!;     // Dereference the old array to allow memory to be reclaimed
            }
        }
        LengthX = lengthX;
        LengthY = lengthY;
    }

    /// <remarks>
    /// This method is only applicable if T is a struct.
    /// </remarks>
    public void Clear(T defaultItem = default!)
    {
        for (int y = 0; y < LengthY; y++)
        {
            for (int x = 0; x < LengthX; x++)
            {
                _array2d[y][x] = defaultItem;
            }
        }
    }

    public void Clear(Func<T> valueFunc)
    {
        for (int y = 0; y < LengthY; y++)
        {
            for (int x = 0; x < LengthX; x++)
            {
                _array2d[y][x] = valueFunc();
            }
        }
    }

    public bool Contains(Array2dPredicate predicate)
    {
        for (int y = 0; y < LengthY; y++)
        {
            for (int x = 0; x < LengthX; x++)
            {
                ref var item = ref _array2d[y][x];
                if (predicate(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int GetItemCount(Array2dPredicate predicate)
    {
        int itemCount = 0;
        for (int y = 0; y < LengthY; y++)
        {
            for (int x = 0; x < LengthX; x++)
            {
                ref var item = ref _array2d[y][x];
                if (predicate(item))
                {
                    itemCount++;
                }
            }
        }

        return itemCount;
    }

    [Conditional("DEBUG")]
    public void PrintToDebug()
    {
        var sb = new StringBuilder();
        sb.AppendLine(ToString());
        for (int y = 0; y < LengthY; y++)
        {
            for (int x = 0; x < LengthX; x++)
            {
                if (x > 0)
                {
                    sb.Append('\t');
                }
                ref var item = ref _array2d[y][x];
                sb.Append(item?.ToString() ?? "[NULL]");
            }
            sb.AppendLine();
        }
        Debug.WriteLine(sb.ToString());
    }

    internal string DebugDisplayString => ToString();

    public override string ToString()
    {
        return $"Array2d ({LengthX}, {LengthY})";
    }

    public ArrayItemEnumerator GetEnumerator() => new(this);

    IEnumerator<KeyValuePair<Int2, T>> IEnumerable<KeyValuePair<Int2, T>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct ArrayItemEnumerator : IEnumerator<KeyValuePair<Int2, T>>
    {
        private readonly Array2d<T> _array2d;
        private int _curX, _curY;

        public ArrayItemEnumerator(Array2d<T> array3d)
        {
            _array2d = array3d;
            Reset();
        }

        public readonly KeyValuePair<Int2, T> Current
        {
            get
            {
                var index = new Int2(_curX, _curY);
                ref var item = ref _array2d[index];
                return new(index, item);
            }
        }

        readonly object IEnumerator.Current => Current;

        public readonly void Dispose() { }

        public bool MoveNext()
        {
            _curX++;
            if (_curX >= _array2d.LengthX)
            {
                _curX = 0;

                _curY++;
                if (_curY >= _array2d.LengthY)
                {
                    return false;
                }
            }
            return true;
        }

        public void Reset()
        {
            _curX = -1;
            _curY = 0;
        }
    }
}
