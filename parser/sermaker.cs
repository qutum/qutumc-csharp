//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace qutum.parser;

using Kord = char;
using Nord = ushort;

// syntax grammar
public partial class SynGram<K, N>
{
	public readonly List<Prod> prods = [];
	public class Prod(N name, string label) : List<Alt>
	{
		public N name = name;
		public string label = label;
	}
	// { K or N ... }
	public partial class Alt : List<object>
	{
		internal short index; // index of whole grammar alts
		public N name;
		public short clash; // reject: 0, solve 1: 1, solve 2: 2
							// as same rule and index as previous one: ~actual alt index
		public short lex = -1; // save lex at this index to Synt.info, no save: <0
		public sbyte synt; // as Synter.tree: 0, make Synt: 1, omit Synt: -1
		public bool rec; // whether recover this alt for error, recover from saving lex if any
		public string label;
	}

	public SynGram<K, N> n(N name, string label = null) { prods.Add(new(name, label)); return this; }

	// K : lexic key, N : syntax name, .. : save last lex
	public SynGram<K, N> this[params object[] cons] {
		get {
			Alt a = new() { name = prods[^1].name, label = prods[^1].label };
			prods[^1].Add(a);
			foreach (var c in cons)
				if (c is Range)
					a.lex = a.lex < 0 ? (short)(a.Count - 1)
						: throw new($"{a.name}.{prods[^1].Count - 1} lex {a.lex}");
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
	public SynGram<K, N> syntOmit { get { prods[^1][^1].synt = -1; return this; } }
	public SynGram<K, N> recover { get { prods[^1][^1].rec = true; return this; } }
	public SynGram<K, N> label(string w) { prods[^1][^1].label = w.ToString(); return this; }
	public SynGram<K, N> labelLow { get { prods[^1][^1].label = null; return this; } }
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
		HashSet<K>> // lookaheads including eor
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
		internal short[] pushs; // push form index, at name ordinal index, no: -1
		internal short[] modes; // solved modes, at key ordinal index, error: -1
	}

	static bool AddItem(Items Is, SynGram<K, N>.Alt alt, short want, IEnumerable<K> heads)
	{
		if (Is.TryGetValue((alt, want), out var hs))
			return hs.Adds(heads);
		Is[(alt, want)] = [.. heads];
		return true;
	}
	short AddForm(Items Is)
	{
		foreach (var f in forms)
			if (f.Is.Count == Is.Count) {
				foreach (var (i, h) in Is)
					if (!f.Is.TryGetValue(i, out var fh) || !fh.SetEquals(h))
						goto No;
				return f.index; No:;
			}
		var F = new Form {
			index = (short)forms.Count, Is = Is, clashs = new Clash[keys.Length],
			pushs = new short[names.Length], modes = new short[keys.Length],
		};
		forms.Add(F);
		Array.Fill(F.modes, (short)-1);
		Array.Fill(F.pushs, (short)-1);
		return F.index;
	}

	// make a whole items
	Items Closure(Items Is)
	{
	Loop: foreach (var ((a, want), heads) in Is) {
			if (want >= a.Count || a[want] is not N name)
				continue;
			var (e, f) = want + 1 >= a.Count ? (false, heads)
						: a[want + 1] is K k ? (false, [k]) : First((N)a[want + 1]);
			var loop = false;
			foreach (var A in prods[Name(name)])
				loop |= AddItem(Is, A, 0, e && heads.Count > 0 ? [.. f, .. heads] : f);
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
				foreach (var ((A, W), heads) in f.Is) {
					if (W < A.Count && A[W].Equals(a[want]))
						AddItem(js, A, (short)(W + 1), heads);
				}
				tos.Add(a[want], to = AddForm(Closure(js)));
			}
			if (a[want] is K k) {
				f.clashs[Key(k)].shift = to;
				(f.clashs[Key(k)].shifts ??= []).Add(a.index);
			}
			else
				f.pushs[Name((N)a[want])] = to;
		}
	}

	// phase 2: make all transition forms
	public void Forms()
	{
		if (forms.Count > 0)
			return;
		Items init = new() { { (alts[0], 0), [default] } };
		AddForm(Closure(init));
		for (var x = 0; x < forms.Count; x++) {
			if (x >= 32767) throw new("too many forms");
			ShiftPush(forms[x]);
		}
		foreach (var f in forms)
			foreach (var ((a, want), heads) in f.Is)
				if (want >= a.Count) // alt could be reduced
					foreach (var head in heads)
						(f.clashs[Key(head)].redus ??= []).Add(a.index);
		if (dump)
			Dump(forms);
	}

