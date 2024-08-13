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

using Modes = List<(int mode, List<(int a, int kx)> alts)>;

// syntax grammar
public class SynGram<K, N>
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Alt> { public N name; }
	// { K or N ... }
	public class Alt : List<object>
	{
		public short lex = -1; // save lex at this index to Synt.info, no save: <0
		public sbyte synt; // as Synter.tree: 0, make Synt: 1, omit Synt: -1
		public string hint;
		internal short alt;
	}

	public SynGram<K, N> n(N name) { prods.Add(new() { name = name }); return this; }
	// K : lexic key, N : syntax name, .. : save last lex
	public SynGram<K, N> this[params object[] cons] {
		get {
			Alt a = [];
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
	struct Item
	{
		internal short alt;
		internal short next;
		internal K ahead; // key ordinal, eor: default
	}
	struct Form
	{
		internal HashSet<Item> Is;
		internal Modes[] modes; // at key ordinal index, shift to: form index, reduce: -2-alt index, error: -1
		internal List<(int push, int nx)> pushs; // push: form index
	}

	readonly Func<K, ushort> keyOrd; // ordinal from 1, default for eor: 0
	readonly Func<N, ushort> nameOrd; // ordinal from 1
	readonly ushort[] keyOs, nameOs; // { ordinal... }
	readonly K[] keys; // at key ordinal index
	readonly N[] names; // at name ordinal index

	readonly int finish; // at name ordinal index
	readonly SynGram<K, N>.Prod[] prods; // at name ordinal index
	readonly SynGram<K, N>.Alt[] alts;

	readonly (bool empty, HashSet<K> first)[] firsts; // at name ordinal index
	readonly List<Form> forms = [];

	readonly SynAlt<N>[] Alts;

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
		finish = Name(gram.prods[0].name);

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
						ks.Add(keyOrd(k), k);
					else if (Name((N)c) < 0)
						throw new($"name {c} in {p.name}.{ax} not found");
				if (a.lex >= 0 && a[a.lex] is not K)
					throw new($"{p.name}.{ax} lex {a[a.lex]} not lexic key");
				alts[alt] = a;
				Alts[alt] = new() {
					name = p.name, size = checked((short)a.Count), lex = a.lex,
					synt = a.synt, hint = a.hint, dump = null, // TODO
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
						var (ce, cf) = First(c, null);
						foreach (var f in cf ?? [])
							loop |= nf.Add(f);
						if (!ce)
							goto A;
					}
					_ = firsts[px].empty || (loop = firsts[px].empty = true); A:;
				}
			}
	}

	public (bool empty, IEnumerable<K> first) First(N name) => firsts[Name(name)];

	public (bool empty, IEnumerable<K> first) First(N name, K ahead)
	{
		var (empty, first) = First(name);
		return (false, empty ? first.Append(ahead) : first);
	}

	public (bool empty, IEnumerable<K> first) First(object name, object ahead)
		=> name is K k ? (false, [k]) : ahead == null ? First((N)name) : First((N)name, (K)ahead);

	int AddForm(HashSet<Item> Is)
	{
		Debug.Assert(Is.Count > 0);
		foreach (var (f, x) in forms.Each())
			if (f.Is.SetEquals(Is))
				return x;
		forms.Add(new() {
			Is = Is, modes = new Modes[keys.Length], pushs = []
		});
		return forms.Count - 1;
	}

	HashSet<Item> Closure(HashSet<Item> Is)
	{
		for (var loop = true; !(loop = !loop);)
			foreach (var i in Is) {
				var a = alts[i.alt];
				if (i.next >= a.Count || a[i.next] is not N next)
					continue;
				var p = prods[Name(next)];
				var f = i.next + 1 < a.Count ? First(a[i.next + 1], i.ahead) : First(i.ahead, null);
				foreach (var h in f.first)
					foreach (var A in p)
						loop |= Is.Add(new() { alt = A.alt, ahead = h });
			}
		return Is;
	}

	void ShiftPush(Form f)
	{
		foreach (var i in f.Is) {
			var a = alts[i.alt];
			if (i.next >= a.Count)
				continue;
			HashSet<Item> js = [];
			foreach (var j in f.Is) {
				var b = alts[j.alt];
				if (j.next < b.Count && b[j.next].Equals(a[i.next]))
					js.Add(j with { next = (short)(j.next + 1) });
			}
			var to = AddForm(Closure(js)); int kx;
			if (a[i.next] is K k)
				f.modes[kx = Key(k)] = [(to, [(i.alt, kx)])];
			else
				f.pushs.Add((Name((N)a[i.next]), to));
		}
	}

	void Reduce(Form f)
	{
		foreach (var i in f.Is) {
			var a = alts[i.alt];
			if (i.next < a.Count)
				continue;
			var R = SynForm.Reduce(i.alt);
			var A = (i.alt, Key(i.ahead));
			var ms = f.modes[Key(i.ahead)] ??= [];
			foreach (var m in ms)
				if (m.mode == R) {
					m.alts.Add(A); goto M;
				}
			ms.Add((R, [A])); M:;
		}
	}

	public List<Modes> Forms()
	{
		if (forms.Count > 0)
			return null;
		forms.Add(new() {
			Is = Closure([.. prods[finish].Select(a => new Item { alt = a.alt })])
		});
		for (var x = 0; x < forms.Count; x++) {
			if (x >= 32767) throw new("too many forms");
			ShiftPush(forms[x]);
		}
		foreach (var f in forms)
			Reduce(f);

		// conflict
		List<Modes> confs = [];
		foreach (var f in forms)
			foreach (var (k, kx) in keys.Each())
				if (f.modes[kx]?.Count > 1)
					confs.Add(f.modes[kx]);

		return confs;
	}
}
;
