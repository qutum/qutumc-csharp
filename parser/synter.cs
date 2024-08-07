//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

using qutum.parser.meta;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static qutum.parser.Qua;

namespace qutum.parser;

using SynterChar = SynterEarley<char, char, string, SyntStr, LerStr>;

// syntax tree
public class Synt<N, T> : LinkTree<T> where T : Synt<N, T>
{
	public N name;
	public int from, to; // lexis from loc to loc excluded, for error may < 0
	public int err; // no error: 0, error: -1, error step: > 0, recovered: -4
	public object info; // error Lexi, expected Alt hint/name or K, or recovered Prod hint/name or K
	public string dump;

	public override string ToString()
	{
		return $"{from}:{to}{(err == 0 ? info : err < -1 ? "" + err : "!")} {dump ?? info ?? name}";
	}

	public override string ToString(object extra)
	{
		if (extra is not Func<int, int, (int, int, int, int)> loc)
			return ToString();
		var (fl, fc, tl, tc) = loc(from, to);
		return $"{fl}.{fc}:{tl}.{tc}{(err == 0 ? info != null ? " " + info : ""
			: err < -1 ? "" + err : "!")} {dump ?? info ?? name}";
	}
}

public class SyntStr : Synt<string, SyntStr>
{
}

// syntax parser
public class Synter<K, N, T, Ler> : SynterEarley<K, Lexi<K>, N, T, Ler>
	where K : struct where T : Synt<N, T>, new() where Ler : LexerSeg<K, Lexi<K>>
{
	public Synter(string gram, Ler ler) : base(gram, ler) { }
}

public class SynterStr : SynterChar
{
	public SynterStr(string gram, LerStr ler = null) : base(gram, ler ?? new LerStr("")) { }
}

public enum Qua : byte { Opt = 0, One = 1, Any = 2, More = 3 };

// syntax parser using extended Earley algorithm
public class SynterEarley<K, L, N, T, Ler> where T : Synt<N, T>, new() where Ler : Lexer<K, L>
{
	sealed class Prod
	{
		internal N name;
		internal Alt[] alts;
	}
	struct Con
	{
		internal object p; // Prod or K or null for complete;
		internal Qua q;
	}
	sealed class Alt
	{
		internal N name;
		internal Con[] s;
		internal bool prior; // prior than other Alts of the same Prod or of all recovery
		internal sbyte greedy; // as synter.greedy: 0, greedy: 1, back greedy: -1
		internal sbyte synt; // as synter.tree: 0, make Synt: 2, omit Synt: -1
							 // omit if no prev nor next or has 0 or 1 sub: 1
		internal bool lex; // save first shifted lexi to Synt.info
		internal bool errExpect; // make expect synt when error
		internal int recover; // no: -1, recover ahead to last One or More K index: > 0,
							  // recover just at last One or More Prod index without shift: > 0
		internal K recPair; // skip each pair of this and recovery K, no skip: recovery K
		internal K recDeny; // deny this K when recover ahead, nothing denied: recovery K
		internal string hint;
		internal string dump;

		public override string ToString()
			=> dump ??= name + "="
				+ string.Join(' ', s.Where(c => c.p != null).Select(c =>
				(c.p is Prod p ? p.name.ToString() : Esc(c.p))
				+ (c.q == More ? "+" : c.q == Any ? "*" : c.q == Opt ? "?" : "")));
	}

	readonly Prod begin;
	readonly List<Alt> reca = [];
	Match[] matchs = new Match[16384];
	internal int matchn, completen;
	internal int lexn; // lexi count
	readonly List<int> lexm = []; // [matchn before lexi]
	readonly int[] recm; // [latest match index of recovery Alt]
	internal Ler ler;
	public bool greedy = false; // greedy: true, may or may not: false
								// eg. S=AB A=1|12 B=23|2  gready: (12)3  back greedy: 1(23)
	public int recover = 10; // no recovery: 0, how many times to recover at eof: > 0
	public bool tree = true; // make whole tree from complete Alts
	public int dump = 0; // no: 0, lexis for tree leaf: 1, lexis: 2, lexis and Alt: 3
	public int errExpect = 2; // no: 0, One or More: 2, all: 3
	public Func<object, string> dumper = null;

	struct Match
	{
		internal Alt a;
		internal int from, to, step; // empty (always predicted): from==to (step could be 0 in quantifier)
		internal int prev; // complete or option: >=0, predict: >=-1, shift: see code, repeat: kept
		internal int tail; // Alt: >=0, predict: -1, shift: -2, option: -3, repeat: kept, recover: -4

