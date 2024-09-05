//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;

namespace qutum.syntax;

public enum Syn : ushort
{
	// TODO
	all = 1, allii, alli, Block,
	block, nestr, nests, nest, line,
	e9, e8, e7, e6, e56, e53, e46, e43, e3, e2, e1, exp,
	F7, f8, f7, f6, f56, f53, f46, f43, f3, f2, f1, feed,
}

public class Synt : Synt<Syn, Synt>
{
}

public class Synter : Synter<Lex, Syn, Synt, Lexier>
{
	static readonly Func<Syn, ushort> NameOrd = n => (ushort)n;
	static readonly SynAlt<Syn>[] alts;
	static readonly SynForm[] forms;
	static readonly char[] recKs;
	static new Func<object, string> dumper;

	static Synter()
	{
		var gram = new SynGram<Lex, Syn>()
			.n(Syn.all);
		var mer = new SerMaker<Lex, Syn>(gram, Lexier.Ordin, NameOrd, Lexier.Distinct);
		(alts, forms, recKs) = mer.Make(out var clashs);
#if DEBUG
		dumper = mer.Dumper;
#endif
	}

	public Synter(Lexier l) : base(Lexier.Ordin, NameOrd, alts, forms, recKs)
	{
		recover = true;
		Begin(l);
	}

	public override Synt Parse()
	{
		var t = base.Parse();
		// add error lexis
		var errs = new Synt { err = -3, info = ler.errs.Count };
		foreach (var (err, x) in ler.errs.Each())
			errs.Add(new Synt {
				from = ~x, to = ~x - 1, err = -3, info = err.key, dump = err.ToString()
			});
		return t.Append(errs.head?.up);
	}

	public override string Dumper(object d)
	{
		return base.Dumper(d); // TODO
	}
}
