//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;

namespace qutum.syntax;

using K = Lek;
using N = Syn;
using Nord = ushort;

public enum Syn : Nord
{
	__ = default,
	qutum,
	// block serie
	Block, block, nook, jsqu,
	// inside block
	sen, with, nookr, nooks, Junc, junc, jbin, Jsqu, jpost,
	// expression
	exp, phr, inp, squ,
}

public class Synt : Synt<N, Synt>
{
}

public partial class Synter : Synter<K, N, Synt, Lexier>
{
	static Synter()
	{
		// syntax grammar
		var gram = new SynGram<K, N>()
		.n(N.qutum)._[N.Block].synt
		.n(N.Block)._[N.block]._[[]]
		.n(N.block)._[N.sen, N.with, N.Block].syntRight.label("block")
		.n(N.nook).__[N.sen, N.with, N.nook].syntRight._[[]]
		.n(N.jsqu).__[N.Jsqu, N.with, N.Block].syntRight.asName(N.block)

		.n(N.sen, "sentence")._[N.exp, K.EOL].recover
		.n(N.with).__[N.nookr, N.nooks, N.Junc]
		.n(N.nookr)._[K.INDR, N.nook, K.DEDR].synt._[[]] // rightmost nooks
		.n(N.nooks)._[K.IND, N.nook, K.DED]._[[]]
		.n(N.Junc).__[N.junc, N.Junc]._[[]]

		.n(N.junc)
				[K.INDJ, N.jbin, .., K.EOL, N.block, K.DED].recover.synt.label("junction")
				[K.INDJ, K.LSB, .., N.jsqu, K.DED].synt
				[K.INDJ, N.jpost, K.EOL, N.Block, K.DED].synt
		.n(N.jbin).__.Alts(K.Bin)
		.n(N.Jsqu).__[N.exp, K.RSB, K.EOL].recover.label("bracket junction")
		.n(N.jpost)._[K.POST, ..].synt._[K.POST, .., N.jpost].syntRight

		.n(N.inp)
				[N.phr, K.INP].clash // trailing comma
				[N.phr, K.INP, .., N.exp].clash.syntLeft.label("serial input")
		.n(N.exp, "expression", false)
				[N.exp, K.BIN1, .., N.exp].clash.syntLeft.label("bin1 operator")
				[N.exp, K.BIN2, .., N.exp].clash.syntLeft.label("bin2 operator")
				[N.exp, K.BIN3, .., N.exp].clash.syntLeft.label("logical operator")
				[N.exp, K.BIN4, .., N.exp].clash.syntLeft.label("comparison operator")
				[N.exp, K.BIN53, .., N.exp].clash.syntLeft.label("arithmetic operator")
				[N.exp, K.BIN57, .., N.exp].clash.syntLeft.label("arithmetic operator")
				[N.exp, K.ORB, .., N.exp].clash.syntLeft.label("bitwise operator")
				[N.exp, K.XORB, .., N.exp].clash.syntLeft.label("bitwise operator")
				[N.exp, K.BIN67, .., N.exp].clash.syntLeft.label("bitwise operator")
				[N.exp, K.BIN7, .., N.exp].clash.syntLeft.label("bin7 operator")
				[K.PRE, .., N.exp].clash.synt.label("prefix operator")
				[N.phr].clash
		.n(N.phr) // phrase
				[N.exp, K.POST, ..].clash.syntLeft.label("postfix operator")
				[N.exp, N.squ].clash // postfix square
				[K.LIT, ..].clash.synt.label("literal")
				[N.inp].clash
		.n(N.inp)
				[N.exp, N.exp].clash.syntLeft // higher for left associative
		.n(N.phr)
				[N.phr, K.POSTD, ..].clash.syntLeft.label("high postfix operator")
				[K.LP, .., N.exp, K.RP].clash.recover.label("parenth") // main lex for recovery
				[N.squ].clash
		.n(N.squ)
				[K.LSB, .., N.exp, K.RSB].clash.recover.synt.label("square bracket") // main lex for recovery
		;
		// make
		var m = new SerMaker<K, N>(gram, LexIs.Ordin, NameOrd);
		(alts, forms, recKs) = m.Make(out var _);
#if DEBUG
		mer = m;
#endif
	}

	static readonly Func<N, Nord> NameOrd = n => (Nord)n;
	static readonly SynAlt<N>[] alts;
	static readonly SynForm[] forms;
	static readonly char[] recKs;
	static readonly SerMaker<K, N> mer;

	public Synter() : base(Lexier.Ordin, NameOrd, alts, forms, recKs)
	{
		recover = true;
		synt = false;
	}

	public override Synter Begin(Lexier ler) => (Synter)base.Begin(ler);

	public override Synt Parse()
	{
		ler.blanks = null;
		var t = base.Parse();
		// add error lexis
		var errs = new Synt { err = -3, info = ler.errs.Count };
		foreach (var (err, x) in ler.errs.Each())
			errs.Add(new() {
				j = (~x, ~(x + 1)), err = -3, info = err.key, dump = err.ToString()
			});
		return t.Append(errs.head?.up);
	}
}

file static class Extension
{
	public static SynGram<K, N> Alts(this SynGram<K, N> gram, K group)
	{
		foreach (var k in group.GroupEach(true))
			if (k.IsKind() || k.IsSingle())
				_ = gram[k];
		return gram;
	}
}