		public override readonly string ToString()
			=> $"{from}:{to}{(a.s[step].p != null ? "'" : "#")}{step}" +
			$"{(tail >= 0 ? "^" : tail == -1 ? "p" : tail == -2 ? "s" : tail == -3 ? "?" : "r")} {a}";
	}

	SynterEarley(Prod begin) { this.begin = begin; recm = []; }

	public SynterEarley<K, L, N, T, Ler> Begin(Ler ler) { this.ler = ler; return this; }

	// make synt from complete Alts
	public virtual T Parse()
	{
		matchn = 0;
		Array.Fill(recm, -1, 0, recm.Length);
		int m = Earley(out T err, recover);
		bool _ = false;
		T t = m >= 0
			? Accepted(m, null, ref _).AddNextSub(err)
			: err.head.Remove().AddNextSub(err);
		Array.Fill(matchs, default, 0, matchn);
		lexm.Clear();
		return t;
	}

	public virtual bool Check()
	{
		matchn = 0;
		Array.Fill(recm, -1, 0, recm.Length);
		bool gre = greedy; greedy = false;
		int m = Earley(out _, 0); greedy = gre;
		Array.Fill(matchs, default, 0, matchn);
		lexm.Clear();
		return m >= 0;
	}

	int Earley(out T err, int rec)
	{
		lexm.Add(lexn = 0);
		foreach (var x in begin.alts)
			Add(x, 0, 0, 0, -1, -1);
		err = null;
		if (rec == 0) rec = -1;
		int shift = 0;
	Loop: do {
			int c, p, cc, pp;
			Complete(lexm[lexn]); cc = c = matchn;
			Predict(lexm[lexn]); pp = p = matchn;
			for (bool re = false; ;) {
				re = Complete(c) | re;
				if (!re && c == (c = matchn))
					break;
				re = Predict(p) | re;
				if (!re && p == (p = matchn))
					break;
				if (re) {
					c = cc; p = pp; re = false;
				}
			}
		} while ((shift = Shift(shift)) > 0);

		completen = matchn;
		for (int x = lexm[lexn]; x < matchn; x++) {
			var m = matchs[x];
			if (Eq.Equals(m.a.name, begin.name) && m.from == 0 && m.a.s[m.step].p == null)
				return x;
		}
		err ??= new T();
		if (rec != 0)
			err.Add(Rejected());
		if (reca.Count > 0 && rec > 0) {
			if (shift == 0)
				for (; ; lexm.Add(matchn), ++lexn)
					if (Recover(false, ref shift))
						goto Loop;
					else if (!ler.Next())
						break;
			rec--;
			if (Recover(true, ref shift))
				goto Loop;
		}
		return -1;
	}

	bool Predict(int x)
	{
		bool back = false;
		for (; x < matchn; x++) {
			var m = matchs[x];
			if (m.a.s[m.step].p is Prod p)
				foreach (var alt in p.alts)
					back = Add(alt, lexn, lexn, 0, x, -1) | back;
			if (((int)m.a.s[m.step].q & 1) == 0)
				back = Add(m.a, m.from, m.to, m.step + 1, x, -3) | back; // m.to == loc
		}
		return back;
	}

	bool Complete(int empty)
	{
		bool back = false;
		for (int x = lexm[lexn]; x < matchn; x++) {
			var m = matchs[x];
			if ((x >= empty || m.from == lexn) && m.a.s[m.step].p == null)
				for (int px = lexm[m.from], py = m.from < lexn ? lexm[m.from + 1] : matchn;
						px < py; px++) {
					var pm = matchs[px];
					if (pm.a.s[pm.step].p is Prod p && Eq.Equals(p.name, m.a.name))
						back = Add(pm.a, pm.from, pm.to, pm.step + 1, px, x) | back; // pm.to <= loc
				}
		}
		return back;
	}

	int Shift(int shift)
	{
		if (shift == -1 || shift >= 0 && !ler.Next())
			return -1;
		if (shift >= 0)
			lexm.Add(matchn);
		else
			lexm[lexn + 1] = matchn;
		for (int x = lexm[lexn], y = lexm[++lexn]; x < y; x++) {
			var m = matchs[x];
			if (m.a.s[m.step].p is K k && ler.Is(k))
				Add(m.a, m.from, m.to, m.step + 1, // m.to < loc
					m.tail != -2 || m.a.lex ? x : m.prev, -2);
		}
		return matchn - lexm[lexn];
	}

