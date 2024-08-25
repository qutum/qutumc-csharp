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
		public O[] x; // compact: { ordinals ... }, normal: null

		public readonly short this[O ord] {
			get => x == null ? s[int.CreateTruncating(ord)]
				: x.Length switch {
					1 => x[0] == ord ? s[0] : No,
					2 => x[0] == ord ? s[0] : x[1] == ord ? s[1] : No,
					3 => x[0] == ord ? s[0] : x[1] == ord ? s[1] : x[2] == ord ? s[2] : No,
					4 => x[0] == ord ? s[0] : x[1] == ord ? s[1] : x[2] == ord ? s[2] : x[3] == ord ? s[3] : No,
					_ => s[Math.Max(Array.BinarySearch(x, ord), 0)]
				};
		}
		internal const short No = -1;
	}
	public short index;
	public S<Kord> goKs; // for each key: shift to: form index, reduce: -2-alt index, error: -1
	public S<Nord> goNs; // for each name: form index, error: -1
	public short other = S<Kord>.No; // reduce by other key ordinals
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
	const short No = -1;

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
	Read:
		(int min, int loop) stuck = (stack.Count, 0);
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var go = form.goKs[key];
		if (go > No) { // shift
			stack.Add((forms[go], ler.Loc(), null));
			goto Read;
		}
		(sbyte err, short size, object info) redu = default;
	Reduce:
		T t = null;
		if (go < No) { // reduce
			var (name, loc) = Reduce(go, ref redu, ref t);
			form = stack[^1].form;
			go = form.goNs[name];
			if (go > No) {
				stack.Add((forms[go], loc, t));
				if (stack.Count < stuck.min) (stuck.min, stuck.loop) = (stack.Count, 1);
				else if (++stuck.loop >= 100) goto Cyclic; // stack stuck without shift
				goto Loop;
			}
			else if (key == default) {
				stack.Clear();
				return t.Append(errs); // reduce alts[0] by eor, accept
			} // reduce alts[0] by others, error
		}
		// error: recover, accept or reject
		redu = Error(form, ref key, ref go, ref errs);
		if (redu.err >> 1 == 0)
			goto Reduce;
		stack.Clear();
		return redu.err > 0 ? t.Append(errs) : errs; // accept or reject
	Cyclic:
		stack.Clear();
		(t.err, t.info) = (-2, "maybe infinite loop due to cyclic grammar or recovery");
		return (errs ?? new() { err = -1, info = 1 }).Add(t);
	}

	(Nord name, int loc) Reduce(short go, ref (sbyte err, short size, object info) redu, ref T t)
	{
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
		return (name, loc);
	}

	(sbyte, short, object) Error(SynForm form, ref Kord key, ref short go, ref T errs)
	{
		if (go == No && (go = form.other) != No) // reduce by other key ordinals
			return (0, 0, null);
		errs ??= new() { err = -1, info = 0 };
		T e = new() {
			from = ler.Loc(), to = ler.Loc() + (key != default ? 1 : 0), err = -1,
			info = (int)errs.info == 100 ? "more than 100 errors ..."
				: form.err ?? (go < No ? SerMaker<K, N>.ErrEor : "unknown error"),
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
		if (key != default) // insert one recovery key only if want it right now
			foreach (var (a, want) in stack[^1].form.recs ?? [])
				if (alts[a].lex >= 0 && want == alts[a].size - 1
						&& want > 1) { // prevent infinite loop mostly
					go = SynForm.Reduce(a); // as if insert final key
					e.to = e.from;
					return (1, want, e); // recover to reduce
				}
		// read until one recovery key
		int rk;
		while (((rk = Array.BinarySearch(recKs, key)) < 0 || ss[rk] == 0) && key != default)
			key = ler.Next() ? keyOrd(ler) : default;
		if (key == default) // only eor
			for (var k = (rk = -1) + 1; k < ss.Length; k++)
				if (ss[k] > (rk < 0 ? 0 : ss[rk])) // form in the latest stack
					foreach (var (a, want) in stack[ss[k] - 1].form.recs)
						if (alts[a].rec == k && // as if insert one recovery key
								want + stack.Count - ss[k] > 1) { // prevent infinite loop mostly
							rk = k; break;
						}
		if (rk >= 0 && ss[rk] > 0)
			foreach (var (a, want) in stack[ss[rk] - 1].form.recs)
				if (alts[a].rec == rk) { // alt index reversed, so latest alt
					stack.RemoveRange(ss[rk], stack.Count - ss[rk]); // drop stack
					go = SynForm.Reduce(a);
					key = ler.Next() ? keyOrd(ler) : default; // as if shift key
					return (1, want, e); // recover to reduce
				}
		return (-1, 0, null); // can not recover, reject
	}

	// accept or reject, no synt no error info no recovery
	public virtual bool Check()
	{
		stack.Add((init, 0, null));
	Next:
		var loop = 0;
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var go = form.goKs[key];
		if (go > No) { // shift
			stack.Add((forms[go], 0, null));
			goto Next;
		}
		else if (go < No) { // reduce
			var alt = alts[SynForm.Reduce(go)];
			stack.RemoveRange(stack.Count - alt.size, alt.size);
			if (alt.size == 1 && ++loop > 100) {
				stack.Clear();
				return false; // maybe infinite loop due to cyclic grammar
			}
			form = stack[^1].form;
			go = form.goNs[nameOrd(alt.name)];
			if (go > No) {
				stack.Add((forms[go], 0, null));
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
	const short No = -1;

	public static IEnumerable<(short d, Kord ord)> Full(this SynForm.S<Kord> s)
	{
		for (var y = 0; y < s.s.Length; y++)
			yield return (s.s[y], s.x?[y] ?? (Kord)y);
	}
	public static IEnumerable<(short d, Nord ord)> Full(this SynForm.S<Nord> s)
	{
		for (var y = 0; y < s.s.Length; y++)
			yield return (s.s[y], s.x?[y] ?? (Nord)y);
	}
	public static IEnumerable<(short d, Kord ord)> Yes(this SynForm.S<Kord> s)
	{
		foreach (var d in s.Full()) if (d.d != No) yield return d;
	}
	public static IEnumerable<(short d, Nord ord)> Yes(this SynForm.S<Nord> s)
	{
		foreach (var d in s.Full()) if (d.d != No) yield return d;
	}
	public static IEnumerable<(short d, Kord ord)> Form(this SynForm.S<Kord> s)
	{
		foreach (var d in s.Full()) if (d.d > No) yield return d;
	}
	public static IEnumerable<(short d, Nord ord)> Form(this SynForm.S<Nord> s)
	{
		foreach (var d in s.Full()) if (d.d > No) yield return d;
	}
	public static IEnumerable<(short d, Kord ord)> Redu(this SynForm.S<Kord> s)
	{
		foreach (var d in s.Full()) if (d.d < No) yield return d;
	}
	public static IEnumerable<(short d, Kord ord)> Alt(this SynForm.S<Kord> s)
	{
		foreach (var d in s.Full()) if (d.d < No) yield return (SynForm.Reduce(d.d), d.ord);
	}
}
