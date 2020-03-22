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

		Prod start;
		readonly List<Match> matchs = new List<Match>();
		readonly List<int> locs = new List<int>();
		readonly HashSet<int> errs = new HashSet<int>();
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
			internal Alt a;
			internal int from, to, step; // empty (always predicted): from==to (step could be 0 in quantifier)
			internal int prev; // complete or option: >=0, predict: >=-1, shift: see code, repeat: kept
			internal int tail; // Alt: >=0, predict: -1, shift: -2, option: -3, repeat: kept, recovery: -4

			public override string ToString() => $"{from}:{to}#{step} {a}";
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
			int m = Earley(out _, 0); greedy = gre;
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
			Loop: int shift;
			do {
				int c, p;
				Complete(locs[loc]); c = matchs.Count;
				Predict(locs[loc]); p = matchs.Count;
				for (; ; ) {
					Complete(c);
					if (c == (c = matchs.Count))
						break;
					Predict(p);
					if (p == (p = matchs.Count))
						break;
				}
				if (matchs.Count - locs[loc] > largest)
					largest = matchs.Count - locs[largestLoc = loc];
			} while ((shift = Shift()) > 0);

			total = matchs.Count;
			for (int x = locs[loc]; x < matchs.Count; x++) {
				var m = matchs[x];
				if (m.a.name == start.name && m.from == 0 && m.a.s[m.step].p == null)
					return x;
			}
			if (reck.Length > 0 && rec > 0) {
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
			for (; x < matchs.Count; x++) {
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
			for (int x = locs[loc]; x < matchs.Count; x++) {
				var m = matchs[x];
				if ((x >= empty || m.from == loc) && m.a.s[m.step].p == null)
					for (int px = locs[m.from], py = m.from < loc ? locs[m.from + 1] : matchs.Count;
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
			locs.Add(matchs.Count);
			for (int x = locs[loc], y = locs[++loc]; x < y; x++) {
				var m = matchs[x];
				if (m.a.s[m.step].p is K k && scan.Is(k))
					Add(m.a, m.from, m.to, m.step + 1, m.tail != -2 ? x : m.prev, -2); // m.to < loc
			}
			return matchs.Count - locs[loc];
		}

		void Add(Alt a, int from, int pto, int step, int prev, int tail)
		{
			var u = new Match {
				a = a, from = from, to = loc, step = step, prev = prev, tail = tail
			};
			for (int x = locs[loc]; x < matchs.Count; x++) {
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
			matchs.Add(u);
			if (pto < loc && step > 0 && (int)a.s[--u.step].q > 1)
				matchs.Add(u); // prev and tail kept
		}

		bool Recover(bool eof)
		{
			for (int r = 0; r < reck.Length; r++)
				if (eof || scan.Is(reck[r])) {
					for (int x = matchs.Count - 1; x >= 0; x--) {
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
			return total < matchs.Count;
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
					t.Insert(new Tree<S> { name = "", from = m.from, to = m.to, err = -1 });
			return insert == null || insert == t ? t : insert.Insert(t);
		}

		Tree<S> Rejected()
		{
			int to = locs[loc] < matchs.Count ? loc : loc - 1, x = locs[to];
			var t = new Tree<S> {
				name = "", from = to, to = loc, err = 1,
				dump = treeDump ? Dump(scan.Tokens(0, loc)) : null
			};
			for (int y = matchs.Count - 1, z; (z = y) >= x; y--) {
				Prev: var m = matchs[z]; var s = m.a.s[m.step];
				if (s.p != null && !errs.Contains(z))
					if (m.step > 0) {
						errs.Add(z);
						if (treeExpect >= 2 ||
								((int)s.q & 1) != 0 && (treeExpect == 1 || s.p is K)) {
							var e = s.p is Prod p ? p.alts[0].hint ?? p.name : s.p;
							var d = treeDump ? $"{Esc(e)} expected by {m.a.hint}!{m.step} {Dump(m)}"
								: m.a.hint;
							t.Insert(new Tree<S> {
								name = m.a.name, from = m.from, to = m.to,
								err = m.step, expect = e, dump = d
							});
						}
					}
					else if ((z = m.prev) >= 0)
						goto Prev;
			}
			errs.Clear();
			return t;
		}

		string Dump(Match m) => !treeDump ? null : $"{m.a} :: {Dump(scan.Tokens(m.from, m.to))}";
		string Dump(object v) => treeDumper?.Invoke(v) ?? Esc(v.ToString());
		static string Esc(object v) => v.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

		// bootstrap

		static readonly Parser<string, char, char, string> boot;

		static Parser()
		{
			boot = new Parser<string, char, char, string>() {
				scan = new BootScan(), greedy = false, treeKeep = false, treeDump = false
			};
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
			// build prod
			var prods = grammar.ToDictionary(
				kv => kv.Key,
				kv => new Parser<string, char, char, string>.Prod { name = kv.Key });
			// build alt
			foreach (var kv in grammar)
				prods[kv.Key].alts = kv.Value.Split("|").Select(alt => {
					var z = alt.Replace('\x1', '|')
						.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					var s = z.Select(c => {
						var q = c.Length == 1 || !(c[^1] is char x) ? One
								: x == '?' ? Opt : x == '*' ? Any : x == '+' ? More : One;
						var p = q == One ? c : c[0..^1];
						return new Parser<string, char, char, string>.Con {
							p = prods.TryGetValue(p, out var a) ? a : (object)boot.scan.Keys(p).First(),
							q = q,
						};
					}).Append(new Parser<string, char, char, string>.Con { q = One })
					.ToArray();
					return new Parser<string, char, char, string>.Alt { name = kv.Key, s = s };
				}).ToArray();
			// build boot parser
			prods["hint"].alts[0].greedy = 1;
			foreach (var c in prods["con"].alts.Take(1) // word
				.Concat(new[] {
					"prod", "alt", "name", "sym", "hintg", "hintk", "hintw", "hinte", "reck"
				}.SelectMany(x => prods[x].alts)))
				c.keep = 1;
			boot.start = prods["gram"];
			boot.reck = Array.Empty<char>();
		}

		void Boot(string gram)
		{
			boot.scan.Load(gram);
			var top = boot.Parse(null);
			if (top.err != 0) {
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
			reck = top.Where(t => t.name == "reck")
				.SelectMany(t => scan.Keys(BootScan.Esc(gram, t.from, t.to, 1)))
				.ToArray();
			if (reck.Length > 4)
				throw new Exception("too many recovery keys");
			var prods = top.Where(t => t.name == "prod");
			var names = prods.ToDictionary(
				p => BootTokens(p.head),
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
							names.TryGetValue(BootTokens(t), out Prod p) ? new object[] { p } :
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
							for (var w = BootTokens(h); ax <= x; ax++) {
								az[ax].greedy = g; az[ax].keep = k; az[ax].hint = w != "" ? w : null;
							}
						// each hint is for only one line
						if (h.name == "hinte") ax = x + 1;
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
