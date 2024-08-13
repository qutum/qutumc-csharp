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
		public short lex = -1; // save lex at this index to Synt.info, no save: <0
		public sbyte synt; // as Synter.tree: 0, make Synt: 1, omit Synt: -1
		public string hint;
		public string dump;
		internal short alt; // alt index of whole grammar

		public override string ToString() => dump ??= $"{name} = {string.Join(' ', this)}"; // TODO
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

	readonly int accept; // at name ordinal index
	readonly SynGram<K, N>.Prod[] prods; // at name ordinal index
	readonly SynGram<K, N>.Alt[] alts;
	readonly (bool empty, HashSet<K> first)[] firsts; // at name ordinal index

	private int Key(K k) => Array.BinarySearch(keyOs, keyOrd(k));
	private int Name(N n) => Array.BinarySearch(nameOs, nameOrd(n));

	public SerMaker(SynGram<K, N> gram, Func<K, ushort> keyOrd, Func<N, ushort> nameOrd,
		Action<IEnumerable<K>> distinct)
	{
		this.keyOrd = keyOrd; this.nameOrd = nameOrd;
		// names
		SortedDictionary<ushort, N> ns = [];
		foreach (var (p, px) in gram.prods.Each())
			if (!ns.TryAdd(nameOrd(p.name), p.name)) throw new($"duplicate name {p.name}");
		nameOs = [.. ns.Keys];
		names = [.. ns.Values];
		accept = Name(gram.prods[0].name);

		// prods
		prods = new SynGram<K, N>.Prod[names.Length];
		foreach (var p in gram.prods)
			prods[Name(p.name)] = p;
		// alts
		SortedDictionary<ushort, K> ks = new() { { keyOrd(default), default } };
		alts = new SynGram<K, N>.Alt[prods.Sum(p => p.Count)];
		if (alts.Length is 0 or > 32767) throw new($"alterns size {alts.Length}");
		Alts = new SynAlt<N>[alts.Length];
		short alt = 0;
		foreach (var p in gram.prods)
			foreach (var (a, ax) in p.Each()) {
				foreach (var c in a)
					if (c is K k)
						ks[keyOrd(k)] = k;
					else if (Name((N)c) < 0)
						throw new($"name {c} in {p.name}.{ax} not found");
				if (a.lex >= 0 && a[a.lex] is not K)
					throw new($"{p.name}.{ax} lex {a[a.lex]} not lexic key");
				alts[alt] = a;
				Alts[alt] = new() {
					name = p.name, size = checked((short)a.Count), lex = a.lex,
					synt = a.synt, hint = a.hint,
				};
				a.alt = alt++;
			}

		// others
		if (keyOrd(default) != 0) throw new($"default key ordinal {keyOrd(default)}");
		if (nameOs[0] == 0) throw new($"name ordinal 0");
		keyOs = [.. ks.Keys];
		keys = [.. ks.Values];
		distinct(keys);
		firsts = new (bool, HashSet<K>)[names.Length];
	}

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
	public struct Modes
	{
		public (int to, SynGram<K, N>.Alt alt) shift; // no shift: null alt
		internal HashSet<SynGram<K, N>.Alt> redus; // no reduces: null

		public override readonly int GetHashCode()
			=> HashCode.Combine(shift.alt, redus?.Count); // ignore form index for clash only
		public override readonly bool Equals(object o)
			=> o is Modes v && shift.alt == v.shift.alt // ignore form index for clash only
				&& (redus?.SetEquals(v.redus) ?? v.redus == null);
	}
	struct Form
	{
		internal Items Is;
		internal Modes[] modes; // at key ordinal index, error: null,
								// { shift to form index for alt index, shift -1 then reduce alt index }
		internal int?[] pushs;  // at name ordinal index, push form index
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
		forms.Add(new() { Is = Is, modes = new Modes[keys.Length], pushs = new int?[names.Length] });
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
				f.modes[Key(k)].shift = (to, a);
			else
				f.pushs[Name((N)a[next])] = to;
		}
	}

	void Reduce(Form f)
	{
		foreach (var ((a, next), heads) in f.Is)
			if (next >= a.Count)
				foreach (var head in heads.Append(default)) // lookaheads and eor
					(f.modes[Key(head)].redus ??= []).Add(a);
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

	public HashSet<(K key, Modes modes)> Clash(bool dump)
	{
		HashSet<(K key, Modes modes)> clash = [];
		foreach (var f in forms)
			foreach (var (m, kx) in f.modes.Each())
				if (m.redus?.Count > (m.shift.alt != null ? 0 : 1))
					clash.Add((keys[kx], m));
		if (dump) {
			using var env = EnvWriter.Begin();
			foreach (var ((key, modes), cx) in clash.Each(1)) {
				env.WriteLine($"clash {cx} by {CharSet.Unesc(key)}");
				using var ind = EnvWriter.Indent("\t\t\t");
				if (modes.shift.alt != null)
					env.WriteLine("shift " + modes.shift.alt);
				foreach (var alt in modes.redus)
					env.WriteLine("reduct " + alt);
			}
		}
		// TODO solve
		return clash;
	}

	public void Make(bool dumpClash = true)
	{
		Firsts();
		Forms();
		Clash(dumpClash);
	}
}
