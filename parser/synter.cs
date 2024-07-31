//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

using System;
using System.Collections.Generic;

namespace qutum.parser;

// syntax tree
public class Synt<N, T> : LinkTree<T> where T : Synt<N, T>
{
	public N name;
	public int from, to; // lexs from loc to loc excluded, for error may < 0
	public int err; // no error: 0, error: -1, recovery: -2
	public object info; // no error: maybe lex, error: lex, expected/recovered Alt hint/name or K
	public string dump;

	public override string ToString()
		=> $"{from}:{to}{(err == 0 ? info : err == -1 ? "!" : "?")} {dump ?? info ?? name}";

	public override string ToString(object extra)
	{
		if (extra is not Func<int, int, (int, int, int, int)> loc)
			return ToString();
		var (fl, fc, tl, tc) = loc(from, to);
		return $"{fl}.{fc}:{tl}.{tc}{(err == 0 ? info != null ? " " + info : ""
			: err == -1 ? "!" : "?")} {dump ?? info ?? name}";
	}
}

public class SyntStr : Synt<string, SyntStr>
{
}

// TODO
public enum Qua : byte { Opt = 0, One = 1, Any = 2, More = 3 };

// synter grammar
public class SynGram<K, N>
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Alt> { public N name; }
	public class Alt
	{
		public readonly List<object> cons = [];
		internal sbyte synt; // as Synter.tree: 0, make Synt: 2, omit Synt: -1
		internal bool lex; // save first lex of cons to Synt.info
		internal bool error; // make expecting Synt when error
		internal string hint;
	}

	public SynGram<K, N> prod(N name) { prods.Add(new Prod { name = name }); return this; }
	public SynGram<K, N> this[params object[] cons] {
		get {
			Alt a = new();
			prods[^1].Add(a);
			foreach (var v in cons)
				if (v is N or K) a.cons.Add(v);
				else throw new($"wrong altern content {v?.GetType()}");
			return this;
		}
	}
	public SynGram<K, N> synt { get { prods[^1][^1].synt = 2; return this; } }
	public SynGram<K, N> syntMay { get { prods[^1][^1].synt = 1; return this; } }
	public SynGram<K, N> syntOmit { get { prods[^1][^1].synt = -1; return this; } }
	public SynGram<K, N> lex { get { prods[^1][^1].lex = true; return this; } }
	public SynGram<K, N> err { get { prods[^1][^1].error = true; return this; } }
	public SynGram<K, N> hint(string w) { prods[^1][^1].hint = w != "" ? w : null; return this; }
}

public sealed class SynAlt<N>
{
	public N name;
	public int len;
	public bool rec; // is this for error recovery
	public sbyte synt; // as Synter.tree: 0, make Synt: 2, omit Synt: -1
	public int lex; // save lex at this index to Synt.info, no save: <0
	public string hint;
	public string dump;

	public override string ToString() => dump ??= $"{name} #{len}";
}
public sealed class SynForm<K, N>
{
	public short[] modes; // for each key: shift to: form index+1, reduce: alt ~index, error: 0
	public ushort[] keys; // compact modes: { 0 for others, key oridnals ... }, normal: null
	public short rec; // recover error: mode, no recovery: 0
	public string err; // error hint
	public short[] pushs; // for each name: push form index
	public ushort[] names; // compact pushs: { 0 for others, name ordinals ... }, normal: null

	public short Get(ushort x, short[] v, ushort[] i)
	{
		if (i == null) return v[x];
		switch (i.Length) {
		case 2: return i[1] == x ? v[1] : v[0];
		case 3: return i[1] == x ? v[1] : i[2] == x ? v[2] : v[0];
		case 4: return i[1] == x ? v[1] : i[2] == x ? v[2] : i[3] == x ? v[3] : v[0];
		default: var j = Array.BinarySearch(i, 1, i.Length, x); return j > 0 ? v[j] : v[0];
		}
	}
}

