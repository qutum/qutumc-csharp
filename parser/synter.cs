//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
// key ordinal index
global using Kordx = qutum.parser.X<char>;
// name ordinal index
global using Nordx = qutum.parser.X<ushort>;
// recovery key ordinal index
global using Krecx = qutum.parser.X<sbyte>;
// SynForm index
global using Formx = qutum.parser.X<qutum.parser.SynForm>;
// SynAlt index
global using Altx = qutum.parser.X<qutum.parser.XAlt>;
// SynAlt.Con in index
global using Conx = qutum.parser.X<qutum.parser.XAltCon>;
// key ordinal indices
global using KordXs = qutum.parser.BitSet<char>;
// SynAlt indices
global using AltXs = qutum.parser.BitSet<qutum.parser.XAlt>;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace qutum.parser;

// key ordinal type
using Kord = char;
// name ordinal type
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

public struct XAlt { }
public sealed partial class SynAlt<N>
{
	public N name;
	public Conx size;
	public Conx lex; // save main lex at this index to synt.info, no save: <0
	public Krecx rec; // recovery key ordinal index, no: <0
	public sbyte synt; // make synt: as synter: 0, omit: -1, make: 1, lift left: 2, lift right: 3
	public N syntName;
	public string label;
}
public sealed partial class SynForm
{
	public Formx index;
	public OrdFormx<Kord> goKs; // for each key: shift to: form index, reduce: -2-alt index, error: -1
	public OrdFormx<Nord> goNs; // for each name: form index, error: -1
	public Formx other = Formx.No; // reduce by other key ordinals
	public string err; // error info
	public (Altx alt, Conx want)[] recs; // { recovery alt index and want ... }

	public static Formx Redu(Altx alt) => (Formx)(short)(-2 - alt.x);
	public static Altx Redu(Formx form) => (Altx)(short)(-2 - form.x);
}
public partial struct OrdFormx<Ord> where Ord : struct, IBinaryInteger<Ord>
{
	public Formx[] X; // at ordinal or compact index
	public Ord[] O; // compact: { ordinals ... }, normal: null

	public readonly Formx this[Ord o] {
		get => O == null ? int.CreateTruncating(o) is var y && y < X.Length ? X[y] : Formx.No
			: O.Length switch {
				1 => O[0] == o ? X[0] : Formx.No,
				2 => O[0] == o ? X[0] : O[1] == o ? X[1] : Formx.No,
				3 => O[0] == o ? X[0] : O[1] == o ? X[1] : O[2] == o ? X[2] : Formx.No,
				4 => O[0] == o ? X[0] : O[1] == o ? X[1] : O[2] == o ? X[2] : O[3] == o ? X[3] : Formx.No,
				_ => Array.BinarySearch(O, o) is var z and >= 0 ? X[z] : Formx.No
			};
	}
}

internal class Stack
{
	internal Func<Altx, Krecx> altRec;

	// (form, lex loc i.e synt start, synt or null for lex)
	readonly List<(SynForm form, int loc, object synt)> s = []; // index from 1
	List<int>[] recCs; // { 0, recovery stack index... } of each recovery key

	internal (SynForm form, int loc, object synt) this[Index x] => s[x];
	internal int size => s.Count;