	// phase 3: find clashes and solve them
	// solve 1: shift or reduce the latest index alt, reduce the same index (left associative)
	// solve 2: shift or reduce the latest index alt, shift the same index (right associative)
	// all clashing alts should be clashable
	public Dictionary<Clash, (HashSet<K> keys, short mode)> Clashs(bool detail = true)
	{
		Dictionary<Clash, (HashSet<K> keys, short mode)> clashs = [];
		foreach (var f in forms)
			foreach (var (c, kx) in f.clashs.Each())
				// shift without clash
				if (c.redus == null)
					f.modes[kx] = (short)(c.shifts == null ? -1 // error
										: c.shift);
				// reduce without clash
				else if (c.shifts == null && c.redus.Count == 1)
					f.modes[kx] = SynForm.Reduce(c.redus.First());
				// already solved
				else if (clashs.TryGetValue(c, out var ok)) {
					f.modes[kx] = ok.mode;
					ok.keys.Add(keys[kx]);
				}
				// solve
				else {
					short mode = -1;
					if (c.redus.All(a => alts[a].clash != 0)) {
						int A(int a) => alts[a].clash < 0 ? ~alts[a].clash : a;
						int r = c.redus.Max(), ra = A(r);
						if (c.shifts == null)
							mode = SynForm.Reduce(r); // reduce
						else {
							int ia = A(c.shifts.Max()), ic = alts[ia].clash;
							if (ic != 0)
								if (ia > ra || ia == ra && ic > 1)
									mode = c.shift; // shift
								else
									mode = SynForm.Reduce(r); // reduce
						}
					}
					f.modes[kx] = mode;
					clashs[c] = ([keys[kx]], mode);
				}
		// solved
		var solvez = 0;
		if (!detail)
			foreach (var (c, (_, mode)) in clashs)
				if (mode != -1 && clashs.Remove(c))
					solvez++;
		if (dump)
			Dump((clashs, detail, solvez));
		return clashs.Count > 0 ? clashs : null;
	}

	// phase all and final: make all data for synter
	public (SynAlt<N>[] alts, SynForm[] forms, Kord[] recKs)
		Make(out Dictionary<Clash, (HashSet<K> keys, short mode)> clashs)
	{
		Firsts();
		Forms();
		clashs = Clashs(false);
		if (clashs != null)
			return (null, null, null);
		// recovery key ordinals
		var recKs = alts.Where(a => a.rec).Select(a => keyOrd((K)a[^1])).Distinct().ToArray();
		if (recKs.Length > 100)
			throw new($"recovery keys size {recKs.Length}");
		Array.Sort(recKs);
		// synter alts
		var As = new SynAlt<N>[alts.Length];
		foreach (var (a, ax) in alts.Each())
			As[ax] = new() {
				name = a.name, size = checked((short)a.Count), lex = a.lex,
#pragma warning disable CS8974 // Converting method group to non-delegate type
				synt = a.synt, label = a.label, dump = a.ToString,
				// final key index in recovery keys
				rec = (sbyte)(a.rec ? Array.IndexOf(recKs, keyOrd((K)a[^1])) : 0),
			};
		// synter forms
		var Fs = new SynForm[forms.Count];
		foreach (var f in forms) {
			void Compact<O>(O[] os, short[] fs, ref SynForm.S<O> Fs) where O : IBinaryInteger<O>
			{ // TODO better compact
				int z;
				if (int.CreateTruncating(os[^1]) < compact
					|| (z = fs.Count(m => m != -1)) >= int.CreateTruncating(os[^1]) >>> 3)
					Fs.s = fs;
				else {
					(Fs.s, Fs.x) = (new short[z + 1], new O[z + 1]); Fs.s[0] = -1;
					for (int X = 1, x = 0; x < os.Length; x++)
						if (fs[x] != -1)
							(Fs.s[X], Fs.x[X++]) = (fs[x], os[x]);
				}
			}
			var F = Fs[f.index] = new SynForm();
			Compact(keyOs, f.modes, ref F.modes);
			Compact(nameOs, f.pushs, ref F.pushs);
			Error(f, F);
		}
		return (As, Fs, recKs);
	}

	public static int ErrZ = 2;
	public static string Err = "{0} wants {1}", ErrMore = " \nand ", ErrEtc = " ...";
	public static string ErrEor = "want end of read";

