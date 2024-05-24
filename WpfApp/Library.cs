using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Entry
{
    public string[] trad;
    public string[] simp;
    public string[] pinyin;
    public string[] english;
}

class CharEntry
{
    public List<Entry> e;
    public string[] s;
    public static CharEntry AddTo(Dictionary<string, CharEntry> dict, string character, Entry e)
    {
        CharEntry ce;
        if (dict.TryGetValue(character, out ce))
        {
            if(!ce.e.Contains(e))
                ce.e.Add(e);
        }
        else
        {
            ce = new CharEntry { e = new List<Entry> { e } };
            dict.Add(character, ce);
        }
        return ce;
    }
}

class PinyinEntry
{
    public List<Entry> e;

    public static PinyinEntry AddTo(Dictionary<string, PinyinEntry> dict, string pinyin, Entry e)
    {
        PinyinEntry pe;
        if (dict.TryGetValue(pinyin, out pe))
        {
            pe.e.Add(e);
        }
        else
        {
            pe = new PinyinEntry { e = new List<Entry> { e } };
            dict.Add(pinyin, pe);
        }
        return pe;
    }
}

class Context
{
    static readonly Regex regex = new Regex(@"^(?<trad>\S+)\s(?<simp>\S+)\s\[(?<pinyin>[^\]]+)\]\s/(?<english>.+)/$", RegexOptions.Compiled);

    const string toneVowels = "aāáǎàeēéěèiīíǐìoōóǒòuūúǔùüǖǘǚǜ";

    public List<Entry> entries;
    public Dictionary<string, CharEntry> characters;
    public Dictionary<string, PinyinEntry> pinyins;
    public Dictionary<string, PinyinEntry> pinyinsNoTones;

    public Context()
    {
        entries = new List<Entry>();

        foreach (var line in System.IO.File.ReadAllLines("cedict_ts.u8"))
        {
            var m = regex.Match(line);
            if (m.Length > 0)
                entries.Add(new Entry
                {
                    trad = SplitIntoCharacters(m.Groups["trad"].Value),
                    simp = SplitIntoCharacters(m.Groups["simp"].Value),
                    pinyin = m.Groups["pinyin"].Value.ToLower().Split(' '),
                    english = m.Groups["english"].Value.Split('/')
                });
        }

        characters = new Dictionary<string, CharEntry>();
        pinyins = new Dictionary<string, PinyinEntry>();
        pinyinsNoTones = new Dictionary<string, PinyinEntry>();
        foreach (var e in entries)
        {
            CharEntry.AddTo(characters, string.Join("", e.simp), e).s = e.simp;
            CharEntry.AddTo(characters, string.Join("", e.trad), e).s = e.trad;
            PinyinEntry.AddTo(pinyins, string.Join("", e.pinyin.Where(p => EndsInNumber(p))), e);
            PinyinEntry.AddTo(pinyinsNoTones, string.Join("", e.pinyin.Where(p => EndsInNumber(p)).Select(p => RemoveToneNumber(p))), e);
        }
    }

    public List<SearchState<T, string[]>.Entry> Search<T>(IEnumerable<T> l, Func<T, string[]> f, string key)
    {
        return new SearchState<T, string[]>
        {
            l = l,
            f = f,
            startsWith = StartsWith,
            length = s => s.Length,
            substring = (s, i) => s.Skip(i).ToArray(),
            equals = (s, t) => EqualStringArray(s, t),
            getHashCode = s => GetStringArrayHashCode(s)
        }.Search(SplitIntoCharacters(key));
    }

    public List<SearchState<T, string>.Entry> Search<T>(IEnumerable<T> l, Func<T, string> f, string key)
    {
        return new SearchState<T, string>
        {
            l = l,
            f = f,
            startsWith = (str, k) => str.StartsWith(k),
            length = s => s.Length,
            substring = (s, i) => s.Substring(i),
            equals = (s, t) => s == t,
            getHashCode = s => s.GetHashCode()
        }.Search(key.Replace("ü", "u:"));
    }

    public class SearchState<T, S>
    {
        public IEnumerable<T> l;
        public Func<T, S> f;
        public Func<S, S, bool> startsWith;
        public Func<S, int> length;
        public Func<S, int, S> substring;
        public Func<S, S, bool> equals;
        public Func<S, int> getHashCode;

        class Comparer : IEqualityComparer<S>
        {
            public SearchState<T, S> s;
            public bool Equals(S x, S y) => s.equals(x, y);
            public int GetHashCode(S obj) => s.getHashCode(obj);
        }

        Dictionary<S, List<Entry>> cache;

        public SearchState()
        {
            cache = new Dictionary<S, List<Entry>>(new Comparer { s = this });
        }

        public class Entry
        {
            public S key;
            public T e;
            public List<Entry> following;
            public int sortIndex = 0;
        }

