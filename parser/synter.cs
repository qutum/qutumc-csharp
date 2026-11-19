//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
// recovery key ordinal index
global using Krecx = qutum.parser.X<sbyte>;
// SynForm index
global using Formx = qutum.parser.X<qutum.parser.SynForm>;
// key ordinal indices
global using KordXs = qutum.parser.BitSet<char>;
// alt indices
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
// stack index
using Stax = X<SynStack<char>>;

// syntax tree
public partial class Synt<N, T, L> : Tree<T> where T : Synt<N, T, L>
{
	public N name;
	public Jov<X<L>> j; // jot of lexs: on >=0, jot of error lexs: on <0
	public sbyte err; // no error: 0, recovered: 1, error: -1, cyclic: -2, lex error: -3
	public object info; // lexi, error info, error synt of recovery ...
}

public class SyntStr : Synt<string, SyntStr, char>
{
}

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

	public static Formx Redu(Altx alt) => (Formx)(-2 - +alt);
	public static Altx Redu(Formx form) => (Altx)(-2 - +form);
}
public partial struct OrdFormx<Ord> where Ord : struct, IBinaryInteger<Ord>
{
	public Formx[] F; // at ordinal or compact index
	public Ord[] O; // compact: { ordinals ... }, normal: null

	public readonly Formx this[Ord o] {
		get => O == null ? int.CreateTruncating(o) is var y && y < F.Length ? F[y] : Formx.No
			: O.Length switch {
				1 => O[0] == o ? F[0] : Formx.No,
				2 => O[0] == o ? F[0] : O[1] == o ? F[1] : Formx.No,
				3 => O[0] == o ? F[0] : O[1] == o ? F[1] : O[2] == o ? F[2] : Formx.No,
				4 => O[0] == o ? F[0] : O[1] == o ? F[1] : O[2] == o ? F[2] : O[3] == o ? F[3] : Formx.No,
				_ => Array.BinarySearch(O, o) is var z and >= 0 ? F[z] : Formx.No
			};
	}
}
internal class SynStack<L> : List<Stax,
	// (form, lex loc i.e synt start, synt or null for lex)
	(SynForm form, X<L> loc, object what)> // index from 1
{
	internal Func<Altx, Krecx> altRec;
	Arr<Krecx, List<Stax>> recTs; // { 0, recovery stack index... } of each recovery key

	public (SynForm form, X<L> loc, object what) Tail(Conx x) => Tail((Stax)(+x));

	internal void Push(SynForm form, X<L> loc, object what)
	{
		Ad((form, loc, what));
		if (recTs.s != null && form.recs != null)
			foreach (var a in form.recs)
				recTs[altRec(a.alt)].Add(size - 1);
	}
	internal void Pops(Stax? on = null, Conx? tail = null)
	{
		var o = on ?? (size - (Stax)(+tail ?? 1));
		Drop((o, size));
		if (recTs.s != null)
			foreach (var cs in recTs) {
				var x = cs.Count;
				for (; o <= cs[x - 1]; x--) ;
				cs.RemoveRange(x, cs.Count - x);
			}
	}
	internal Arr<Krecx, List<Stax>> RecStax(Krecx krecSize)
	{
		if (recTs.s != null) return recTs;
		recTs = new(krecSize);
		foreach (var x in recTs.j)
			recTs[x] = new(+size) { Stax.No };
		foreach (var ((f, _, _), x) in Each())
			foreach (var a in f?.recs ?? [])
				recTs[altRec(a.alt)].Add(x);
		return recTs;
	}
	public override SynStack<L> Clear() { base.Clear(); Ad(default); recTs = default; return this; }
}

// syntax parser
public class Synter<K, N, T, Ler> : Synter<K, Lexi<K>, N, T, Ler>
	where K : struct where T : Synt<N, T, Lexi<K>>, new() where Ler : class, Lexer<K, Lexi<K>>
{
	public Synter(Func<Ler, Kord> keyOrd, Func<N, Nord> nameOrd, SynterNew<N> New)
		: base(keyOrd, nameOrd, New) { }
}

public class SynterStr : Synter<char, char, string, SyntStr, LerStr>
{
	public SynterStr(Func<string, Nord> nameOrd, SynterNew<string> New)
		: base(ler => ler.Lex(), nameOrd, New) { }
}

public record struct SynterNew<N>
	(Arr<Altx, SynAlt<N>> alts, Arr<Formx, SynForm> forms, Arr<Krecx, Kord>? recKs = null);

