//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace qutum.parser;

using Kord = char;
using Nord = ushort;

public struct XAltCon { }
// syntax grammar
public partial class SynGram<K, N>
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Alt>
	{
		public N name;
		public string label;
		public bool labelYes;
	}
	public partial class Alt : List<Con>
	{
		internal Altx index; // index of whole grammar alts
		public N name;
		public Conx lex = Conx.No; // save main lex at this index to synt.info, no save: <0
		public Altx clash = Altx.No; // reject: -1, left: -2, right: -3
									// solve and index as same as previous one: actual alt index
		public bool rec; // whether recover this alt for error, after main lex if any
		public sbyte synt; // make synt: as synter: 0, omit: -1, make: 1, lift left: 2, lift right: 3
		public N asName; // as name: not default, same as name: default
		public string label;
	}
	public readonly struct Con
	{
		public readonly Kordx kx;
		public readonly Nordx nx;
		public readonly K k;
		public readonly N n;
		public readonly bool K => kx.Yes;

		public Con(Kordx x, K k) => (kx, nx, this.k) = (x, Nordx.No, k);
		public Con(Nordx x, N n) => (kx, nx, this.n) = (Kordx.No, x, n);
	}

	public SynGram<K, N> n(N name, string label = null, bool labelYes = true)
	{
		prods.Add(new() { name = name, label = label, labelYes = labelYes }); return this;
	}

	// K : lexic key, N : syntax name, .. : previous is main lex
	public SynGram<K, N> this[params object[] cons] {
		get {
			var p = prods[^1];
			Alt a = new() { name = p.name, label = p.labelYes ? p.label : null };
			p.Add(a);
			foreach (var c in cons)
				if (c is Range)
					a.lex = a.lex.Yes ? throw new($"{p.name}.{p.Count - 1} lex {a.lex}")
						: (Conx)(short)(a.Count - 1);
				else if (c is K k)
					a.Add(new((Kordx)0, k));
				else if (c is N n)
					a.Add(new((Nordx)0, n));
				else
					throw new($"wrong altern content {c?.GetType()}");
			return this;
		}
	}
	public SynGram<K, N> clash { get { prods[^1][^1].clash = (Altx)(-2); return this; } }
	public SynGram<K, N> clashRight { get { prods[^1][^1].clash = (Altx)(-3); return this; } }
	public SynGram<K, N> clashPrev { get { prods[^1][^1].clash = (Altx)0; return this; } }
	public SynGram<K, N> synt { get { prods[^1][^1].synt = 1; return this; } }
	public SynGram<K, N> syntLeft { get { prods[^1][^1].synt = 2; return this; } }
	public SynGram<K, N> syntRight { get { prods[^1][^1].synt = 3; return this; } }
	public SynGram<K, N> syntOmit { get { prods[^1][^1].synt = -1; return this; } }
	public SynGram<K, N> asName(N n) { prods[^1][^1].asName = n; return this; }
	public SynGram<K, N> recover { get { prods[^1][^1].rec = true; return this; } }
	public SynGram<K, N> label(string w) { prods[^1][^1].label = w.ToString(); return this; }
	public SynGram<K, N> labelNo { get { prods[^1][^1].label = null; return this; } }
	public SynGram<K, N> labelYes { get { prods[^1][^1].label = prods[^1].label; return this; } }
	// for sourcecode format
	public SynGram<K, N> _ => this;
	public SynGram<K, N> __ => this;
	public SynGram<K, N> ___ => this;
	public SynGram<K, N> ____ => this;
}

// syntax parser maker
// K for lexical key i.e lexeme
// N for syntax name i.e synteme
public partial class SerMaker<K, N>
{
	readonly Func<K, Kord> keyOrd; // ordinal from 1, default for eor: 0
	readonly Func<N, Nord> nameOrd; // ordinal from 1
	readonly Kord[] keyOs; // { key ordinal... }
	readonly Nord[] nameOs; // { name ordinal... }
	readonly K[] keys; // at key ordinal index
	readonly N[] names; // at name ordinal index
	readonly Kord[] recKs; // { recovery key ordinal ... }

