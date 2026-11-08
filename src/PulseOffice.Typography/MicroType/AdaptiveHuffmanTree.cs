namespace PulseOffice.Typography.MicroType;

internal sealed class AdaptiveHuffmanTree
{
    private const int Root = 1;

    private readonly int _range;
    private readonly short[] _up;
    private readonly short[] _left;
    private readonly short[] _right;
    private readonly short[] _code;
    private readonly long[] _weight;
    private readonly short[] _symbolIndex;
    private readonly BitReader _bits;

    public AdaptiveHuffmanTree(BitReader bits, int range)
    {
        ArgumentNullException.ThrowIfNull(bits);
        if (range is < 2 or > 16384) throw new InvalidDataException("Adaptive Huffman alphabet size is out of range.");

        _bits = bits;
        _range = range;
        var nodes = 2 * range;
        _up = new short[nodes];
        _left = new short[nodes];
        _right = new short[nodes];
        _code = new short[nodes];
        _weight = new long[nodes];
        _symbolIndex = new short[range];

        _weight[0] = long.MaxValue;
        for (var index = 2; index < nodes; index++)
        {
            _up[index] = (short)(index / 2);
            _weight[index] = 1;
        }

        for (var index = 1; index < range; index++)
        {
            _left[index] = (short)(2 * index);
            _right[index] = (short)((2 * index) + 1);
        }

        for (var index = 0; index < range; index++)
        {
            _code[index] = -1;
            _code[range + index] = (short)index;
            _left[range + index] = -1;
            _right[range + index] = -1;
            _symbolIndex[index] = (short)(range + index);
        }

        for (var index = range - 1; index >= Root; index--)
            _weight[index] = _weight[_left[index]] + _weight[_right[index]];

        Prime();
    }

    public int ReadSymbol()
    {
        var node = Root;
        do
        {
            node = _bits.ReadBit() ? _right[node] : _left[node];
        }
        while (_code[node] < 0);

        var symbol = _code[node];
        Update(node);
        return symbol;
    }

    private void Prime()
    {
        if (_range is > 256 and < 512)
        {
            Update(_symbolIndex[256]);
            Update(_symbolIndex[257]);
            for (var index = 0; index < 12; index++) Update(_symbolIndex[_range - 3]);
            for (var index = 0; index < 6; index++) Update(_symbolIndex[_range - 2]);
            return;
        }

        for (var pass = 0; pass < 2; pass++)
            for (var symbol = 0; symbol < _range; symbol++)
                Update(_symbolIndex[symbol]);
    }

    private void Update(int node)
    {
        for (; node != Root; node = _up[node])
        {
            var weight = _weight[node];
            var leader = node - 1;
            if (_weight[leader] == weight)
            {
                do
                {
                    leader--;
                }
                while (_weight[leader] == weight);

                leader++;
                if (leader > Root)
                {
                    Swap(node, leader);
                    node = leader;
                }
            }

            _weight[node] = weight + 1;
        }

        _weight[Root]++;
    }

    private void Swap(int first, int second)
    {
        (_code[first], _code[second]) = (_code[second], _code[first]);
        (_left[first], _left[second]) = (_left[second], _left[first]);
        (_right[first], _right[second]) = (_right[second], _right[first]);
        (_weight[first], _weight[second]) = (_weight[second], _weight[first]);
        Relink(first);
        Relink(second);
    }

    private void Relink(int node)
    {
        var code = _code[node];
        if (code < 0)
        {
            _up[_left[node]] = (short)node;
            _up[_right[node]] = (short)node;
        }
        else
        {
            _symbolIndex[code] = (short)node;
        }
    }
}
