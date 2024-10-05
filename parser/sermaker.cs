//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
namespace qutum.parser;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Kord = char;
using Nord = ushort;

// syntax grammar
public partial class SynGram<K, N>
{
	public readonly List<Prod> prods = [];
	public class Prod(N name, string label, bool labelYes) : List<Alt>
	{
		public N name = name;
		public string label = label;
		public bool labelYes = labelYes;
	}
	public partial class Alt : List<Con>
	{
		internal short index; // index of whole grammar alts
		public N name;
		public short lex = -1; // save main lex at this index to synt.info, no save: <0
		public short clash; // reject: 0, left: 1, right: 2
							// solve and index as same as previous one: ~actual alt index
		public bool rec; // whether recover this alt for error, after main lex if any
		public sbyte synt; // make synt: as synter: 0, omit: -1, make: 1, lift left: 2, lift right: 3
		public N syntN; // synt name: not default, same as name: default
		public string label;
	}
	public readonly struct Con
	{
		public readonly int nx; // key: ~ordinal index, name: ordinal index
		public readonly K k;
		public readonly N n;

		public Con(int x, K k) => (nx, this.k) = (~x, k);
		public Con(int x, N n) => (nx, this.n) = (x, n);
		public readonly bool K => nx < 0;
		public readonly int kx => ~nx;
	}

	public SynGram<K, N> n(N name, string label = null, bool labelYes = true)
	{
		prods.Add(new(name, label, labelYes)); return this;
	}

	// K : lexic key, N : syntax name, .. : last is main lex
	public SynGram<K, N> this[params object[] cons] {
		get {
			var p = prods[^1];
			Alt a = new() { name = p.name, label = p.labelYes ? p.label : null };
			p.Add(a);
			foreach (var c in cons)
				if (c is Range)
					a.lex = a.lex < 0 ? (short)(a.Count - 1)
						: throw new($"{p.name}.{p.Count - 1} lex {a.lex}");
				else if (c is K k)
					a.Add(new(0, k));
				else if (c is N n)
					a.Add(new(0, n));
				else
					throw new($"wrong altern content {c?.GetType()}");
			return this;
		}
	}
	public SynGram<K, N> clash { get { prods[^1][^1].clash = 1; return this; } }
	public SynGram<K, N> clashRight { get { prods[^1][^1].clash = 2; return this; } }
	public SynGram<K, N> clashPrev { get { prods[^1][^1].clash = -1; return this; } }
	public SynGram<K, N> synt { get { prods[^1][^1].synt = 1; return this; } }
	public SynGram<K, N> syntLeft { get { prods[^1][^1].synt = 2; return this; } }
	public SynGram<K, N> syntRight { get { prods[^1][^1].synt = 3; return this; } }
	public SynGram<K, N> syntOmit { get { prods[^1][^1].synt = -1; return this; } }
	public SynGram<K, N> syntName(N n) { prods[^1][^1].syntN = n; return this; }
	public SynGram<K, N> recover { get { prods[^1][^1].rec = true; return this; } }
	public SynGram<K, N> label(string w) { prods[^1][^1].label = w.ToString(); return this; }
	public SynGram<K, N> labelNo { get { prods[^1][^1].label = null; return this; } }
	public SynGram<K, N> labelYes { get { prods[^1][^1].label = prods[^1].label; return this; } }
	// for sourcecode format
	public SynGram<K, N> _ { get => this; }
	public SynGram<K, N> __ { get => this; }
	public SynGram<K, N> ___ { get => this; }
	public SynGram<K, N> ____ { get => this; }
}

// syntax parser maker
// K for lexical key i.e lexeme
// N for syntax name i.e synteme
public partial class SerMaker<K, N>
{
	public int compact = 50;

	readonly Func<K, Kord> keyOrd; // ordinal from 1, default for eor: 0
	readonly Func<N, Nord> nameOrd; // ordinal from 1
	readonly Kord[] keyOs; // { key ordinal... }
	readonly Nord[] nameOs; // { name ordinal... }
	readonly K[] keys; // at key ordinal index
	readonly N[] names; // at name ordinal index
	readonly Kord[] recKs; // { recovery key ordinal ... }

