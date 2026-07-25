using UnityEngine;

/// <summary>
/// Deterministic sci-fi designations for territories and stars, derived from the
/// map seed so a given map always names things the same way.
/// </summary>
public static class StarMapNaming
{
    static readonly string[] Prefixes =
    {
        "VEGA", "LYRA", "ORIN", "TAURI", "ZENTH", "NOXIS", "CAELUM", "ARCTUR",
        "DRACO", "HELIOS", "KEPLER", "MIRA", "PHOEN", "RIGEL", "SERAPH", "THALOS",
    };

    static readonly string[] Suffixes =
    {
        "SECTOR", "REACH", "EXPANSE", "DRIFT", "VERGE", "SPUR", "FIELD", "MARCHES",
    };

    static readonly string[] Designators =
    {
        "HD", "KX", "GL", "PSR", "NGC", "TYC", "WR", "BD",
    };

    public static string Territory(int seed, int index)
    {
        int a = Hash(seed, index, 11);
        int b = Hash(seed, index, 23);

        // Roman numeral tail keeps names unique when prefixes collide.
        string prefix = Prefixes[a % Prefixes.Length];
        string suffix = Suffixes[b % Suffixes.Length];
        return string.Format("{0} {1} {2}", prefix, suffix, Roman(index + 1));
    }

    public static string Star(int seed, int territoryIndex, int starIndex)
    {
        int a = Hash(seed, territoryIndex * 397 + starIndex, 31);
        int b = Hash(seed, territoryIndex * 397 + starIndex, 57);

        return string.Format("{0}-{1}", Designators[a % Designators.Length], 1000 + b % 8999);
    }

    public static string TypeLabel(StarMapNodeType type)
    {
        switch (type)
        {
            case StarMapNodeType.Start: return "STAGING POINT";
            case StarMapNodeType.Elite: return "HOSTILE";
            case StarMapNodeType.Shop:  return "TRADE POST";
            case StarMapNodeType.Boss:  return "CAPITAL WORLD";
            default:                    return "UNCHARTED";
        }
    }

    static string Roman(int value)
    {
        // Only ever needs small numbers — one territory index.
        string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII",
                              "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI" };
        return value >= 1 && value <= numerals.Length ? numerals[value - 1] : value.ToString();
    }

    static int Hash(int seed, int a, int b)
    {
        unchecked
        {
            int h = seed ^ (a * 668265263) ^ (b * 374761393);
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return h & 0x7FFFFFFF;
        }
    }
}
