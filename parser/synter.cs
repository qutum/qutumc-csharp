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

	public override string ToString() => $"{from}:{to}{(err == 0 ? info != null ? " " + info : ""
		: err < -1 ? "!!" : "!")} {dump ?? (err == 0 ? null : info) ?? name}";

	public override string ToString(object extra)
	{
		if (extra is not Func<int, int, (int, int, int, int)> loc)
			return ToString();
		var (fl, fc, tl, tc) = loc(from, to);
		return $"{fl}.{fc}:{tl}.{tc}{(err == 0 ? info != null ? " " + info : ""
			: err < -1 ? "!!" : "!")} {dump ?? (err == 0 ? null : info) ?? name}";
	}
}

public class SyntStr : Synt<string, SyntStr>
{
}

public sealed class SynAlt<N>
{
	public N name;
	public short size;
	public short lex; // save lex at this index to Synt.info, no save: <0
	public sbyte synt; // as Synter.tree: 0, make Synt: 1, omit Synt: -1
	public bool rec; // is this for error recovery
	public string hint;
	public object dump;

	public override string ToString() => dump as string ??
		(string)(dump = (dump as Func<string>)?.Invoke() ?? hint ?? $"{name} alt");
}
public sealed class SynForm
{
	public short[] modes; // for each key: shift to: form index, reduce: -2-alt index, error: -1
	public ushort[] keys; // compact modes: { others, key ordinals ... }, normal: null
	public short rec; // recover error: mode, no recovery: 0
	public string err; // error hint, {0} filled with current key
	public short[] pushs; // for each name: push: form index
	public ushort[] names; // compact pushs: { others, name ordinals ... }, normal: null

	public static short Reduce(int alt) => (short)(-2 - alt);

	public static short Get(ushort o, short[] s, ushort[] x)
	{
		if (x == null) return s[o];
		switch (x.Length) {
		case 2: return x[1] == o ? s[1] : s[0];
		case 3: return x[1] == o ? s[1] : x[2] == o ? s[2] : s[0];
		case 4: return x[1] == o ? s[1] : x[2] == o ? s[2] : x[3] == o ? s[3] : s[0];
		default: var y = Array.BinarySearch(x, 1, x.Length - 1, o); return s[y > 0 ? y : 0];
		}
	}
}

// syntax parser
public class Synter<K, N, T, Ler> : Synter<K, Lexi<K>, N, T, Ler>
	where K : struct where T : Synt<N, T>, new() where Ler : class, LexerSeg<K, Lexi<K>>
{
	public Synter(Func<Ler, ushort> keyOrd, Func<N, ushort> nameOrd, SynAlt<N>[] alts, SynForm[] forms)
		: base(keyOrd, nameOrd, alts, forms) { }
}

public class SynterStr : Synter<char, char, string, SyntStr, LerStr>
{
	public SynterStr(Func<string, ushort> nameOrd, SynAlt<string>[] alts, SynForm[] forms)
		: base(ler => ler.Lex(), nameOrd, alts, forms) { }
}

// syntax parser using LR algorithm
// K for lexical key i.e lexeme, L for lexical token
// N for syntax name i.e synteme, T for syntax tree
public class Synter<K, L, N, T, Ler> where T : Synt<N, T>, new() where Ler : class, Lexer<K, L>
{
	readonly SynAlt<N>[] alts; // reduce [0].name by eor after forms[init]: accept
	readonly SynForm[] forms;
	readonly Stack<(short form, int loc, object with)> stack
		= new(); // (form index, lex loc or Synt.from, null for lex or Synt or recovery hint)
	protected short init; // first form index

	protected Func<Ler, ushort> keyOrd; // ordinal from 1, default for eor: 0
	protected Func<N, ushort> nameOrd; // ordinal from 1
	public Ler ler;
	public bool tree = true; // make Synt tree by default
	public int dump = 0; // no: 0, lexs only for tree leaf: 1, lexs: 2, lexs and Alts: 3
	public Func<object, string> dumper = null;

	public Synter(Func<Ler, ushort> keyOrd, Func<N, ushort> nameOrd, SynAlt<N>[] alts, SynForm[] forms)
	{
		if (alts.Length is 0 or > 32767)
			throw new($"{nameof(alts)} size {alts.Length}");
		if (forms.Length is 0 or > 32767)
			throw new($"{nameof(forms)} size {forms.Length}");
		this.keyOrd = keyOrd; this.nameOrd = nameOrd; this.alts = alts; this.forms = forms;
		init = (short)(forms[0] != null ? 0 : 1);
	}

	public Synter<K, L, N, T, Ler> Begin(Ler ler) { this.ler = ler; return this; }

	// make Synt tree followed by errors
	public virtual T Parse()
	{
		stack.Push((init, -1, null));
		T err = null, errs = null;
		var accept = nameOrd(alts[0].name);
	Next:
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		object info = null;
		var form = forms[stack.Peek().form];
		var mode = SynForm.Get(key, form.modes, form.keys);
	Recover:
		if (mode >= init) { // shift
			stack.Push((mode, ler.Loc(), info));
			goto Next;
		}
		if (mode < -1) { // reduce
			var alt = alts[SynForm.Reduce(mode)];
			bool omit = alt.synt == 0 ? !tree : alt.synt < 0; // omit Synt of Alt
			T t = omit ? null : new() { name = alt.name, to = ler.Loc(), err = alt.rec ? -2 : 0 };
			int loc = 0; // lexic loc of Alt head
			for (var i = alt.size - 1; i >= 0; i--) {
				(_, loc, var with) = stack.Pop();
				if (omit)
					t = (with as T)?.Append(t) ?? t; // flatten Synts inside Alt
				else {
					t.from = loc;
					if (with is T head)
						t.AddHead(head);
					else
						t.info = i == alt.lex ? ler.Lex(loc) : with;
				}
			}
			form = forms[stack.Peek().form];
			var name = nameOrd(alt.name);
			if (form == forms[init] && name == accept && key == default) {
				stack.Clear();
				return t.Append(errs); // reduce alts[0] by eor after forms[init], accept
			}
			var push = SynForm.Get(name, form.pushs, form.names);
			stack.Push((push, loc, t));
			goto Loop;
		}
		// error
		mode = form.rec;
		info = string.Format(form.err ?? "", key == default ? "end of read" : ler.Lex());
		T e = new() { err = -1, info = info };
		_ = err == null ? errs = e : err.Append(e);
		err = e;
		if (mode == -1) {
			stack.Clear();
			return errs; // no Alt for recovery now, reject
		}
		// TODO make error hint Synt
		goto Recover;
	}

	public virtual bool Check()
	{
		stack.Push((init, -1, null));
		var accept = nameOrd(alts[0].name);
	Next:
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = forms[stack.Peek().form];
		var mode = SynForm.Get(key, form.modes, form.keys);
		if (mode >= init) { // shift
			stack.Push((mode, 0, null));
			goto Next;
		}
		else if (mode < -1) { // reduce
			var alt = alts[SynForm.Reduce(mode)];
			for (var i = 0; i < alt.size; i++)
				stack.Pop();
			form = forms[stack.Peek().form];
			var name = nameOrd(alt.name);
			if (form == forms[init] && name == accept && key == default) {
				stack.Clear();
				return true; // reduce alts[0] by eor after forms[init], accept
			}
			var push = SynForm.Get(nameOrd(alt.name), form.pushs, form.names);
			stack.Push((push, 0, null));
			goto Loop;
		}
		stack.Clear();
		return false;
	}
}
