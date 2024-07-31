//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

using System.Collections.Generic;

namespace qutum.parser;

// TODO
public enum Qua : byte { Opt = 0, One = 1, Any = 2, More = 3 };

// synter grammar
public class SynGram<K, N>
{
	public readonly List<Prod> prods = [];
	public class Prod : List<Alt> { public N name; }
	public class Alt
	{
		public readonly List<object> cons = [];
		internal sbyte synt; // as Synter.tree: 0, make Synt: 2, omit Synt: -1
		internal bool lex; // save first lex of cons to Synt.info
		internal bool error; // make expecting Synt when error
		internal string hint;
	}

	public SynGram<K, N> prod(N name) { prods.Add(new Prod { name = name }); return this; }
	public SynGram<K, N> this[params object[] cons] {
		get {
			Alt a = new();
			prods[^1].Add(a);
			foreach (var v in cons)
				if (v is N or K) a.cons.Add(v);
				else throw new($"wrong altern content {v?.GetType()}");
			return this;
		}
	}
	public SynGram<K, N> synt { get { prods[^1][^1].synt = 2; return this; } }
	public SynGram<K, N> syntMay { get { prods[^1][^1].synt = 1; return this; } }
	public SynGram<K, N> syntOmit { get { prods[^1][^1].synt = -1; return this; } }
	public SynGram<K, N> lex { get { prods[^1][^1].lex = true; return this; } }
	public SynGram<K, N> err { get { prods[^1][^1].error = true; return this; } }
	public SynGram<K, N> hint(string w) { prods[^1][^1].hint = w != "" ? w : null; return this; }
}
