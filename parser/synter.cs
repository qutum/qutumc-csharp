//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections.Generic;
using System.Numerics;

namespace qutum.parser;

using Kord = char;
using Nord = ushort;

// syntax tree
public partial class Synt<N, T> : LinkTree<T> where T : Synt<N, T>
{
	public N name;
	public int from, to; // lexs from loc to loc excluded, for error may < 0
	public sbyte err; // no error: 0, recovered: 1, error: -1, cyclic: -2
	public object info; // no error: maybe lex, error: error info or recovery info
}

public class SyntStr : Synt<string, SyntStr>
{
}

public sealed partial class SynAlt<N>
{
	public N name;
	public short size;
	public short lex; // save lex at this index to Synt.info, no save: <0
	public sbyte synt; // as Synter.tree: 0, make Synt: 1, omit Synt: -1
	public sbyte rec; // at recovery key ordinal index, or 0
	public string label;
}
public sealed partial class SynForm
{
	public struct S<O> where O : IBinaryInteger<O>
	{
		public short[] s; // at ordinal or ordinal index
		public O[] x; // compact: { for others, ordinals ... }, normal: null

		public readonly short this[O ord] {
			get => x == null ? s[int.CreateTruncating(ord)]
				: x.Length switch {
					1 => s[0],
					2 => x[1] == ord ? s[1] : s[0],
					3 => x[1] == ord ? s[1] : x[2] == ord ? s[2] : s[0],
					4 => x[1] == ord ? s[1] : x[2] == ord ? s[2] : x[3] == ord ? s[3] : s[0],
					_ => s[Math.Max(Array.BinarySearch(x, 1, x.Length - 1, ord), 0)]
				};
		}
	}
	public short index;
	public S<Kord> modes; // for each key: shift to: form index, reduce: -2-alt index, error: -1
	public S<Nord> pushs; // for each name: push: form index
	public string err; // error info
	public short[] recs; // recovery alts indexes wanting their second contents

	public static short Reduce(int alt) => (short)(-2 - alt);
}

// syntax parser
public class Synter<K, N, T, Ler> : Synter<K, Lexi<K>, N, T, Ler>
	where K : struct where T : Synt<N, T>, new() where Ler : class, LexerSeg<K, Lexi<K>>
{
	public Synter(Func<Ler, Kord> keyOrd, Func<N, Nord> nameOrd,
		SynAlt<N>[] alts, SynForm[] forms, Kord[] recKs = null)
		: base(keyOrd, nameOrd, alts, forms, recKs) { }
}

public class SynterStr : Synter<char, char, string, SyntStr, LerStr>
{
	public SynterStr(Func<string, Nord> nameOrd,
		SynAlt<string>[] alts, SynForm[] forms, Kord[] recKs = null)
		: base(ler => ler.Lex(), nameOrd, alts, forms, recKs) { }
}

// syntax parser using LR algorithm
// K for lexical key i.e lexeme, L for lexical token
// N for syntax name i.e synteme, T for syntax tree
public partial class Synter<K, L, N, T, Ler> where T : Synt<N, T>, new() where Ler : class, Lexer<K, L>
{
	readonly SynAlt<N>[] alts; // reduce [0] by eor: accept
	readonly SynForm[] forms;
	readonly SynForm init;
	readonly Kord[] recKs; // { recovery key ordinal ... }

	protected Func<Ler, Kord> keyOrd; // ordinal from 1, default for eor: 0
	protected Func<N, Nord> nameOrd; // ordinal from 1

	public Ler ler;
	public bool recover = false; // whether recover errors
	public bool synt = true; // make Synt tree by default

	public Synter(Func<Ler, Kord> keyOrd, Func<N, Nord> nameOrd,
		SynAlt<N>[] alts, SynForm[] forms, Kord[] recKs = null)
	{
		if (alts.Length is 0 or > 32767)
			throw new($"{nameof(alts)} size {alts.Length}");
		if (forms.Length is 0 or > 32767)
			throw new($"{nameof(forms)} size {forms.Length}");
		this.keyOrd = keyOrd; this.nameOrd = nameOrd;
		this.alts = alts; this.forms = forms; this.recKs = recKs;
		init = forms[0] ?? forms[1];
		foreach (var (f, fx) in forms.Each())
			if (f != null)
				f.index = (short)fx;
		InitDump();
	}

	public Synter<K, L, N, T, Ler> Begin(Ler ler) { this.ler = ler; return this; }

	// (form, lex loc i.e Synt.from, null for lex or Synt)
	readonly List<(SynForm form, int loc, object synt)> stack = [];

	// make Synt tree followed by errors
	public virtual T Parse()
	{
		T errs = null;
		stack.Add((init, -1, null));
	Next:
		var loop = 0;
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var mode = form.modes[key];
		Nord name = 0; // reduced alt name
		int loc = 0; // reduced head lexic loc
		T t = null; // reduced synt
		if (mode >= 0) { // shift
			stack.Add((forms[mode], ler.Loc(), null));
			goto Next;
		}
		if (mode < -1) { // reduce
			var alt = alts[SynForm.Reduce(mode)];
			name = nameOrd(alt.name);
			bool make = alt.synt == 0 ? synt : alt.synt > 0;
			if (make)
				t = new() { name = alt.name, to = ler.Loc() };
			for (var i = alt.size - 1; i >= 0; i--) {
				(_, loc, var synt) = stack[^(alt.size - i)];
				if (make) {
					t.from = loc;
					if (synt is T head)
						t.AddHead(head);
					else if (i == alt.lex)
						t.info = ler.Lex(loc);
				}
				else if (synt is T head)
					t = head.Append(t); // flatten Synts inside Alt
			}
			stack.RemoveRange(stack.Count - alt.size, alt.size);
			if (alt.size == 1 && ++loop > 100)
				goto Cyclic;
			form = stack[^1].form;
		}
	Recover:
		if (mode < -1) { // push
			var push = form.pushs[name];
			if (push >= 0) {
				stack.Add((forms[push], loc, t));
				goto Loop;
			}
			else if (key == default) {
				stack.Clear();
				return t.Append(errs); // reduce alts[0] by eor, accept
			} // otherwise reject
		}
		// error, recover, accept or reject
		if (Error(key, ref form, ref mode, ref name, ref loc, ref t, ref errs)) {
			loop = 0;
			if (key != default) // next
				key = ler.Next() ? keyOrd(ler) : default;
			goto Recover;
		}
		stack.Clear();
		return t; // accept or reject
	Cyclic:
		stack.Clear();
		(t.err, t.info) = (-2, "maybe infinite loop due to cyclic grammar");
		return (errs ??= new() { err = -1 }).Add(t);
	}

