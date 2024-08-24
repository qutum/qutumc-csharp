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
	public (short alt, short want)[] recs; // { recovery alt index and want ... }

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

	// (form, lex loc i.e Synt.from, synt or null for lex)
	readonly List<(SynForm form, int loc, object synt)> stack = [];

	// make Synt tree followed by errors
	public virtual T Parse()
	{
		T errs = null;
		stack.Add((init, 0, null));
	Next:
		var loop = 0;
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var go = form.modes[key];
		if (go >= 0) { // shift
			stack.Add((forms[go], ler.Loc(), null));
			goto Next;
		}
		(sbyte err, short size, object info) redu = default;
	Reduce:
		T t = null;
		if (go < -1) { // reduce
			var alt = alts[SynForm.Reduce(go)];
			var name = nameOrd(alt.name);
			int loc = stack[^1].loc;
			bool make = redu.err > 0 || (alt.synt == 0 ? synt : alt.synt > 0);
			if (make)
				t = new() {
					name = alt.name, from = loc, to = ler.Loc(), err = redu.err, info = redu.info
				};
			if (redu.err == 0)
				redu.size = alt.size;
			for (var i = redu.size - 1; i >= 0; i--) {
				(_, loc, var synt) = stack[^(redu.size - i)];
				if (make) {
					t.from = loc; // head loc
					if (synt is T head)
						t.AddHead(head);
					else if (t.err == 0 && i == alt.lex)
						t.info = ler.Lex(loc);
				}
				else if (synt is T head)
					t = head.Append(t); // flatten Synts inside Alt
			}
			stack.RemoveRange(stack.Count - redu.size, redu.size);
			if (redu.size == 1 && ++loop > 100)
				goto Cyclic;
			form = stack[^1].form;
			go = form.pushs[name];
			if (go >= 0) {
				stack.Add((forms[go], loc, t));
				goto Loop;
			}
			else if (key == default) {
				stack.Clear();
				return t.Append(errs); // reduce alts[0] by eor, accept
			} // reduce alts[0] by others, error
		}
		// error: recover, accept or reject
		redu = Error(form, ref key, ref go, ref errs);
		if (redu.err > 0) {
			loop = 0;
			goto Reduce;
		}
		stack.Clear();
		return redu.err == 0 ? t.Append(errs) : errs; // accept or reject
	Cyclic:
		stack.Clear();
		(t.err, t.info) = (-2, "maybe infinite loop due to cyclic grammar");
		return (errs ?? new() { err = -1, info = 1 }).Add(t);
	}

	(sbyte, short, object) Error(SynForm form, ref Kord key, ref short go, ref T errs)
	{
		errs ??= new() { err = -1, info = 0 };
		T e = new() {
			from = ler.Loc(), to = ler.Loc() + (key != default ? 1 : 0), err = -1,
			info = (int)errs.info == 100 ? "more than 100 errors ..."
				: form.err ?? (go < -1 ? SerMaker<K, N>.ErrEor : "unknown error"),
		};
		if ((int)errs.info <= 100)
			errs.Add(e).info = (int)errs.info + 1;
		if (!recover) // reject
			return (-1, 0, null);
		// recover
		// which forms in stack to recover
		int[] ss = null; // stack index by each recovery key
		foreach (var ((f, _, _), x) in stack.Each())
			foreach (var a in f.recs ?? [])
				(ss ??= new int[recKs.Length])[alts[a.alt].rec] = x + 1;
		if (ss == null) // no recovery form in stack, reject
			return (-1, 0, null);
		// insert one recovery key only if want it right now
		foreach (var (x, k) in ss.Each())
			if (x == stack.Count) // latest stack
				foreach (var (a, want) in stack[x - 1].form.recs)
					if (alts[a].rec == k && alts[a].lex >= 0 && want > 1 && want == alts[a].size - 1) {
						go = SynForm.Reduce(a); // as if insert final key
						e.to = e.from;
						return (1, want, e); // recover to reduce
					}
		// read until one recovery key
		var rk = 0;
		while (key != default && ((rk = Array.BinarySearch(recKs, key)) < 0 || ss[rk] == 0))
			key = ler.Next() ? keyOrd(ler) : default;
		if (key == default) // only eor
			for (var k = (rk = 0) + 1; k < ss.Length; k++)
				if (ss[k] > ss[rk])
					rk = k; // form in the latest stack
		if (ss[rk] > 0)
			foreach (var (a, want) in stack[ss[rk] - 1].form.recs)
				if (alts[a].rec == rk) { // alt index reversed, so latest alt
					key = key != default && ler.Next() ? keyOrd(ler) : default;// shift key
					stack.RemoveRange(ss[rk], stack.Count - ss[rk]); // drop stack
					go = SynForm.Reduce(a);
					return (1, want, e); // recover to reduce
				}
		return (-1, 0, null); // can not recover, reject
	}

	public virtual bool Check()
	{
		stack.Add((init, 0, null));
	Next:
		var loop = 0;
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var mode = form.modes[key];
		if (mode >= 0) { // shift
			stack.Add((forms[mode], 0, null));
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
				stack.Add((forms[push], 0, null));
				goto Loop;
			}
			else if (key == default) {
				stack.Clear();
				return true; // reduce alts[0] by eor, accept
			} // reduce alts[0] by others, reject
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
