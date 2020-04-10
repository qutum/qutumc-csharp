//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
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
	using ParserChar = ParserBase<char, char, string, TreeStr, ScanStr>;

	public class Tree<N, T> : LinkTree<T> where T : Tree<N, T>
	{
		public N name;
		public int from, to; // from token index to index excluded
		public int err; // no error: 0, error: -1, error step: > 0, recovered -step: < -1
		public object info; // error Token, expected Alt hint/name or K, or recovered K
		public string dump;

		public override string ToString()
		{
			return $"{from}:{to}{(err == 0 ? "" : err < -1 ? "!" + err : "!")} {dump ?? info ?? name}";
		}

		public override string ToString(object extra)
		{
			if (!(extra is Func<int, int, (int, int, int, int)> loc))
				return ToString();
			var (fl, fc, tl, tc) = loc(from, to);
			return $"{fl}.{fc}:{tl}.{tc}{(err == 0 ? "" : err > 0 ? "!" : "!!")} {dump ?? info ?? name}";
		}
	}

	public class TreeStr : Tree<string, TreeStr>
	{
	}

	public class Parser<K, N, Tr, Sc> : ParserBase<K, Token<K>, N, Tr, Sc>
		where K : struct where Tr : Tree<N, Tr>, new() where Sc : ScanSeg<K, Token<K>>
	{
		public Parser(string gram, Sc scan) : base(gram, scan) { }
	}

	public class ParserStr : ParserChar
	{
		public ParserStr(string gram, ScanStr scan = null) : base(gram, scan ?? new ScanStr("")) { }
	}

	enum Qua : byte { Opt = 0, One = 1, Any = 2, More = 3 };

	public class ParserBase<K, Tk, N, Tr, Sc> where Tr : Tree<N, Tr>, new() where Sc : Scan<K, Tk>
	{
		sealed class Prod
		{
			internal N name;
			internal Alt[] alts;
		}
		struct Con
		{
			internal object p; // Prod or K or null;
			internal Qua q;
		}
		sealed class Alt
		{
			internal N name;
			internal Con[] s;
			internal sbyte greedy; // parser.greedy:0, greedy: 1, back greedy: -1
			internal byte reck; // bit-or of reck index
			internal sbyte keep; // parser.treeKeep: 0, thru: -1, keep: 1
			internal string hint;

			public override string ToString()
			{
				return name + "="
					+ string.Join(' ', s.Where(c => c.p != null).Select(c =>
					(c.p is Prod p ? p.name.ToString() : Esc(c.p))
					+ (c.q == More ? "+" : c.q == Any ? "*" : c.q == Opt ? "?" : "")));
			}
		}

		readonly Prod start;
		readonly K[] reck;
		Match[] matchs = new Match[16384];
		int matchn, completen;
		readonly List<int> locs = new List<int>(); // loc to match index
		int loc; // current token loc
		internal int largest, largestLoc;
		internal Sc scan;
		public bool greedy = false; // for any Alt eg. S=AB A=1|12 B=23|2  gready: (12)3  back greedy: 1(23)
		public int recovery = 10; // no recovery: 0, how many times to recover at eof: > 0
		public bool treeKeep = true;
		public int treeDump = 0; // no: 0, tokens for tree leaf: 1, tokens: 2, tokens and Alt: 3
		public int treeExpect = 1; // One or More K: 0, One or More: 1, all: 2
		public Func<object, string> treeDumper = null;

		struct Match
		{
			internal Alt a;
			internal int from, to, step; // empty (always predicted): from==to (step could be 0 in quantifier)
			internal int prev; // complete or option: >=0, predict: >=-1, shift: see code, repeat: kept
			internal int tail; // Alt: >=0, predict: -1, shift: -2, option: -3, repeat: kept, recovery: -4

			public override string ToString()
				=> $"{from}:{to}{(a.s[step].p != null ? "'" : "#")}{step}" +
				$"{(tail >= 0 ? "^" : tail == -1 ? "p" : tail == -2 ? "s" : tail == -3 ? "?" : "r")} {a}";
		}

		ParserBase(Prod start) { this.start = start; reck = Array.Empty<K>(); }

		public ParserBase<K, Tk, N, Tr, Sc> Load(Sc scan) { this.scan = scan; return this; }

		// build a Tree from matched and kept Alts, Tree.tokens unset
		public virtual Tr Parse()
		{
			int m = Earley(out Tr recs, recovery);
			Tr t = m >= 0 ? Accepted(m, null) : Rejected();
			matchn = 0;
			locs.Clear();
			if (recs != null)
				if (m >= 0)
					t.AddNextSub(recs);
				else
					t = recs.head.Remove().AddNextSub(recs);
			return t;
		}

		public virtual bool Check()
		{
			bool gre = greedy; greedy = false;
			int m = Earley(out _, 0); greedy = gre;
			matchn = 0;
			locs.Clear();
			return m >= 0;
		}

		int Earley(out Tr recs, int rec)
		{
			locs.Add(loc = 0);
			foreach (var x in start.alts)
				Add(x, 0, 0, 0, -1, -1);
			largest = largestLoc = 0;
			recs = null;
		Loop: int shift;
			do {
				int c, p;
				Complete(locs[loc]); c = matchn;
				Predict(locs[loc]); p = matchn;
				for (; ; ) {
					Complete(c);
					if (c == (c = matchn))
						break;
					Predict(p);
					if (p == (p = matchn))
						break;
				}
				if (matchn - locs[loc] > largest)
					largest = matchn - locs[largestLoc = loc];
			} while ((shift = Shift()) > 0);

			completen = matchn;
			for (int x = locs[loc]; x < matchn; x++) {
				var m = matchs[x];
				if (Eq.Equals(m.a.name, start.name) && m.from == 0 && m.a.s[m.step].p == null)
					return x;
			}
			if (reck.Length > 0 && rec > 0) {
				recs ??= new Tr();
				recs.Add(Rejected());
				if (shift == 0)
					for (; ; locs.Add(matchn), ++loc)
						if (Recover(false))
							goto Loop;
						else if (!scan.Next())
							break;
				rec--;
				if (Recover(true))
					goto Loop;
			}
			return -1;
		}

		void Predict(int x)
		{
			for (; x < matchn; x++) {
				var m = matchs[x];
				if (m.a.s[m.step].p is Prod p)
					foreach (var alt in p.alts)
						Add(alt, loc, loc, 0, x, -1);
				if (((int)m.a.s[m.step].q & 1) == 0)
					Add(m.a, m.from, m.to, m.step + 1, x, -3); // m.to == loc
			}
		}

		void Complete(int empty)
		{
			for (int x = locs[loc]; x < matchn; x++) {
				var m = matchs[x];
				if ((x >= empty || m.from == loc) && m.a.s[m.step].p == null)
					for (int px = locs[m.from], py = m.from < loc ? locs[m.from + 1] : matchn;
							px < py; px++) {
						var pm = matchs[px];
						if (pm.a.s[pm.step].p is Prod p && Eq.Equals(p.name, m.a.name))
							Add(pm.a, pm.from, pm.to, pm.step + 1, px, x); // pm.to <= loc
					}
			}
		}

		int Shift()
		{
			if (!scan.Next())
				return -1;
			locs.Add(matchn);
			for (int x = locs[loc], y = locs[++loc]; x < y; x++) {
				var m = matchs[x];
				if (m.a.s[m.step].p is K k && scan.Is(k))
					Add(m.a, m.from, m.to, m.step + 1, m.tail != -2 ? x : m.prev, -2); // m.to < loc
			}
			return matchn - locs[loc];
		}

		void Add(Alt a, int from, int pto, int step, int prev, int tail)
		{
			var u = new Match {
				a = a, from = from, to = loc, step = step, prev = prev, tail = tail
			};
			for (int x = locs[loc]; x < matchn; x++) {
				var m = matchs[x];
				if (m.a == a && m.from == from && m.step == step) {
					if ((a.greedy == 0 ? !greedy : a.greedy < 0) || m.tail == -1 || u.tail == -1)
						return;
					bool g = false; var w = u;
					for (int mp = m.prev, wp = w.prev; ;) {
						Debug.Assert(m.tail != -1 && w.tail != -1);
						int y = (m.to - matchs[mp].to) - (w.to - matchs[wp].to);
						if (y != 0) g = y < 0;
						if (mp == wp)
							break;
						y = matchs[mp].to - matchs[wp].to;
						if (y >= 0)
							mp = (m = matchs[mp]).tail != -1 ? m.prev : -1;
						if (y <= 0)
							wp = (w = matchs[wp]).tail != -1 ? w.prev : -1;
					}
					if (g) matchs[x] = u;
					return;
				}
			}
			if (matchn + 2 > matchs.Length) Array.Resize(ref matchs, matchs.Length << 1);
			matchs[matchn++] = u;
			if (pto < loc && step > 0 && (int)a.s[--u.step].q > 1)
				matchs[matchn++] = u; // prev and tail kept
		}

		bool Recover(bool eof)
		{
			for (int r = 0; r < reck.Length; r++)
				if (eof || scan.Is(reck[r])) {
					for (int x = matchn - 1; x >= 0; x--) {
						var m = matchs[x]; var y = m.step;
						if (y > 0 && (m.a.reck & 1 << r) != 0)
							for (object v; (v = m.a.s[y++].p) != null;)
								if (v is K k && scan.Is(k, reck[r])) {
									Add(m.a, m.from, m.to, y, x, -4);
									goto Rec;
								}
					}
				Rec:;
				}
			return completen < matchn;
		}

		Tr Accepted(int match, Tr up)
		{
			var m = matchs[match];
			if (m.from == m.to && m.step == 0 && m.a.s.Length > 1)
				return null;
			Tr t = (m.a.keep == 0 ? treeKeep : m.a.keep > 0) ? null : up, New = null;
			if (t == null)
				t = New = new Tr { name = m.a.name, from = m.from, to = m.to };
			for (var p = m; p.tail != -1; p = matchs[p.prev])
				if (p.tail >= 0)
					Accepted(p.tail, t);
				else if (p.tail == -4) {
					Debug.Assert(p.a == m.a);
					t.AddHead(new Tr {
						name = p.a.name, from = p.from, to = p.to, err = -p.step,
						info = (K)p.a.s[p.step - 1].p
					});
				}
			if (up != null && up != t)
				up.AddHead(t);
			if (New != null)
				t.dump = Dump(m, t.head == null);
			return t;
		}

		Tr Rejected()
		{
			int from = locs[loc] < matchn ? loc : loc - 1, x = locs[from];
			var t = new Tr {
				name = start.name, from = from, to = loc, err = -1,
				info = from < loc ? (object)scan.Token(from) : null
			};
			var errs = new bool[matchn];
			for (int y = matchn - 1, z; (z = y) >= x; y--) {
			Prev: var m = matchs[z]; var s = m.a.s[m.step];
				if (s.p != null && !errs[z])
					if (m.step > 0) {
						errs[z] = true;
						if (treeExpect >= 2 ||
								((int)s.q & 1) != 0 && (treeExpect == 1 || s.p is K)) {
							var e = s.p is Prod p ? p.alts[0].hint ?? (object)p.name : s.p;
							var d = treeDump <= 0 ? m.a.hint
								: treeDump <= 2 ? $"{Esc(e)} expected by {m.a.hint}"
								: $"{Esc(e)} expected by {m.a.hint}!{m.step} {Dump(m, true)}";
							t.AddHead(new Tr {
								name = m.a.name, from = m.from, to = m.to,
								err = m.step, info = e, dump = d
							});
						}
					}
					else if ((z = m.prev) >= 0)
						goto Prev;
			}
			return t;
		}

		protected static EqualityComparer<N> Eq = EqualityComparer<N>.Default;

		protected virtual N Name(string name) => (N)(object)name;

		string Dump(Match m, bool leaf)
		{
			return treeDump <= 0 || treeDump == 1 && !leaf ? null :
				$"{(treeDump <= 2 ? m.a.name.ToString() : m.a.ToString())} :: {Dump(scan.Tokens(m.from, m.to))}";
		}
		string Dump(object v) => treeDumper?.Invoke(v) ?? Esc(v.ToString());
		static string Esc(object v)
		{
			return v.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
		}

		// bootstrap
		static readonly ParserChar boot;

		static ParserBase()
		{
			var scan = new BootScan("");
			// prod name, prod-or-key-with-qua-alt1 con1|alt2 con2  \x1 is |
			var grammar = new Dictionary<string, string> {
			{ "gram",  "rec? eol* prod prods* eol*" },
			{ "prods", "eol+ prod" },
			{ "prod",  "name S* = con* alt* hint?" },
			{ "name",  "W+" },
			{ "con",   "W+|sym|S+" }, // word to prod name or scan.Keys
			{ "sym",   "Q|O+|\\ E" }, // unescaped to scan.Keys except Qua
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
			{ "reck",  "W+|O+|\\ E" } }; // unescape to scan.Keys

			// build prod
			var prods = grammar.ToDictionary(
				kv => kv.Key,
				kv => new ParserChar.Prod { name = kv.Key });
			foreach (var kv in grammar)
				// split into alts
				prods[kv.Key].alts = kv.Value.Split("|").Select(alt => {
					// split into cons
					var z = alt.Replace('\x1', '|')
						.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					// build con
					var s = z.Select(c => {
						var q = c.Length == 1 || !(c[^1] is char x) ? One
								: x == '?' ? Opt : x == '*' ? Any : x == '+' ? More : One;
						var p = q == One ? c : c[0..^1];
						return new ParserChar.Con {
							p = prods.TryGetValue(p, out var a) ? a : (object)scan.Keys(p).First(),
							q = q,
						};
					}).Append(new ParserChar.Con { q = One })
					.ToArray();
					return new ParserChar.Alt { name = kv.Key, s = s };
				}).ToArray();
			// spaces before hintw are greedy
			prods["hint"].alts[0].greedy = 1;
			// keep these in the tree
			foreach (var c in prods["con"].alts.Take(1) // word
				.Concat(new[] { "prod", "alt", "name", "sym", "hintg", "hintk", "hintw", "hinte", "reck" }
				.SelectMany(x => prods[x].alts)))
				c.keep = 1;
			boot = new ParserChar(prods["gram"]) {
				greedy = false, treeKeep = false, treeDump = 0
			};
		}

		public ParserBase(string gram, Sc scan)
		{
			Load(scan);
			using var bscan = new BootScan(gram);
			var top = boot.Load(bscan).Parse();
			if (top.err != 0) {
				using var bscan2 = new BootScan(gram);
				var dump = boot.treeDump; boot.treeDump = 3;
				boot.Load(bscan2).Parse().Dump(); boot.treeDump = dump;
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
			reck = top.Where(t => t.name == "reck")
				.SelectMany(t => scan.Keys(BootScan.Unesc(gram, t.from, t.to)))
				.ToArray();
			if (reck.Length > 4)
				throw new Exception("too many recovery keys");
			var prods = top.Where(t => t.name == "prod");
			var names = prods.ToDictionary(
				p => p.head.dump = boot.scan.Tokens(p.head.from, p.head.to),
				p => new Prod { name = Name(p.head.dump) }
			);

			foreach (var p in prods) {
				var prod = names[p.head.dump];
				// build alts
				var az = p.Where(t => t.name == "alt").Prepend(p).Select(ta => {
					// prod name or keys or quantifier ...
					var z = ta.Where(t => t.name == "sym" || t.name == "con").SelectMany(t =>
						t.name == "sym" ?
							gram[t.from] == '?' ? new object[] { Opt } :
							gram[t.from] == '*' ? new object[] { Any } :
							gram[t.from] == '+' ? new object[] { More } :
							scan.Keys(BootScan.Unesc(gram, t.from, t.to)).Cast<object>()
						// for word, search product names first, then scan keys
						: names.TryGetValue(t.dump = boot.scan.Tokens(t.from, t.to), out Prod p)
							? new object[] { p } :
							scan.Keys(t.dump).Cast<object>())
						.Append(null)
						.ToArray();
					// build alt
					var s = z.Select((v, x) =>
						new Con { p = v, q = v != null && z[x + 1] is Qua r ? r : One })
						.Where(v => !(v.p is Qua))
						.ToArray();
					var rk = (byte)reck.Select((r, x) =>
						z.Any(v => v is K k && scan.Is(k, r)) ? 1 << x : 0).Sum();
					return new Alt {
						name = prod.name, s = s, reck = rk
					};
				}).ToArray();
				// build hint
				int ax = 0;
				p.Where(t => t.name == "alt").Append(p).Each((t, x) => {
					sbyte g = 0, k = 0;
					foreach (var h in t) {
						if (h.name == "hintg") g = 1;
						if (h.name == "hintk") k = (sbyte)(gram[h.from] == '+' ? 1 : -1);
						if (h.name == "hintw")
							for (var w = boot.scan.Tokens(h.from, h.to); ax <= x; ax++) {
								az[ax].greedy = g; az[ax].keep = k; az[ax].hint = w != "" ? w : null;
							}
						// each hint is for only one line
						if (h.name == "hinte") ax = x + 1;
					}
				});
				prod.alts = az;
			}
			start = names[prods.First().head.dump];
		}
	}
}