	private Kordx Key(K k, Kord[] os) => (Kordx)(short)Array.BinarySearch(os, keyOrd(k));
	private Nordx Name(N n) => (Nordx)(short)Array.BinarySearch(nameOs, nameOrd(n));

	readonly SynGram<K, N>.Prod[] prods; // at name ordinal index
	readonly SynGram<K, N>.Alt[] alts;
	readonly (bool empty, KordXs first)[] firsts; // at name ordinal index
	readonly (bool empty, KordXs first)[][] firstAs; // at alt index and want

	// phase 1: find first keys of each name and each alt want
	public void Firsts()
	{
		if (firstAs[0] != null)
			return;
		for (bool loop = true; !(loop = !loop);)
			foreach (var (p, nx) in prods.Each()) {
				var nf = firsts[nx].first.Use(keys.Length);
				foreach (var a in p) {
					foreach (var c in a) {
						var (ce, cf) = c.K ? (false, KordXs.One(keys.Length, c.kx))
							: firsts[c.nx.x];
						loop |= nf.Or(cf);
						if (!ce)
							goto A;
					}
					_ = firsts[nx].empty || (loop = firsts[nx].empty = true);
				A:;
				}
			}
		for (var x = 0; x < firstAs.Length; x++) {
			var a = alts[x];
			var fs = firstAs[x] = new (bool, KordXs)[a.Count + 1];
			fs[^1].empty = true;
			for (var w = a.Count - 1; w > 0; w--)
				if (a[w].K)
					fs[w] = (false, KordXs.One(keys.Length, a[w].kx));
				else {
					var (e, f) = firsts[a[w].nx.x];
					fs[w] = !e ? (e, f) : (fs[w + 1].empty, f.NewOr(fs[w + 1].first, true));
				}
		}
	}
	public (bool empty, IEnumerable<K> first) First(N name)
	{
		var (e, f) = firsts[Name(name).x];
		return (e, KeySet(f));
	}
	public IEnumerable<K> KeySet(KordXs s) => s.Select(kx => keys[kx.x]);