	bool Add(Alt a, int from, int pto, int step, int prev, int tail)
	{
		var u = new Match {
			a = a, from = from, to = lexn, step = step, prev = prev, tail = tail
		};
		if (a.prior && (tail >= 0 || tail == -2) && a.s[step].p == null)
			for (int x = lexm[lexn]; x < matchn; x++) {
				var m = matchs[x];
				if (!m.a.prior && m.from == from && m.a.s[m.step].p == null
						&& Eq.Equals(m.a.name, a.name)) {
					matchs[x] = u;
					return true;
				}
			}
		for (int x = lexm[lexn]; x < matchn; x++) {
			var m = matchs[x];
			if (m.a == a && m.from == from && m.step == step) {
				if (a.greedy == 0 && !greedy || m.tail == -1 || u.tail == -1
					|| m.prev == prev && m.tail == tail)
					return false;
				bool set = false; var w = u;
				for (int mp = m.prev, wp = w.prev; ;) {
					Debug.Assert(m.tail != -1 && w.tail != -1);
					int y = (m.to - matchs[mp].to) - (w.to - matchs[wp].to);
					if (y != 0) set = y < 0 == a.greedy >= 0;
					if (mp == wp)
						break;
					y = matchs[mp].to - matchs[wp].to;
					if (y >= 0)
						mp = (m = matchs[mp]).tail != -1 ? m.prev : -1;
					if (y <= 0)
						wp = (w = matchs[wp]).tail != -1 ? w.prev : -1;
				}
				if (set) {
					matchs[x] = u;
					if (x + 1 < matchn && x + 1 != prev
						&& (m = matchs[x + 1]).a == a && m.from == from
						&& m.step == --u.step && (int)a.s[m.step].q > 1)
						matchs[x + 1] = u;
				}
				return set;
			}
		}
		if (matchn + 2 > matchs.Length) Array.Resize(ref matchs, matchs.Length << 1);
		matchs[matchn++] = u;
		if (pto < lexn && step > 0 && (int)a.s[--u.step].q > 1)
			matchs[matchn++] = u; // prev and tail kept
		if (step > 0 && a.recover >= 0)
			recm[reca.IndexOf(a)] = step <= a.recover ? matchn - 1 : -1;
		return false;
	}

	bool Recover(bool eof, ref int shift)
	{
		shift = eof ? -1 : 0;
		int max = -1;
		for (int ax = 0; ax < reca.Count; ax++) {
			var x = recm[ax];
			if (x < 0)
				continue;
			var m = matchs[x]; var pair = 0;
			if (m.a.s[m.a.recover].p is K k)
				for (int y = m.to; y <= lexn - 2; y++)
					if (ler.Is(y, k))
						if (pair == 0) {
							recm[ax] = -1; // closed
							goto Cont;
						}
						else
							pair--;
					else if (ler.Is(y, m.a.recPair))
						pair++;
					else if (ler.Is(y, m.a.recDeny)) {
						recm[ax] = -1;
						goto Cont;
					}
			if (m.a.s[m.a.recover].p is K k2 ? pair == 0 && (eof || ler.Is(k2))
				: m.step == m.a.recover && (eof || m.to == lexn - 1))
				if (max < 0
					|| m.a.prior && !matchs[max].a.prior
					|| m.a.prior == matchs[max].a.prior && x > max)
					max = x;
				Cont:;
		}
		if (max >= 0) {
			var m = matchs[max];
			if (m.a.s[m.a.recover].p is Prod) {
				--lexn; shift = -2;
			}
			Add(m.a, m.from, m.to, m.a.recover + 1, max, -4);
		}
		return completen < matchn;
	}

	T Accepted(int match, T up, ref bool omitSelf)
	{
		var m = matchs[match];
		if (m.from == m.to && m.step == 0 && m.a.s.Length > 1)
			return null;
		T t = (m.a.synt == 0 ? tree : m.a.synt > 0) ? null : up;
		t ??= new T {
			name = m.a.name, from = m.from, to = m.to
		};
		bool omitSub = false;
		for (var mp = m; mp.tail != -1; mp = matchs[mp.prev])
			if (mp.tail >= 0)
				if (t == up)
					Accepted(mp.tail, t, ref omitSelf);
				else
					Accepted(mp.tail, t, ref omitSub);
			else if (mp.tail == -2 && mp.a.lex)
				t.info = ler.Lex(mp.to - 1);
			else if (mp.tail == -4) {
				Debug.Assert(mp.a == m.a);
				omitSelf = omitSub = m.a.synt == 1;
				var p = m.a.s[mp.step - 1].p;
				t.AddHead(new T {
					name = m.a.name, from = mp.from, to = mp.to, err = -4, info = m.a.hint,
					dump = dump < 2 ? null : $"{m.a.name} :: recover " +
						$"{(p is Prod pp ? pp.name : p)} at {Dump(ler.Lex(mp.to - 1))}",
				});
			}
		if (t != up) {
			if (omitSub && t.head != null && t.head.next == null)
				t.AddSub(t.head.Remove());
			if (m.a.synt != 1 || up == null || t.head?.next != null) {
				up?.AddHead(t);
				t.dump = Dump(m, t.head == null);
				omitSelf = m.a.synt == 1;
			}
			else if (t.head != null) {
				up.AddHead(t.head.Remove());
				omitSelf = false;
			}
		}
		return t;
	}

