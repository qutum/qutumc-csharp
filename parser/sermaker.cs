//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace qutum.parser;

// syntax grammar
public class SynGram<K, N>
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Alt> { public N name; }
	// { K or N ... }
	public class Alt : List<object>
	{
		public N name;
		public int clash; // reject: 0, solve 1: 1, solve 2: 2
		public short lex = -1; // save lex at this index to Synt.info, no save: <0
		public sbyte synt; // as Synter.tree: 0, make Synt: 1, omit Synt: -1
		public bool rec;
		public string hint;
		internal short alt; // alt index of whole grammar

		public override string ToString() => $"{name} = {string.Join(' ', this)}  {(
			clash == 0 ? "" : clash == 1 ? "<" : ">")}{(rec ? "!!" : "")}{(
			synt > 0 ? "+" : synt < 0 ? "-" : "")}{(lex >= 0 ? "_" + lex : "")} {hint}";
	}

	public SynGram<K, N> n(N name) { prods.Add(new() { name = name }); return this; }
	// K : lexic key, N : syntax name, .. : save last lex
	public SynGram<K, N> this[params object[] cons] {
		get {
			Alt a = [];
			a.name = prods[^1].name;
			prods[^1].Add(a);
			foreach (var c in cons)
				if (c is Range) a.lex = (short)(a.Count - 1);
				else if (c is N or K) a.Add(c);
				else throw new($"wrong altern content {c?.GetType()}");
			return this;
		}
	}
	public SynGram<K, N> clash { get { prods[^1][^1].clash = 1; return this; } }
	public SynGram<K, N> clashRight { get { prods[^1][^1].clash = 2; return this; } }
	public SynGram<K, N> synt { get { prods[^1][^1].synt = 1; return this; } }
	public SynGram<K, N> syntOmit { get { prods[^1][^1].synt = -1; return this; } }
	public SynGram<K, N> hint(string w) { prods[^1][^1].hint = w != "" ? w : null; return this; }
}

// syntax parser maker
public class SerMaker<K, N>
{
	readonly Func<K, ushort> keyOrd; // ordinal from 1, default for eor: 0
	readonly Func<N, ushort> nameOrd; // ordinal from 1
	readonly ushort[] keyOs, nameOs; // { ordinal... }
	readonly K[] keys; // at key ordinal index
	readonly N[] names; // at name ordinal index

	private int Key(K k) => Array.BinarySearch(keyOs, keyOrd(k));
	private int Name(N n) => Array.BinarySearch(nameOs, nameOrd(n));

	readonly int accept; // at name ordinal index
	readonly SynGram<K, N>.Prod[] prods; // at name ordinal index
	public readonly SynGram<K, N>.Alt[] alts;
	readonly (bool empty, HashSet<K> first)[] firsts; // at name ordinal index

	public void Firsts()
	{
		if (firsts[0].first != null)
			return;
		for (bool loop = true; !(loop = !loop);)
			foreach (var (p, px) in prods.Each()) {
				var nf = firsts[px].first ??= [];
				foreach (var a in p) {
					foreach (var c in a) {
						var (cempty, cf) = c is K k ? (false, [k]) : First((N)c);
						loop |= nf.Adds(cf);
						if (!cempty)
							goto A;
					}
					_ = firsts[px].empty || (loop = firsts[px].empty = true); A:;
				}
			}
	}
	public (bool empty, IEnumerable<K> first) First(N name) => firsts[Name(name)];

	class Items : Dictionary<(SynGram<K, N>.Alt alt, short next),
		HashSet<K>> // lookaheads, eor excluded
	{ }
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2231:Overload operator equals")]
	public struct Clash
	{
		public HashSet<short> redus; // alt index, no reduce: null
		public short? shift; // alt index, no shift: null
		public override readonly int GetHashCode() => HashCode.Combine(shift, redus?.Count);
		public override readonly bool Equals(object o) => o is Clash c && shift == c.shift
				&& (redus?.SetEquals(c.redus) ?? c.redus == null);
	}
	struct Form
	{
		internal Items Is;
		internal (int shift, Clash c)[] clashs; // shift: form index if c.shift not null
												// reduce: c.redus otherwise, at key ordinal index
		internal int?[] pushs; // push form index, at name ordinal index
		internal short[] modes; // solved modes, at key ordinal index
	}

