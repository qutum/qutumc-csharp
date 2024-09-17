//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;

namespace qutum.syntax;

using Nord = ushort;
using L = Lex;
using S = Syn;

public enum Syn : Nord
{
	__ = default,
	qutum, Block,
	block, nestr, nests, Nest, nest, bin, line,
	exp, exph, inp,
}

public class Synt : Synt<S, Synt>
{
}

public partial class Synter : Synter<L, S, Synt, Lexier>
{
	static Synter()
	{
		// syntax grammar
		var gram = new SynGram<L, S>()
		.n(S.qutum)._[S.Block].synt
		.n(S.Block)._[[]]._[S.Block, S.block].syntLeft

		.n(S.block)._[S.line, S.nestr, S.nests]
		.n(S.nestr)._[[]]._[L.INDR, S.Nest, L.DEDR].synt
		.n(S.nests)._[[]]._[L.IND, S.Nest, L.DED]
		.n(S.Nest).__[S.nest]._[S.Nest, S.nest]

		.n(S.nest)._[S.block].synt
					[S.bin, .., S.block].synt.label("nested binary block")
		.n(S.bin).__.Alts(L.Bin)

		.n(S.line, "line")._[S.exp, L.EOL].recover

		.n(S.inp)
				[S.exph, L.INP].clash // trailing comma
				[S.exph, L.INP, .., S.exp].clash.syntLeft.label("serial input")
		.n(S.exp, "expression", false)
				[S.exp, L.BIN1, .., S.exp].clash.syntLeft.label("bin1 operator")
				[S.exp, L.BIN2, .., S.exp].clash.syntLeft.label("logical operator")
				[S.exp, L.BIN3, .., S.exp].clash.syntLeft.label("comparison operator")
				[S.exp, L.BIN43, .., S.exp].clash.syntLeft.label("arithmetic operator")
				[S.exp, L.BIN47, .., S.exp].clash.syntLeft.label("arithmetic operator")
				[S.exp, L.ORB, .., S.exp].clash.syntLeft.label("bitwise operator")
				[S.exp, L.XORB, .., S.exp].clash.syntLeft.label("bitwise operator")
				[S.exp, L.ANDB, .., S.exp].clash.syntLeft.label("bitwise operator")
				[S.exp, L.BIN57, .., S.exp].clash.syntLeft.label("bitwise operator")
				[S.exp, L.BIN6, .., S.exp].clash.syntLeft.label("bin6 operator")
				[L.PRE, .., S.exp].clash.synt.label("prefix operator")
				[S.exph].clash
		.n(S.exph)
				[S.inp].clash
				[S.exp, L.POST, ..].clash.syntLeft.label("postfix operator")
				[L.LITERAL, ..].clash.synt.label("literal")
				[L.LP, .., S.exp, L.RP].clash.recover.label("parenth") // main lex for recovery
		.n(S.inp)
				[S.exp, S.exp].clash.syntLeft // higher than others for left associative
		.n(S.exph)
				[S.exph, L.POSTD, ..].clash.syntLeft.label("high postfix operator")
		;
		// make
		var m = new SerMaker<L, S>(gram, LexIs.Ordin, NameOrd, LexIs.Distinct);
		(alts, forms, recKs) = m.Make(out var _);
#if DEBUG
		mer = m;
#endif
	}

	static readonly Func<S, Nord> NameOrd = n => (Nord)n;
	static readonly SynAlt<S>[] alts;
	static readonly SynForm[] forms;
	static readonly char[] recKs;
	static readonly SerMaker<L, S> mer;

	public Synter() : base(Lexier.Ordin, NameOrd, alts, forms, recKs)
	{
		recover = true;
		synt = false;
	}

	public override Synter Begin(Lexier ler) => (Synter)base.Begin(ler);

	public override Synt Parse()
	{
		(ler.leadInd, ler.eor, ler.allBlank) = (false, true, false);
		var t = base.Parse();
		// add error lexis
		var errs = new Synt { err = -3, info = ler.errs.Count };
		foreach (var (err, x) in ler.errs.Each())
			errs.Add(new Synt {
				from = ~x, to = ~x - 1, err = -3, info = err.key, dump = err.ToString()
			});
		return t.Append(errs.head?.up);
	}
}

file static class Extension
{
	public static SynGram<L, S> Alts(this SynGram<L, S> gram, L group)
	{
		foreach (var k in LexIs.OfGroup(group))
			if (k.IsKind() || k.IsSingle())
				_ = gram[k];
		return gram;
	}
}
