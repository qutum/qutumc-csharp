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
using static qutum.parser.Qua;

namespace qutum.parser
{
	public class ParserStr : Parser<string, char, char, string>
	{
		public ParserStr(string grammar) : base(grammar, new ScanStr()) { }
	}

	public class Tree<S> : LinkTree<Tree<S>>
	{
		public string name, dump;
		public int from, to;
		public S tokens;
		public int err; // step break: > 0, recovered: -1
		public object expect; // step expected, Alt hint/name, or Key

		public override string DumpSelf(string ind, string pos) =>
			$"{ind}{pos}{from}:{to}{(err == 0 ? "" : err > 0 ? "!" : "!!")} {dump ?? expect ?? name}";
	}

	enum Qua : byte { Opt = 0, One = 1, Any = 2, More = 3 };

	public class Parser<I, K, T, S> where S : IEnumerable<T>
	{
		sealed class Prod
		{
			internal string name;
			internal Con[] alts;
		}

		sealed class Con
		{
			internal string name, hint;
			internal object[] s; // Prod or Key or null
			internal Qua[] qs;
			internal sbyte greedy; // parser.greedy:0, greedy: 1, back greedy: -1
			internal byte reck; // bit-or of reck index
			internal sbyte keep; // parser.treeKeep: 0, thru: -1, keep: 1

			public override string ToString() => name + "=" + string.Join(' ',
				s.Where(v => v != null).Select((v, x) => (v is Prod p ? p.name : Esc(v))
					+ (qs[x] == More ? "+" : qs[x] == Any ? "*" : qs[x] == Opt ? "?" : "")));
		}

		Prod start;
		List<Match> matchs = new List<Match>();
		List<int> locs = new List<int>();
		HashSet<int> errs = new HashSet<int>();
		int loc;
		K[] reck;
		internal Scan<I, K, T, S> scan;
		internal int largest, largestLoc, total;
		internal bool greedy = false; // S=AB A=1|12 B=23|2  gready: (12)3  back greedy: 1(23)
		internal int recovery = 10; // no recovery: 0, how many times to recover at eof: > 0
		internal bool treeKeep = true;
		internal bool treeDump = false;
		internal int treeExpect = 1; // no option and key only: 0, no option: 1, more: 2
		internal Func<object, string> treeDumper = null;

		struct Match
		{
			internal Con con;
			internal int from, to, step; // empty (always predicted): from==to (step could be 0 in quantifier)
			internal int prev; // complete or option: >=0, predict: >=-1, shift: see code, repeat: kept
			internal int tail; // Con: >=0, predict: -1, shift: -2, option: -3, repeat: kept, recovery: -4

			public override string ToString() => $"{from}:{to}#{step} {con}";
		}

		Parser() { }

		public Parser(string grammar, Scan<I, K, T, S> scan) { this.scan = scan; Boot(grammar); }

		public virtual Tree<S> Parse(I input)
		{
			if (input != null)
				scan.Load(input);
			int m = Earley(out Tree<S> recs, recovery);
			Tree<S> t = m >= 0 ? Accepted(m, null) : Rejected();
			matchs.Clear();
			locs.Clear();
			if (input != null)
				scan.Unload();
			if (recs != null)
				t.Adds(recs);
			return t;
		}

		public virtual bool Check(I input)
		{
			if (input != null)
				scan.Load(input);
			bool gre = greedy; greedy = false;
			int m = Earley(out Tree<S> recs, 0); greedy = gre;
			matchs.Clear();
			locs.Clear();
			if (input != null)
				scan.Unload();
			return m >= 0;
		}