	readonly List<Form> forms = [];
	readonly SynAlt<N>[] Alts;

	static bool AddItem(Items Is, SynGram<K, N>.Alt alt, int next, IEnumerable<K> heads)
	{
		if (Is.TryGetValue((alt, (short)next), out var hs))
			return hs.Adds(heads);
		Is[(alt, (short)next)] = [.. heads];
		return true;
	}
	int AddForm(Items Is)
	{
		Debug.Assert(Is.Count > 0);
		foreach (var (f, x) in forms.Each())
			if (f.Is.Count == Is.Count) {
				foreach (var (i, h) in Is)
					if (!f.Is.TryGetValue(i, out var fh) || !fh.SetEquals(h))
						goto No;
				return x; No:;
			}
		forms.Add(new() {
			Is = Is, clashs = new (int, Clash)[keys.Length], pushs = new int?[names.Length],
			modes = new short[keys.Length]
		});
		Array.Fill(forms[^1].modes, (short)-1);
		return forms.Count - 1;
	}

	Items Closure(Items Is)
	{
	Loop: foreach (var ((a, next), heads) in Is) {
			if (next >= a.Count || a[next] is not N name)
				continue;
			var (e, f) = next + 1 >= a.Count ? (false, heads)
						: a[next + 1] is K k ? (false, [k]) : First((N)a[next + 1]);
			var loop = false;
			foreach (var A in prods[Name(name)])
				loop |= AddItem(Is, A, 0, e && heads.Count > 0 ? [.. f, .. heads] : f);
			if (loop)
				goto Loop;
		}
		return Is;
	}

	void ShiftPush(Form f)
	{
		foreach (var ((a, next), _) in f.Is) {
			if (next >= a.Count)
				continue;
			Items js = [];
			foreach (var ((A, Next), heads) in f.Is) {
				if (Next < A.Count && A[Next].Equals(a[next]))
					AddItem(js, A, Next + 1, heads);
			}
			var to = AddForm(Closure(js));
			if (a[next] is K k)
				f.clashs[Key(k)] = (to, new() { shift = a.alt });
			else
				f.pushs[Name((N)a[next])] = to;
		}
	}

	void Reduce(Form f)
	{
		foreach (var ((a, next), heads) in f.Is)
			if (next >= a.Count)
				foreach (var head in heads.Append(default)) // lookaheads and eor
					(f.clashs[Key(head)].c.redus ??= []).Add(a.alt);
	}

	public void Forms()
	{
		if (forms.Count > 0)
			return;
		Items init = [];
		foreach (var a in prods[accept])
			init[(a, 0)] = [];
		AddForm(Closure(init));
		for (var x = 0; x < forms.Count; x++) {
			if (x >= 32767) throw new("too many forms");
			ShiftPush(forms[x]);
		}
		foreach (var f in forms)
			Reduce(f);
	}