        public List<Entry> Search(S s)
        {
            List<Entry> result;
            while (true)
            {
                if (cache.TryGetValue(s, out result))
                    return result;
                if (length(s) == 0)
                    return new List<Entry>();
                foreach (T e in l)
                {
                    var key = f(e);
                    int l = length(key);
                    if (l > 0 && startsWith(s, key))
                    {
                        var entry = new Entry { e = e, key = key, following = Search(substring(s, l)) };
                        if (entry.following != null)
                        {
                            if (result == null)
                            {
                                result = new List<Entry>();
                                cache.Add(s, result);
                            }
                            result.Add(entry);
                        }
                    }
                }
                if (result != null)
                    return result.OrderByDescending(ee => length(ee.key)).ToList();
                s = substring(s, 1);
            }
        }
    }
    static bool ContainsPinyinNumNonIdiom(Entry e, string pinyin)
    {
        return e.pinyin.Any(p => p == pinyin) && e.english.All(ee => !ee.Contains("idiom"));
    }

    static bool IsSingleCharacterEquivalent(Entry e)
    {
        return e.english.Any(ee => ee.StartsWith("single-character"));
    }

    static bool EndsInNumber(string s)
    {
        if (s.Length < 1)
            return false;
        char l = s[s.Length - 1];
        return l >= '0' && l <= '9';
    }

    internal static string RemoveToneNumber(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
        char l = s[s.Length - 1];
        return s.Substring(0, s.Length - (l >= '0' && l <= '9' ? 1 : 0));
    }

    internal static int AccentVowelIndex(string s)
    {
        if (string.IsNullOrEmpty(s))
            return -1;
        s = s.ToLower();
        int r = s.IndexOf('a');
        if (r >= 0)
            return r;
        r = s.IndexOf('e');
        if (r >= 0)
            return r;
        r = s.IndexOf('o');
        if (r >= 0)
            return r;
        r = s.IndexOf('ü');
        if (r >= 0)
            return r;
        r = s.IndexOf("ui");
        if (r >= 0)
            return r + 1;
        r = s.IndexOf('u');
        if (r >= 0)
            return r;
        r = s.IndexOf('i');
        if (r >= 0)
            return r;
        return -1;
    }

    internal static int GetToneNumber(string s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;
        char c = s[s.Length - 1];
        if (c >= '1' && c <= '5')
            return (int)(c - '0');
        return 0;
    }

    internal static char AddToneAccent(char c, int tone)
    {
        if (tone < 0 || tone > 4)
            tone = 0;
        int i = toneVowels.IndexOf(c);
        if (i < 0)
            return c;
        return toneVowels[i / 5 * 5 + tone];
    }

    internal static string AddToneAccent(string s, int tone)
    {
        string s2 = RemoveToneNumber(s);
        var sb = new StringBuilder(s2);
        var i = AccentVowelIndex(s2);
        if (i < 0)
            return s;
        sb[i] = AddToneAccent(s[i], tone);
        return sb.ToString();
    }

    internal static string AddToneAccent(string s)
    {
        s = s.Replace("u:", "ü");
        return AddToneAccent(s, GetToneNumber(s));
    }

    static string[] SplitIntoCharacters(string s)
    {
        var indices = StringInfo.ParseCombiningCharacters(s);
        Debug.Assert(indices[0] == 0);
        var list = new List<string>();
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];
            list.Add(s.Substring(index, (i < indices.Length - 1 ? indices[i + 1] : s.Length) - index));
        }
        return list.ToArray();
    }

    static bool StartsWith(string[] s, string[] key)
    {
        if (key.Length > s.Length)
            return false;
        for (int i = 0; i < key.Length; i++)
            if (key[i] != s[i])
                return false;
        return true;
    }

    static int GetStringArrayHashCode(string[] s)
    {
        int result = 0;
        for (int i = 0; i < s.Length; i++)
            result ^= s.GetHashCode();
        return result;
    }

    internal static bool EqualStringArray(string[] s, string[] t)
    {
        if (s.Length != t.Length)
            return false;
        for (int i = 0; i < s.Length; i++)
            if (s[i] != t[i])
                return false;
        return true;
    }

    static void AddEntries<S, T>(Dictionary<Context.SearchState<KeyValuePair<string, T>, S>.Entry, int> d, List<Context.SearchState<KeyValuePair<string, T>, S>.Entry> entries, ref int i)
    {
        if (entries == null)
            return;

        foreach (var e in entries)
        {
            if (!d.ContainsKey(e))
            {
                d.Add(e, i);
                i++;
            }
            AddEntries(d, e.following, ref i);
        }
    }

    public List<Entry> SearchEnglish(string s)
    {
        s = s.ToLower();
        return entries.Where(e => e.english.Any(ee => ee.ToLower().Contains(s))).ToList();
    }
}