	T Rejected()
	{
		int from = lexm[lexn] < matchn ? lexn : lexn - 1, x = lexm[from];
		var t = new T {
			name = begin.name, from = from, to = lexn, err = -1,
			info = from < lexn ? (object)ler.Lex(from) : null
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
			if (m.a.errExpect || errExpect >= 3 || errExpect == 2 && ((int)s.q & 1) > 0) {
				var exp = s.p is Prod p ? p.alts[0].hint ?? (object)p.name : s.p;
				var d = m.a.hint ?? m.a.name.ToString();
				d = dump <= 0 ? d
					: dump <= 2 ? $"{Esc(exp)} expected for {d}"
					: $"{Esc(exp)} expected for {d}!{m.step} {Dump(m, true)}";
				t.AddHead(new T {
					name = m.a.name, from = m.from, to = m.to,
					err = m.step, info = exp, dump = d
				});
			}
		}
		return t;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211")]
	protected static EqualityComparer<N> Eq = EqualityComparer<N>.Default;

	protected virtual N Name(string name) => (N)(object)name;

	string Dump(Match m, bool leaf)
	{
		return dump <= 0 || dump == 1 && !leaf ? null :
			(dump <= 2 ? m.a.hint ?? m.a.name.ToString() : m.a.ToString())
			+ $" :: {Dump(ler.Lexs(m.from, m.to))}";
	}
	string Dump(object v) => dumper?.Invoke(v) ?? Esc(v.ToString());
	static string Esc(object v)
	{
		return v.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
	}

	// bootstrap
	static readonly SynterChar meta;

	static SynterEarley()
	{
		var keys = new LerStr("");
		// prod name, prod-or-key-with-qua-alt1 con1|alt2 con2  \x1 is |
		var grammar = new Dictionary<string, string> {
			{ "gram",  "eol* prod prods* eol*" },
			{ "prods", "eol+ prod" },
			{ "prod",  "name S* = con* alt* hint?" },
			{ "name",  "W+" },
			{ "con",   "W+|sym|S+" }, // word to prod name or lexer.Keys
			{ "sym",   "Q|G+|\\ E" }, // unescaped to lexer.Keys except Qua
			{ "alt",   "ahint? \x1 S* con*" },
			{ "hint",  "= hintp? hintg? hintt? hintl? hinte? hintr? hintd? S* hintw" },
			{ "hintp", "^" }, // hint prior
			{ "hintg", "*|/" }, // hint greedy
			{ "hintt", "+|-|+ -" }, // hint synt
			{ "hintl", "_" }, // hint lexi
			{ "hinte", "!" }, // hint err expect
			{ "hintr", "\x1|\x1 W+|\x1 G|\x1 \\ E" }, // hint recover
			{ "hintd", "\x1 W+|\x1 G|\x1 \\ E" }, // hint recovery deny
			{ "hintw", "H*" }, // hint words
			{ "heol", "eol" }, // to split prod into lines
			{ "ahint", "hint? heol" },
			{ "eol",   "S* comm? \r? \n S*" },
			{ "comm",  "= = V*" } }; // unescape to lexer.Keys

		// build prod
		var prods = grammar.ToDictionary(
			kv => kv.Key,
			kv => new SynterChar.Prod { name = kv.Key });
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
					return new SynterChar.Con {
						p = prods.TryGetValue(p, out var a) ? a : (object)keys.Keys(p).First(),
						q = q,
					};
				}).Append(new SynterChar.Con { q = One })
				.ToArray();
				return new SynterChar.Alt { name = kv.Key, s = s, recover = -1 };
			}).ToArray();
		// hint synt is greedy
		prods["hintt"].alts[2].greedy = 1;
		// spaces before hintw are greedy
		prods["hint"].alts[0].greedy = 1;
		// make these trees
		foreach (var c in prods["con"].alts.Take(1) // word
			.Concat(new[] { "prod", "alt", "name", "sym",
					"hintp", "hintg", "hintt", "hintl", "hinte", "hintr", "hintd", "hintw", "heol" }
				.SelectMany(x => prods[x].alts)))
			c.synt = 2;
		meta = new SynterChar(prods["gram"]) {
			greedy = false, tree = false, dump = 0
		};
	}

	public SynterEarley(string gram, Ler ler)
	{
		using var input = new MetaStr(gram);
		var top = meta.Begin(input).Parse();
		if (top.err != 0) {
			using var input2 = new MetaStr(gram);
			var dump = meta.dump; meta.dump = 3;
			meta.Begin(input2).Parse().Dump(); meta.dump = dump;
			var e = new Exception(); e.Data["err"] = top;
			throw e;
		}
		// gram
		// \ prod
		//   \ name
		//   \ word or sym or alt ...
		//   \ con // .p refer prod.name
		//   \ alt // .name == prod.name
		//     \ hintg or ... hintw
		//     \ hinte
		//     \ word or sym ...
		//   \ hintg or ... hintw
		// \ prod ...
		var prods = top.Where(t => t.name == "prod");
		var names = prods.ToDictionary(
			p => p.head.dump = meta.ler.Lexs(p.head.from, p.head.to),
			p => new Prod { name = Name(p.head.dump) }
		);

		foreach (var p in prods) {
			var prod = names[p.head.dump];
			// build alts
			var az = p.Where(t => t.name == "alt").Prepend(p).Select(ta => {
				// prod name or keys or quantifier ...
				var z = ta.Where(t => t.name is "sym" or "con").SelectMany(t =>
					t.name == "sym" ? gram[t.from] == '?' ? [Opt]
						: gram[t.from] == '*' ? [Any] : gram[t.from] == '+' ? [More]
						: ler.Keys(MetaStr.Unesc(gram, t.from, t.to)).Cast<object>()
					// for word, search product names first, then lexer keys
					: names.TryGetValue(t.dump = meta.ler.Lexs(t.from, t.to), out Prod p)
						? [p] : ler.Keys(t.dump).Cast<object>())
					.Append(null)
					.ToArray();
				// build alt
				var s = z.Select((v, x) =>
					new Con { p = v, q = v != null && z[x + 1] is Qua r ? r : One })
					.Where(v => v.p is not Qua)
					.ToArray();
				return new Alt { name = prod.name, s = s, recover = -1 };
			}).ToArray();
			// build hint
			int ax = 0;
			foreach (var (t, x) in p.Where(t => t.name == "alt").Append(p).Each()) {
				bool pr = false, l = false, e = false; sbyte g = 0, st = 0; SyntStr r = null, d = null;
				foreach (var h in t) {
					if (h.name == "hintp") pr = true;
					if (h.name == "hintg") g = (sbyte)(gram[h.from] == '*' ? 1 : -1);
					if (h.name == "hintt")
						st = (sbyte)(gram[h.from] == '-' ? -1 : h.from == h.to - 1 ? 2 : 1);
					if (h.name == "hintl") l = true;
					if (h.name == "hinte") e = true;
					if (h.name == "hintr") r = h;
					if (h.name == "hintd") d = h;
					if (h.name == "hintw") { // apply hints
						var w = meta.ler.Lexs(h.from, h.to);
						for (; ax <= x; ax++) {
							var a = az[ax];
							a.prior = pr; a.greedy = g; a.synt = st; a.lex = l; a.errExpect = e;
							a.hint = w != "" ? w : null;
							if (r != null) {
								for (int y = a.s.Length - 2; y > 0; y--)
									if (((int)a.s[y].q & 1) > 0) {
										a.recover = y; // last One or More index found
										reca.Add(a);
										break;
									}
								if (a.recover > 0 && a.s[a.recover].p is K k) {
									a.recPair = r.to - r.from == 1 ? k :
										ler.Keys(MetaStr.Unesc(gram, r.from + 1, r.to)).First();
									a.recDeny = d == null ? k :
										ler.Keys(MetaStr.Unesc(gram, d.from + 1, d.to)).First();
								}
							}
						}
					}
					// each hint is for only one line
					if (h.name == "heol") ax = x + 1;
				}
			}
			prod.alts = az;
		}
		begin = names[prods.First().head.dump];
		recm = new int[reca.Count];
		Begin(ler);
	}
}