	// solve 1: shift or reduce the latest alt, rerduce the same alt (left associative)
	// solve 2: shift or reduce the latest alt, shift the same alt (right associative)
	public Dictionary<Clash, (HashSet<K> keys, short mode)> Clashs(bool detail = true, bool dump = true)
	{
		Dictionary<Clash, (HashSet<K> keys, short mode)> clashs = [];
		foreach (var f in forms)
			foreach (var ((shift, c), kx) in f.clashs.Each())
				if (c.redus?.Count > (c.shift != null ? 0 : 1)) {
					if (clashs.TryGetValue(c, out var ok)) {
						// already solved
						f.modes[kx] = ok.mode;
						ok.keys.Add(keys[kx]);
					}
					else { // solve
						short mode = -1;
						if (c.redus.All(r => alts[r].clash > 0)) {
							var r = c.redus.Max();
							if (c.shift is not short i)
								mode = SynForm.Reduce(r); // reduce
							else if (alts[i].clash > 0)
								if (i > r || i == r && alts[i].clash > 1)
									mode = (short)shift; // shift
								else
									mode = SynForm.Reduce(r); // reduce
						}
						f.modes[kx] = mode;
						clashs[c] = ([keys[kx]], mode);
					}
				}
		if (!detail)
			foreach (var (ok, _) in clashs.Where(c => c.Value.mode != -1))
				clashs.Remove(ok);
		if (dump) {
			using var env = EnvWriter.Begin();
			env.WriteLine("clashes and solutions:");
			foreach (var ((c, (keys, mode)), cx) in clashs.Each(1)) {
				env.Write(cx + (mode == -1 ? "  : " : " :: "));
				env.WriteLine(string.Join(' ', keys.Select(k => CharSet.Unesc(k))));
				using var _ = EnvWriter.Indent("\t\t");
				if (c.shift is short i)
					env.WriteLine($"{(mode >= 0 ? "SHIFT" : "shift")} {i}  {Alts[i]}");
				foreach (var r in c.redus)
					env.WriteLine($"{(r == SynForm.Reduce(mode) ? "REDUCE" : "reduce")} {r}  {Alts[r]}");
			}
		}
		return clashs.Count > 0 ? clashs : null;
	}

	public void Make(bool dumpClash = true)
	{
		Firsts();
		Forms();
		Clashs(dumpClash);
	}

	public SerMaker(SynGram<K, N> gram, Func<K, ushort> keyOrd, Func<N, ushort> nameOrd,
		Action<IEnumerable<K>> distinct)
	{
		this.keyOrd = keyOrd; this.nameOrd = nameOrd;
		// names
		SortedDictionary<ushort, N> ns = [];
		foreach (var (p, px) in gram.prods.Each())
			ns[nameOrd(p.name)] = p.name;
		nameOs = [.. ns.Keys];
		names = [.. ns.Values];
		accept = Name(gram.prods[0].name);

		// prods
		prods = new SynGram<K, N>.Prod[names.Length];
		foreach (var p in gram.prods)
			prods[Name(p.name)] ??= p;
		// alts
		SortedDictionary<ushort, K> ks = new() { { keyOrd(default), default } };
		alts = new SynGram<K, N>.Alt[gram.prods.Sum(p => p.Count)];
		if (alts.Length is 0 or > 32767) throw new($"alterns size {alts.Length}");
		Alts = new SynAlt<N>[alts.Length];
		short alt = 0;
		foreach (var p in gram.prods) {
			var np = prods[Name(p.name)];
			foreach (var (a, ax) in p.Each()) {
				foreach (var c in a)
					if (c is K k)
						ks[keyOrd(k)] = k;
					else if (Name((N)c) < 0)
						throw new($"name {c} in {p.name}.{ax} not found");
				if (a.lex >= 0 && a[a.lex] is not K)
					throw new($"{p.name}.{ax} lex {a[a.lex]} not lexic key");
				alts[alt] = a;
				Alts[alt] = new() { // synter alt
					name = p.name, size = checked((short)a.Count), lex = a.lex,
					synt = a.synt, rec = a.rec, hint = a.hint,
#pragma warning disable CS8974 // Converting method group to non-delegate type
					dump = a.ToString,
				};
				a.alt = alt++;
				if (np != p)
					np.Add(a); // append alterns to same name production
			}
			}
		// others
		if (keyOrd(default) != 0) throw new($"default key ordinal {keyOrd(default)}");
		if (nameOs[0] == 0) throw new($"name ordinal 0");
		keyOs = [.. ks.Keys];
		keys = [.. ks.Values];
		distinct(keys);
		firsts = new (bool, HashSet<K>)[names.Length];
	}
}