	internal void Push(SynForm form, int loc, object synt)
	{
		s.Add((form, loc, synt));
		if (recCs != null && form.recs != null)
			foreach (var a in form.recs)
				recCs[altRec(a.alt).x].Add(s.Count - 1);
	}
	internal void Pop(int on = 0, int size = 0)
	{
		if (on == 0) on = s.Count - size;
		else if (size == 0) size = s.Count - on;
		s.RemoveRange(on, size);
		if (recCs != null)
			foreach (var cs in recCs) {
				var x = cs.Count;
				for (; on <= cs[x - 1]; x--) ;
				cs.RemoveRange(x, cs.Count - x);
			}
	}
	internal List<int>[] RecStacks(int krecSize)
	{
		if (recCs != null) return recCs;
		recCs = new List<int>[krecSize];
		for (var x = 0; x < recCs.Length; x++)
			recCs[x] = new(s.Capacity) { 0 };
		foreach (var ((f, _, _), x) in s.Each())
			foreach (var a in f?.recs ?? [])
				recCs[altRec(a.alt).x].Add(x);
		return recCs;
	}
	internal void Clear() { s.Clear(); s.Add(default); recCs = null; }
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
		stack = new() { altRec = a => this.alts[a.x].rec };
		init = forms[0] ?? forms[1];
		foreach (var (f, fx) in forms.Each())
			f?.index = (Formx)(short)fx;
		InitDump();
	}

	public virtual Synter<K, L, N, T, Ler> Begin(Ler ler) { this.ler = ler; return this; }

	Stack stack;

	// accept or reject, no synt no error info no recovery
	public virtual bool Check()
	{
		stack.Clear();
		stack.Push(init, -1, null);
	Read:
		(int min, int loop) stuck = (stack.size, 0);
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var go = form.goKs[key];
		if (go > Formx.No) { // shift
			stack.Push(forms[go.x], -1, null);
			goto Read;
		}
		else if (go < Formx.No) { // reduce
			var alt = alts[SynForm.Redu(go).x];
			stack.Pop(size: alt.size.x);
			form = stack[^1].form;
			go = form.goNs[nameOrd(alt.name)];
			if (go > Formx.No) {
				stack.Push(forms[go], -1, null);
				if (stack.size < stuck.min) (stuck.min, stuck.loop) = (stack.size, 1);
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

	// make synt tree followed by errors
	public virtual T Parse()
	{
		var errs = new T { err = -1, info = 0 };
		stack.Clear();
		stack.Push(init, ler.Loc(), null);
	Read:
		(int min, int loop) stuck = (stack.size, 0);
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack[^1].form;
		var go = form.goKs[key];
		if (go > Formx.No) { // shift
			stack.Push(forms[go.x], ler.Loc(), null);
			goto Read;
		}
		(sbyte err, Conx size, T info) redu = default;
	Reduce:
		T t;
		if (go < Formx.No) { // reduce
			(var name, var loc, t) = Reduce(go, ref redu);
			form = stack[^1].form;
			go = form.goNs[name];
			if (go > Formx.No) {
				stack.Push(forms[go.x], loc, t);
				if (stack.size < stuck.min) (stuck.min, stuck.loop) = (stack.size, 1);
				else if (++stuck.loop >= 100) goto Cyclic; // stack stuck without shift
				goto Loop;
			}
			else if (key == default) {
				stack.Clear();
				return (t ?? new()).Append(errs.head?.up); // reduce alts[0] by eor, accept
			} // reduce alts[0] by others, error
		}
		// error: other, recover, accept, reject
		redu = Error(form, ref key, ref go, errs);
		if (redu.err >= 0)
			goto Reduce;
		stack.Clear();
		return errs; // reject
	Cyclic:
		stack.Clear();
		(t ??= new()).err = -2;
		t.info = "maybe infinite loop due to cyclic grammar or recovery";
		return errs.Add(t);
	}

	(Nord name, int loc, T t) Reduce(Formx go, ref (sbyte err, Conx size, T info) redu)
	{
		var alt = alts[SynForm.Redu(go).x];
		if (redu.err == 0)
			redu.size = alt.size;
		int loc = redu.size.x > 0 ? stack[^redu.size.x].loc : stack[^1].loc + 1;
		if (redu.err > 0 && redu.info is T e && loc > e.j.on)
			loc = e.j.on; // recovery synt start
		bool make = alt.synt > 0 || alt.synt == 0 && synt;
		T t = make || redu.err > 0 ? new() { // for omitted recovery synt, append it
			name = alt.syntName, j = (loc, ler.Loc()), dump = dump >= 3 ? alt : alt.label,
			err = redu.err, info = redu.info,
		} : null;
		for (var i = redu.size.x - 1; i >= 0; i--) {
			var (_, l, synt) = stack[^(redu.size.x - i)];
			if (synt is T head)
				if (!make // flatten synts
					|| alt.synt == 2 && i == 0) // lift first synt as prev
					t = head.Append(t); // maybe slow if deep left recursive
				else if (alt.synt == 3 && i == redu.size.x - 1) // lift last synt as next
					t.Append(head);
				else
					t.AddHead(head);
			else if (make && i == alt.lex.x)
				t.info ??= ler.Lex(l);
		}
		stack.Pop(size: redu.size.x);
		return (nameOrd(alt.name), loc, t);
	}

	(sbyte err, Conx size, T info) Error(SynForm form, ref Kord key, ref Formx go, T errs)
	{
		(int cx, Altx a, Conx want) rec = default;
		if (!go.Yes && form.other.Yes) // reduce by other key ordinals
			if (!recover || stack[^1].form.recs == null) {
				go = form.other;
				return (0, Conx.No, null);
			}
		T e = new() {
			j = (ler.Loc(), ler.Loc() + (key != default ? 1 : 0)), err = -1,
			info = (int)errs.info == 100 ? "more than 100 errors ..."
				: form.err ?? (go < Formx.No ? SerMaker<K, N>.ErrEor : "unknown error"),
		};
		if ((int)errs.info <= 100)
			errs.Add(e).info = (int)errs.info + 1;
		// recovery disabled
		if (!recover)
			return (-1, Conx.No, null);
		// want a recovery key right now, fake it
		if (key != default && (rec = Fake(key, -1, stack.size - 1)).cx > 0) {
			go = SynForm.Redu(rec.a);
			e.j.via = e.j.on;
			return (1, rec.want, e); // recover
		}
		// recovery stack
		var recCs = stack.RecStacks(recKs.Length);
		// read until one recovery key and shift it
		for (; rec.cx == 0; key = ler.Next() ? keyOrd(ler) : default)
			// not found, fake it
			if (key == default) {
				rec.cx = 1;
				for (int x = 0; x < recCs.Length; x++)
					if (recCs[x][^1] >= rec.cx && Fake(key, x, recCs[x][^1]) is var r && r.cx > 0)
						if (r.cx > rec.cx || r.want > rec.want)
							rec = r; // latest recovery stack and most want
				if (rec.want.x == 0)
					return (-1, (Conx)0, null); // can not recover, reject
			}
			else if (Array.BinarySearch(recKs, key) is var kx and >= 0
					&& recCs[kx][^1] is var cx and > 0)
				foreach (var (a, want) in stack[cx].form.recs)
					if (alts[(rec = (cx, a, want)).a.x].rec == kx) // latest alt due to reversed index
						break;
		// recover
		stack.Pop(on: rec.cx + 1);
		go = SynForm.Redu(rec.a);
		e.name = alts[rec.a.x].name;
		return (1, rec.want, e);
	}

	(int cx, Altx a, Conx want) Fake(Kord key, int kx, int cx)
	{
		if (key != default)
			foreach (var (a, want) in stack[cx].form.recs ?? [])
				if (alts[a.x].lex.Yes && want.x > 0 && want.x == alts[a.x].size.x - 1) // final key
					return (cx, a, want);
		if (key == default)
			foreach (var (a, want) in stack[cx].form.recs ?? [])
				if ((kx < 0 || alts[a.x].rec == kx) && want.x + stack.Count - cx - 1 > 0)
					return (cx, a, want);
		return (0, (Altx)0, (Conx)0);
	}
}

public partial struct OrdFormx<Ord>
{
	public readonly IEnumerable<(Formx x, Ord ord)> Full()
	{
		for (var y = 0; y < X.Length; y++)
			yield return (X[y], O?[y] ?? Ord.CreateTruncating(y));
	}
	public readonly IEnumerable<(Formx x, Ord ord)> Yes()
	{
		foreach (var o in Full()) if (o.x.Yes) yield return o;
	}

	public static OrdFormx<Ord> Compact(Formx[] xs, Ord[] os, int compact = 50) // TODO better compact
	{
		OrdFormx<Ord> S = new();
		int z = int.CreateTruncating(os[^1]), zz;
		if (z < compact || (zz = xs.Count(x => x.Yes)) >= z >>> 3) {
			S.X = new Formx[z + 1];
			Array.Fill(S.X, Formx.No);
			foreach (var (v, i) in xs.Each())
				S.X[int.CreateTruncating(os[i])] = v;
		}
		else {
			(S.X, S.O) = (new Formx[zz], new Ord[zz]);
			for (int I = 0, i = 0; i < os.Length; i++)
				if (xs[i].Yes)
					(S.X[I], S.O[I++]) = (xs[i], os[i]);
		}
		return S;
	}
}
