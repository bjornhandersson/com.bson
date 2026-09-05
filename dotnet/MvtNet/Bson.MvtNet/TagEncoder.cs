using System.Text;

namespace Bson.MvtNet;

internal sealed class TagEncoder
{
    private const byte LayerKeysTag = (3 << 3) | ProtoBuffer.LengthDelimited;
    private const byte LayerValuesTag = (4 << 3) | ProtoBuffer.LengthDelimited;

    private const byte StringValueTag = (1 << 3) | ProtoBuffer.LengthDelimited;
    private const byte FloatValueTag = (2 << 3) | ProtoBuffer.Fixed32;
    private const byte DoubleValueTag = (3 << 3) | ProtoBuffer.Fixed64;
    private const byte UintValueTag = (5 << 3) | ProtoBuffer.Varint;
    private const byte SintValueTag = (6 << 3) | ProtoBuffer.Varint;
    private const byte BoolValueTag = (7 << 3) | ProtoBuffer.Varint;

    private readonly Dictionary<string, int> _keys = new();
    private readonly Dictionary<string, int> _stringValues = new();
    private readonly Dictionary<float, int> _floatValues = new();
    private readonly Dictionary<double, int> _doubleValues = new();
    private readonly Dictionary<long, int> _intValues = new();
    private readonly Dictionary<ulong, int> _uintValues = new();
    private readonly Dictionary<bool, int> _boolValues = new();
    private readonly ProtoBuffer _encodedKeys = new();
    private readonly ProtoBuffer _encodedValues = new();
    private int _keyCount;
    private int _valueCount;

    public ProtoBuffer EncodedKeys => _encodedKeys;

    public ProtoBuffer EncodedValues => _encodedValues;

    public void WriteTags<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> attributes,
        ProtoBuffer tags
    )
    {
        foreach (var pair in attributes)
        {
            if (pair.Value is null)
            {
                continue;
            }

            tags.WriteVarint((uint)GetOrAddKey(pair.Key));
            tags.WriteVarint((uint)GetOrAddValue(pair.Value));
        }
    }

    private int GetOrAddKey(string key)
    {
        int index = GetOrAddIndex(_keys, key, ref _keyCount, out bool added);
        if (added)
        {
            _encodedKeys.WriteByte(LayerKeysTag);
            _encodedKeys.WriteString(key);
        }

        return index;
    }

    private static int GetOrAddIndex<TKey>(
        Dictionary<TKey, int> map,
        TKey key,
        ref int count,
        out bool added
    )
        where TKey : notnull
    {
        if (map.TryGetValue(key, out int index))
        {
            added = false;
            return index;
        }

        index = count++;
        map.Add(key, index);
        added = true;
        return index;
    }

    private int GetOrAddValue(object value)
    {
        return value switch
        {
            string s => GetOrAddStringValue(s),
            int i => GetOrAddIntValue(i),
            long l => GetOrAddIntValue(l),
            double d => GetOrAddDoubleValue(d),
            float f => GetOrAddFloatValue(f),
            bool b => GetOrAddBoolValue(b),
            short sh => GetOrAddIntValue(sh),
            byte by => GetOrAddIntValue(by),
            sbyte sb => GetOrAddIntValue(sb),
            ushort us => GetOrAddIntValue(us),
            uint ui => GetOrAddIntValue(ui),
            ulong ul => GetOrAddUintValue(ul),
            decimal m => GetOrAddDoubleValue((double)m),
            Enum e => GetOrAddStringValue(e.ToString()),
            char c => GetOrAddStringValue(c.ToString()),
            Guid g => GetOrAddStringValue(g.ToString()),
            DateTime dt => GetOrAddStringValue(dt.ToString("o")),
            DateTimeOffset dto => GetOrAddStringValue(dto.ToString("o")),
            _ => throw new ArgumentException(
                $"Unsupported tag value type: {value.GetType().Name}",
                nameof(value)
            ),
        };
    }

    private int GetOrAddStringValue(string s)
    {
        int index = GetOrAddIndex(_stringValues, s, ref _valueCount, out bool added);
        if (added)
        {
            int bytes = Encoding.UTF8.GetByteCount(s);
            WriteValueHeader(StringValueTag, 1 + ProtoBuffer.VarintSize((ulong)bytes) + bytes);
            _encodedValues.WriteString(s);
        }

        return index;
    }

    private int GetOrAddFloatValue(float f)
    {
        int index = GetOrAddIndex(_floatValues, f, ref _valueCount, out bool added);
        if (added)
        {
            WriteValueHeader(FloatValueTag, 1 + sizeof(float));
            _encodedValues.WriteFixed32(BitConverter.ToUInt32(BitConverter.GetBytes(f), 0));
        }

        return index;
    }

    private int GetOrAddDoubleValue(double d)
    {
        int index = GetOrAddIndex(_doubleValues, d, ref _valueCount, out bool added);
        if (added)
        {
            WriteValueHeader(DoubleValueTag, 1 + sizeof(double));
            _encodedValues.WriteFixed64((ulong)BitConverter.DoubleToInt64Bits(d));
        }

        return index;
    }

    private int GetOrAddIntValue(long l)
    {
        int index = GetOrAddIndex(_intValues, l, ref _valueCount, out bool added);
        if (added)
        {
            ulong zigzag = (ulong)((l << 1) ^ (l >> 63));
            WriteValueHeader(SintValueTag, 1 + ProtoBuffer.VarintSize(zigzag));
            _encodedValues.WriteVarint(zigzag);
        }

        return index;
    }

    private int GetOrAddUintValue(ulong u)
    {
        int index = GetOrAddIndex(_uintValues, u, ref _valueCount, out bool added);
        if (added)
        {
            WriteValueHeader(UintValueTag, 1 + ProtoBuffer.VarintSize(u));
            _encodedValues.WriteVarint(u);
        }

        return index;
    }

    private int GetOrAddBoolValue(bool b)
    {
        int index = GetOrAddIndex(_boolValues, b, ref _valueCount, out bool added);
        if (added)
        {
            WriteValueHeader(BoolValueTag, 1 + sizeof(byte));
            _encodedValues.WriteByte(b ? (byte)1 : (byte)0);
        }

        return index;
    }

    private void WriteValueHeader(byte valueFieldTag, int bodySize)
    {
        _encodedValues.WriteByte(LayerValuesTag);
        _encodedValues.WriteVarint((ulong)bodySize);
        _encodedValues.WriteByte(valueFieldTag);
    }
}
