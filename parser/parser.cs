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
	public class Tree<S> : LinkTree<Tree<S>>
	{
		public string name, dump;
		public int from, to; // from token index to index excluded
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
			internal Alt[] alts;
		}
		struct Con
		{
			internal object p; // Prod or Key or null;
			internal Qua q;
		}
		sealed class Alt
		{
			internal string name, hint;
			internal Con[] s;
			internal sbyte greedy; // parser.greedy:0, greedy: 1, back greedy: -1
			internal byte reck; // bit-or of reck index
			internal sbyte keep; // parser.treeKeep: 0, thru: -1, keep: 1

			public override string ToString() =>
				name + "=" + string.Join(' ', s.Where(c => c.p != null).Select(
					c => (c.p is Prod p ? p.name : Esc(c))
					+ (c.q == More ? "+" : c.q == Any ? "*" : c.q == Opt ? "?" : "")));
		}

		readonly Prod start;
		readonly K[] reck;
		Match[] matchs = new Match[16384];
		int matchn;
		readonly List<int> locs = new List<int>(); // loc to match index
		int loc; // current token loc
		internal readonly Scan<I, K, T, S> scan;
		internal int largest, largestLoc, total;
		internal bool greedy = false; // S=AB A=1|12 B=23|2  gready: (12)3  back greedy: 1(23)
		internal int recovery = 10; // no recovery: 0, how many times to recover at eof: > 0
		internal bool treeKeep = true;
		internal bool treeDump = false;
		internal int treeExpect = 1; // no option and key only: 0, no option: 1, more: 2
		internal Func<object, string> treeDumper = null;

		struct Match
		{
			internal Alt a;
			internal int from, to, step; // empty (always predicted): from==to (step could be 0 in quantifier)
			internal int prev; // complete or option: >=0, predict: >=-1, shift: see code, repeat: kept
			internal int tail; // Alt: >=0, predict: -1, shift: -2, option: -3, repeat: kept, recovery: -4

			public override string ToString() => $"{from}:{to}#{step} {a}";
		}

		Parser(Prod start, Scan<I, K, T, S> scan)
		{
			this.start = start; reck = Array.Empty<K>(); this.scan = scan;
		}

		// build a Tree from matched and kept Alts, Tree.tokens unset
		public virtual Tree<S> Parse(I input)
		{
			if (input != null)
				scan.Load(input);
			int m = Earley(out Tree<S> recs, recovery);
			Tree<S> t = m >= 0 ? Accepted(m, null) : Rejected();
			matchn = 0;
			locs.Clear();
			if (input != null)
				scan.Unload();
			if (recs != null)
				t.AddSub(recs);
			return t;
		}

		public virtual bool Check(I input)
		{
			if (input != null)
				scan.Load(input);
			bool gre = greedy; greedy = false;
			int m = Earley(out _, 0); greedy = gre;
			matchn = 0;
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

			total = matchn;
			for (int x = locs[loc]; x < matchn; x++) {
				var m = matchs[x];
				if (m.a.name == start.name && m.from == 0 && m.a.s[m.step].p == null)
					return x;
			}
			if (reck.Length > 0 && rec > 0) {
				recs ??= new Tree<S>();
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
			recs = null;
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
						if (pm.a.s[pm.step].p is Prod p && p.name == m.a.name)
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
			if (matchn == matchs.Length) Array.Resize(ref matchs, matchs.Length << 1);
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
								if (v is K k && scan.Is(reck[r], k)) {
									Add(m.a, m.from, m.to, y, x, -4);
									goto Rec;
								}
					}
				Rec:;
				}
			return total < matchn;
		}

		Tree<S> Accepted(int match, Tree<S> insert)
		{
			var m = matchs[match];
			if (m.from == m.to && m.step == 0 && m.a.s.Length > 1)
				return null;
			var t = ((m.a.keep == 0 ? treeKeep : m.a.keep > 0) ? null : insert)
				?? new Tree<S> {
					name = m.a.name, from = m.from, to = m.to, dump = Dump(m)
				};
			for (; m.tail != -1; m = matchs[m.prev])
				if (m.tail >= 0)
					Accepted(m.tail, t);
				else if (m.tail == -4)
					t.AddHead(new Tree<S> { name = "", from = m.from, to = m.to, err = -1 });
			return insert == null || insert == t ? t : insert.AddHead(t);
		}

		Tree<S> Rejected()
		{
			int from = locs[loc] < matchn ? loc : loc - 1, x = locs[from];
			var t = new Tree<S> {
				name = "", from = from, to = loc, err = 1,
				dump = treeDump ? Dump(scan.Tokens(0, loc)) : null
			};
			var errs = new bool[matchn];
			for (int y = matchn - 1, z; (z = y) >= x; y--) {
			Prev: var m = matchs[z]; var s = m.a.s[m.step];
				if (s.p != null && !errs[z])
					if (m.step > 0) {
						errs[z] = true;
						if (treeExpect >= 2 ||
								((int)s.q & 1) != 0 && (treeExpect == 1 || s.p is K)) {
							var e = s.p is Prod p ? p.alts[0].hint ?? p.name : s.p;
							var d = treeDump ? $"{Esc(e)} expected by {m.a.hint}!{m.step} {Dump(m)}"
								: m.a.hint;
							t.AddHead(new Tree<S> {
								name = m.a.name, from = m.from, to = m.to,
								err = m.step, expect = e, dump = d
							});
						}
					}
					else if ((z = m.prev) >= 0)
						goto Prev;
			}
			return t;
		}

		public S Tokens(Tree<S> t) => t.tokens ??= scan.Tokens(t.from, t.to);

		string Dump(Match m) => !treeDump ? null : $"{m.a} :: {Dump(scan.Tokens(m.from, m.to))}";
		string Dump(object v) => treeDumper?.Invoke(v) ?? Esc(v.ToString());
		static string Esc(object v) => v.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

		// bootstrap

		static readonly Parser<string, char, char, string> boot;

		static Parser()
		{
			var scan = new BootScan();
			// prod name, prod-or-key-with-qua-alt1 con1|alt2 con2  \x1 is |
			var grammar = new Dictionary<string, string>() {
			{ "gram",  "rec? eol* prod prods* eol*" },
			{ "prods", "eol+ prod" },
			{ "prod",  "name S* = con* alt* hint?" },
			{ "name",  "W+" },
			{ "con",   "W+|sym|S+" }, // word to prod name or scan.Keys
			{ "sym",   "Q|O+|\\ E|\\ u X X X X" }, // unescape to scan.Keys except Qua
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
			{ "reck",  "W+|O+|\\ E|\\ u X X X X" } }; // unescape to scan.Keys

			// build prod
			var prods = grammar.ToDictionary(
				kv => kv.Key,
				kv => new Parser<string, char, char, string>.Prod { name = kv.Key });
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
						return new Parser<string, char, char, string>.Con {
							p = prods.TryGetValue(p, out var a) ? a : (object)scan.Keys(p).First(),
							q = q,
						};
					}).Append(new Parser<string, char, char, string>.Con { q = One })
					.ToArray();
					return new Parser<string, char, char, string>.Alt { name = kv.Key, s = s };
				}).ToArray();
			// spaces before hintw are greedy
			prods["hint"].alts[0].greedy = 1;
			// keep these in the tree
			foreach (var c in prods["con"].alts.Take(1) // word
				.Concat(new[] { "prod", "alt", "name", "sym", "hintg", "hintk", "hintw", "hinte", "reck" }
				.SelectMany(x => prods[x].alts)))
				c.keep = 1;
			boot = new Parser<string, char, char, string>(prods["gram"], scan) {
				greedy = false, treeKeep = false, treeDump = false
			};
		}

		public Parser(string gram, Scan<I, K, T, S> scan)
		{
			boot.scan.Load(gram);
			var top = boot.Parse(null);
			if (top.err != 0) {
				boot.scan.Unload(); boot.treeDump = true;
				boot.Parse(gram).Dump(); boot.treeDump = false;
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
				.SelectMany(t => scan.Keys(BootScan.Esc(gram, t.from, t.to, 1)))
				.ToArray();
			if (reck.Length > 4)
				throw new Exception("too many recovery keys");
			var prods = top.Where(t => t.name == "prod");
			var names = prods.ToDictionary(
				p => boot.Tokens(p.head),
				p => new Prod { name = p.head.tokens });

			foreach (var p in prods) {
				// build alts
				var az = p.Where(t => t.name == "alt").Prepend(p).Select(ta => {
					// prod name or keys or quantifier ...
					var z = ta.Where(t => t.name == "sym" || t.name == "con").SelectMany(t =>
						t.name == "sym" ?
							gram[t.from] == '?' ? new object[] { Opt } :
							gram[t.from] == '*' ? new object[] { Any } :
							gram[t.from] == '+' ? new object[] { More } :
							scan.Keys(BootScan.Esc(gram, t.from, t.to, 1)).Cast<object>()
						:
							names.TryGetValue(boot.Tokens(t), out Prod p) ? new object[] { p } :
							scan.Keys(t.tokens).Cast<object>())
						.Append(null).ToArray();
					// build alt
					var s = z.Select((v, x) =>
						new Con { p = v, q = v != null && z[x + 1] is Qua r ? r : One })
						.Where(v => !(v.p is Qua))
						.ToArray();
					var rk = (byte)reck.Select((r, x) =>
						z.Any(v => v is K k && scan.Is(r, k)) ? 1 << x : 0).Sum();
					return new Alt {
						name = p.head.tokens, s = s, reck = rk
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
							for (var w = boot.Tokens(h); ax <= x; ax++) {
								az[ax].greedy = g; az[ax].keep = k; az[ax].hint = w != "" ? w : null;
							}
						// each hint is for only one line
						if (h.name == "hinte") ax = x + 1;
					}
				});
				names[p.head.tokens].alts = az;
			}
			start = names[prods.First().head.tokens];
			this.scan = scan;
			boot.scan.Unload();
		}
	}
}
