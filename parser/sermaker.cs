//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using System;
using System.Collections.Generic;
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
		public int lex = -1; // save lex at this index to Synt.info, no save: <0
		public sbyte synt; // as Synter.tree: 0, make Synt: 1, omit Synt: -1
		public string hint;
	}

	public SynGram<K, N> n(N name) { prods.Add(new() { name = name }); return this; }
	// K : lexic key, N : syntax name, .. : save last lex
	public SynGram<K, N> this[params object[] cons] {
		get {
			Alt a = [];
			prods[^1].Add(a);
			foreach (var c in cons)
				if (c is Range) a.lex = a.Count - 1;
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
	readonly Func<K, ushort> keyOrd;
	readonly Func<N, ushort> nameOrd;
	readonly SortedDictionary<ushort, (K k, int x)> keys = []; // ordinal to (key, index)
	readonly SortedDictionary<ushort, (N n, int x)> names = []; // ordinal to (name, index)
	readonly N finish;
	readonly SynGram<K, N>.Prod[] prods; // at name ordinal index
	readonly SynAlt<N>[] alts;
	readonly (bool empty, HashSet<K> first)[] firsts; // at name ordinal index

	public SerMaker(SynGram<K, N> gram, Func<K, ushort> keyOrd, Func<N, ushort> nameOrd,
		Action<IEnumerable<K>> distinct)
	{
		this.keyOrd = keyOrd; this.nameOrd = nameOrd;
		// names
		foreach (var (p, px) in gram.prods.Each())
			if (!names.TryAdd(nameOrd(p.name), (p.name, 0)))
				throw new($"duplicate name {p.name}");
		foreach (var ((o, (n, _)), x) in names.ToArray().Each())
			names[o] = (n, x);
		finish = gram.prods[0].name;

		// prods
		prods = new SynGram<K, N>.Prod[names.Count];
		foreach (var p in gram.prods)
			prods[names[nameOrd(p.name)].x] = p;
		// alts
		List<SynAlt<N>> As = [];
		foreach (var p in gram.prods)
			foreach (var (a, ax) in p.Each()) {
				foreach (var c in a)
					if (c is K k)
						keys.Add(keyOrd(k), (k, 0));
					else if (!names.ContainsKey(nameOrd((N)c)))
						throw new($"name {c} in {p.name}.{ax} not found");
				if (a.lex >= 0 && a[a.lex] is not K)
					throw new($"{p.name}.{ax} lex {a[a.lex]} must be lexic key");
				As.Add(new() {
					name = p.name, size = a.Count, lex = a.lex, synt = a.synt,
					hint = a.hint, dump = null, // TODO
				});
			}
		alts = [.. As];

		// keys
		distinct(keys.Select(i => i.Value.k));
		foreach (var ((o, (k, _)), x) in keys.ToArray().Each())
			keys[o] = (k, x);
		// others
		firsts = new (bool, HashSet<K>)[names.Count];
		Firsts();
	}

	void Firsts()
	{
		for (bool loop = true; !(loop = !loop);)
			foreach (var (p, px) in prods.Each()) {
				var nf = firsts[px].first ??= [];
				foreach (var a in p) {
					foreach (var c in a) {
						var (ce, cf) = c is K k ? (false, k.Enum())
							: ((bool, IEnumerable<K>))firsts[names[nameOrd((N)c)].x];
						foreach (var f in cf ?? [])
							loop |= nf.Add(f);
						if (!ce)
							goto A;
					}
					_ = firsts[px].empty || (loop = firsts[px].empty = true); A:;
				}
			}
	}

	public (bool empty, IEnumerable<K> first) First(N name) => firsts[names[nameOrd(name)].x];

	public (bool empty, IEnumerable<K> first) First(N name, K ahead)
	{
		var (empty, first) = First(name);
		return (false, empty ? first.Append(ahead) : first);
	}

	public (bool empty, IEnumerable<K> first) First(object name, object ahead) =>
		name is K k ? (false, k.Enum()) : ahead == null ? First((N)name) : First((N)name, (K)ahead);
}
