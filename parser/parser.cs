//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static qutum.parser.Rep;

namespace qutum.parser
{
	public class ParserStr : QEarley<string, char>
	{
		public ParserStr(string grammar) : base(grammar, new ScanStr()) { }
	}

	public class Tree<T> : LinkTree<Tree<T>>
	{
		public string name, dump;
		public int from, to;
		public T tokens;
		public int err; // step break: > 0
		public object expect; // step expected, Alt tip/name, or Key

		public override string DumpSelf(string ind, string pos) =>
			$"{ind}{pos}{from}:{to}{(err > 0 ? $"{expect} expected by {name}!{err}" : "")} {dump}";
	}

	sealed class Alt
	{
		internal string name;
		internal Con[] s;
	}

	sealed class Con
	{
		internal string name, tip;
		internal object[] s; // Alt or Key or null
		internal Rep[] reps;
		internal sbyte greedy; // default:0, greedy: 1, back greedy: -1
		internal sbyte keep; // default: 0, thru: -1, keep: 1

		public override string ToString() => name + "=" + string.Join(' ',
			s.Where(v => v != null).Select((v, x) => (v is Alt a ? a.name : v.ToString())
				+ (reps[x] == More ? "+" : reps[x] == Any ? "*" : reps[x] == Opt ? "?" : "")));
	}

	enum Rep : byte { Opt = 0, One = 1, Any = 2, More = 3 };

	public class QEarley<T, K> where T : class
	{
		Alt start;
		Scan<T, K> scan;
		List<Match> matchs;
		List<int> locs;
		HashSet<int> errs;
		int loc;
		public int largest, largestLoc, total;
		public bool greedy = false; // S=AB A=1|12 B=23|2  gready: (12)3  back greedy: 1(23)
		public bool treeKeep = true;
		public bool treeDump = false;

		struct Match
		{
			internal Con con;
			internal int from, to, step; // empty (always predicted): from==to (step could be 0 in repeatition)
			internal int prev; // complete or option: >=0, predict: >=-1, shift: see code, repeat: kept
			internal int tail; // alt: >=0, predict: -1, shift: -2, option: -3, repeat: kept

			public override string ToString() => $"{from}:{to}#{step} {con}";
		}

		QEarley() { matchs = new List<Match>(); locs = new List<int>(); errs = new HashSet<int>(); }

		public QEarley(string grammar, Scan<T, K> scan) : this() { start = Boot(grammar); this.scan = scan; }

		public Tree<T> Parse(T tokens)
		{
			if (tokens != null) scan.Load(tokens);
			int m = Parsing();
			Tree<T> t = m >= 0 ? Accepted(m, null) : Rejected();
			matchs.Clear(); locs.Clear(); errs.Clear();
			if (tokens != null) scan.Unload();
			return t;
		}

		public bool Check(T tokens)
		{
			if (tokens != null) scan.Load(tokens);
			bool greedy = this.greedy; this.greedy = false;
			int m = Parsing();
			this.greedy = greedy; matchs.Clear(); locs.Clear(); errs.Clear();
			if (tokens != null) scan.Unload();
			return m >= 0;
		}

