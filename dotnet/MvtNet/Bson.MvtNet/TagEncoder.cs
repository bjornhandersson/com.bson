using Google.Protobuf.Collections;
#if !NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif
using VectorTile;

namespace Bson.MvtNet;

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

    public void EncodeInto(
        IEnumerable<KeyValuePair<string, object>> attributes,
        RepeatedField<uint> tags
    )
    {
        if (attributes is ICollection<KeyValuePair<string, object>> collection)
        {
            tags.Capacity = Math.Max(tags.Capacity, tags.Count + collection.Count * 2);
        }

        foreach (var pair in attributes)
        {
            if (pair.Value is null)
            {
                continue;
            }

            tags.Add((uint)GetOrAddKey(pair.Key));
            tags.Add((uint)GetOrAddValue(pair.Value));
        }
    }

    public IReadOnlyList<string> Keys => _keyList;
    public IReadOnlyList<Tile.Types.Value> Values => _valueList;

    private int GetOrAddKey(string key)
    {
#if NETSTANDARD2_0
        if (_keys.TryGetValue(key, out int index))
        {
            return index;
        }

        index = _keyList.Count;
        _keys[key] = index;
        _keyList.Add(key);
        return index;
#else
        ref int slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_keys, key, out bool existed);
        if (existed)
        {
            return slot;
        }

        slot = _keyList.Count;
        _keyList.Add(key);
        return slot;
#endif
    }

    private int Intern<TKey>(
        Dictionary<TKey, int> map,
        TKey key,
        Func<TKey, Tile.Types.Value> make
    )
        where TKey : notnull
    {
#if NETSTANDARD2_0
        if (map.TryGetValue(key, out int index))
        {
            return index;
        }

        index = _valueList.Count;
        map[key] = index;
        _valueList.Add(make(key));
        return index;
#else
        ref int slot = ref CollectionsMarshal.GetValueRefOrAddDefault(map, key, out bool existed);
        if (existed)
        {
            return slot;
        }

        slot = _valueList.Count;
        _valueList.Add(make(key));
        return slot;
#endif
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

    private int GetOrAddStringValue(string s) =>
        Intern(_stringValues, s, static v => new Tile.Types.Value { StringValue = v });

    private int GetOrAddFloatValue(float f) =>
        Intern(_floatValues, f, static v => new Tile.Types.Value { FloatValue = v });

    private int GetOrAddDoubleValue(double d) =>
        Intern(_doubleValues, d, static v => new Tile.Types.Value { DoubleValue = v });

    private int GetOrAddIntValue(long l) =>
        Intern(_intValues, l, static v => new Tile.Types.Value { SintValue = v });

    private int GetOrAddUintValue(ulong u) =>
        Intern(_uintValues, u, static v => new Tile.Types.Value { UintValue = v });

    private int GetOrAddBoolValue(bool b) =>
        Intern(_boolValues, b, static v => new Tile.Types.Value { BoolValue = v });
}
