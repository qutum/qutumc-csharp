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
using static qutum.parser.earley.Qua;

namespace qutum.parser.earley;

using EarleyChar = Earley<char, char, string, EsynStr, LerStr>;

// syntax tree
public class Esyn<N, T> : LinkTree<T> where T : Esyn<N, T>
{
	public N name;
	public Jov j; // lexs jot loc, for error may < 0
	public int err; // no error: 0, error: -1, error step: > 0, recovered: -4
	public object info; // error lex, expected alt hint/name or K, or recovered prod hint/name or K
	public string dump;

	public string Dumper(object on, object via) => $"{on ?? j.on}:{via ?? j.via}{(
		err == 0 ? info != null ? " " + info : "" : err < -1 ? "!!" : "!"
		)} {dump ?? (err == 0 ? null : info) ?? name}";
	public override string ToString() => Dumper(null, null);
}

public class EsynStr : Esyn<string, EsynStr>
{
}

public enum Qua : byte { Opt = 0, One = 1, Any = 2, More = 3 };

// syntax parser
public class Earley<K, N, T, Ler> : Earley<K, Lexi<K>, N, T, Ler>
	where K : struct where T : Esyn<N, T>, new() where Ler : LexerSeg<K, Lexi<K>>
{
	public Earley(string gram, Ler ler) : base(gram, ler) { }
}

public class EarleyStr : EarleyChar
{
	public EarleyStr(string gram, LerStr ler = null) : base(gram, ler ?? new LerStr("")) { }
}