	// make error info and recovery
	void Error(Form f, SynForm F)
	{
		bool accept = false;
		if (F.modes.Redu().May().Max()?.d is short other)
			if (other == SynForm.Reduce(0))
				accept = true;
			else // for error and recovery, form with any reduce will always reduce on error keys
				foreach (var (m, kx) in F.modes.s.Each())
					if (m == -1) F.modes.s[kx] = other;

		// error infos
		List<(bool label, int half, int ax, object expect)> errNs = [], errKs = [], errs = [];
		foreach (var ((a, want), _) in f.Is)
			if (want < a.Count && want != a.lex) // saved lex is usually for distinct alts of same name
				(a[want] is N ? errNs : errKs).Add(
					(a.label != null, want + want - a.Count, a.index, a[want]));
		errNs.Sort(); errKs.Sort();
		// only few infos
		for (int z = -1, x = 1; z < (z = errs.Count); x++) {
			if (errs.Count <= ErrZ && x <= errNs.Count) errs.Add(errNs[^x]);
			if (errs.Count <= ErrZ && x <= errKs.Count) errs.Add(errKs[^x]);
		}
		StrMaker e = new();
		foreach (var ((label, half, ax, expect), x) in errs.Each())
			e += x == ErrZ ? ErrEtc : e - ErrMore + e.F(Err, // label or name wants name or key
				alts[ax].label ?? prods[Name(alts[ax].name)].label ?? alts[ax].name.ToString(),
				expect is N n ? prods[Name(n)].label ?? n.ToString() : CharSet.Unesc(expect),
				(expect is N ? "n" : "k", label ? "l" : "", half, ax)); // {2}
		if (e.Size > 0)
			F.err = e;
		else if (accept)
			F.err = ErrEor;

		// recover alts
		List<(short, short)> recs = [];
		foreach (var ((a, want), _) in f.Is)
			if (a.rec && want > a.lex && want < a.Count)
				recs.Add((a.index, want));
		recs.Sort(); recs.Reverse(); // reduce the latest alt of same recovery key

		//TODO remove this case
		//if (F.modes[default] == SynForm.Reduce(0) && F.modes.Yes().Count() == 1)
		//	recs.Add((0, (short)alts[0].Count)); // want eor only
		if (recs.Count > 0)
			F.recs = [.. recs];
	}

	// phase init: get grammar
	public SerMaker(SynGram<K, N> gram, Func<K, Kord> keyOrd, Func<N, Nord> nameOrd,
		Action<IEnumerable<K>> distinct, bool dump = true)
	{
		this.dump = dump; this.keyOrd = keyOrd; this.nameOrd = nameOrd;
		// names
		SortedDictionary<Nord, N> ns = [];
		foreach (var (p, px) in gram.prods.Each())
			if (!ns.TryAdd(nameOrd(p.name), p.name) && !ns[nameOrd(p.name)].Equals(p.name))
				throw new($"duplicate ordinal of {ns[nameOrd(p.name)]} and {p.name}");
		nameOs = [.. ns.Keys];
		names = [.. ns.Values];
		var accept = gram.prods[0].name;

		// prods
		prods = new SynGram<K, N>.Prod[names.Length];
		foreach (var p in gram.prods)
			prods[Name(p.name)] ??= p;
		// alts
		SortedDictionary<Kord, K> ks = new() { { keyOrd(default), default } };
		alts = new SynGram<K, N>.Alt[gram.prods.Sum(p => p.Count)];
		if (alts.Length is 0 or > 32767)
			throw new($"total {alts.Length} alterns");
		short ax = 0;
		foreach (var p in gram.prods) {
			var np = prods[Name(p.name)];
			foreach (var (a, pax) in p.Each()) {
				if (a.Count > 30)
					throw new($"{p.name}.{pax} size {a.Count}");
				if (a.Count == 0 && ax == 0)
					throw new($"initial {p.name}.{pax} emppty");
				foreach (var c in a)
					if (c is K k)
						ks[keyOrd(k)] = !k.Equals(default) ? k
							: throw new($"end of read in {p.name}.{pax}");
					else if (accept.Equals(c))
						throw new($"initial name {accept} in {p.name}.{pax} ");
					else if (Name((N)c) < 0)
						throw new($"name {c} in {p.name}.{pax} not found");
				if (a.clash != 0 && a.Count == 0)
					throw new($"{p.name}.{pax} clash but empty");
				if (a.clash < 0 && (ax == 0 || alts[ax - 1].clash == 0))
					throw new($"{p.name}.{pax} no previous clash");
				if (a.clash < 0)
					a.clash = alts[ax - 1].clash < 0 ? alts[ax - 1].clash : (short)~(ax - 1);
				if (a.lex >= 0 && a[a.lex] is not K)
					throw new($"{p.name}.{pax} content {a[a.lex]} not lexic key");
				if (a.rec && ax == 0)
					throw new($"initial {p.name}.{pax} recovery");
				if (a.rec && (a.Count == 0 || a[^1] is not K))
					throw new($"{p.name}.{pax} no final lexic key");
				alts[a.index = ax++] = a;
				if (np != p)
					np.Add(a); // append alterns to same name production
			}
		}
		if (prods[Name(accept)].Count > 1)
			throw new($"multiple alterns of initial name {accept}");

		// others
		if (keyOrd(default) != default) throw new($"default key ordinal {keyOrd(default)}");
		if (nameOs[0] == 0) throw new($"name ordinal 0");
		keyOs = [.. ks.Keys];
		keys = [.. ks.Values];
		distinct(keys);
		firsts = new (bool, HashSet<K>)[names.Length];
	}
}