	private int Key(K k, Kord[] os) => Array.BinarySearch(os, keyOrd(k));
	private int Name(N n) => Array.BinarySearch(nameOs, nameOrd(n));

	readonly SynGram<K, N>.Prod[] prods; // at name ordinal index
	readonly SynGram<K, N>.Alt[] alts;
	readonly (bool empty, BitSet first)[] firsts; // at name ordinal index
	readonly (bool empty, BitSet first)[][] firstAs; // at alt index and want

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
						var (ce, cf) = c.K ? (false, BitSet.One(keys.Length, c.kx))
							: firsts[c.nx];
						loop |= nf.Or(cf);
						if (!ce)
							goto A;
					}
					_ = firsts[nx].empty || (loop = firsts[nx].empty = true); A:;
				}
			}
		for (var x = 0; x < firstAs.Length; x++) {
			var a = alts[x];
			var fs = firstAs[x] = new (bool, BitSet)[a.Count + 1];
			fs[^1].empty = true;
			for (var w = a.Count - 1; w > 0; w--)
				if (a[w].K)
					fs[w] = (false, BitSet.One(keys.Length, a[w].kx));
				else {
					var (e, f) = firsts[a[w].nx];
					fs[w] = !e ? (e, f) : (fs[w + 1].empty, f.NewOr(fs[w + 1].first, true));
				}
		}
	}
	public (bool empty, IEnumerable<K> first) First(N name)
	{
		var (e, f) = firsts[Name(name)];
		return (e, KeySet(f));
	}
	public IEnumerable<K> KeySet(BitSet s) => s.Select(kx => keys[kx]);

	class Items : List<(
		SynGram<K, N>.Alt alt,
		short want,
		BitSet heads, // lookaheads including eor
		int clo
		)>
	{
	}
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2231:Overload operator equals")]
	public struct Clash
	{
		internal short shift; // shift to form index if shifts not null
		public BitSet redus; // alt index, no reduce: null
		public BitSet shifts; // alt index, no shift: null
		public override readonly int GetHashCode() => HashCode.Combine(
			shifts.bits?[0] ?? 0, shifts.bits?[^1] ?? 0,
			redus.bits?[0] ?? 0, redus.bits?[^1] ?? 0);
		public override readonly bool Equals(object o) =>
			o is Clash c && redus.Same(c.redus) && shifts.Same(c.shifts);
	}
	struct Form
	{
		internal short index;
		internal Items Is;
		internal Clash[] clashs; // at key ordinal index
		internal short[] goKs; // solved, at key ordinal index, error: -1
		internal short[] goNs; // form index, at name ordinal index, error: -1
	}
	const short No = -1;

	readonly List<Form> forms = [];
	readonly Dictionary<(SynGram<K, N>.Alt, short), int> addItems = [];
	readonly List<int> addHeads = [];
	readonly Dictionary<object, short> addGos = [];

	int AddItem(Items Is, SynGram<K, N>.Alt alt, short want, BitSet heads1, BitSet heads2, int clo)
	{
		if (addItems.TryGetValue((alt, want), out var x))
			return (Is[x].heads.Or(heads1) | Is[x].heads.Or(heads2))
				&& firstAs[alt.index][want + 1].empty ? x : -1;
		addItems[(alt, want)] = Is.Count;
		Is.Add((alt, want, heads1.NewOr(heads2), clo));
		return -1;
	}
	short AddForm(Items Is)
	{
		foreach (var f in forms)
			if (f.Is.Count == Is.Count) {
				foreach (var (fi, i) in f.Is.Zip(Is))
					if (fi.alt != i.alt || fi.want != i.want || !fi.heads.Same(i.heads))
						goto No;
				return f.index; No:;
			}
		var F = new Form {
			index = (short)forms.Count, Is = Is, clashs = new Clash[keys.Length],
			goNs = new short[names.Length], goKs = new short[keys.Length],
		};
		forms.Add(F);
		Array.Fill(F.goKs, No); Array.Fill(F.goNs, No);
		return F.index;
	}

	// make a whole items
	Items Closure(Items Is)
	{
		for (var x = 0; x < Is.Count + addHeads.Count; x++) {
			var y = x < Is.Count ? x : addHeads[x - Is.Count];
			var (a, want, heads, clo) = Is[y];
			if (want >= a.Count || a[want].K)
				continue;
			var (e, f) = firstAs[a.index][want + 1];
			foreach (var A in prods[a[want].nx])
				if ((y = AddItem(Is, A, 0, f, e ? heads : default, clo + 1)) >= 0)
					addHeads.Add(y);
		}
		addItems.Clear(); addHeads.Clear();
		return Is;
	}

	// make shifting or pushing between forms
	void ShiftPush(Form f)
	{
		foreach (var (a, want, _, _) in f.Is) {
			if (want >= a.Count)
				continue;
			if (!addGos.TryGetValue(a[want], out var go)) {
				Items js = [];
				foreach (var (A, W, heads, _) in f.Is) {
					if (W < A.Count && A[W].Equals(a[want]))
						AddItem(js, A, (short)(W + 1), heads, default, 1);
				}
				addGos.Add(a[want], go = AddForm(Closure(js)));
			}
			if (a[want].K) {
				f.clashs[a[want].kx].shift = go;
				f.clashs[a[want].kx].shifts.Use(alts.Length).Or(a.index);
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
		AddItem(init, alts[0], 0, BitSet.One(keys.Length, default), default, 1);
		AddForm(Closure(init));
		for (var x = 0; x < forms.Count; x++) {
			if (x >= 32767) throw new("too many forms");
			ShiftPush(forms[x]);
		}
		foreach (var f in forms)
			foreach (var (a, want, heads, _) in f.Is)
				if (want >= a.Count) // alt could be reduced
					foreach (var head in heads)
						f.clashs[head].redus.Use(alts.Length).Or(a.index);
		if (dump)
			Dump(forms);
	}

	// phase 3: find clashes and solve them
	// left: shift or reduce the latest index alt, reduce the same index (left associative)
	// right: shift or reduce the latest index alt, shift the same index (right associative)
	// all clashing alts not be rejected
	public Dictionary<Clash, (BitSet keys, short go)> Clashs(bool detail = true)
	{
		Dictionary<Clash, (BitSet keys, short go)> clashs = [];
		foreach (var f in forms)
			foreach (var (c, kx) in f.clashs.Each())
				// shift without clash
				if (c.redus.size == 0)
					f.goKs[kx] = c.shifts.size == 0 ? No : c.shift;
				// reduce without clash
				else if (c.shifts.size == 0 && c.redus.Max() is int r && r == c.redus.Min()) // one redu
					f.goKs[kx] = SynForm.Redu(r);
				// already solved
				else if (clashs.TryGetValue(c, out var ok)) {
					f.goKs[kx] = ok.go;
					ok.keys.Or(kx);
				}
				// try to solve
				else {
					short go = No;
					if (c.redus.All(a => alts[a].clash != 0)) {
						int A(int a) => alts[a].clash < 0 ? ~alts[a].clash : a;
						r = c.redus.Max(); int ra = A(r);
						if (c.shifts.size == 0)
							go = SynForm.Redu(r); // reduce
						else {
							int ia = A(c.shifts.Max()), ic = alts[ia].clash;
							if (ic != 0)
								if (ia > ra || ia == ra && ic > 1)
									go = c.shift; // shift
								else
									go = SynForm.Redu(r); // reduce
						}
					}
					f.goKs[kx] = go;
					clashs[c] = (BitSet.One(keys.Length, kx), go);
				}
		// solved
		var solvez = 0;
		if (!detail)
			foreach (var (c, (_, go)) in clashs)
				if (go != No && clashs.Remove(c))
					solvez++;
		if (dump)
			Dump((clashs, detail, solvez));
		return clashs.Count > 0 ? clashs : null;
	}

	// phase all and final: make all data for synter
	public (SynAlt<N>[] alts, SynForm[] forms, Kord[] recKs)
		Make(out Dictionary<Clash, (BitSet keys, short go)> clashs)
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
				name = a.name, size = checked((short)a.Count), lex = a.lex,
#pragma warning disable CS8974 // Converting method group to non-delegate type
				synt = a.synt, label = a.label, dump = a.ToString,
				syntN = EqualityComparer<N>.Default.Equals(a.syntN, default) ? a.name : a.syntN,
				// final key index in recovery keys
				rec = (sbyte)(a.rec ? Key(a[^1].k, recKs) : 0),
			};
		// synter forms
		var Fs = new SynForm[forms.Count];
		foreach (var f in forms) {
			void Compact<O>(O[] os, short[] gs, ref SynForm.S<O> Gs) where O : IBinaryInteger<O>
			{ // TODO better compact
				int z = int.CreateTruncating(os[^1]), gz;
				if (z < compact || (gz = gs.Count(m => m != No)) >= z >>> 3) {
					Gs.s = new short[z + 1];
					Array.Fill(Gs.s, No);
					foreach (var (g, x) in gs.Each())
						Gs.s[int.CreateTruncating(os[x])] = g;
				}
				else {
					(Gs.s, Gs.x) = (new short[gz], new O[gz]);
					for (int X = 0, x = 0; x < os.Length; x++)
						if (gs[x] != No)
							(Gs.s[X], Gs.x[X++]) = (gs[x], os[x]);
				}
			}
			var F = Fs[f.index] = new();
			Compact(keyOs, f.goKs, ref F.goKs);
			Compact(nameOs, f.goNs, ref F.goNs);
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
		if (F.goKs.Redu().May().Max()?.d is short other)
			if (other == SynForm.Redu(0))
				accept = true;
			else // for error and recovery, form with any reduce will always reduce on error keys
				F.other = other;

		// recover alts
		List<(short, short)> recs = [];
		foreach (var (a, want, _, _) in f.Is)
			if (a.rec && want > a.lex && want < a.Count) // usually useless before main lex
				recs.Add((a.index, want));
		recs.Sort(); recs.Reverse(); // reduce the latest alt of same recovery key
		if (recs.Count > 0)
			F.recs = [.. recs];

		// error infos
		List<(bool label, int clo, int half, int ax, SynGram<K, N>.Con)> ws = [];
		foreach (var (a, want, _, c) in f.Is)
			if (want < a.Count && want != a.lex) // main lex is usually for distinct alts of same name
				ws.Add((a.label != null, -c, want + want - a.Count, a.index, a[want]));
		ws.Sort();
		// only few infos, reverse
		List<(string a, string b, (string, int, int, int) c)> es = [];
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
		if (nameOs[0] == default)
			throw new($"default name ordinal");
		var accept = gram.prods[0].name;

		SortedDictionary<Kord, K> ks = new() { [keyOrd(default)] = default };
		SortedSet<Kord> recs = [];
		// prods
		prods = new SynGram<K, N>.Prod[names.Length];
		foreach (var p in gram.prods)
			prods[Name(p.name)] ??= p;
		// alts
		alts = new SynGram<K, N>.Alt[gram.prods.Sum(p => p.Count)];
		if (alts.Length is 0 or > 32767)
			throw new($"total {alts.Length} alterns");
		short ax = 0; Kord ko;
		foreach (var p in gram.prods) {
			var np = prods[Name(p.name)];
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
					else if (Name(c.n) < 0)
						throw new($"name {c} in {p.name}.{pax} not found");
				// clash
				if (a.clash < 0 && (ax == 0 || alts[ax - 1].clash == 0))
					throw new($"{p.name}.{pax} no previous clash");
				if (a.clash < 0)
					a.clash = alts[ax - 1].clash < 0 ? alts[ax - 1].clash : (short)~(ax - 1);
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
				alts[a.index = ax++] = a;
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
			for (var x = 0; x < a.Count; x++)
				a[x] = a[x].K ? new(Key(a[x].k, keyOs), a[x].k) : new(Name(a[x].n), a[x].n);
		firsts = new (bool, BitSet)[names.Length];
		firstAs = new (bool, BitSet)[alts.Length][];
	}
}
