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
	public partial class Alt : List<Conx, Con>
	{
		internal Altx index; // index of whole grammar alts
		public N name;
		public Conx lex = Conx.No; // save main lex at this index to synt.info, no save: -1
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
		public readonly bool K => kx.yes;

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
					a.lex = a.lex.yes ? throw new($"{p.name}.{p.Count - 1} lex {a.lex}")
						: a.size - 1;
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
	readonly Arr<Kordx, Kord> keyOs; // { key ordinal... }
	readonly Arr<Nordx, Nord> nameOs; // { name ordinal... }
	readonly Arr<Kordx, K> keys; // at key ordinal index
	readonly Arr<Nordx, N> names; // at name ordinal index
	private Kordx Key(K k) => keyOs.BinFind(keyOrd(k));
	private Nordx Name(N n) => nameOs.BinFind(nameOrd(n));

	readonly Arr<Nordx, SynGram<K, N>.Prod> prods;
	readonly Arr<Altx, SynGram<K, N>.Alt> alts;
	readonly Arr<Nordx, (bool empty, KordXs first)> firsts;
	readonly Arr<Altx, (bool empty, KordXs first)[]> firstAs; // at alt index and want
	readonly Arr<Krecx, Kord> recKs; // { recovery key ordinal ... }
	private Krecx KeyRec(K k) => recKs.BinFind(keyOrd(k));

	// phase 1: find first keys of each name and each alt want
	public void Firsts()
	{
		if (firstAs[default] != null)
			return;
		for (bool loop = true; !(loop = !loop);)
			foreach (var (p, nx) in prods.Each()) {
				var nf = firsts[nx].first.Use(keys.size);
				foreach (var a in p) {
					foreach (var c in a) {
						var (ce, cf) = c.K ? (false, KordXs.One(keys.size, c.kx))
							: firsts[c.nx];
						loop |= nf.Or(cf);
						if (!ce)
							goto A;
					}
					_ = firsts[nx].empty || (loop = firsts[nx].empty = true);
				A:;
				}
			}
		foreach (var (_, x) in firstAs.Each()) {
			var a = alts[x];
			var fs = firstAs[x] = new (bool, KordXs)[+a.size + 1];
			fs[^1].empty = true;
			for (var w = +a.size - 1; w > 0; w--)
				if (a[(Conx)w].K)
					fs[w] = (false, KordXs.One(keys.size, a[(Conx)w].kx));
				else {
					var (e, f) = firsts[a[(Conx)w].nx];
					fs[w] = !e ? (e, f) : (fs[w + 1].empty, f.NewOr(fs[w + 1].first, true));
				}
		}
	}
	public (bool empty, IEnumerable<K> first) First(N name)
	{
		var (e, f) = firsts[Name(name)];
		return (e, KeySet(f));
	}
	public IEnumerable<K> KeySet(KordXs s) => s.Select(kx => keys[kx]);

	class Items : List<(
		SynGram<K, N>.Alt alt,
		Conx want,
		KordXs heads, // lookaheads including eor
		int clo
		)>
	{
		internal (SynGram<K, N>.Alt alt, Conx want, KordXs heads, int clo) this[Conx x] => this[+x];
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
		internal Arr<Kordx, Clash> clashs; // at key ordinal index
		internal Arr<Kordx, Formx> goKs; // solved, at key ordinal index, error: -1
		internal Arr<Nordx, Formx> goNs; // form index, at name ordinal index, error: -1
	}

	readonly List<Form> forms = [];
	readonly Dictionary<(SynGram<K, N>.Alt a, Conx want), int> addItems = [];
	readonly List<int> addHeads = [];
	readonly Dictionary<object, Formx> addGos = [];

	int AddItem(Items Is, SynGram<K, N>.Alt alt, Conx want, KordXs heads1, KordXs heads2, int clo)
	{
		if (addItems.TryGetValue((alt, want), out var x))
			return (Is[x].heads.Or(heads1) | Is[x].heads.Or(heads2))
				&& firstAs[alt.index][+want + 1].empty ? x : -1;
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
			index = (Formx)forms.Count, Is = Is, clashs = new(keys.size),
			goNs = new(names.size), goKs = new(keys.size),
		};
		forms.Add(F);
		Array.Fill(F.goKs.S, Formx.No); Array.Fill(F.goNs.S, Formx.No);
		return F.index;
	}

	// make a whole items
	Items Closure(Items Is)
	{
		for (var x = 0; x < Is.Count + addHeads.Count; x++) {
			var y = x < Is.Count ? x : addHeads[x - Is.Count];
			var (a, want, heads, clo) = Is[y];
			if (want >= a.size || a[want].K)
				continue;
			var (e, f) = firstAs[a.index][+want + 1];
			foreach (var A in prods[a[want].nx])
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
			if (want >= a.size)
				continue;
			if (!addGos.TryGetValue(a[want], out var go)) {
				Items js = [];
				foreach (var (A, W, heads, _) in f.Is) {
					if (W < A.size && A[W].Equals(a[want]))
						AddItem(js, A, W + 1, heads, default, 1);
				}
				addGos.Add(a[want], go = AddForm(Closure(js)));
			}
			if (a[want].K) {
				f.clashs[a[want].kx].shift = go;
				f.clashs[a[want].kx].shifts.Use(alts.size).Or(a.index);
			}
			else
				f.goNs[a[want].nx] = go;
		}
		addGos.Clear();
	}

	// phase 2: make all transition forms
	public void Forms()
	{
		if (forms.Count > 0)
			return;
		Items init = [];
		AddItem(init, alts[default], (Conx)0, KordXs.One(keys.size, default), default, 1);
		AddForm(Closure(init));
		for (var x = 0; x < forms.Count; x++) {
			if (x >= 32767) throw new("too many forms");
			ShiftPush(forms[x]);
		}
		foreach (var f in forms)
			foreach (var (a, want, heads, _) in f.Is)
				if (want >= a.size) // alt could be reduced
					foreach (var head in heads)
						f.clashs[head].redus.Use(alts.size).Or(a.index);
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
					ok.keys.Or((Kordx)kx);
				}
				// try to solve
				else {
					var go = Formx.No;
					if (c.redus.All(a => alts[a].clash.yes)) {
						Altx A(Altx a) => alts[a].clash.zpos ? alts[a].clash : a;
						r = c.redus.Max(); var ra = A(r);
						if (c.shifts.Empty)
							go = SynForm.Redu(r); // reduce
						else {
							Altx ia = A(c.shifts.Max()), ic = alts[ia].clash;
							if (ic.yes)
								if (ia > ra || ia == ra && +ic <= -3)
									go = c.shift; // shift
								else
									go = SynForm.Redu(r); // reduce
						}
					}
					f.goKs[kx] = go;
					clashs[c] = (KordXs.One(keys.size, (Kordx)kx), go);
				}
		// solved
		var solvez = 0;
		if (!detail)
			foreach (var (c, (_, go)) in clashs)
				if (go.yes && clashs.Remove(c))
					solvez++;
		if (dump)
			Dump((clashs, detail, solvez));
		return clashs.Count > 0 ? clashs : null;
	}

	// phase all and final: make all data for synter
	public SynterNew<N> Make(out Dictionary<Clash, (KordXs keys, Formx go)> clashs)
	{
		Firsts();
		Forms();
		clashs = Clashs(false);
		if (clashs != null)
			throw new("can not make synter due to clashes");
		// synter alts
		Arr<Altx, SynAlt<N>> As = new(alts.size);
		foreach (var (a, ax) in alts.Each())
			As[ax] = new() {
				name = a.name, size = a.size, lex = a.lex,
#pragma warning disable CS8974 // Converting method group to non-delegate type
				synt = a.synt, label = a.label, dump = a.ToString,
				syntName = EqualityComparer<N>.Default.Equals(a.asName, default) ? a.name : a.asName,
				// final key index in recovery keys
				rec = a.rec ? KeyRec(a.last.k) : Krecx.No,
			};
		// synter forms
		Arr<Formx, SynForm> Fs = new((Formx)forms.Count);
		foreach (var f in forms) {
			var F = Fs[f.index] = new();
			F.goKs = OrdFormx<Kord>.Compact(f.goKs, keyOs);
			F.goNs = OrdFormx<Nord>.Compact(f.goNs, nameOs);
			Error(f, F);
		}
		return new(As, Fs, recKs);
	}

	public static int ErrZ = 2; // etc excluded
	public static string Err = "{0} wants {1}", ErrMore = " and\n", ErrEtc = " ...";
	public static string ErrEor = "want end of read";

	// make error info and recovery
	void Error(Form f, SynForm F)
	{
		bool accept = false;
		if (F.goKs.Full().Select(go => go.fx).Where(x => x < Formx.No).May().Max() is Formx other)
			if (other == SynForm.Redu((Altx)0))
				accept = true;
			else // for error and recovery, form with any reduce will always reduce on error keys
				F.other = other;

		// recover alts
		List<(Altx, Conx)> recs = [];
		foreach (var (a, want, _, _) in f.Is)
			if (a.rec && want > a.lex && want < a.size) // usually useless before main lex
				recs.Add((a.index, want));
		recs.Sort(); recs.Reverse(); // reduce the latest alt of same recovery key
		if (recs.Count > 0)
			F.recs = [.. recs];

		// error infos
		List<(bool label, int clo, Conx half, Altx ax, SynGram<K, N>.Con)> ws = [];
		foreach (var (a, want, _, c) in f.Is)
			if (want < a.size && want != a.lex) // main lex is usually for distinct alts of same name
				ws.Add((a.label != null, -c, want + want - a.size, a.index, a[want]));
		ws.Sort();
		// only few infos, reverse
		List<(string a, string b, (string pre, int clo, Conx half, Altx ax) c)> es = [];
		bool label = false, debug = Err.Contains("{2}");
		int cloMax = int.MinValue;
		for (int x = 1; x <= ws.Count && es.Count <= ErrZ; x++) {
			var (lab, clo, half, ax, con) = ws[^x];
			if (lab != (label |= lab)) continue;
			if (clo < (cloMax = int.Max(cloMax, clo))) continue;
			// label or name wants name or key, no duplicate
			var e = (alts[ax].label ?? prods[Name(alts[ax].name)].label ?? alts[ax].name.ToString(),
				con.K ? Dumper(con) : prods[con.nx].label ?? con.n.ToString(),
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
		if (nameOs[default] == default)
			throw new($"default name ordinal");
		var accept = gram.prods[0].name;

		SortedDictionary<Kord, K> ks = new() { [keyOrd(default)] = default };
		SortedSet<Kord> recs = [];
		// prods
		prods = new(names.size);
		foreach (var p in gram.prods)
			prods[Name(p.name)] ??= p;
		// alts
		alts = new((Altx)gram.prods.Sum(p => p.Count));
		if (+alts.size is 0 or > 32767)
			throw new($"total {alts.size} alterns");
		Altx ax = (Altx)0; Kord ko;
		foreach (var p in gram.prods) {
			var np = prods[Name(p.name)];
			foreach (var (a, pax) in p.Each()) {
				// content
				if (+a.size > 30)
					throw new($"{p.name}.{pax} size {a.size}");
				if (a.size.zero && ax.zero)
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
				if (a.clash.zpos && (ax.zero || alts[ax - 1].clash.no))
					throw new($"{p.name}.{pax} no previous clash");
				if (a.clash.zpos)
					a.clash = alts[ax - 1].clash.zpos ? alts[ax - 1].clash : ax - 1;
				// recover
				if (a.rec && ax.zero)
					throw new($"initial {p.name}.{pax} recovery");
				if (a.rec)
					if (a.size.pos && a.last.K)
						recs.Add(keyOrd(a.last.k));
					else
						throw new($"{p.name}.{pax} no final lexic key");
				// synt
				if (a.synt == 2 && (a.size.zero || a[default].K))
					throw new($"{p.name}.{pax} leftmost not name");
				if (a.synt == 3 && (a.size.zero || a.last.K))
					throw new($"{p.name}.{pax} rightmost not name");
				alts[a.index = ax] = a; ax += 1;
				if (np != p)
					np.Add(a); // append alterns to same name production
			}
		}
		if (prods[Name(accept)].Count > 1)
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
			foreach (var (c, x) in a.Each())
				a[x] = c.K ? new(Key(c.k), c.k) : new(Name(c.n), c.n);
		firsts = new(names.size);
		firstAs = new(alts.size);
	}
}
