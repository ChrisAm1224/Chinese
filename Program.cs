using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Chinese
{
    class Program
    {
        static void PrintEntries<S, T>(List<Context.SearchState<KeyValuePair<string, T>, S>.Entry> entries, Action<T> print)
        {
            var d = new Dictionary<Context.SearchState<KeyValuePair<string, T>, S>.Entry, int>();
            int i = 0;
            //AddEntries(d, entries, ref i);
            var arr = new Context.SearchState<KeyValuePair<string, T>, S>.Entry[i];
            foreach (var kvp in d)
                arr[kvp.Value] = kvp.Key;

            while (entries != null && 0 < entries.Count)
            {
                var entry = entries[0];
                print(entry.e.Value);
                entries = entry.following;
            }
        }

        static void PrintStrings(IEnumerable<string> strings)
        {
            if (strings == null)
                return;
            foreach (var s in strings)
                Console.Write(s);
            Console.WriteLine();
        }

        static void PrintStrings2(IEnumerable<string> strings)
        {
            if (strings == null)
                return;
            foreach (var s in strings)
                Console.WriteLine(s);
        }

        static void PrintEntries(IEnumerable<Entry> entries)
        {
            foreach (var entry in entries)
            {
                PrintStrings(entry.pinyin);
                PrintStrings2(entry.english);
                Console.WriteLine();
            }
        }


        static void Main(string[] args)
        {
            var c = new Context();

            PrintEntries(c.Search(c.pinyinsNoTones, p => p.Key, "dianxia"), delegate (PinyinEntry e) { PrintEntries(e.e); });
            Console.WriteLine();

            var syllables = c.entries.SelectMany(e => e.pinyin.Select(p => Context.RemoveToneNumber(p))).Distinct().ToList();
        }
    }
}