	bool Error(Kord key, ref SynForm form, ref short mode, ref Nord name, ref int loc,
		ref T t, ref T errs)
	{
		T e = new() {
			from = ler.Loc(), to = ler.Loc() + (key != default ? 1 : 0),
			err = -1, info = form.err ?? (mode < -1 ? SerMaker<K, N>.ErrEor : "unknown error"),
		};
		(errs ??= new() { err = -1 }).Add(e);
		if (!recover)
			goto Reject;
		// recover
		if (mode < -1) { // want eor only
			while (ler.Next()) ;
			t = t.Append(errs); // drop all following lexs, accept
			return false;
		}
		// which forms in stack to recover
		int[] ss = null; // stack index by each recovery key
		foreach (var ((f, _, _), sx) in stack.Each())
			foreach (var a in f.recs ?? [])
				(ss ??= new int[recKs.Length])[alts[a].rec] = sx; // never stack 0
		if (ss == null) // no recovery form in stack, reject
			goto Reject;
		// read until one recovery key
		int rk = 0;
		while (key != default && ((rk = Array.BinarySearch(recKs, key)) < 0 || ss[rk] == 0))
			key = ler.Next() ? keyOrd(ler) : default;
		if (key == default) // only eor
			for (var k = 0; k < ss.Length; k++)
				if (ss[k] > ss[rk])
					rk = k; // form in the latest stack
		form = stack[ss[rk] - 1].form;
		foreach (var a in form.recs)
			if (alts[a].rec == rk) { // latest alt
				mode = SynForm.Reduce(a);
				name = nameOrd(alts[a].name);
				loc = stack[ss[rk]].loc;
				t = new() { name = alts[a].name, from = loc, to = ler.Loc() + 1, err = 1, info = e };
				stack.RemoveRange(ss[rk], stack.Count - ss[rk]);
				return true;
			}
		Reject:
		t = errs;
		return false;
	}

	public virtual bool Check()
	{
		stack.Add((init, -1, null));
	Next:
		var loop = 0;
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var mode = form.modes[key];
		if (mode >= 0) { // shift
			stack.Add((forms[mode], -1, null));
			goto Next;
		}
		else if (mode < -1) { // reduce
			var alt = alts[SynForm.Reduce(mode)];
			stack.RemoveRange(stack.Count - alt.size, alt.size);
			if (alt.size == 1 && ++loop > 100) {
				stack.Clear();
				return false; // maybe infinite loop due to cyclic grammar
			}
			form = stack[^1].form;
			var push = form.pushs[nameOrd(alt.name)];
			if (push >= 0) {
				stack.Add((forms[push], -1, null));
				goto Loop;
			}
			else if (key == default) {
				stack.Clear();
				return true; // reduce alts[0] by eor, accept
			} // otherwise reject
		} // error
		stack.Clear();
		return false;
	}
}

public static partial class Extension
{
	public static IEnumerable<(short d, Kord ord, bool other)> Full(this SynForm.S<Kord> s)
	{
		for (var y = 0; y < s.s.Length; y++)
			yield return (s.s[y], s.x?[y] ?? (Kord)y, s.x != null && y == 0);
	}
	public static IEnumerable<(short d, Nord ord, bool other)> Full(this SynForm.S<Nord> s)
	{
		for (var y = 0; y < s.s.Length; y++)
			yield return (s.s[y], s.x?[y] ?? (Nord)y, s.x != null && y == 0);
	}
	public static IEnumerable<(short d, Kord ord, bool other)> Yes(this SynForm.S<Kord> s)
	{
		foreach (var d in s.Full()) if (d.d != -1) yield return d;
	}
	public static IEnumerable<(short d, Nord ord, bool other)> Yes(this SynForm.S<Nord> s)
	{
		foreach (var d in s.Full()) if (d.d != -1) yield return d;
	}
	public static IEnumerable<(short d, Kord ord, bool other)> Form(this SynForm.S<Kord> s)
	{
		foreach (var d in s.Full()) if (d.d >= 0) yield return d;
	}
	public static IEnumerable<(short d, Nord ord, bool other)> Form(this SynForm.S<Nord> s)
	{
		foreach (var d in s.Full()) if (d.d >= 0) yield return d;
	}
	public static IEnumerable<(short d, Kord ord, bool other)> Redu(this SynForm.S<Kord> s)
	{
		foreach (var d in s.Full()) if (d.d < -1) yield return d;
	}
	public static IEnumerable<(short d, Kord ord, bool other)> Alt(this SynForm.S<Kord> s)
	{
		foreach (var d in s.Full()) if (d.d < -1) yield return (SynForm.Reduce(d.d), d.ord, d.other);
	}
}