// syntax parser
public class Synter<K, N, T, Ler> : Synter<K, Lexi<K>, N, T, Ler>
	where K : struct where T : Synt<N, T>, new() where Ler : class, LexerSeg<K, Lexi<K>>
{
	public Synter(Func<Ler, ushort> lexOrd, Func<N, ushort> nameOrd,
		SynAlt<N>[] alts, SynForm<K, N>[] forms) : base(lexOrd, nameOrd, alts, forms) { }
}

public class SynterStr : Synter<char, char, string, SyntStr, LerStr>
{
	public SynterStr(Func<LerStr, ushort> lexOrd, Func<string, ushort> nameOrd,
		SynAlt<string>[] alts, SynForm<char, string>[] forms) : base(lexOrd, nameOrd, alts, forms) { }
}

// syntax parser using LR algorithm
public class Synter<K, L, N, T, Ler> where T : Synt<N, T>, new() where Ler : class, Lexer<K, L>
{
	readonly SynAlt<N>[] alts; // reduce [0] by eof after forms[0]: finish
	readonly SynForm<K, N>[] forms;
	readonly Stack<(SynForm<K, N> form, object with)> stack = new(); // with is lex or Synt

	protected Func<Ler, ushort> lexOrd; // ordinal from 1, default lex for eof: 0
	protected Func<N, ushort> nameOrd; // ordinal from 1
	public Ler ler;
	public int lexn; // lex count
	public bool tree = true; // make Synt tree by default
	public int dump = 0; // no: 0, lexs only for tree leaf: 1, lexs: 2, lexs and Alts: 3
	public Func<object, string> dumper = null;


	public Synter(Func<Ler, ushort> lexOrd, Func<N, ushort> nameOrd,
		SynAlt<N>[] alts, SynForm<K, N>[] forms)
	{
		if (alts.Length is < 1 or > 32767)
			throw new($"{nameof(alts)} length: {alts.Length}");
		if (forms.Length is < 1 or > 32767)
			throw new($"{nameof(forms)} length: {forms.Length}");
		this.lexOrd = lexOrd; this.nameOrd = nameOrd; this.alts = alts; this.forms = forms;
	}

	public Synter<K, L, N, T, Ler> Begin(Ler ler) { this.ler = ler; return this; }

	// make synt from complete Alts
	public virtual T Parse()
	{
		var t = Parse(out T errs, false);
		stack.Clear();
		return t?.Append(errs) ?? errs;
	}

	public virtual bool Check()
	{
		Parse(out T errs, true);
		stack.Clear();
		return errs == null;
	}

	T Parse(out T errs, bool check)
	{
		stack.Push((forms[0], null));
		T err = errs = null;
	Next:
		var key = ler.Next() ? lexOrd(ler) : default;
	Loop:
		object info = ler;
		var (form, _) = stack.Peek();
		var mode = form.Get(key, form.modes, form.keys);
	Recover:
		if (mode > 0) { // shift
			stack.Push((forms[mode - 1], check ? null : info == ler ? ler.Lex() : info));
			goto Next;
		}
		else if (mode < 0) { // reduce
			var alt = alts[~mode];
			T t = check ? null : new T {
				name = alt.name, to = ler.Loc() + 1,
				err = alt.rec ? -2 : 0
			};
			for (var i = alt.len - 1; i >= 0; i--) {
				var (_, with) = stack.Pop();
				if (check) ;
				else if (with is T h)
					t.AddHead(h);
				else if (i == alt.lex)
					t.info = with;
			}
			form = stack.Peek().form;
			var push = form.Get(nameOrd(alt.name), form.pushs, form.names);
			stack.Push((forms[push], t));
			// reduce alts[0] by eof after forms[0]
			if (form == forms[0] && alt == alts[0] && key == default)
				goto Done;
			goto Loop;
		}
		else { // error
			(mode, info) = (form.rec, form.err);
			if (check) {
				errs = new T();
				return null;
			}
			var e = new T() { err = -1, info = info };
			_ = err == null ? errs = e : err.Append(e);
			err = e;
			if (mode == 0) // can not recover
				return null;
			goto Recover;
		}
	Done:
		return (T)stack.Peek().with;
	}
}
