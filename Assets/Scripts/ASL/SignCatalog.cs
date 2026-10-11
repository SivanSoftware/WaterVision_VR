using System.Collections.Generic;
using UnityEngine;

namespace WaterVision.ASL
{
    /// <summary>
    /// Single ScriptableObject that holds the entire ASL sign vocabulary +
    /// stoplist + aliases. Build via:
    ///   Tools → ASL → Seed Sign Catalog
    /// Edit individual signs in Inspector. Hot-reloads in Play mode.
    /// </summary>
    [CreateAssetMenu(fileName = "SignCatalog", menuName = "WaterVision/ASL/Sign Catalog")]
    public class SignCatalog : ScriptableObject
    {
        public List<SignDefinition> signs = new();
        public List<StringPair>     aliases = new();
        public List<string>         stoplist = new();

        Dictionary<string, SignDefinition> _lookup;
        Dictionary<string, string>         _aliasMap;
        HashSet<string>                    _stop;

        void OnEnable() => RebuildIndices();

        public void RebuildIndices()
        {
            _lookup = new Dictionary<string, SignDefinition>();
            foreach (var s in signs)
            {
                if (string.IsNullOrWhiteSpace(s.word)) continue;
                var key = s.word.ToLowerInvariant().Trim();
                _lookup[key] = s;
            }

            _aliasMap = new Dictionary<string, string>();
            foreach (var p in aliases)
            {
                if (string.IsNullOrWhiteSpace(p.from) || string.IsNullOrWhiteSpace(p.to)) continue;
                _aliasMap[p.from.ToLowerInvariant().Trim()] = p.to.ToLowerInvariant().Trim();
            }

            _stop = new HashSet<string>();
            foreach (var w in stoplist)
            {
                if (string.IsNullOrWhiteSpace(w)) continue;
                _stop.Add(w.ToLowerInvariant().Trim());
            }
        }

        /// <summary>True if `word` should never be signed (article, filler, etc).</summary>
        public bool IsStopWord(string word)
        {
            if (_stop == null) RebuildIndices();
            return _stop.Contains(word.ToLowerInvariant().Trim());
        }

        /// <summary>
        /// Try resolve a word to a Sign — checks direct lookup AND alias map.
        /// Strips non-alpha first ("hello." → "hello").
        /// </summary>
        public bool TryResolve(string word, out SignDefinition def)
        {
            if (_lookup == null) RebuildIndices();

            string clean = Clean(word);
            if (string.IsNullOrEmpty(clean)) { def = default; return false; }

            if (_lookup.TryGetValue(clean, out def)) return true;

            if (_aliasMap.TryGetValue(clean, out var alias))
                if (_lookup.TryGetValue(alias, out def)) return true;

            def = default;
            return false;
        }

        /// <summary>Lowercase + strip non-[a-z] (so "Hello!" → "hello").</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                char lc = char.ToLowerInvariant(c);
                if (lc >= 'a' && lc <= 'z') sb.Append(lc);
            }
            return sb.ToString();
        }

        public int SignCount => signs?.Count ?? 0;
    }
}
