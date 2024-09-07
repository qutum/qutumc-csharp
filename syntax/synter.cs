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
	all, all__, all_, Block,
	block, nestr, nests, Nest,
	nest, bin, line,
	e9, e8, e7, exp,
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
		.n(S.all).___[S.all__, S.all_, S.Block].synt
		.n(S.all__)._[[]]._[L.INDR, S.Block, L.DEDR]
		.n(S.all_).__[[]]._[L.IND, S.Block, L.DED]
		.n(S.Block)._[[]]._[S.block, S.Block].syntRight

		.n(S.block)._[S.line, S.nestr, S.nests]
		.n(S.nestr)._[[]]._[L.INDR, S.Nest, L.DEDR].recover.synt.label("right nested blocks")
		.n(S.nests)._[[]]._[L.IND, S.Nest, L.DED].recover.label("nested blocks")
		.n(S.Nest).__[S.nest]._[S.nest, S.Nest]

		.n(S.nest)._[S.block].synt
					[S.bin, .., S.block].synt.label("nested binary block")
		.n(S.bin).__[L.BIN1][L.BIN2][L.BIN3]._[L.BIN46][L.BIN53][L.BIN56][L.BIN6]

		.n(S.line, "line")._[S.exp, L.EOL].recover

		.n(S.exp, "expression")
				[S.exp, L.BIN1, .., S.exp].clash.syntLeft.label("bin1 operator")
				[S.exp, L.BIN2, .., S.exp].clash.syntLeft.label("logical operator")
				[S.exp, L.BIN3, .., S.exp].clash.syntLeft.label("comparison operator")
				[S.exp, L.BIN43, .., S.exp].clash.syntLeft.label("arithmetic operator")
				[S.exp, L.BIN46, .., S.exp].clash.syntLeft.label("arithmetic operator")
				[S.exp, L.BIN53, .., S.exp].clash.syntLeft.label("bitwise operator")
				[S.exp, L.BIN56, .., S.exp].clash.syntLeft.label("bitwise operator")
				[S.exp, L.BIN6, .., S.exp].clash.syntLeft.label("bin6 operator")
				[S.e7][S.e9]
		.n(S.e7)[L.BIN43, .., S.exp].clash.synt.label("binary prefix operator")
				[L.PRE, .., S.exp].clash.synt.label("prefix operator")
		.n(S.e9)[L.LITERAL, ..].clash.synt.label("literal")
				[L.LP, .., S.exp, L.RP].clash.recover.label("parenth")
		.n(S.bin)[L.BIN43].clash
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