// syntax parser using extended Earley algorithm
public class Earley<K, L, N, T, Ler>
	where K : struct where T : Esyn<N, T>, new() where Ler : Lexer<K, L>
{
	sealed class Prod
	{
		internal N name;
		internal Alt[] alts;
	}
	struct Con
	{
		internal object p; // prod or K or null for complete;
		internal Qua q;
	}
	sealed class Alt
	{
		internal N name;
		internal Con[] s;
		internal bool prior; // prior than other alts of the same prod or of all recovery
		internal sbyte greedy; // as earley.greedy: 0, greedy: 1, lazy: -1
		internal sbyte synt; // as earley.tree: 0, make synt: 2, omit synt: -1
							 // omit if no up or has 0 or 1 sub: 1
		internal bool lex; // save first shifted lex to synt.info
		internal bool errExpect; // make expect synt when error
		internal int recover; // no: -1, recover ahead to last One or More K index: > 0,
							  // recover just at last One or More prod index without shift: > 0
		internal K recPair; // skip each pair of this and recovery K, no skip: recovery K
		internal K recDeny; // deny this K when recover ahead, nothing denied: recovery K
		internal string hint;
		internal string dump;

		public override string ToString() => dump ??= name + "="
			+ string.Join(' ', s.Where(c => c.p != null).Select(c =>
				(c.p is Prod p ? p.name : c.p).ToString()
				+ (c.q == More ? "+" : c.q == Any ? "*" : c.q == Opt ? "?" : "")));
	}

	readonly Prod begin;
	readonly List<Alt> reca = [];
	Match[] matchs = new Match[16384];
	public int matchz, completez;
	public int lexz; // lexm size
	readonly List<int> lexm = []; // {matchz before each lex}
	readonly int[] recm; // {latest match index of each recovery alt}
	public Ler ler;
	public bool greedy = false; // greedy: true, may or may not: false
								// eg. S=AB A=1|12 B=23|3  gready: (12)3  lazy: 1(23)
	public int recover = 10; // no recovery: 0, how many times to recover at eor: > 0
	public bool tree = true; // make whole tree from complete alts
	public int dump = 0; // no: 0, lexs for tree leaf: 1, lexs: 2, lexs and alt: 3
	public int errExpect = 2; // no: 0, One or More: 2, all: 3
	public Func<object, string> dumper = null;

	struct Match
	{
		internal Alt a;
		internal Jov j;
		internal int step; // empty (always predicted): j.size==0 (step could be 0 in quantifier)
		internal int prev; // complete or option: >=0, predict: >=-1, shift: see code, repeat: kept
		internal int tail; // alt: >=0, predict: -1, shift: -2, option: -3, repeat: kept, recover: -4

		public override readonly string ToString()
			=> $"{j.on}:{j.via}{(a.s[step].p != null ? "'" : "#")}{step}" +
			$"{(tail >= 0 ? "^" : tail == -1 ? "p" : tail == -2 ? "s" : tail == -3 ? "?" : "r")} {a}";
	}

	Earley(Prod begin) { this.begin = begin; recm = []; }

	public Earley<K, L, N, T, Ler> Begin(Ler ler) { this.ler = ler; return this; }

	// make synt from complete alts
	public virtual T Parse()
	{
		matchz = 0;
		Array.Fill(recm, -1, 0, recm.Length);
		int m = Parse(out T err, recover);
		bool _ = false;
		T t = m >= 0
			? Accepted(m, null, ref _).AppendSubOf(err)
			: err.head.Remove().AppendSubOf(err);
		Array.Fill(matchs, default, 0, matchz);
		lexm.Clear();
		return t;
	}

	public virtual bool Check()
	{
		matchz = 0;
		Array.Fill(recm, -1, 0, recm.Length);
		bool gre = greedy; greedy = false;
		int m = Parse(out _, 0); greedy = gre;
		Array.Fill(matchs, default, 0, matchz);
		lexm.Clear();
		return m >= 0;
	}

	int Parse(out T err, int rec)
	{
		for (lexz = -1; lexz <= ler.Loc(); lexz++)
			lexm.Add(0);
		var from0 = lexz;
		foreach (var x in begin.alts)
			Add(x, (from0, from0), 0, -1, -1);
		err = null;
		if (rec == 0) rec = -1;
		int shift = 0;
	Loop: do {
			int c, p, cc, pp;
			Complete(lexm[lexz]); cc = c = matchz;
			Predict(lexm[lexz]); pp = p = matchz;
			for (bool re = false; ;) {
				re = Complete(c) | re;
				if (!re && c == (c = matchz))
					break;
				re = Predict(p) | re;
				if (!re && p == (p = matchz))
					break;
				if (re) {
					c = cc; p = pp; re = false;
				}
			}
		} while ((shift = Shift(shift)) > 0);

		completez = matchz;
		for (int x = lexm[lexz]; x < matchz; x++) {
			var m = matchs[x];
			if (Eq.Equals(m.a.name, begin.name) && m.j.on == from0 && m.a.s[m.step].p == null)
				return x;
		}
		err ??= new T();
		if (rec != 0)
			err.Add(Rejected());
		if (reca.Count > 0 && rec > 0) {
			if (shift == 0)
				for (; ; lexm.Add(matchz), ++lexz)
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
		for (; x < matchz; x++) {
			var m = matchs[x];
			if (m.a.s[m.step].p is Prod p)
				foreach (var alt in p.alts)
					back = Add(alt, (lexz, lexz), 0, x, -1) | back;
			if (((int)m.a.s[m.step].q & 1) == 0)
				back = Add(m.a, m.j, m.step + 1, x, -3) | back; // m.to == loc
		}
		return back;
	}

	bool Complete(int empty)
	{
		bool back = false;
		for (int x = lexm[lexz]; x < matchz; x++) {
			var m = matchs[x];
			if ((x >= empty || m.j.on == lexz) && m.a.s[m.step].p == null)
				for (int px = lexm[m.j.on], py = m.j.on < lexz ? lexm[m.j.on + 1] : matchz;
						px < py; px++) {
					var pm = matchs[px];
					if (pm.a.s[pm.step].p is Prod p && Eq.Equals(p.name, m.a.name))
						back = Add(pm.a, pm.j, pm.step + 1, px, x) | back; // pm.to <= loc
				}
		}
		return back;
	}

	int Shift(int shift)
	{
		if (shift == -1 || shift >= 0 && !ler.Next())
			return -1;
		if (shift >= 0)
			lexm.Add(matchz);
		else
			lexm[lexz + 1] = matchz;
		for (int x = lexm[lexz], y = lexm[++lexz]; x < y; x++) {
			var m = matchs[x];
			if (m.a.s[m.step].p is K k && ler.Is(k))
				Add(m.a, m.j, m.step + 1, // m.to < loc
					m.tail != -2 || m.a.lex ? x : m.prev, -2);
		}
		return matchz - lexm[lexz];
	}

	bool Add(Alt a, Jov pj, int step, int prev, int tail)
	{
		var u = new Match {
			a = a, j = (pj.on, lexz), step = step, prev = prev, tail = tail
		};
		if (a.prior && (tail >= 0 || tail == -2) && a.s[step].p == null)
			for (int x = lexm[lexz]; x < matchz; x++) {
				var m = matchs[x];
				if (!m.a.prior && m.j.on == pj.on && m.a.s[m.step].p == null
						&& Eq.Equals(m.a.name, a.name)) {
					matchs[x] = u;
					return true;
				}
			}
		for (int x = lexm[lexz]; x < matchz; x++) {
			var m = matchs[x];
			if (m.a == a && m.j.on == pj.on && m.step == step) {
				if (a.greedy == 0 && !greedy || m.tail == -1 || u.tail == -1
					|| m.prev == prev && m.tail == tail)
					return false;
				bool set = false; var w = u;
				for (int mp = m.prev, wp = w.prev; ;) {
					Debug.Assert(m.tail != -1 && w.tail != -1);
					int y = (m.j.via - matchs[mp].j.via) - (w.j.via - matchs[wp].j.via);
					if (y != 0) set = y < 0 == a.greedy >= 0;
					if (mp == wp)
						break;
					y = matchs[mp].j.via - matchs[wp].j.via;
					if (y >= 0)
						mp = (m = matchs[mp]).tail != -1 ? m.prev : -1;
					if (y <= 0)
						wp = (w = matchs[wp]).tail != -1 ? w.prev : -1;
				}
				if (set) {
					matchs[x] = u;
					if (x + 1 < matchz && x + 1 != prev
						&& (m = matchs[x + 1]).a == a && m.j.on == pj.on
						&& m.step == --u.step && (int)a.s[m.step].q > 1)
						matchs[x + 1] = u;
				}
				return set;
			}
		}
		if (matchz + 2 > matchs.Length) Array.Resize(ref matchs, matchs.Length << 1);
		matchs[matchz++] = u;
		if (pj.via < lexz && step > 0 && (int)a.s[--u.step].q > 1)
			matchs[matchz++] = u; // prev and tail kept
		if (step > 0 && a.recover >= 0)
			recm[reca.IndexOf(a)] = step <= a.recover ? matchz - 1 : -1;
		return false;
	}

	bool Recover(bool eor, ref int shift)
	{
		shift = eor ? -1 : 0;
		int max = -1;
		for (int ax = 0; ax < reca.Count; ax++) {
			var x = recm[ax];
			if (x < 0)
				continue;
			var m = matchs[x]; var pair = 0;
			if (m.a.s[m.a.recover].p is K k)
				for (int y = m.j.via; y <= lexz - 2; y++)
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
			if (m.a.s[m.a.recover].p is K k2 ? pair == 0 && (eor || ler.Is(k2))
				: m.step == m.a.recover && (eor || m.j.via == lexz - 1))
				if (max < 0
					|| m.a.prior && !matchs[max].a.prior
					|| m.a.prior == matchs[max].a.prior && x > max)
					max = x;
				Cont:;
		}
		if (max >= 0) {
			var m = matchs[max];
			if (m.a.s[m.a.recover].p is Prod) {
				--lexz; shift = -2;
			}
			Add(m.a, m.j, m.a.recover + 1, max, -4);
		}
		return completez < matchz;
	}

	T Accepted(int match, T up, ref bool omitSelf)
	{
		var m = matchs[match];
		if (m.j.size == 0 && m.step == 0 && m.a.s.Length > 1)
			return null;
		T t = (m.a.synt == 0 ? tree : m.a.synt > 0) ? null : up;
		t ??= new T { name = m.a.name, j = m.j };
		bool omitSub = false;
		for (var mp = m; mp.tail != -1; mp = matchs[mp.prev])
			if (mp.tail >= 0)
				if (t == up)
					Accepted(mp.tail, t, ref omitSelf);
				else
					Accepted(mp.tail, t, ref omitSub);
			else if (mp.tail == -2 && mp.a.lex)
				t.info = ler.Lex(mp.j.via - 1);
			else if (mp.tail == -4) {
				Debug.Assert(mp.a == m.a);
				omitSelf = omitSub = m.a.synt == 1;
				var p = m.a.s[mp.step - 1].p;
				t.AddHead(new T {
					name = m.a.name, j = mp.j, err = -4, info = m.a.hint,
					dump = dump < 2 ? null : $"{m.a.name} :: recover " +
						$"{(p is Prod pp ? pp.name : p)} at {Dump(ler.Lex(mp.j.via - 1))}",
				});
			}
		if (t != up) {
			if (omitSub && t.head != null && t.head.next == null)
				t.AddSubOf(t.head.Remove());
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
		var from = lexm[lexz] < matchz ? lexz : lexz - 1;
		var t = new T {
			name = begin.name, j = (from, lexz), err = -1,
			info = from < lexz ? (object)ler.Lex(from) : null
		};
		var errs = new bool[matchz];
		for (int x = matchz - 1, y; (y = x) >= lexm[from]; x--) {
		Prev: var m = matchs[y]; var c = m.a.s[m.step];
			if (c.p == null || errs[y])
				continue;
			if (m.step == 0)
				if ((y = m.prev) >= 0)
					goto Prev;
				else
					continue;
			errs[y] = true;
			if (m.a.errExpect || errExpect >= 3 || errExpect == 2 && ((int)c.q & 1) > 0) {
				var exp = c.p is Prod p ? p.alts[0].hint ?? (object)p.name : c.p;
				var d = m.a.hint ?? m.a.name.ToString();
				d = dump <= 0 ? d
					: dump <= 2 ? $"{Esc(exp)} expected for {d}"
					: $"{Esc(exp)} expected for {d}!{m.step} {Dump(m, true)}";
				t.AddHead(new T {
					name = m.a.name, j = m.j,
					err = m.step, info = exp, dump = d
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
				+ $" :: {Dump(ler.Lexs(m.j))}";
	}
	public string Dump(object d)
	{
		if (d is T t)
			return (ler as LexierBuf<K>)?.LineCol(t.j) is var (l, c)
				? t.Dumper($"{l.on}.{c.on}", $"{l.to}.{c.via}") : t.ToString();
		return dumper?.Invoke(d) ?? Esc(d);
	}

	static string Esc(object d)
	{
		return d.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
	}

	// bootstrap
	static readonly EarleyChar meta;

	static Earley()
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
			{ "hintl", "_" }, // hint lex
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
			kv => new EarleyChar.Prod { name = kv.Key });
		foreach (var kv in grammar)
			// split into alts
			prods[kv.Key].alts = kv.Value.Split("|").Select(alt => {
				// split into cons
				var s = alt.Replace('\x1', '|')
					.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				// build con
				var cons = s.Select(c => {
					var q = c.Length == 1 ? One
							: c[^1] switch { '?' => Opt, '*' => Any, '+' => More, _ => One };
					var p = q == One ? c : c[0..^1];
					return new EarleyChar.Con {
						p = prods.TryGetValue(p, out var a) ? a : (object)keys.Keys(p).First(),
						q = q,
					};
				}).Append(new EarleyChar.Con { q = One })
				.ToArray();
				return new EarleyChar.Alt { name = kv.Key, s = cons, recover = -1 };
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
		meta = new EarleyChar(prods["gram"]) {
			greedy = false, tree = false, dump = 0
		};
	}

	public Earley(string gram, Ler ler)
	{
		using var read = new MetaStr(gram);
		var top = meta.Begin(read).Parse();
		if (top.err != 0) {
			using var read2 = new MetaStr(gram);
			var dump = meta.dump; meta.dump = 3;
			meta.Begin(read2).Parse().Dump(); meta.dump = dump;
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
			p => p.head.dump = meta.ler.Lexs(p.head.j),
			p => new Prod { name = Name(p.head.dump) }
		);

		foreach (var p in prods) {
			var prod = names[p.head.dump];
			// build alts
			var As = p.Where(t => t.name == "alt").Prepend(p).Select(ta => {
				// prod name or keys or quantifier ...
				var s = ta.Where(t => t.name is "sym" or "con").SelectMany(t =>
					t.name == "sym" ? gram[t.j.on] == '?' ? [Opt]
						: gram[t.j.on] == '*' ? [Any] : gram[t.j.on] == '+' ? [More]
						: ler.Keys(MetaStr.Unesc(gram, t.j)).Cast<object>()
					// for word, search product names first, then lexer keys
					: names.TryGetValue(t.dump = meta.ler.Lexs(t.j), out Prod p)
						? [p] : ler.Keys(t.dump).Cast<object>())
					.Append(null)
					.ToArray();
				// build alt
				var cons = s.Select((c, x) =>
					new Con { p = c, q = c != null && s[x + 1] is Qua r ? r : One })
					.Where(c => c.p is not Qua)
					.ToArray();
				return new Alt { name = prod.name, s = cons, recover = -1 };
			}).ToArray();
			// build hint
			int ax = 0;
			foreach (var (t, x) in p.Where(t => t.name == "alt").Append(p).Each()) {
				bool pr = false, l = false, e = false; sbyte g = 0, st = 0; EsynStr r = null, d = null;
				foreach (var h in t) {
					if (h.name == "hintp") pr = true;
					if (h.name == "hintg") g = (sbyte)(gram[h.j.on] == '*' ? 1 : -1);
					if (h.name == "hintt")
						st = (sbyte)(gram[h.j.on] == '-' ? -1 : h.j.size == 1 ? 2 : 1);
					if (h.name == "hintl") l = true;
					if (h.name == "hinte") e = true;
					if (h.name == "hintr") r = h;
					if (h.name == "hintd") d = h;
					if (h.name == "hintw") { // apply hints
						var w = meta.ler.Lexs(h.j);
						for (; ax <= x; ax++) {
							var a = As[ax];
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
									a.recPair = r.j.size == 1 ? k :
										ler.Keys(MetaStr.Unesc(gram, (r.j.on + 1, r.j.via))).First();
									a.recDeny = d == null ? k :
										ler.Keys(MetaStr.Unesc(gram, (d.j.on + 1, d.j.via))).First();
								}
							}
						}
					}
					// each hint is for only one line
					if (h.name == "heol") ax = x + 1;
				}
			}
			prod.alts = As;
		}
		begin = names[prods.First().head.dump];
		recm = new int[reca.Count];
		Begin(ler);
	}
}
