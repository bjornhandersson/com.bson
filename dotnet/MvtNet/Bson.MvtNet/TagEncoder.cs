using VectorTile;

namespace Bson.MvtNet;

/// <summary>
/// Manages key/value dictionaries per layer and encodes feature tags as index pairs.
/// </summary>
internal class TagEncoder
{
    private readonly Dictionary<string, int> _keys = new();
    private readonly Dictionary<string, int> _stringValues = new();
    private readonly Dictionary<float, int> _floatValues = new();
    private readonly Dictionary<double, int> _doubleValues = new();
    private readonly Dictionary<long, int> _intValues = new();
    private readonly Dictionary<ulong, int> _uintValues = new();
    private readonly Dictionary<bool, int> _boolValues = new();
    private readonly List<string> _keyList = new();
    private readonly List<Tile.Types.Value> _valueList = new();

    /// <summary>
    /// Encodes a set of key/value pairs into tag index pairs for a feature.
    /// Supported value types: string, bool, all integer primitives, float,
    /// double, decimal (stored as double) and enums (stored by name).
    /// Pairs with a null value are skipped, since MVT has no null tag type.
    /// </summary>
    public uint[] Encode(IEnumerable<KeyValuePair<string, object>> attributes)
    {
        // Fast path for the common Dictionary case where Count is known
        if (attributes is ICollection<KeyValuePair<string, object>> collection)
        {
            var tags = new uint[collection.Count * 2];
            int pos = 0;
            foreach (var pair in collection)
            {
                if (pair.Value is null)
                {
                    continue;
                }
                tags[pos++] = (uint)GetOrAddKey(pair.Key);
                tags[pos++] = (uint)GetOrAddValue(pair.Value);
            }

            if (pos < tags.Length)
            {
                Array.Resize(ref tags, pos);
            }
            return tags;
        }

        var tagList = new List<uint>();
        foreach (var pair in attributes)
        {
            if (pair.Value is null)
            {
                continue;
            }
            tagList.Add((uint)GetOrAddKey(pair.Key));
            tagList.Add((uint)GetOrAddValue(pair.Value));
        }
        return tagList.ToArray();
    }

    public IReadOnlyList<string> Keys => _keyList;
    public IReadOnlyList<Tile.Types.Value> Values => _valueList;

    private int GetOrAddKey(string key)
    {
        if (_keys.TryGetValue(key, out int index))
        {
            return index;
        }

        index = _keyList.Count;
        _keys[key] = index;
        _keyList.Add(key);
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
            _ => throw new ArgumentException(
                $"Unsupported tag value type: {value.GetType().Name}",
                nameof(value)
            ),
        };
    }

    private int GetOrAddStringValue(string s)
    {
        if (_stringValues.TryGetValue(s, out int index))
        {
            return index;
        }

        index = _valueList.Count;
        _stringValues[s] = index;
        _valueList.Add(new Tile.Types.Value { StringValue = s });
        return index;
    }

    private int GetOrAddFloatValue(float f)
    {
        if (_floatValues.TryGetValue(f, out int index))
        {
            return index;
        }

        index = _valueList.Count;
        _floatValues[f] = index;
        _valueList.Add(new Tile.Types.Value { FloatValue = f });
        return index;
    }

    private int GetOrAddDoubleValue(double d)
    {
        if (_doubleValues.TryGetValue(d, out int index))
        {
            return index;
        }

        index = _valueList.Count;
        _doubleValues[d] = index;
        _valueList.Add(new Tile.Types.Value { DoubleValue = d });
        return index;
    }

    private int GetOrAddIntValue(long l)
    {
        if (_intValues.TryGetValue(l, out int index))
        {
            return index;
        }

        index = _valueList.Count;
        _intValues[l] = index;
        _valueList.Add(new Tile.Types.Value { SintValue = l });
        return index;
    }

    private int GetOrAddUintValue(ulong u)
    {
        if (_uintValues.TryGetValue(u, out int index))
        {
            return index;
        }

        index = _valueList.Count;
        _uintValues[u] = index;
        _valueList.Add(new Tile.Types.Value { UintValue = u });
        return index;
    }

    private int GetOrAddBoolValue(bool b)
    {
        if (_boolValues.TryGetValue(b, out int index))
        {
            return index;
        }

        index = _valueList.Count;
        _boolValues[b] = index;
        _valueList.Add(new Tile.Types.Value { BoolValue = b });
        return index;
    }
}
