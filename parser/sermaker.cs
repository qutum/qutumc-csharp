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
	// { K or N ... }
	public partial class Alt : List<object>
	{
		internal short index; // index of whole grammar alts
		public N name;
		public short lex = -1; // save main lex at this index to synt.info, no save: <0
		public short clash; // reject: 0, left: 1, right: 2
							// solve and index as same as previous one: ~actual alt index
		public bool rec; // whether recover this alt for error, after main lex if any
		public sbyte synt; // make synt: as synter: 0, omit: -1, make: 1, lift left: 2, lift right: 3
		public string label;
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
						: throw new($"{a.name}.{p.Count - 1} lex {a.lex}");
				else if (c is N or K)
					a.Add(c);
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
	public bool dump = true;
	public int compact = 50;

	readonly Func<K, Kord> keyOrd; // ordinal from 1, default for eor: 0
	readonly Func<N, Nord> nameOrd; // ordinal from 1
	readonly Kord[] keyOs; // { key ordinal... }
	readonly Nord[] nameOs; // { name ordinal... }
	readonly K[] keys; // at key ordinal index
	readonly N[] names; // at name ordinal index
	readonly Kord[] recKs; // { recovery key ordinal ... }

	private int Key(K k) => Array.BinarySearch(keyOs, keyOrd(k));
	private int Name(N n) => Array.BinarySearch(nameOs, nameOrd(n));

	readonly SynGram<K, N>.Prod[] prods; // at name ordinal index
	readonly SynGram<K, N>.Alt[] alts;
	readonly (bool empty, HashSet<K> first)[] firsts; // at name ordinal index
	readonly List<Form> forms = [];

	// phase 1: find possible first key of each name
	public void Firsts()
	{
		if (firsts[0].first != null)
			return;
		for (bool loop = true; !(loop = !loop);)
			foreach (var (p, nx) in prods.Each()) {
				var nf = firsts[nx].first ??= [];
				foreach (var a in p) {
					foreach (var c in a) {
						var (cempty, cf) = c is K k ? (false, [k]) : First((N)c);
						loop |= nf.Adds(cf);
						if (!cempty)
							goto A;
					}
					_ = firsts[nx].empty || (loop = firsts[nx].empty = true); A:;
				}
			}
	}
	public (bool empty, IEnumerable<K> first) First(N name) => firsts[Name(name)];

	class Items : Dictionary<(SynGram<K, N>.Alt alt, short want),
		(HashSet<K> heads, int clo)> // lookaheads including eor
	{ }
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2231:Overload operator equals")]
	public struct Clash
	{
		internal short shift; // shift to form index if shifts not null
		public HashSet<short> redus; // alt index, no reduce: null
		public HashSet<short> shifts; // alt index, no shift: null
		public override readonly int GetHashCode() =>
			HashCode.Combine(shifts?.Count == 1 ? shifts.First() : shifts?.Count,
				redus?.Count == 1 ? redus.First() : redus.Count);
		public override readonly bool Equals(object o) => o is Clash c
			&& (redus?.SetEquals(c.redus) ?? c.redus == null)
			&& (shifts?.SetEquals(c.shifts) ?? c.shifts == null);
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

	static bool AddItem(Items Is, SynGram<K, N>.Alt alt, short want, IEnumerable<K> heads, int clo)
	{
		if (Is.TryGetValue((alt, want), out var hs))
			return hs.heads.Adds(heads);
		Is[(alt, want)] = ([.. heads], clo);
		return true;
	}
	short AddForm(Items Is)
	{
		foreach (var f in forms)
			if (f.Is.Count == Is.Count) {
				foreach (var (i, h) in Is)
					if (!f.Is.TryGetValue(i, out var fh) || !fh.heads.SetEquals(h.heads))
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
	Loop: foreach (var ((a, want), (heads, clo)) in Is) {
			if (want >= a.Count || a[want] is not N name)
				continue;
			bool empty = true; IEnumerable<K> h = null;
			for (var w = want + 1; empty; w++)
				if (w >= a.Count)
					(empty, h) = (false, h?.Concat(heads) ?? heads);
				else if (a[w] is K k)
					(empty, h) = (false, h?.Append(k) ?? [k]);
				else {
					(empty, var f) = First((N)a[w]);
					h = h?.Concat(f) ?? f;
				}
			var loop = false;
			foreach (var A in prods[Name(name)])
				loop |= AddItem(Is, A, 0, h, clo + 1);
			if (loop)
				goto Loop;
		}
		return Is;
	}

	// make shifting or pushing between forms
	void ShiftPush(Form f)
	{
		Dictionary<object, short> tos = [];
		foreach (var ((a, want), _) in f.Is) {
			if (want >= a.Count)
				continue;
			if (!tos.TryGetValue(a[want], out var to)) {
				Items js = [];
				foreach (var ((A, W), (heads, _)) in f.Is) {
					if (W < A.Count && A[W].Equals(a[want]))
						AddItem(js, A, (short)(W + 1), heads, 0);
				}
				tos.Add(a[want], to = AddForm(Closure(js)));
			}
			if (a[want] is K k) {
				f.clashs[Key(k)].shift = to;
				(f.clashs[Key(k)].shifts ??= []).Add(a.index);
			}
			else
				f.goNs[Name((N)a[want])] = to;
		}
	}

	// phase 2: make all transition forms
	public void Forms()
	{
		if (forms.Count > 0)
			return;
		Items init = new() { [(alts[0], 0)] = ([default], 0) };
		AddForm(Closure(init));
		for (var x = 0; x < forms.Count; x++) {
			if (x >= 32767) throw new("too many forms");
			ShiftPush(forms[x]);
		}
		foreach (var f in forms)
			foreach (var ((a, want), (heads, _)) in f.Is)
				if (want >= a.Count) // alt could be reduced
					foreach (var head in heads)
						(f.clashs[Key(head)].redus ??= []).Add(a.index);
		if (dump)
			Dump(forms);
	}

	// phase 3: find clashes and solve them
	// left: shift or reduce the latest index alt, reduce the same index (left associative)
	// right: shift or reduce the latest index alt, shift the same index (right associative)
	// all clashing alts not be rejected
	public Dictionary<Clash, (HashSet<K> keys, short go)> Clashs(bool detail = true)
	{
		Dictionary<Clash, (HashSet<K> keys, short go)> clashs = [];
		foreach (var f in forms)
			foreach (var (c, kx) in f.clashs.Each())
				// shift without clash
				if (c.redus == null)
					f.goKs[kx] = c.shifts == null ? No : c.shift;
				// reduce without clash
				else if (c.shifts == null && c.redus.Count == 1)
					f.goKs[kx] = SynForm.Redu(c.redus.First());
				// already solved
				else if (clashs.TryGetValue(c, out var ok)) {
					f.goKs[kx] = ok.go;
					ok.keys.Add(keys[kx]);
				}
				// try to solve
				else {
					short go = No;
					if (c.redus.All(a => alts[a].clash != 0)) {
						int A(int a) => alts[a].clash < 0 ? ~alts[a].clash : a;
						int r = c.redus.Max(), ra = A(r);
						if (c.shifts == null)
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
					clashs[c] = ([keys[kx]], go);
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
		Make(out Dictionary<Clash, (HashSet<K> keys, short go)> clashs)
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
				// final key index in recovery keys
				rec = (sbyte)(a.rec ? Array.BinarySearch(recKs, keyOrd((K)a[^1])) : 0),
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
		foreach (var ((a, want), _) in f.Is)
			if (a.rec && want > a.lex && want < a.Count) // usually useless before main lex
				recs.Add((a.index, want));
		recs.Sort(); recs.Reverse(); // reduce the latest alt of same recovery key
		if (recs.Count > 0)
			F.recs = [.. recs];

		// error infos
		List<(bool label, int clo, int half, int ax, bool n, object need)> ws = [];
		foreach (var ((a, want), (_, c)) in f.Is)
			if (want < a.Count && want != a.lex) // main lex is usually for distinct alts of same name
				ws.Add((a.label != null, -c, want + want - a.Count, a.index, a[want] is N, a[want]));
		ws.Sort();
		// only few infos, reverse
		List<(string a, string b, (string, int, int, int) c)> es = [];
		bool label = false, debug = Err.Contains("{2}");
		int closu = int.MinValue;
		for (int x = 1; x <= ws.Count && es.Count <= ErrZ; x++) {
			var (lab, clo, half, ax, _, need) = ws[^x];
			if (lab != (label |= lab)) continue;
			if (clo < (closu = Math.Max(closu, clo))) continue;
			// label or name wants name or key, no duplicate
			var e = (alts[ax].label ?? prods[Name(alts[ax].name)].label ?? alts[ax].name.ToString(),
				need is N n ? prods[Name(n)].label ?? n.ToString() : Dumper(need),
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
		if (nameOs[0] == 0)
			throw new($"name ordinal 0");
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
					if (c is not K k) {
						if (accept.Equals(c))
							throw new($"initial name {accept} in {p.name}.{pax} ");
						else if (Name((N)c) < 0)
							throw new($"name {c} in {p.name}.{pax} not found");
					}
					else if ((ko = keyOrd(k)) == default)
						throw new($"end of read in {p.name}.{pax}");
					else if (!ks.TryGetValue(ko, out var kk))
						ks[ko] = k;
					else if (!k.Equals(kk))
						throw new($"{p.name}.{pax} lexic key {k} confused with {kk}");
				// clash
				if (a.clash < 0 && (ax == 0 || alts[ax - 1].clash == 0))
					throw new($"{p.name}.{pax} no previous clash");
				if (a.clash < 0)
					a.clash = alts[ax - 1].clash < 0 ? alts[ax - 1].clash : (short)~(ax - 1);
				// recover
				if (a.rec && ax == 0)
					throw new($"initial {p.name}.{pax} recovery");
				if (a.rec)
					if (a.Count > 0 && a[^1] is K k)
						recs.Add(keyOrd(k));
					else
						throw new($"{p.name}.{pax} no final lexic key");
				// synt
				if (a.synt == 2 && (a.Count == 0 || a[0] is not N))
					throw new($"{p.name}.{pax} leftmost not name");
				if (a.synt == 3 && (a.Count == 0 || a[^1] is not N))
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
		firsts = new (bool, HashSet<K>)[names.Length];
	}
}
