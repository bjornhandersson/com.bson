using VectorTile;

namespace MvtNet;

/// <summary>
/// Manages key/value dictionaries per layer and encodes feature tags as index pairs.
/// </summary>
public class TagEncoder
{
    private readonly Dictionary<string, int> _keys = new();
    private readonly Dictionary<string, int> _stringValues = new();
    private readonly Dictionary<double, int> _doubleValues = new();
    private readonly Dictionary<long, int> _intValues = new();
    private readonly Dictionary<bool, int> _boolValues = new();
    private readonly List<string> _keyList = new();
    private readonly List<Tile.Types.Value> _valueList = new();

    /// <summary>
    /// Encodes a set of key/value pairs into tag index pairs for a feature.
    /// Supported value types: string, int/long, double/float, bool.
    /// </summary>
    public uint[] Encode(IEnumerable<KeyValuePair<string, object>> attributes)
    {
        var tags = new List<uint>();

        foreach (var (key, value) in attributes)
        {
            int keyIndex = GetOrAddKey(key);
            int valueIndex = GetOrAddValue(value);
            tags.Add((uint)keyIndex);
            tags.Add((uint)valueIndex);
        }

        return tags.ToArray();
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
            float f => GetOrAddDoubleValue(f),
            bool b => GetOrAddBoolValue(b),
            _ => throw new ArgumentException($"Unsupported tag value type: {value.GetType().Name}", nameof(value))
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