// syntax parser using LR algorithm
// K for lexical key i.e lexeme, L for lexical token
// N for syntax name i.e synteme, T for syntax tree
public partial class Synter<K, L, N, T, Ler>
	where K : struct where T : Synt<N, T, L>, new() where Ler : class, Lexer<K, L>
{
	readonly Arr<Altx, SynAlt<N>> alts; // reduce [0] by eor: accept
	readonly Arr<Formx, SynForm> forms;
	readonly SynForm init;
	readonly Arr<Krecx, Kord>? recKs; // { recovery key ordinal ... }

	protected Func<Ler, Kord> keyOrd; // ordinal from 1, default for eor: 0
	protected Func<N, Nord> nameOrd; // ordinal from 1

	public Ler ler;
	public bool recover = false; // whether recover errors
	public bool synt = true; // make synt tree by default, always make recovery synt

	public Synter(Func<Ler, Kord> keyOrd, Func<N, Nord> nameOrd, SynterNew<N> New)
	{
		this.keyOrd = keyOrd; this.nameOrd = nameOrd;
		alts = New.alts; forms = New.forms; recKs = New.recKs;
		if (+alts.size is 0 or > 32767)
			throw new($"{nameof(alts)} size {alts.size}");
		if (+forms.size is 0 or > 32767)
			throw new($"{nameof(forms)} size {forms.size}");
		stack = new() { altRec = a => alts[a].rec };
		init = forms[Formx.Z] ?? forms[(Formx)1];
		foreach (var (f, fx) in forms.Each())
			f?.index = fx;
		InitDump();
	}

	public virtual Synter<K, L, N, T, Ler> Begin(Ler ler) { this.ler = ler; return this; }

	SynStack<L> stack;

	// accept or reject, no synt no error info no recovery
	public virtual bool Check()
	{
		stack.Clear();
		stack.Push(init, X<L>.No, null);
	Read:
		(Stax min, int loop) stuck = (stack.size, 0);
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack.tail.form;
		var go = form.goKs[key];
		if (go > Formx.No) { // shift
			stack.Push(forms[go], X<L>.No, null);
			goto Read;
		}
		else if (go < Formx.No) { // reduce
			var alt = alts[SynForm.Redu(go)];
			stack.Pops(tail: alt.size);
			form = stack.tail.form;
			go = form.goNs[nameOrd(alt.name)];
			if (go > Formx.No) {
				stack.Push(forms[go], X<L>.No, null);
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
		(Stax min, int loop) stuck = (stack.size, 0);
		var key = ler.Next() ? keyOrd(ler) : default;
	Loop:
		var form = stack.tail.form;
		var go = form.goKs[key];
		if (go > Formx.No) { // shift
			stack.Push(forms[go], ler.Loc(), null);
			goto Read;
		}
		(sbyte err, Conx size, T info) redu = default;
	Reduce:
		T synt;
		if (go < Formx.No) { // reduce
			(var name, var loc, synt) = Reduce(go, ref redu);
			form = stack.tail.form;
			go = form.goNs[name];
			if (go > Formx.No) {
				stack.Push(forms[go], loc, synt);
				if (stack.size < stuck.min) (stuck.min, stuck.loop) = (stack.size, 1);
				else if (++stuck.loop >= 100) goto Cyclic; // stack stuck without shift
				goto Loop;
			}
			else if (key == default) {
				stack.Clear();
				return (synt ?? new()).Append(errs.head?.up); // reduce alts[0] by eor, accept
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
		(synt ??= new()).err = -2;
		synt.info = "maybe infinite loop due to cyclic grammar or recovery";
		return errs.Add(synt);
	}

	(Nord name, X<L> loc, T synt) Reduce(Formx go, ref (sbyte err, Conx size, T info) redu)
	{
		var alt = alts[SynForm.Redu(go)];
		if (redu.err == 0)
			redu.size = alt.size;
		var loc = redu.size.pos ? stack.Tail(redu.size).loc : stack.tail.loc + 1;
		if (redu.err > 0 && redu.info is T e && loc > e.j.on)
			loc = e.j.on; // recovery synt start
		bool make = alt.synt > 0 || alt.synt == 0 && this.synt;
		T synt = make || redu.err > 0 ? new() { // for omitted recovery synt, append it
			name = alt.syntName, j = (loc, ler.Loc()), dump = dump >= 3 ? alt : alt.label,
			err = redu.err, info = redu.info,
		} : null;
		for (var x = redu.size - 1; x.zpos; x--) { // backward
			var (_, l, what) = stack.Tail(redu.size - x);
			if (what is T t)
				if (!make // flatten synts
					|| alt.synt == 2 && x.zero) // lift first synt as prev
					synt = t.Append(synt); // maybe slow if deep left recursive
				else if (alt.synt == 3 && x == redu.size - 1) // lift last synt as next
					synt.Append(t);
				else
					synt.HeadAdd(t);
			else if (make && x == alt.lex)
				synt.info ??= ler.Lex(l);
		}
		stack.Pops(tail: redu.size);
		return (nameOrd(alt.name), loc, synt);
	}

	(sbyte err, Conx size, T info) Error(SynForm form, ref Kord key, ref Formx go, T errs)
	{
		(Stax tx, Altx a, Conx want) rec = (Stax.No, Altx.No, Conx.No);
		if (go.no && form.other.yes) // reduce by other key ordinals
			if (!recover || stack.tail.form.recs == null) {
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
		if (key != default) {
			rec = Fake(key, Krecx.No, stack.size - 1);
			if (rec.tx.yes) {
				go = SynForm.Redu(rec.a);
				e.j.via = e.j.on;
				return (1, rec.want, e); // recover
			}
		}
		// recovery stack
		var recTs = stack.RecStax(recKs?.size ?? Krecx.Z);
		// read until one recovery key and shift it
		for (; rec.tx.no; key = ler.Next() ? keyOrd(ler) : default)
			// not found, fake it
			if (key == default) {
				var tx = (Stax)1;
				foreach (var (_, x) in recTs.Each())
					if (recTs[x][^1] >= tx && Fake(key, x, recTs[x][^1]) is var r && r.tx.yes)
						if (r.tx > tx || r.want > rec.want)
							tx = (rec = r).tx; // latest recovery stack and most want
				if (rec.tx.no)
					return (-1, Conx.No, null); // can not recover, reject
			}
			else if (recKs?.BinFind(key) is Krecx kx && kx.yes && recTs[kx][^1] is var tx && tx.yes)
				foreach (var (a, want) in stack[tx].form.recs)
					if (alts[(rec = (tx, a, want)).a].rec == kx) // latest alt due to reversed index
						break;
		// recover
		stack.Pops(rec.tx + 1);
		go = SynForm.Redu(rec.a);
		e.name = alts[rec.a].name;
		return (1, rec.want, e);
	}

	(Stax tx, Altx a, Conx want) Fake(Kord key, Krecx kx, Stax tx)
	{
		if (key != default)
			foreach (var (a, want) in stack[tx].form.recs ?? [])
				if (alts[a].lex.yes && want.pos && want == alts[a].size - 1) // final key
					return (tx, a, want);
		if (key == default)
			foreach (var (a, want) in stack[tx].form.recs ?? [])
				if ((kx.no || alts[a].rec == kx) && (want.pos || (stack.size - 1 - tx).pos))
					return (tx, a, want);
		return (Stax.No, Altx.No, Conx.No);
	}
}

public partial struct OrdFormx<Ord>
{
	public readonly IEnumerable<(Formx fx, Ord ord)> Full()
	{
		for (var x = 0; x < F.Length; x++)
			yield return (F[x], O?[x] ?? Ord.CreateTruncating(x));
	}
	public readonly IEnumerable<(Formx fx, Ord ord)> Yes()
	{
		foreach (var o in Full()) if (o.fx.yes) yield return o;
	}

	public static OrdFormx<Ord> Compact<X>(Arr<X, Formx> fs, Arr<X, Ord> os, int compact = 50)
		where X : IUnaryPlusOperators<X, int>, IAdditionOperators<X, int, X>
	{ // TODO better compact
		OrdFormx<Ord> S = new();
		int z = int.CreateTruncating(os.tail), zz;
		if (z < compact || (zz = fs.Count(x => x.yes)) >= z >>> 3) {
			S.F = new Formx[z + 1];
			Array.Fill(S.F, Formx.No);
			foreach (var (f, i) in fs.Each())
				S.F[int.CreateTruncating(os[i])] = f;
		}
		else {
			(S.F, S.O) = (new Formx[zz], new Ord[zz]);
			int I = 0;
			foreach (var (o, i) in os.Each())
				if (fs[i].yes)
					(S.F[I], S.O[I++]) = (fs[i], o);
		}
		return S;
	}
}
