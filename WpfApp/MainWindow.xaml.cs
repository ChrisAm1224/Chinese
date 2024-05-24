using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace WpfApp
{
    public class MyList : List<object>
    {
        public MyList() { }

        public MyList(IEnumerable<object> l)
        {
            AddRange(l);
        }

        public string Header { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Context c;
        ManualResetEvent initialized = new ManualResetEvent(false);

        public MainWindow()
        {
            InitializeComponent();
            var bw = new BackgroundWorker();
            bw.DoWork += (obj, dwea) =>
            {
                c = new Context();
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    initialized.Set();
                }));
            };
            bw.RunWorkerAsync();
            FocusManager.SetFocusedElement(this, tbxSearch);
        }

        static IEnumerable<string> AddToneVowelsToPinyin(Entry e)
        {
            return e.pinyin.Select(p => Context.AddToneAccent(p));
        } 

        static MyList MyListFrom(Entry e)
        {
            var pinyins = AddToneVowelsToPinyin(e).ToList();
            MyList r = new MyList { Header = string.Join("", e.simp) + " " + string.Join(" ", pinyins) + ": " + string.Join(", ", e.english) };
            r.Add(new MyList(e.simp) { Header = string.Join("", e.simp) });
            r.Add(new MyList(pinyins) { Header = string.Join(" ", pinyins) });
            r.Add(new MyList(e.english) { Header = string.Join(", ", e.english) });
            return r;
        }

        class State<S, T>
        {
            public MyList l = new MyList();
            public Func<T, List<Entry>> func;
            public Dictionary<List<Entry>, Context.SearchState<KeyValuePair<string, T>, S>.Entry> dict = new Dictionary<List<Entry>, Context.SearchState<KeyValuePair<string, T>, S>.Entry>();

            public static MyList CreateEntry(List<Entry> v)
            {
                v = v.OrderBy(ee => string.Join("", ee.pinyin)).ToList();
                bool sameChar = v.All(ee => Context.EqualStringArray(ee.simp, v[0].simp));
                bool samePinYin = v.All(ee => Context.EqualStringArray(ee.pinyin, v[0].pinyin));
                bool sameEnglish = v.All(ee => Context.EqualStringArray(ee.english, v[0].english));
                var f = v.First();
                return new MyList(v.Select(ee => MyListFrom(ee))) { Header =
                    (sameChar ? string.Join("", f.simp) + " " : "") + 
                    string.Join(" ", f.pinyin.Select(p => samePinYin ? Context.AddToneAccent(p) : Context.RemoveToneNumber(p))) + 
                    (v.Count == 1 && f.english.Length == 1 ? ": " + f.english[0] : ( sameEnglish ? ": " + string.Join(", ", f.english) : "" )) };
            }

            public void AddToDict(List<Context.SearchState<KeyValuePair<string, T>, S>.Entry> el, int depth)
            {
                if (el != null)
                    foreach (var e in el)
                    {
                        e.sortIndex = Math.Max(e.sortIndex, depth);
                        AddToDict(e, depth);
                    }
            }

            void AddToDict(Context.SearchState<KeyValuePair<string, T>, S>.Entry e, int depth)
            {
                var v = func(e.e.Value);
                if (!dict.ContainsKey(v))
                    dict[v] = e;
                AddToDict(e.following, depth + 1);
            }
        }

        static MyList PrepareForTreeView<S, T>(List<Context.SearchState<KeyValuePair<string, T>, S>.Entry> entries, Func<T, List<Entry>> func)
        {
            var s = new State<S, T> { func = func };
            s.AddToDict(entries, 0);
            return new MyList(s.dict.OrderBy(kvp => kvp.Value.sortIndex).Select(kvp => State<S, T>.CreateEntry(kvp.Key)));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var bw = new BackgroundWorker();
            var searchString = tbxSearch.Text;
            btnSearch.IsEnabled = false;
            Cursor cursor = Cursor;
            Cursor = Cursors.Wait;
            bw.DoWork += (obj, dwea) =>
            {
                try
                {
                    initialized.WaitOne();
                    var pinyins = PrepareForTreeView(c.Search(c.pinyinsNoTones, p => p.Key, searchString), p => p.e);
                    var chars = PrepareForTreeView(c.Search(c.characters, cc => cc.Value.s, searchString), c => c.e);
                    var english = new MyList(c.SearchEnglish(searchString).Select(ee => MyListFrom(ee)));
                    pinyins.Header = "Pinyin";
                    chars.Header = "Simplified";
                    english.Header = "English";
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var lml = new List<MyList>() { pinyins, chars, english }.Where(l => l.Count > 0);
                        switch(lml.Count())
                        {
                            case 0:
                                trv.ItemsSource = english;
                                break;
                            case 1:
                                trv.ItemsSource = lml.Single();
                                break;
                            default:
                                trv.ItemsSource = lml;
                                break;
                        }
                    }));
                }
                finally
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        Cursor = cursor;
                        btnSearch.IsEnabled = true;
                    }));
                }
            };
            bw.RunWorkerAsync();
        }
    }
}
