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
		public int from, to; // from token index to index excluded, for error tokens may < 0
		public int err; // no error: 0, error: -1, error step: > 0, recovered ~step: < -1
		public object info; // error Token, expected Alt hint/name or K, or recovered Prod hint/name or K
		public string dump;

		public override string ToString()
		{
			return $"{from}:{to}{(err == 0 ? info : err < -1 ? "" + err : "!")} {dump ?? info ?? name}";
		}

		public override string ToString(object extra)
		{
			if (!(extra is Func<int, int, (int, int, int, int)> loc))
				return ToString();
			var (fl, fc, tl, tc) = loc(from, to);
			return $"{fl}.{fc}:{tl}.{tc}{(err == 0 ? info : err < -1 ? "" + err : "!")} {dump ?? info ?? name}";
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
			internal sbyte keep; // parser.keep: 0, thru: -1, keep: 1
			internal bool token; // save first shift token to Tree.info
			internal int recover; // no: -1, recover at Con index of last One or More K: > 0,
								  // or recover at last Con without scan: s.Length
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
		List<K> reck;
		Match[] matchs = new Match[16384];
		int matchn, completen;
		readonly List<int> locs = new List<int>(); // matchn before each token
		int loc; // next token index
		internal int largest, largestLoc; // largest number of new matches before each token, and the loc
		internal Sc scan;
		public bool greedy = false; // for any Alt eg. S=AB A=1|12 B=23|2  gready: (12)3  back greedy: 1(23)
		public int recover = 10; // no recovery: 0, how many times to recover at eof: > 0
		public bool keep = true;
		public int dump = 0; // no: 0, tokens for tree leaf: 1, tokens: 2, tokens and Alt: 3
		public int errExpect = 2; // hint only: 0, and One or More K: 1, and One or More: 2, all: 3
		public Func<object, string> dumper = null;

		struct Match
		{
			internal Alt a;
			internal int from, to, step; // empty (always predicted): from==to (step could be 0 in quantifier)
			internal int prev; // complete or option: >=0, predict: >=-1, shift: see code, repeat: kept
			internal int tail; // Alt: >=0, predict: -1, shift: -2, option: -3, repeat: kept, recover: -4

			public override string ToString()
				=> $"{from}:{to}{(a.s[step].p != null ? "'" : "#")}{step}" +
				$"{(tail >= 0 ? "^" : tail == -1 ? "p" : tail == -2 ? "s" : tail == -3 ? "?" : "r")} {a}";
		}

		ParserBase(Prod start) => this.start = start;

		public ParserBase<K, Tk, N, Tr, Sc> Load(Sc scan) { this.scan = scan; return this; }

		// build a Tree from matched and kept Alts, Tree.tokens unset
		public virtual Tr Parse()
		{
			int m = Earley(out Tr recs, recover);
			Tr t = m >= 0 ? Accepted(m, null) : Rejected();
			if (recs != null)
				if (m >= 0)
					t.AddNextSub(recs);
				else
					t = recs.head.Remove().AddNextSub(recs);
			Array.Fill(matchs, default, 0, matchn);
			matchn = 0;
			locs.Clear();
			return t;
		}

		public virtual bool Check()
		{
			bool gre = greedy; greedy = false;
			int m = Earley(out _, 0); greedy = gre;
			Array.Fill(matchs, default, 0, matchn);
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
			int shift = 0;
		Loop: do {
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
			} while ((shift = Shift(shift)) > 0);

			completen = matchn;
			for (int x = locs[loc]; x < matchn; x++) {
				var m = matchs[x];
				if (Eq.Equals(m.a.name, start.name) && m.from == 0 && m.a.s[m.step].p == null)
					return x;
			}
			if (reck?.Count != 0 && rec > 0) {
				recs ??= new Tr();
				recs.Add(Rejected());
				if (shift == 0)
					for (; ; locs.Add(matchn), ++loc)
						if (Recover(false, ref shift))
							goto Loop;
						else if (!scan.Next())
							break;
				rec--;
				if (Recover(true, ref shift))
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

		int Shift(int shift)
		{
			if (shift == -1 || shift >= 0 && !scan.Next())
				return -1;
			if (shift >= 0)
				locs.Add(matchn);
			else
				locs[loc + 1] = matchn;
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

		bool Recover(bool eof, ref int shift)
		{
			shift = eof ? -1 : 0;
			if (eof || reck == null || reck.Exists(k => scan.Is(k)))
				for (int x = matchn - 1; x >= 0; x--) {
					var m = matchs[x]; var r = m.a.recover; var s = m.a.s;
					if (m.step <= r && m.step > 0
						&& (eof ? s[m.step].p != null
							: r == s.Length ? m.step == r - 2 : scan.Is((K)s[r].p))) {
						if (r == s.Length) {
							--loc; shift = -2;
						}
						Add(m.a, m.from, m.to, Math.Min(r + 1, s.Length - 1), x, -4);
						break;
					}
				}
			return completen < matchn;
		}

		Tr Accepted(int match, Tr up)
		{
			var m = matchs[match];
			if (m.from == m.to && m.step == 0 && m.a.s.Length > 1)
				return null;
			Tr t = (m.a.keep == 0 ? keep : m.a.keep > 0) ? null : up, New = null;
			if (t == null)
				t = New = new Tr { name = m.a.name, from = m.from, to = m.to };
			for (var mp = m; mp.tail != -1; mp = matchs[mp.prev])
				if (mp.tail >= 0)
					Accepted(mp.tail, t);
				else if (mp.tail == -2 && mp.a.token)
					t.info = scan.Token(mp.to - 1);
				else if (mp.tail == -4) {
					Debug.Assert(mp.a == m.a);
					var p = m.a.s[mp.step - 1].p;
					t.AddHead(new Tr {
						name = m.a.name, from = mp.from, to = mp.to, err = ~m.a.recover,
						info = p is Prod cp ? cp.alts[0].hint ?? (object)cp.name : p,
						dump = dump < 2 ? null : $"{m.a.name} :: recover " +
							$"{(p is Prod pp ? pp.name : p)} at {Dump(scan.Token(mp.to - 1))}",
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
				if (s.p == null || errs[z])
					continue;
				if (m.step == 0)
					if ((z = m.prev) >= 0)
						goto Prev;
					else
						continue;
				errs[z] = true;
				var p = s.p as Prod;
				if (errExpect >= 3 || p?.alts[0].hint != null ||
						((int)s.q & 1) > 0 &&
							(errExpect == 2 || errExpect == 1 && s.p is K)) {
					var e = p != null ? p.alts[0].hint ?? (object)p.name : s.p;
					var d = m.a.hint ?? m.a.name.ToString();
					d = dump <= 0 ? d
						: dump <= 2 ? $"{Esc(e)} expected for {d}"
						: $"{Esc(e)} expected for {d}!{m.step} {Dump(m, true)}";
					t.AddHead(new Tr {
						name = m.a.name, from = m.from, to = m.to,
						err = m.step, info = e, dump = d
					});
				}
			}
			return t;
		}

		protected static EqualityComparer<N> Eq = EqualityComparer<N>.Default;

		protected virtual N Name(string name) => (N)(object)name;

		string Dump(Match m, bool leaf)
		{
			return dump <= 0 || dump == 1 && !leaf ? null :
				(dump <= 2 ? m.a.hint ?? m.a.name.ToString() : m.a.ToString())
				+ $" :: {Dump(scan.Tokens(m.from, m.to))}";
		}
		string Dump(object v) => dumper?.Invoke(v) ?? Esc(v.ToString());
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
			{ "gram",  "eol* prod prods* eol*" },
			{ "prods", "eol+ prod" },
			{ "prod",  "name S* = con* alt* hint?" },
			{ "name",  "W+" },
			{ "con",   "W+|sym|S+" }, // word to prod name or scan.Keys
			{ "sym",   "Q|O+|\\ E" }, // unescaped to scan.Keys except Qua
			{ "alt",   "ahint? \x1 S* con*" },
			{ "hint",  "= hintg? hintk? hintt? hintr? S* hintw" },
			{ "hintg", "*" }, // hint greedy
			{ "hintk", "+|-" }, // hint keep
			{ "hintt", "_" }, // hint token
			{ "hintr", "\x1|\x1 \x1" }, // hint recover
			{ "hintw", "H*" }, // hint words
			{ "hinte", "eol" }, // to split prod into lines
			{ "ahint", "hint? hinte" },
			{ "eol",   "S* comm? \r? \n S*" },
			{ "comm",  "= = V*" } }; // unescape to scan.Keys

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
				.Concat(new[] { "prod", "alt", "name", "sym", "hintg", "hintk", "hintt", "hintr", "hintw", "hinte" }
				.SelectMany(x => prods[x].alts)))
				c.keep = 1;
			boot = new ParserChar(prods["gram"]) {
				greedy = false, keep = false, dump = 0
			};
		}

		public ParserBase(string gram, Sc scan)
		{
			Load(scan);
			using var bscan = new BootScan(gram);
			var top = boot.Load(bscan).Parse();
			if (top.err != 0) {
				using var bscan2 = new BootScan(gram);
				var dump = boot.dump; boot.dump = 3;
				boot.Load(bscan2).Parse().Dump(); boot.dump = dump;
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
			//   \ hintg or hintk or hintr ... hintw
			// \ prod ...
			var prods = top.Where(t => t.name == "prod");
			var names = prods.ToDictionary(
				p => p.head.dump = boot.scan.Tokens(p.head.from, p.head.to),
				p => new Prod { name = Name(p.head.dump) }
			);
			reck = new List<K>();

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
					return new Alt { name = prod.name, s = s, recover = -1 };
				}).ToArray();
				// build hint
				int ax = 0;
				p.Where(t => t.name == "alt").Append(p).Each((t, x) => {
					sbyte g = 0, k = 0; bool tk = false; int r = -1;
					foreach (var h in t) {
						if (h.name == "hintg") g = 1;
						if (h.name == "hintk") k = (sbyte)(gram[h.from] == '+' ? 1 : -1);
						if (h.name == "hintt") tk = true;
						if (h.name == "hintr") r = h.to - h.from;
						if (h.name == "hintw") { // apply hints
							var w = boot.scan.Tokens(h.from, h.to);
							for (; ax <= x; ax++) {
								var a = az[ax];
								a.greedy = g; a.keep = k; a.token = tk;
								a.hint = w != "" ? w : null;
								if (r == 1) // search Con for recovery
									for (int y = a.s.Length - 1; y > 0; y--)
										if (a.s[y].p is K key && ((int)a.s[y].q & 1) > 0) {
											a.recover = y;
											if (reck?.Contains(key) == false)
												reck.Add(key);
											break;
										}
								if (r == 2) { // recover in place
									a.recover = a.s.Length;
									reck = null;
								}
							}
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