		int Earley(out Tree<S> recs, int rec)
		{
			locs.Add(loc = 0);
			foreach (var x in start.alts)
				Add(x, 0, 0, 0, -1, -1);
			largest = largestLoc = 0;
			recs = null;
			Loop: int shift; do
			{
				int c, p;
				Complete(locs[loc]); c = matchs.Count;
				Predict(locs[loc]); p = matchs.Count;
				for (; ; )
				{
					Complete(c);
					if (c == (c = matchs.Count)) break;
					Predict(p);
					if (p == (p = matchs.Count)) break;
				}
				if (matchs.Count - locs[loc] > largest)
					largest = matchs.Count - locs[largestLoc = loc];
			} while ((shift = Shift()) > 0);

			total = matchs.Count;
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.name == start.name && m.from == 0 && m.con.s[m.step] == null)
					return x;
			}
			if (reck.Length > 0 && rec > 0)
			{
				if (recs == null) recs = new Tree<S>();
				recs.Add(Rejected());
				if (shift == 0)
					for (; ; locs.Add(matchs.Count), ++loc)
						if (Recover(false))
							goto Loop;
						else if (!scan.Next())
							break;
				rec--;
				if (Recover(true))
					goto Loop;
			}
			recs = null;
			return -1;
		}

		void Predict(int x)
		{
			for (; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is Prod p)
					foreach (var con in p.alts)
						Add(con, loc, loc, 0, x, -1);
				if (((int)m.con.qs[m.step] & 1) == 0)
					Add(m.con, m.from, m.to, m.step + 1, x, -3); // m.to == loc
			}
		}

		void Complete(int empty)
		{
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if ((x >= empty || m.from == loc) && m.con.s[m.step] == null)
					for (int px = locs[m.from], py = m.from < loc ? locs[m.from + 1] : matchs.Count;
						px < py; px++)
					{
						var pm = matchs[px];
						if (pm.con.s[pm.step] is Prod p && p.name == m.con.name)
							Add(pm.con, pm.from, pm.to, pm.step + 1, px, x); // pm.to <= loc
					}
			}
		}

		int Shift()
		{
			if (!scan.Next())
				return -1;
			locs.Add(matchs.Count);
			for (int x = locs[loc], y = locs[++loc]; x < y; x++)
			{
				var m = matchs[x];
				if (m.con.s[m.step] is K k && scan.Is(k))
					Add(m.con, m.from, m.to, m.step + 1, m.tail != -2 ? x : m.prev, -2); // m.to < loc
			}
			return matchs.Count - locs[loc];
		}

		void Add(Con c, int from, int pto, int step, int prev, int tail)
		{
			var a = new Match { con = c, from = from, to = loc, step = step, prev = prev, tail = tail };
			for (int x = locs[loc]; x < matchs.Count; x++)
			{
				var m = matchs[x];
				if (m.con == c && m.from == from && m.step == step)
				{
					if ((c.greedy == 0 ? !greedy : c.greedy < 0) || m.tail == -1 || a.tail == -1)
						return;
					bool g = false; var b = a;
					for (int mp = m.prev, bp = b.prev; ;)
					{
						Debug.Assert(m.tail != -1); Debug.Assert(b.tail != -1);
						int v = (m.to - matchs[mp].to) - (b.to - matchs[bp].to);
						if (v != 0) g = v < 0;
						if (mp == bp)
							break;
						v = matchs[mp].to - matchs[bp].to;
						if (v >= 0) mp = (m = matchs[mp]).tail != -1 ? m.prev : -1;
						if (v <= 0) bp = (b = matchs[bp]).tail != -1 ? b.prev : -1;
					}
					if (g) matchs[x] = a;
					return;
				}
			}
			matchs.Add(a);
			if (pto < loc && step > 0 && (int)c.qs[--a.step] > 1)
				matchs.Add(a); // prev and tail kept
		}

		bool Recover(bool eof)
		{
			for (int r = 0; r < reck.Length; r++)
				if (eof || scan.Is(reck[r]))
				{
					for (int x = matchs.Count - 1; x >= 0; x--)
					{
						var m = matchs[x]; var y = m.step;
						if (y > 0 && (m.con.reck & 1 << r) != 0)
							for (object v; (v = m.con.s[y++]) != null;)
								if (v is K k && scan.Is(reck[r], k))
								{
									Add(m.con, m.from, m.to, y, x, -4);
									goto Rec;
								}
					}
					Rec:;
				}
			return total < matchs.Count;
		}

		Tree<S> Accepted(int match, Tree<S> insert)
		{
			var m = matchs[match];
			if (m.from == m.to && m.step == 0 && m.con.s.Length > 1)
				return null;
			var t = ((m.con.keep == 0 ? treeKeep : m.con.keep > 0) ? null : insert) ??
				new Tree<S> { name = m.con.name, from = m.from, to = m.to, dump = Dump(m) };
			for (; m.tail != -1; m = matchs[m.prev])
				if (m.tail >= 0)
					Accepted(m.tail, t);
				else if (m.tail == -4)
					t.Insert(new Tree<S> { name = "", from = m.from, to = m.to, err = -1 });
			return insert == null || insert == t ? t : insert.Insert(t);
		}

		Tree<S> Rejected()
		{
			int to = locs[loc] < matchs.Count ? loc : loc - 1, x = locs[to];
			var t = new Tree<S>
			{ name = "", from = to, to = loc, err = 1, dump = treeDump ? Dump(scan.Tokens(0, loc)) : null };
			for (int y = matchs.Count - 1, z; (z = y) >= x; y--)
			{
				Prev: var m = matchs[z]; var s = m.con.s[m.step];
				if (s != null && !errs.Contains(z))
					if (m.step > 0)
					{
						errs.Add(z);
						if (treeExpect >= 2
							|| ((int)m.con.qs[m.step] & 1) != 0 && (treeExpect == 1 || s is K))
						{
							var e = s is Prod p ? p.alts[0].hint ?? p.name : s;
							var d = treeDump ? $"{Esc(e)} expected by {m.con.hint}!{m.step} {Dump(m)}"
								: m.con.hint;
							t.Insert(new Tree<S>
							{ name = m.con.name, from = m.from, to = m.to, err = m.step, expect = e, dump = d });
						}
					}
					else if ((z = m.prev) >= 0)
						goto Prev;
			}
			errs.Clear();
			return t;
		}

		string Dump(Match m) => !treeDump ? null : $"{m.con.ToString()} :: {Dump(scan.Tokens(m.from, m.to))}";
		string Dump(object v) => treeDumper?.Invoke(v) ?? Esc(v.ToString());
		static string Esc(object v) => v.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

		// bootstrap

		static readonly Parser<string, char, char, string> boot;

		static Parser()
		{
			boot = new Parser<string, char, char, string>()
			{ scan = new BootScan(), greedy = false, treeKeep = false, treeDump = false };

			// prod name, prod-or-key-with-qua-alt1 con1|alt2 con2  \x1 is |
			var grammar = new Dictionary<string, string>() {
			{ "gram",  "rec? eol* prod prods* eol*" },
			{ "prods", "eol+ prod" },
			{ "prod",  "name S* = con* alt* hint?" },
			{ "name",  "W+" },
			{ "con",   "W+|sym|S+" },
			{ "sym",   "Q|O+|\\ E|\\ u X X X X" },
			{ "alt",   "ahint? \x1 S* con*" },
			{ "hint",  "= hintg? hintk? S* hintw" },
			{ "hintg", "*" }, // hint greedy
			{ "hintk", "+|-" }, // hint keep
			{ "hintw", "H*" }, // hint words
			{ "hinte", "eol" }, // to split prod into lines
			{ "ahint", "hint? hinte" },
			{ "eol",   "S* comm? \r? \n S*" },
			{ "comm",  "= = V*" },
			{ "rec",   "\x1 = recs+" },
			{ "recs",  "reck|S+" },
			{ "reck",  "W+|O+|\\ E|\\ u X X X X" } };
			// build Prod
			var prods = grammar.ToDictionary(
				kv => kv.Key,
				kv => new Parser<string, char, char, string>.Prod { name = kv.Key });
			// build Con
			foreach (var kv in grammar)
				prods[kv.Key].alts = kv.Value.Split("|").Select(con =>
				{
					var z = con.Replace('\x1', '|').Split(' ', StringSplitOptions.RemoveEmptyEntries);
					var qs = z.Select(v =>
						v.Length == 1 || !(v[v.Length - 1] is char c) ? One
							: c == '?' ? Opt : c == '*' ? Any : c == '+' ? More : One)
						.Append(One).ToArray();
					var s = z.Select((v, x) =>
						prods.TryGetValue(v = qs[x] == One ? v : v.Substring(0, v.Length - 1), out var a)
							? a : (object)boot.scan.Keys(v).First())
						.Append(null).ToArray();
					return new Parser<string, char, char, string>.Con { name = kv.Key, s = s, qs = qs };
				}).ToArray();
			// build boot parser
			prods["hint"].alts[0].greedy = 1;
			foreach (var c in prods["con"].alts.Take(1) // word
				.Concat(new[] { "prod", "alt", "name", "sym", "hintg", "hintk", "hintw", "hinte", "reck" }
				.SelectMany(x => prods[x].alts)))
				c.keep = 1;
			boot.start = prods["gram"];
			boot.reck = Array.Empty<char>();
		}

		void Boot(string gram)
		{
			boot.scan.Load(gram);
			var top = boot.Parse(null);
			if (top.err != 0)
			{
				boot.scan.Unload();
				boot.treeDump = true; boot.Parse(gram).Dump(); boot.treeDump = false;
				var e = new Exception(); e.Data["err"] = top;
				throw e;
			}
			// gram
			// \ prod
			//   \ name
			//   \ word or sym or alt ...
			//   \ con .tokens refer prod.name
			//   \ alt .name == prod.name
			//     \ hintg or hintk ... hintw
			//     \ hinte
			//     \ word or sym ...
			//   \ hintg or hintk ... hintw
			// \ prod ...
			reck = top.Where(t => t.name == "reck").SelectMany
				(t => scan.Keys(BootScan.Esc(gram, t.from, t.to, 1))).ToArray();
			if (reck.Length > 4)
				throw new Exception("too many recovery keys");
			var prods = top.Where(t => t.name == "prod");
			var names = prods.ToDictionary(
				p => BootTokens(p.head), p => new Prod { name = p.head.tokens });

			foreach (var p in prods)
			{
				// build alt
				var az = p.Where(t => t.name == "alt").Prepend(p).Select(ta =>
				{
					// alt name or keys or quantifier ...
					var z = ta.Where(t => t.name == "sym" || t.name == "con").SelectMany(t =>
						t.name == "sym" ?
							gram[t.from] == '?' ? new object[] { Opt } :
							gram[t.from] == '*' ? new object[] { Any } :
							gram[t.from] == '+' ? new object[] { More } :
							scan.Keys(BootScan.Esc(gram, t.from, t.to, 1)).Cast<object>()
						:
							names.TryGetValue(BootTokens(t), out Prod p) ? new object[] { p } :
							scan.Keys(t.tokens).Cast<object>())
						.Append(null).ToArray();
					// build con
					var s = z.Where(
						v => !(v is Qua)).ToArray();
					var qs = z.Select(
						(v, x) => v != null && z[x + 1] is Qua r ? r : One).Where(
						(v, x) => !(z[x] is Qua)).ToArray();
					var rk = (byte)reck.Select(
						(r, x) => z.Any(v => v is K k && scan.Is(r, k)) ? 1 << x : 0)
						.Sum();
					return new Con { name = p.head.tokens, s = s, qs = qs, reck = rk };
				}).ToArray();
				// build hint
				int cx = 0;
				p.Where(t => t.name == "alt").Append(p).Each((t, x) =>
				{
					sbyte g = 0, k = 0;
					foreach (var h in t)
					{
						if (h.name == "hintg") g = 1;
						if (h.name == "hintk") k = (sbyte)(gram[h.from] == '+' ? 1 : -1);
						if (h.name == "hintw") for (var w = BootTokens(h); cx <= x; cx++)
							{
								az[cx].greedy = g; az[cx].keep = k; az[cx].hint = w != "" ? w : null;
							}
						// each hint is for only one line
						if (h.name == "hinte") cx = x + 1;
					}
				});
				names[p.head.tokens].alts = az;
			}
			boot.scan.Unload();
			start = names[prods.First().head.tokens];
		}

		static string BootTokens(Tree<string> t) =>
			t.tokens ?? (t.tokens = boot.scan.Tokens(t.from, t.to));
	}
}