		int Parsing()
		{
			locs.Add(loc = 0);
			foreach (var x in start.s)
				Add(x, 0, 0, 0, -1, -1);
			largest = largestLoc = 0;
			do
			{
				int c; Complete(locs[loc]); c = matchs.Count;
				int p; Predict(locs[loc]); p = matchs.Count;
				for (; ; )
				{
					Complete(c); if (c == (c = matchs.Count)) break;
					Predict(p); if (p == (p = matchs.Count)) break;
				}
				if (matchs.Count - locs[loc] > largest)
					largest = matchs.Count - locs[largestLoc = loc];
			} while (Shift());
			total = matchs.Count;
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.name == start.name && m.from == 0 && m.con.s[m.step] == null)
					return x;
			}
			return -1;
		}

		void Predict(int x)
		{
			for (; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is Alt a)
					foreach (var con in a.s)
						Add(con, loc, loc, 0, x, -1);
				if (((int)m.con.reps[m.step] & 1) == 0)
					Add(m.con, m.from, m.to, m.step + 1, x, -3); // m.to == loc
			}
		}

		void Complete(int empty)
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if ((x >= empty || m.from == loc) && m.con.s[m.step] == null)
					for (int px = locs[m.from], py = m.from < loc ? locs[m.from + 1] : matchs.Count; px < py; px++)
					{
						var pm = matchs[px];
						if (pm.con.s[pm.step] is Alt a && a.name == m.con.name)
							Add(pm.con, pm.from, pm.to, pm.step + 1, px, x); // pm.to <= loc
					}
			}
		}

		bool Shift()
		{
			if (!scan.Next())
				return false;
			locs.Add(matchs.Count);
			for (int x = locs[loc], y = locs[++loc]; x < y; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is K k && scan.Is(k))
					Add(m.con, m.from, m.to, m.step + 1, m.tail != -2 ? x : m.prev, -2); // m.to < loc
			}
			return locs[loc] < matchs.Count;
		}

		void Add(Con c, int from, int pto, int step, int prev, int tail)
		{
			var a = new Match { con = c, from = from, to = loc, step = step, prev = prev, tail = tail };
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con == c && m.from == from && m.step == step)
				{
					if ((c.greedy == 0 ? !greedy : c.greedy < 0) || m.tail == -1 || a.tail == -1) return;
					bool g = false; var b = a;
					for (int mp = m.prev, bp = b.prev; ;)
					{
						Debug.Assert(m.tail != -1); Debug.Assert(b.tail != -1);
						int v = (m.to - matchs[mp].to) - (b.to - matchs[bp].to);
						if (v != 0) g = v < 0;
						if (mp == bp) break;
						v = matchs[mp].to - matchs[bp].to;
						if (v >= 0) mp = (m = matchs[mp]).tail != -1 ? m.prev : -1;
						if (v <= 0) bp = (b = matchs[bp]).tail != -1 ? b.prev : -1;
					}
					if (g) matchs[x] = a;
					return;
				}
			}
			matchs.Add(a);
			if (pto < loc && step > 0 && (int)c.reps[--a.step] > 1)
				matchs.Add(a); // prev and tail kept
		}

		Tree<T> Accepted(int match, Tree<T> insert)
		{
			var m = matchs[match];
			if (m.from == m.to && m.step == 0 && m.con.s.Length > 1)
				return null;
			var t = ((m.con.keep == 0 ? treeKeep : m.con.keep > 0) ? null : insert) ??
				new Tree<T> { name = m.con.name, dump = Dump(m), from = m.from, to = m.to };
			for (; m.tail != -1; m = matchs[m.prev])
				if (m.tail >= 0)
					Accepted(m.tail, t);
			return insert == null || insert == t ? t : insert.Insert(t);
		}

		Tree<T> Rejected()
		{
			int to = locs[loc] < matchs.Count ? loc : loc - 1, x = locs[to];
			var t = new Tree<T> { name = "", dump = Dump(matchs[0], loc), from = 0, to = to, err = 1 };
			for (int y = matchs.Count - 1, z; (z = y) >= x; y--)
			{
				Prev: var m = matchs[z]; var s = m.con.s[m.step];
				if (s != null && !errs.Contains(z))
					if (m.step > 0)
					{
						var n = m.con.tip ?? m.con.name; var e = s is Alt a ? a.s[0].tip ?? a.name : s;
						t.Insert(new Tree<T>{ name = n, dump = Dump(m), from = m.from, to = m.to, err = m.step, expect = e });
						errs.Add(z);
					}
					else if ((z = m.prev) >= 0)
						goto Prev;
			}
			return t;
		}

		string Dump(Match m, int to = -1) => !treeDump ? null : to >= 0 ? scan.Tokens(0, to).ToString() :
			$"{m.con.ToString()} :: {scan.Tokens(m.from, m.to).ToString().Replace("\n", "\\n").Replace("\r", "\\r")}";

		public T Tokens(Tree<T> t) => t.tokens = t.tokens ?? scan.Tokens(t.from, t.to);

		// bootstrap

		static QEarley<string, char> boot = new QEarley<string, char>()
		{ scan = new Scan(), greedy = false, treeKeep = false, treeDump = false };

		static QEarley()
		{
			var grammar = new Dictionary<string, string>() { // \x1:|
			{ "gram", "eol* prod prods* eol*" },
			{ "prods","eol+ prod" },
			{ "prod", "name S* = con* alt* ptip?" },
			{ "con",  "W+|sym|S+" },
			{ "alt",  "atip? \x1 S* con*" },
			{ "name", "W+" },
			{ "sym",  "O+|R|\\ E|\\ u H H H H" },
			{ "ptip", "tip" },
			{ "atip", "tip? tipe" },
			{ "tip",  "= tipg? tipk? S* tipw" },
			{ "tipg", "*" },
			{ "tipk", "+|-" },
			{ "tipw", "T*" },
			{ "tipe", "eol" },
			{ "eol",  "S* comm? \r? \n S*" },
			{ "comm", "= = V*" } };
			var alt = grammar.ToDictionary(kv => kv.Key, kv => new Alt { name = kv.Key });
			foreach (var kv in grammar) alt[kv.Key].s = kv.Value.Split("|").Select(con =>
			{
				var z = con.Replace('\x1', '|').Split(' ', StringSplitOptions.RemoveEmptyEntries);
				var rs = z.Select(v => (v.Length > 1 && v[v.Length - 1] is char c ?
					c == '?' ? Opt : c == '*' ? Any : c == '+' ? More : One : One)).Append(One).ToArray();
				var s = z.Select((v, x) => alt.TryGetValue(v = rs[x] == One ? v : v.Substring(0, v.Length - 1),
					out Alt a) ? a : boot.scan.Keys(v).First()).Append(null).ToArray();
				return new Con { name = kv.Key, s = s, reps = rs };
			}).ToArray();
			alt["tip"].s[0].greedy = 1;
			foreach (var c in new[] { "prod", "alt", "name", "sym", "ptip", "tipg", "tipk", "tipw", "tipe" }
				.Aggregate(alt["con"].s.Take(1), (x, y) => x.Concat(alt[y].s)))
				c.keep = 1;
			boot.start = alt["gram"];
		}

		static Alt Boot(string grammar)
		{
			boot.scan.Load(grammar);
			var top = boot.Parse(null);
			if (top.err > 0)
			{ var e = new Exception(); e.Data["err"] = top; throw e; }
			var prod = top.Where(t => t.name == "prod");
			var alt = prod.ToDictionary(t => boot.Tokens(t.head), t => new Alt { name = t.head.tokens });
			foreach (var p in prod)
			{
				var cs = p.Where(t => t.name == "alt").Prepend(p).Select(ta =>
				{
					var s = ta.Where(t => (t.name == "con" || t.name == "sym") && boot.Tokens(t) != null).SelectMany
						(t => t.name == "sym" ? Scan.Rep(t.tokens) ?? Scan.Sym(t.tokens).Cast<object>()
							: alt.TryGetValue(t.tokens, out Alt a) ? new object[] { a } : boot.scan.Keys(t.tokens)
						).Append(null).ToArray();
					var rs = s.Select((v, x) => v == null || !(s[x + 1] is Rep r) ? One : r).Where((v, x) => !(s[x] is Rep));
					return new Con { name = p.head.tokens, s = s.Where(v => !(v is Rep)).ToArray(), reps = rs.ToArray() };
				}).ToArray();
				int tx = 0;
				p.Where(t => t.name == "alt" || t.name == "ptip").Select((a, x) =>
				{
					sbyte g = 0, k = 0; string w = null; a = a.head;
					if (a.name == "tipg") { g = 1; a = a.next ?? top; }
					if (a.name == "tipk") { k = (sbyte)(boot.Tokens(a) == "+" ? 1 : -1); a = a.next ?? top; }
					if (a.name == "tipw") { w = boot.Tokens(a); a = a.next ?? top; }
					for (; w != null && tx <= x; tx++) { cs[tx].greedy = g; cs[tx].keep = k; cs[tx].tip = w != "" ? w : null; }
					if (a.name == "tipe") tx = x + 1;
					return true;
				}).Count();
				alt[p.head.tokens].s = cs;
			}
			boot.scan.Unload();
			return alt[prod.First().head.tokens];
		}

		class Scan : ScanStr
		{
			public override bool Is(char key)
			{
				char t = text[x];
				switch (key)
				{
					case 'V': return t >= ' ';
					case 'S': return t == ' ' || t == '\t';
					case 'H': return t >= 'a' || t <= 'f' || t >= 'A' && t <= 'F' || t >= '0' && t <= '9';
					case 'W': return t >= 'a' && t <= 'z' || t >= 'A' && t <= 'Z' || t >= '0' && t <= '9' || t == '_';
					case 'O': return O.Contains(t);
					case 'R': return t == '?' || t == '*' || t == '+';
					case 'T': return t >= ' ' && t != '=';
					case 'E':
						switch (t)
						{
							case 's': case 't': case 'n': case 'r': case '\\': case '|': case '=': case '?': case '*': case '+': return true;
						}
						return false;
					default: return t == key;
				}
			}
			//	t > ' ' && t < '0' && t != '*' && t != '+' || t > '9' && t < 'A' && t != '=' && t != '?'
			//		|| t > 'Z' && t < 'a' && t != '\\' && t != '_' || t > 'z' && t <= '~' && t != '|'
			static HashSet<char> O = "!\"#$%&'(),-./:;<>@[]^`{}~".ToHashSet();

			internal static IEnumerable<object> Rep(string s)
			{
				if (s[0] == '?') return new object[] { Opt };
				if (s[0] == '*') return new object[] { Any };
				if (s[0] == '+') return new object[] { More };
				return null;
			}

			internal static string Sym(string s)
			{
				if (s[0] != '\\') return s;
				switch (s[1])
				{
					case 's': return " ";
					case 't': return "\t";
					case 'n': return "\n";
					case 'r': return "\r";
					case 'u':
						return ((char)(s[2] - (s[2] < 'a' ? '0' : 87) << 12 | s[3] - (s[3] < 'a' ? '0' : 87) << 8
							| s[4] - (s[4] < 'a' ? '0' : 87) << 4 | s[5] - (s[5] < 'a' ? '0' : 87))).ToString();
					default: return s[1].ToString();
				}
			}
		}
	}
}
