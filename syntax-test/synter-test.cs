//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

#pragma warning disable IDE0059
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using qutum.syntax;
using System;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax;

using L = Lex;
using S = Syn;
using Ser = (Synt t, Synter s);

static class TestExtension
{
	public static Ser Eq(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
	{
		AreNotEqual(null, t.t);
		AreEqual(err, t.t.err);
		if (name != null) AreEqual(name, t.t.name);
		if (v != null)
			AreEqual(v, v is L && t.t.info is Lexi<L> l ? l.key : t.t.info);
		var (fl, fc, tl, tc) = t.s.ler.LineCol(t.t.from, t.t.to);
		if (from != null) AreEqual($"{from}", $"{fl}.{fc}");
		if (to != null) AreEqual($"{to}", $"{tl}.{tc}");
		return t;
	}
	public static Ser V(this Ser t, params object[] vs)
	{
		Lexi<L>[] s = new Lexi<L>[vs.Length];
		int n = 0;
		for (int x = 0; x < vs.Length; x++)
			if (vs[x] is L l)
				s[n++] = new Lexi<L> { key = l };
			else
				s[n - 1].value = vs[x];
		AreEqual(t.s.dumper(s.Seg(0, n)),
			t.s.dumper(t.t.from >= 0 ? t.s.ler.Lexs(t.t.from, t.t.to)
				: t.s.ler.errs.GetRange(~t.t.from, ~t.t.to - ~t.t.from).ToArray().Seg()));
		return t;
	}

	public static Ser h(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> (t.t.head, t.s).Eq(name, v, from, to, err);
	public static Ser t(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> (t.t.tail, t.s).Eq(name, v, from, to, err);
	public static Ser n(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> (t.t.next, t.s).Eq(name, v, from, to, err);
	public static Ser p(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> (t.t.prev, t.s).Eq(name, v, from, to, err);

	public static Ser H(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> h(t, name, v, from, to, err).Leaf();
	public static Ser T(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> TestExtension.t(t, name, v, from, to, err).Leaf();
	public static Ser N(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> n(t, name, v, from, to, err).Leaf();
	public static Ser P(this Ser t,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> p(t, name, v, from, to, err).Leaf();

	public static Ser Leaf(this Ser t) { AreEqual(null, t.t.head); return t; }
	public static Ser U(this Ser t) { AreEqual(null, t.t.next); return (t.t.up, t.s); }
	public static Ser UU(this Ser t) => U(U(t));
	public static Ser UUU(this Ser t) => U(U(U(t)));
	public static Ser UUUU(this Ser t) => U(U(U(U(t))));
	public static Ser UUUUU(this Ser t) => U(U(U(U(U(t)))));
}

[TestClass]
public class TestSynter : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	readonly Synter ser = new(new Lexier()) { dump = 2 };

	Ser Parse(string input)
	{
		env.WriteLine(input);
		ser.ler.Dispose();
		ser.ler.Begin(new LerByte(Encoding.UTF8.GetBytes(input)));
		var t = ser.Parse().Dump((Func<int, int, (int, int, int, int)>)ser.ler.LineCol);
		env.WriteLine($"--- match {ser.matchn} / lexi {ser.lexn} = {ser.matchn / Math.Max(ser.lexn, 1)} ---");
		return (t, ser);
	}

	public const S B = S.Block;

	[TestMethod]
	public void Blocks()
	{
		var t = Parse("").Eq(S.all).U();
		t = Parse(@"
			1
			2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1).U();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 2).UUU();
		t = Parse(@"
				1
		\##\ 2
				3
	\##\ 4
	5
6");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1).U();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 2).U();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 3).U();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 4).U();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 5).U();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 6).UU();
		t = t/**/.N(null, L.INDR, 3.1, 3.3, -1).U();
	}

	[TestMethod]
	public void Nested()
	{
		ser.dump = 3;
		var t = Parse(@"
			1
				2
					3
				4");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest).H(S.e0).V(L.INT, 2);
		t = t/**/				.n(S.nest).H(S.e0).V(L.INT, 3).UU();
		t = t/**/		.n(S.nest).H(S.e0).V(L.INT, 4).UUUU();
		t = Parse(@"
			1
				2
				3
					4
						5
					6
			7");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest).H(S.e0).V(L.INT, 2).U();
		t = t/**/		.n(S.nest).H(S.e0).V(L.INT, 3);
		t = t/**/			.n(S.nest).H(S.e0).V(L.INT, 4);
		t = t/**/				.n(S.nest).H(S.e0).V(L.INT, 5).UU();
		t = t/**/			.n(S.nest).H(S.e0).V(L.INT, 6).UUU();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 7).UU();
	}

	[TestMethod]
	public void NestedRight()
	{
		var t = Parse(@"
			1
					2
						3
					4");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nestr).h(S.nest);
		t = t/**/						.H(S.e0).V(L.INT, 2);
		t = t/**/						.n(S.nest).H(S.e0).V(L.INT, 3).UU();
		t = t/**/				.n(S.nest).H(S.e0).V(L.INT, 4).UUU();
		t = Parse(@"
			1
						2
								3
					4
				5");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nestr).h(S.nest).H(S.e0).V(L.INT, 2);
		t = t/**/						.n(S.nestr).h(S.nest).H(S.e0).V(L.INT, 3).UUU();
		t = t/**/				.n(S.nest).H(S.e0).V(L.INT, 4).UU();
		t = t/**/		.n(S.nest).H(S.e0).V(L.INT, 5).UUU();
		t = t/**/.N(null, L.INDR, 5.1, 5.6, -1).U();
	}

	[TestMethod]
	public void Prefix()
	{
		var t = Parse(@"1- -2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.e56, v: L.SUB);
		t = t/**/			.h(S.e2, L.SUB).H(S.e0).V(L.INT, 2).UUUU();
		t = Parse(@"1--2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.feed);
		t = t/**/			.h(S.F2, L.NOTB).H(S.e0).V(L.INT, 2).UUUU();
		t = t/**/.N(null, L.NOTB, 1.2, 1.4, -1).U();
	}

	[TestMethod]
	public void PrefixNested()
	{
		var t = Parse(@"
			1
				--2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest).h(S.e2, L.NOTB);
		t = t/**/				.h(S.e0).V(L.INT, 2).UUUU();
		t = Parse(@"
			1
				- -2
					*!3
				/+4");
		t = t/**/	.h(B).h(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest, L.SUB);
		t = t/**/			.h(S.e2, L.SUB).H(S.e0).V(L.INT, 2).U();
		t = t/**/			.n(S.nest, L.MUL).h(S.e2, L.NOT).H(S.e0).V(L.INT, 3).UUU();
		t = t/**/		.n(S.nest, L.DIV);
		t = t/**/			.h(S.e2, L.ADD).H(S.e0).V(L.INT, 4).UUUU();
	}

	[TestMethod]
	public void Binary()
	{
		var t = Parse(@"1+2*3-4");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.e56, L.ADD).H(S.e0).V(L.INT, 2);
		t = t/**/					.n(S.e53, L.MUL).H(S.e0).V(L.INT, 3).UU();
		t = t/**/		.n(S.e56, L.SUB).H(S.e0).V(L.INT, 4).UUU();
		t = Parse(@"1 + 2 * 3 >> 4 % 5 < 6 +| 7 ++ 8");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.e56, L.ADD).H(S.e0).V(L.INT, 2);
		t = t/**/			.n(S.e53, L.MUL).H(S.e0).V(L.INT, 3);
		t = t/**/				.n(S.e43, L.SHR).H(S.e0).V(L.INT, 4).UU();
		t = t/**/			.n(S.e53, L.MOD).H(S.e0).V(L.INT, 5).UU();
		t = t/**/		.n(S.e6, L.LT).H(S.e0).V(L.INT, 6).U();
		t = t/**/		.n(S.e7, L.XOR).H(S.e0).V(L.INT, 7);
		t = t/**/			.n(S.e46, L.XORB).H(S.e0).V(L.INT, 8).UUUUU();
	}

	[TestMethod]
	public void BinaryNested()
	{
		var t = Parse(@"
			1
				-2
				*3+4");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest, L.SUB).H(S.e0).V(L.INT, 2).U();
		t = t/**/		.n(S.nest, L.MUL).H(S.e0).V(L.INT, 3);
		t = t/**/					.n(S.e56, L.ADD).H(S.e0).V(L.INT, 4).UUUUU();
		t = Parse(@"
			1
				+ 2
					* 3
					/ 4
						+5
			6
				- 7");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest, L.ADD).H(S.e0).V(L.INT, 2);
		t = t/**/						.n(S.nest, L.MUL).H(S.e0).V(L.INT, 3).U();
		t = t/**/						.n(S.nest, L.DIV).H(S.e0).V(L.INT, 4);
		t = t/**/									.n(S.nest, L.ADD);
		t = t/**/										.H(S.e0).V(L.INT, 5).UUUU();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 6);
		t = t/**/		.n(S.nest, L.SUB).H(S.e0).V(L.INT, 7).UUUU();
	}

	[TestMethod]
	public void RecoverNested()
	{
		var t = Parse(@"
			1
				)
				2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest).H(S.line, err: -4).U();
		t = t/**/		.n(S.nest).H(S.e0).V(L.INT, 2).UUU();
		t = t/**/.n(null, L.RP, 3.5, 3.6, -1);
		t = t/**/	.H(S.nests, err: 1).N(S.line, err: 1).UU();
	}

	[TestMethod]
	public void RecoverBinNest()
	{
		var t = Parse(@"
			1
				*");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest, L.MUL).H(S.line, err: -4).UUU();
		t = t/**/.n(null, L.EOL, 3.6, 3.6, -1).H(S.nest, err: 1).N(S.line, err: 1).UU();
		t = Parse(@"
			1
				+
					2
					*3
				/ 4
			-
				5");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest, L.ADD).H(S.line, err: -4);
		t = t/**/							.n(S.nest).H(S.e0).V(L.INT, 2).U();
		t = t/**/							.n(S.nest, L.MUL).H(S.e0).V(L.INT, 3).UU();
		t = t/**/		.n(S.nest, L.DIV).H(S.e0).V(L.INT, 4).UU();
		t = t/**/	.n(B).H(S.line, err: -4);
		t = t/**/		.n(S.nest).H(S.e0).V(L.INT, 5).UUU();
		t = t/**/.n(null, L.EOL, 3.6, 4.1, -1).H(S.nest, err: 1).N(S.e2, err: 1).N(S.line, err: 1).U();
		t = t/**/.n(null, L.EOL, 7.5, 8.1, -1).H(S.e2, err: 1).UU();
	}

	[TestMethod]
	public void RecoverLine()
	{
		var t = Parse(@"
			1 *>
						2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1).N(S.line, err: -4);
		t = t/**/		.n(S.nestr).h(S.nest).H(S.e0).V(L.INT, 2).UUUU();
		t = t/**/.n(null, L.GT, 2.7, 2.8, -1);
		t = t/**/	.H(S.e53, err: 1).UU();
	}

	[TestMethod]
	public void RecoverExp()
	{
		var t = Parse(@"
			1
				!3/
					-4-
				5");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nest).h(S.e2, L.NOT).H(S.e0).V(L.INT, 3).U();
		t = t/**/				.n(S.line, err: -4).Leaf();
		t = t/**/				.n(S.nest).h(S.e2, L.SUB).H(S.e0).V(L.INT, 4).U();
		t = t/**/						.N(S.line, err: -4).UU();
		t = t/**/		.n(S.nest).H(S.e0).V(L.INT, 5).UUU();
		t = t/**/.n(null, L.EOL, 3.8, 4.1, -1).H(S.e53, err: 1).U();
		t = t/**/.n(null, L.EOL, 4.9, 5.1, -1).H(S.e56, err: 1).UU();
	}
}
