//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;

namespace qutum.syntax;

using Kord = char;
using Nord = ushort;
using L = Lex;
using S = Syn;

public enum Syn : Nord
{
	__ = default,
	all, allir, alli, blocks,
	block, nestr, nests, nest, line,
	e9, e8, e7, e6, e56, e53, e46, e43, e3, e2, e1, exp,
	F7, f8, f7, f6, f56, f53, f46, f43, f3, f2, f1, feed,
}

public class Synt : Synt<S, Synt>
{
}

public class Synter : Synter<L, S, Synt, Lexier>
{
	static readonly Func<S, Nord> NameOrd = n => (Nord)n;
	static readonly SynAlt<S>[] alts;
	static readonly SynForm[] forms;
	static readonly char[] recKs;
	static readonly SerMaker<L, S> mer;

	static Synter()
	{
		// syntax grammar
		var gram = new SynGram<L, S>()
			.n(S.all)[S.allir, S.alli, S.blocks]
			.n(S.allir)[[]][L.INDR, S.blocks, L.DEDR]
			.n(S.alli)[[]][L.IND, S.blocks, L.DED]
			.n(S.blocks)[[]][S.block, S.blocks]

			.n(S.block)[S.line]
						[S.line, L.INDR, S.nests, L.DEDR]
						[S.line, L.INDR, S.nests, L.DEDR, L.IND, S.nests, L.DED]
						[S.line, L.IND, S.nests, L.DED]
			.n(S.nests)[S.nest]
						[S.nest, S.nests]
			.n(S.line)[L.LITERAL]
			.n(S.nest)[S.block]
		;
		// make
		var m = new SerMaker<L, S>(gram, Lexier.Ordin, NameOrd, Lexier.Distinct);
		(alts, forms, recKs) = m.Make(out var _);
#if DEBUG
		mer = m;
#endif
	}

	public Synter(Lexier l) : base(Lexier.Ordin, NameOrd, alts, forms, recKs)
	{
		recover = true;
		synt = false;
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
		if (d is Kord k && k != default) return Lexier.Ordin(k).ToString();
		if (d is Nord n) return ((S)n).ToString();
		return base.Dumper(d);
	}
}
