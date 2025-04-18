//
// Qutum 10 Compiler
// Copyright 2008-2025 Qianyan Cai
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
	public Jov j; // lexs jot loc, error lex: < 0
	public sbyte err; // no error: 0, recovered: 1, error: -1, cyclic: -2, lex error: -3
	public object info; // lexi, error info, error synt of recovery ...
}

public class SyntStr : Synt<string, SyntStr>
{
}

public sealed partial class SynAlt<N>
{
	public N name;
	public short size;
	public short lex; // save main lex at this index to synt.info, no save: <0
	public sbyte rec; // at recovery key ordinal index, or 0
	public sbyte synt; // make synt: as synter: 0, omit: -1, make: 1, lift left: 2, lift right: 3
	public N syntName;
	public string label;
}
public sealed partial class SynForm
{
	public struct S<O> where O : IBinaryInteger<O>
	{
		public short[] s; // at ordinal or ordinal index
		public O[] x; // compact: { ordinals ... }, normal: null

		public readonly short this[O ord] {
			get => x == null ? int.CreateTruncating(ord) is var y && y < s.Length ? s[y] : No
				: x.Length switch {
					1 => x[0] == ord ? s[0] : No,
					2 => x[0] == ord ? s[0] : x[1] == ord ? s[1] : No,
					3 => x[0] == ord ? s[0] : x[1] == ord ? s[1] : x[2] == ord ? s[2] : No,
					4 => x[0] == ord ? s[0] : x[1] == ord ? s[1] : x[2] == ord ? s[2] : x[3] == ord ? s[3] : No,
					_ => Array.BinarySearch(x, ord) is var z and >= 0 ? s[z] : No
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

	public static short Redu(int alt) => (short)(-2 - alt);
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
public partial class Synter<K, L, N, T, Ler>
	where K : struct where T : Synt<N, T>, new() where Ler : class, Lexer<K, L>
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
	public bool synt = true; // make synt tree by default, always make recovery synt

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

	public virtual Synter<K, L, N, T, Ler> Begin(Ler ler) { this.ler = ler; return this; }

	// accept or reject, no synt no error info no recovery
	public virtual bool Check()
	{
		stack.Add(default);
		stack.Add((init, -1, null));
	Read:
		(int min, int loop) stuck = (stack.Count, 0);
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var go = form.goKs[key];
		if (go > No) { // shift
			stack.Add((forms[go], -1, null));
			goto Read;
		}
		else if (go < No) { // reduce
			var alt = alts[SynForm.Redu(go)];
			stack.RemoveRange(stack.Count - alt.size, alt.size);
			form = stack[^1].form;
			go = form.goNs[nameOrd(alt.name)];
			if (go > No) {
				stack.Add((forms[go], -1, null));
				if (stack.Count < stuck.min) (stuck.min, stuck.loop) = (stack.Count, 1);
				else if (++stuck.loop >= 100) goto Reject; // stack stuck without shift
				goto Loop;
			}
			else if (key == default) {
				stack.Clear();
				return true; // reduce alts[0] by eor, accept
			} // reduce alts[0] by others, reject
		} // error
	Reject:
		stack.Clear();
		return false;
	}

	// (form, lex loc i.e synt start, synt or null for lex)
	readonly List<(SynForm form, int loc, object synt)> stack = []; // index from 1
	internal List<int>[] recCs; // { 0, recovery stack index... } of each recovery key

	void StackPush(SynForm form, int loc, object synt)
	{
		stack.Add((form, loc, synt));
		if (recCs != null && form.recs != null)
			foreach (var a in form.recs)
				recCs[alts[a.alt].rec].Add(stack.Count - 1);
	}
	void StackPop(int on = 0, int size = 0)
	{
		if (on == 0) on = stack.Count - size;
		else if (size == 0) size = stack.Count - on;
		stack.RemoveRange(on, size);
		if (recCs != null)
			foreach (var cs in recCs) {
				var x = cs.Count;
				for (; cs[x - 1] >= on; x--) ;
				cs.RemoveRange(x, cs.Count - x);
			}
	}
	void StackRecs()
	{
		recCs = new List<int>[recKs.Length];
		for (var x = 0; x < recCs.Length; x++)
			recCs[x] = new(stack.Capacity) { 0 };
		foreach (var ((f, _, _), x) in stack.Each())
			foreach (var a in f?.recs ?? [])
				recCs[alts[a.alt].rec].Add(x);
	}
	void StackClear() { stack.Clear(); recCs = null; }

	// make synt tree followed by errors
	public virtual T Parse()
	{
		var errs = new T { err = -1, info = 0 };
		stack.Add(default);
		StackPush(init, ler.Loc(), null);
	Read:
		(int min, int loop) stuck = (stack.Count, 0);
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var go = form.goKs[key];
		if (go > No) { // shift
			StackPush(forms[go], ler.Loc(), null);
			goto Read;
		}
		(sbyte err, short size, T info) redu = default;
	Reduce:
		T t;
		if (go < No) { // reduce
			(var name, var loc, t) = Reduce(go, ref redu);
			form = stack[^1].form;
			go = form.goNs[name];
			if (go > No) {
				StackPush(forms[go], loc, t);
				if (stack.Count < stuck.min) (stuck.min, stuck.loop) = (stack.Count, 1);
				else if (++stuck.loop >= 100) goto Cyclic; // stack stuck without shift
				goto Loop;
			}
			else if (key == default) {
				StackClear();
				return (t ?? new()).Append(errs.head?.up); // reduce alts[0] by eor, accept
			} // reduce alts[0] by others, error
		}
		// error: other, recover, accept, reject
		redu = Error(form, ref key, ref go, errs);
		if (redu.err >= 0)
			goto Reduce;
		StackClear();
		return errs; // reject
	Cyclic:
		StackClear();
		(t ??= new()).err = -2;
		t.info = "maybe infinite loop due to cyclic grammar or recovery";
		return errs.Add(t);
	}

	(Nord name, int loc, T t) Reduce(short go, ref (sbyte err, short size, T info) redu)
	{
		var alt = alts[SynForm.Redu(go)];
		if (redu.err == 0)
			redu.size = alt.size;
		int loc = redu.size > 0 ? stack[^redu.size].loc : stack[^1].loc + 1;
		if (redu.err > 0 && redu.info is T e && loc > e.j.on)
			loc = e.j.on; // recovery synt start
		bool make = alt.synt > 0 || alt.synt == 0 && synt;
		T t = make || redu.err > 0 ? new() { // for omitted recovery synt, append it
			name = alt.syntName, j = (loc, ler.Loc()), dump = dump >= 3 ? alt : alt.label,
			err = redu.err, info = redu.info,
		} : null;
		for (var i = redu.size - 1; i >= 0; i--) {
			var (_, l, synt) = stack[^(redu.size - i)];
			if (synt is T head)
				if (!make // flatten synts
					|| alt.synt == 2 && i == 0) // lift first synt as prev
					t = head.Append(t); // maybe slow if deep left recursive
				else if (alt.synt == 3 && i == redu.size - 1) // lift last synt as next
					t.Append(head);
				else
					t.AddHead(head);
			else if (make && i == alt.lex)
				t.info ??= ler.Lex(l);
		}
		StackPop(size: redu.size);
		return (nameOrd(alt.name), loc, t);
	}

	(sbyte err, short size, T info) Error(SynForm form, ref Kord key, ref short go, T errs)
	{
		(int cx, short a, short want) rec = default;
		if (go == No && form.other != No) // reduce by other key ordinals
			if (!recover || stack[^1].form.recs == null) {
				go = form.other;
				return (0, 0, null);
			}
		T e = new() {
			j = (ler.Loc(), ler.Loc() + (key != default ? 1 : 0)), err = -1,
			info = (int)errs.info == 100 ? "more than 100 errors ..."
				: form.err ?? (go < No ? SerMaker<K, N>.ErrEor : "unknown error"),
		};
		if ((int)errs.info <= 100)
			errs.Add(e).info = (int)errs.info + 1;
		// recovery disabled
		if (!recover)
			return (-1, 0, null);
		// want a recovery key right now, fake it
		if (key != default && (rec = Fake(key, -1, stack.Count - 1)).cx > 0) {
			go = SynForm.Redu(rec.a);
			e.j.via = e.j.on;
			return (1, rec.want, e); // recover
		}
		// recovery stack
		if (recCs == null)
			StackRecs();
		// read until one recovery key and shift it
		for (; rec.cx == 0; key = ler.Next() ? keyOrd(ler) : default)
			// not found, fake it
			if (key == default) {
				rec.cx = 1;
				for (int x = 0; x < recCs.Length; x++)
					if (recCs[x][^1] >= rec.cx && Fake(key, x, recCs[x][^1]) is var r && r.cx > 0)
						if (r.cx > rec.cx || r.want > rec.want)
							rec = r; // latest recovery stack and most want
				if (rec.want == 0)
					return (-1, 0, null); // can not recover, reject
			}
			else if (Array.BinarySearch(recKs, key) is var kx and >= 0
					&& recCs[kx][^1] is var cx and > 0)
				foreach (var (a, want) in stack[cx].form.recs)
					if (alts[(rec = (cx, a, want)).a].rec == kx) // latest alt due to reversed index
						break;
		// recover
		StackPop(on: rec.cx + 1);
		go = SynForm.Redu(rec.a);
		e.name = alts[rec.a].name;
		return (1, rec.want, e);
	}

	(int cx, short a, short want) Fake(Kord key, int kx, int cx)
	{
		if (key != default)
			foreach (var (a, want) in stack[cx].form.recs ?? [])
				if (alts[a].lex >= 0 && want > 0 && want == alts[a].size - 1) // final key
					return (cx, a, want);
		if (key == default)
			foreach (var (a, want) in stack[cx].form.recs ?? [])
				if ((kx < 0 || alts[a].rec == kx) && want + stack.Count - cx - 1 > 0)
					return (cx, a, want);
		return (0, 0, 0);
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
		foreach (var d in s.Full()) if (d.d < No) yield return (SynForm.Redu(d.d), d.ord);
	}
}