	class Items : List<(
		SynGram<K, N>.Alt alt,
		Conx want,
		KordXs heads, // lookaheads including eor
		int clo
		)>
	{
	}
	[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2231:Overload operator equals")]
	public struct Clash
	{
		internal Formx shift; // shift to form index if shifts not null
		public AltXs redus; // alt index, no reduce: null
		public AltXs shifts; // alt index, no shift: null
		public override readonly int GetHashCode() => HashCode.Combine(
			shifts.set.bits?[0] ?? 0, shifts.set.bits?[^1] ?? 0,
			redus.set.bits?[0] ?? 0, redus.set.bits?[^1] ?? 0);
		public override readonly bool Equals(object o) =>
			o is Clash c && redus.Same(c.redus) && shifts.Same(c.shifts);
	}
	struct Form
	{
		internal Formx index;
		internal Items Is;
		internal Clash[] clashs; // at key ordinal index
		internal Formx[] goKs; // solved, at key ordinal index, error: -1
		internal Formx[] goNs; // form index, at name ordinal index, error: -1
	}

	readonly List<Form> forms = [];
	readonly Dictionary<(SynGram<K, N>.Alt, Conx), int> addItems = [];
	readonly List<int> addHeads = [];
	readonly Dictionary<object, Formx> addGos = [];

	int AddItem(Items Is, SynGram<K, N>.Alt alt, Conx want, KordXs heads1, KordXs heads2, int clo)
	{
		if (addItems.TryGetValue((alt, want), out var x))
			return (Is[x].heads.Or(heads1) | Is[x].heads.Or(heads2))
				&& firstAs[alt.index.x][want.x + 1].empty ? x : -1;
		addItems[(alt, want)] = Is.Count;
		Is.Add((alt, want, heads1.NewOr(heads2), clo));
		return -1;
	}
	Formx AddForm(Items Is)
	{
		foreach (var f in forms)
			if (f.Is.Count == Is.Count) {
				foreach (var (fi, i) in f.Is.Zip(Is))
					if (fi.alt != i.alt || fi.want != i.want || !fi.heads.Same(i.heads))
						goto No;
				return f.index;
			No:;
			}
		var F = new Form {
			index = (Formx)checked((short)forms.Count), Is = Is, clashs = new Clash[keys.Length],
			goNs = new Formx[names.Length], goKs = new Formx[keys.Length],
		};
		forms.Add(F);
		Array.Fill(F.goKs, Formx.No); Array.Fill(F.goNs, Formx.No);
		return F.index;
	}

	// make a whole items
	Items Closure(Items Is)
	{
		for (var x = 0; x < Is.Count + addHeads.Count; x++) {
			var y = x < Is.Count ? x : addHeads[x - Is.Count];
			var (a, want, heads, clo) = Is[y];
			if (want.x >= a.Count || a[want.x].K)
				continue;
			var (e, f) = firstAs[a.index.x][want.x + 1];
			foreach (var A in prods[a[want.x].nx.x])
				if ((y = AddItem(Is, A, (Conx)0, f, e ? heads : default, clo + 1)) >= 0)
					addHeads.Add(y);
		}
		addItems.Clear(); addHeads.Clear();
		return Is;
	}

	// make shifting or pushing between forms
	void ShiftPush(Form f)
	{
		foreach (var (a, want, _, _) in f.Is) {
			if (want.x >= a.Count)
				continue;
			if (!addGos.TryGetValue(a[want.x], out var go)) {
				Items js = [];
				foreach (var (A, W, heads, _) in f.Is) {
					if (W.x < A.Count && A[W.x].Equals(a[want.x]))
						AddItem(js, A, (Conx)(short)(W.x + 1), heads, default, 1);
				}
				addGos.Add(a[want.x], go = AddForm(Closure(js)));
			}
			if (a[want.x].K) {
				f.clashs[a[want.x].kx.x].shift = go;
				f.clashs[a[want.x].kx.x].shifts.Use(alts.Length).Or(a.index);
			}
			else
				f.goNs[a[want.x].nx.x] = go;
		}
		addGos.Clear();
	}

	// phase 2: make all transition forms
	public void Forms()
	{
		if (forms.Count > 0)
			return;
		Items init = [];
		AddItem(init, alts[0], (Conx)0, KordXs.One(keys.Length, default), default, 1);
		AddForm(Closure(init));
		for (var x = 0; x < forms.Count; x++) {
			if (x >= 32767) throw new("too many forms");
			ShiftPush(forms[x]);
		}
		foreach (var f in forms)
			foreach (var (a, want, heads, _) in f.Is)
				if (want.x >= a.Count) // alt could be reduced
					foreach (var head in heads)
						f.clashs[head.x].redus.Use(alts.Length).Or(a.index);
		if (dump)
			Dump(forms);
	}

	// phase 3: find clashes and solve them
	// left: shift or reduce the latest index alt, reduce the same index (left associative)
	// right: shift or reduce the latest index alt, shift the same index (right associative)
	// all clashing alts not be rejected
	public Dictionary<Clash, (KordXs keys, Formx go)> Clashs(bool detail = true)
	{
		Dictionary<Clash, (KordXs keys, Formx go)> clashs = [];
		foreach (var f in forms)
			foreach (var (c, kx) in f.clashs.Each())
				// shift without clash
				if (c.redus.Empty)
					f.goKs[kx] = c.shifts.Empty ? Formx.No : c.shift;
				// reduce without clash
				else if (c.shifts.Empty && c.redus.Max() is Altx r && r == c.redus.Min()) // one redu
					f.goKs[kx] = SynForm.Redu(r);
				// already solved
				else if (clashs.TryGetValue(c, out var ok)) {
					f.goKs[kx] = ok.go;
					ok.keys.Or((Kordx)(short)kx);
				}
				// try to solve
				else {
					var go = Formx.No;
					if (c.redus.All(a => alts[a.x].clash.Yes)) {
						Altx A(Altx a) => alts[a.x].clash.x >= 0 ? alts[a.x].clash : a;
						r = c.redus.Max(); var ra = A(r);
						if (c.shifts.Empty)
							go = SynForm.Redu(r); // reduce
						else {
							Altx ia = A(c.shifts.Max()), ic = alts[ia.x].clash;
							if (ic.x != 0)
								if (ia > ra || ia == ra && ic.x <= -3)
									go = c.shift; // shift
								else
									go = SynForm.Redu(r); // reduce
						}
					}
					f.goKs[kx] = go;
					clashs[c] = (KordXs.One(keys.Length, (Kordx)(short)kx), go);
				}
		// solved
		var solvez = 0;
		if (!detail)
			foreach (var (c, (_, go)) in clashs)
				if (go.Yes && clashs.Remove(c))
					solvez++;
		if (dump)
			Dump((clashs, detail, solvez));
		return clashs.Count > 0 ? clashs : null;
	}

	// phase all and final: make all data for synter
	public (SynAlt<N>[] alts, SynForm[] forms, Kord[] recKs)
		Make(out Dictionary<Clash, (KordXs keys, Formx go)> clashs)
	{
		Firsts();
		Forms();
		clashs = Clashs(false);
		if (clashs != null)
			throw new("can not make synter due to clashes");
		// synter alts
		var As = new SynAlt<N>[alts.Length];
		foreach (var (a, ax) in alts.Each())
			As[ax] = new() {
				name = a.name, size = (short)a.Count, lex = a.lex,
#pragma warning disable CS8974 // Converting method group to non-delegate type
				synt = a.synt, label = a.label, dump = a.ToString,
				syntName = EqualityComparer<N>.Default.Equals(a.asName, default) ? a.name : a.asName,
				// final key index in recovery keys
				rec = (sbyte)(a.rec ? Key(a[^1].k, recKs).x : 0),
			};
		// synter forms
		var Fs = new SynForm[forms.Count];
		foreach (var f in forms) {
			var F = Fs[f.index.x] = new();
			F.goKs = OrdFormx<Kord>.Compact(f.goKs, keyOs);
			F.goNs = OrdFormx<Nord>.Compact(f.goNs, nameOs);
			Error(f, F);
		}
		return (As, Fs, recKs);
	}

	public static int ErrZ = 2; // etc excluded
	public static string Err = "{0} wants {1}", ErrMore = " and\n", ErrEtc = " ...";
	public static string ErrEor = "want end of read";

	// make error info and recovery
	void Error(Form f, SynForm F)
	{
		bool accept = false;
		if (F.goKs.Full().Select(kx => kx.x).Where(x => x < Formx.No).May().Max() is Formx other)
			if (other == SynForm.Redu((Altx)0))
				accept = true;
			else // for error and recovery, form with any reduce will always reduce on error keys
				F.other = other;

		// recover alts
		List<(Altx, Conx)> recs = [];
		foreach (var (a, want, _, _) in f.Is)
			if (a.rec && want > a.lex && want.x < a.Count) // usually useless before main lex
				recs.Add((a.index, want));
		recs.Sort(); recs.Reverse(); // reduce the latest alt of same recovery key
		if (recs.Count > 0)
			F.recs = [.. recs];

		// error infos
		List<(bool label, int clo, int half, short ax, SynGram<K, N>.Con)> ws = [];
		foreach (var (a, want, _, c) in f.Is)
			if (want.x < a.Count && want != a.lex) // main lex is usually for distinct alts of same name
				ws.Add((a.label != null, -c, want.x + want.x - a.Count, a.index.x, a[want.x]));
		ws.Sort();
		// only few infos, reverse
		List<(string a, string b, (string pre, int clo, int half, int ax) c)> es = [];
		bool label = false, debug = Err.Contains("{2}");
		int cloMax = int.MinValue;
		for (int x = 1; x <= ws.Count && es.Count <= ErrZ; x++) {
			var (lab, clo, half, ax, con) = ws[^x];
			if (lab != (label |= lab)) continue;
			if (clo < (cloMax = int.Max(cloMax, clo))) continue;
			// label or name wants name or key, no duplicate
			var e = (alts[ax].label ?? prods[Name(alts[ax].name).x].label ?? alts[ax].name.ToString(),
				con.K ? Dumper(con) : prods[con.nx.x].label ?? con.n.ToString(),
				debug ? (lab ? "l" : "", clo, half, ax) : default);
			if (es.IndexOf(e) < 0)
				es.Add(e);
		}
		StrMake err = new();
		for (int x = 0; x < es.Count; x++)
			err += x == ErrZ ? ErrEtc : err - ErrMore + err.F(Err, es[x].a, es[x].b, es[x].c);
		if (err.Size > 0)
			F.err = err;
		else if (accept)
			F.err = ErrEor;
	}

	// phase init: get grammar
	public SerMaker(SynGram<K, N> gram, Func<K, Kord> keyOrd, Func<N, Nord> nameOrd, bool dump = true)
	{
		this.dump = dump; this.keyOrd = keyOrd; this.nameOrd = nameOrd;
		// names
		SortedDictionary<Nord, N> ns = [];
		foreach (var (p, px) in gram.prods.Each())
			if (!ns.TryAdd(nameOrd(p.name), p.name) && !ns[nameOrd(p.name)].Equals(p.name))
				throw new($"duplicate ordinal of {ns[nameOrd(p.name)]} and {p.name}");
		nameOs = [.. ns.Keys];
		names = [.. ns.Values];
		if (nameOs[0] == default)
			throw new($"default name ordinal");
		var accept = gram.prods[0].name;

		SortedDictionary<Kord, K> ks = new() { [keyOrd(default)] = default };
		SortedSet<Kord> recs = [];
		// prods
		prods = new SynGram<K, N>.Prod[names.Length];
		foreach (var p in gram.prods)
			prods[Name(p.name).x] ??= p;
		// alts
		alts = new SynGram<K, N>.Alt[gram.prods.Sum(p => p.Count)];
		if (alts.Length is 0 or > 32767)
			throw new($"total {alts.Length} alterns");
		short ax = 0; Kord ko;
		foreach (var p in gram.prods) {
			var np = prods[Name(p.name).x];
			foreach (var (a, pax) in p.Each()) {
				// content
				if (a.Count > 30)
					throw new($"{p.name}.{pax} size {a.Count}");
				if (a.Count == 0 && ax == 0)
					throw new($"initial {p.name}.{pax} emppty");
				foreach (var c in a)
					if (c.K) {
						if ((ko = keyOrd(c.k)) == default)
							throw new($"end of read in {p.name}.{pax}");
						else if (!ks.TryGetValue(ko, out var kk))
							ks[ko] = c.k;
						else if (!c.k.Equals(kk))
							throw new($"{p.name}.{pax} lexic key {c.k} confused with {kk}");
					}
					else if (accept.Equals(c.n))
						throw new($"initial name {accept} in {p.name}.{pax} ");
					else if (Name(c.n) == Nordx.No)
						throw new($"name {c} in {p.name}.{pax} not found");
				// clash
				if (a.clash.x >= 0 && (ax == 0 || !alts[ax - 1].clash.Yes))
					throw new($"{p.name}.{pax} no previous clash");
				if (a.clash.x >= 0)
					a.clash = alts[ax - 1].clash.x >= 0 ? alts[ax - 1].clash : (Altx)(short)(ax - 1);
				// recover
				if (a.rec && ax == 0)
					throw new($"initial {p.name}.{pax} recovery");
				if (a.rec)
					if (a.Count > 0 && a[^1].K)
						recs.Add(keyOrd(a[^1].k));
					else
						throw new($"{p.name}.{pax} no final lexic key");
				// synt
				if (a.synt == 2 && (a.Count == 0 || a[0].K))
					throw new($"{p.name}.{pax} leftmost not name");
				if (a.synt == 3 && (a.Count == 0 || a[^1].K))
					throw new($"{p.name}.{pax} rightmost not name");
				alts[(a.index = (Altx)ax++).x] = a;
				if (np != p)
					np.Add(a); // append alterns to same name production
			}
		}
		if (prods[Name(accept).x].Count > 1)
			throw new($"multiple alterns of initial name {accept}");

		// keys
		if (keyOrd(default) != default)
			throw new($"default key ordinal {keyOrd(default)}");
		keyOs = [.. ks.Keys];
		keys = [.. ks.Values];
		if (recs.Count > 10)
			throw new($"recovery keys size {recs.Count}");
		recKs = [.. recs];
		// others
		foreach (var a in alts)
			for (var x = 0; x < a.Count; x++)
				a[x] = a[x].K ? new(Key(a[x].k, keyOs), a[x].k) : new(Name(a[x].n), a[x].n);
		firsts = new (bool, KordXs)[names.Length];
		firstAs = new (bool, KordXs)[alts.Length][];
	}
}
