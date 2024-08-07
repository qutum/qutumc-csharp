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
	public static Ser Eq(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
	{
		AreNotEqual(null, s.t);
		AreEqual(err, s.t.err);
		if (name != null) AreEqual(name, s.t.name);
		if (v != null)
			AreEqual(v, v is L && s.t.info is Lexi<L> l ? l.key : s.t.info);
		var (fl, fc, tl, tc) = s.s.ler.LineCol(s.t.from, s.t.to);
		if (from != null) AreEqual($"{from}", $"{fl}.{fc}");
		if (to != null) AreEqual($"{to}", $"{tl}.{tc}");
		return s;
	}
	public static Ser V(this Ser s, params object[] Vs)
	{
		Lexi<L>[] vs = new Lexi<L>[Vs.Length];
		int n = 0;
		for (int x = 0; x < Vs.Length; x++)
			if (Vs[x] is L l)
				vs[n++] = new Lexi<L> { key = l };
			else
				vs[n - 1].value = Vs[x];
		AreEqual(s.s.dumper(vs.Seg(0, n)),
			s.s.dumper(s.t.from >= 0 ? s.s.ler.Lexs(s.t.from, s.t.to)
				: s.s.ler.errs.GetRange(~s.t.from, ~s.t.to - ~s.t.from).ToArray().Seg()));
		return s;
	}

	public static Ser h(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> (s.t.head, s.s).Eq(name, v, from, to, err);
	public static Ser t(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> (s.t.tail, s.s).Eq(name, v, from, to, err);
	public static Ser n(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> (s.t.next, s.s).Eq(name, v, from, to, err);
	public static Ser p(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> (s.t.prev, s.s).Eq(name, v, from, to, err);

	public static Ser H(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> h(s, name, v, from, to, err).Leaf();
	public static Ser T(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> t(s, name, v, from, to, err).Leaf();
	public static Ser N(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> n(s, name, v, from, to, err).Leaf();
	public static Ser P(this Ser s,
		S? name = null, object v = null, double? from = null, double? to = null, int err = 0)
		=> p(s, name, v, from, to, err).Leaf();

	public static Ser Leaf(this Ser s) { AreEqual(null, s.t.head); return s; }
	public static Ser u(this Ser s) { AreEqual(null, s.t.next); AreNotEqual(null, s.t.up); return (s.t.up, s.s); }
	public static Ser U(this Ser s) { AreEqual(null, s.t.next); AreEqual(null, s.t.up); return (s.t.up, s.s); }
	public static Ser uu(this Ser s) => u(u(s));
	public static Ser uU(this Ser s) => U(u(s));
	public static Ser uuu(this Ser s) => u(u(u(s)));
	public static Ser uuU(this Ser s) => U(u(u(s)));
	public static Ser uuuu(this Ser s) => u(u(u(u(s))));
	public static Ser uuuU(this Ser s) => U(u(u(u(s))));
	public static Ser uuuuU(this Ser s) => U(u(u(u(u(s)))));
	public static Ser uuuuuU(this Ser s) => U(u(u(u(u(u(s))))));
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

	public const S B = S.Block, N = S.nest, F = S.feed;

	[TestMethod]
	public void Blocks()
	{
		var t = Parse("").Eq(S.all).U();
		t = Parse(@"
			1
			2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1).u();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 2).uuU();
		t = Parse(@"
				1
		\##\ 2
				3
	\##\ 4
	5
6");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1).u();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 2).u();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 3).u();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 4).u();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 5).u();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 6).uu();
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
		t = t/**/		.n(N).H(S.e0).V(L.INT, 2);
		t = t/**/			.n(N).H(S.e0).V(L.INT, 3).uu();
		t = t/**/		.n(N).H(S.e0).V(L.INT, 4).uuuU();
		t = Parse(@"
			1
				2
				3
					4
						5
					6
			7");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(N).H(S.e0).V(L.INT, 2).u();
		t = t/**/		.n(N).H(S.e0).V(L.INT, 3);
		t = t/**/			.n(N).H(S.e0).V(L.INT, 4);
		t = t/**/				.n(N).H(S.e0).V(L.INT, 5).uu();
		t = t/**/			.n(N).H(S.e0).V(L.INT, 6).uuu();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 7).uuU();
	}

	[TestMethod]
	public void RecoverNested()
	{
		var t = Parse(@"
			1
				)
				2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(N).H(S.line, err: -4).u();
		t = t/**/		.n(N).H(S.e0).V(L.INT, 2).uuu();
		t = t/**/.n(null, L.RP, 3.5, 3.6, -1);
		t = t/**/	.H(S.nests, err: 1).N(S.line, err: 1).uU();
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
		t = t/**/		.n(S.nestr).h(N);
		t = t/**/					.H(S.e0).V(L.INT, 2);
		t = t/**/					.n(N).H(S.e0).V(L.INT, 3).uu();
		t = t/**/				.n(N).H(S.e0).V(L.INT, 4).uuuuU();
		t = Parse(@"
			1
						2
								3
					4
				5");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.nestr).h(N).H(S.e0).V(L.INT, 2);
		t = t/**/						.n(S.nestr).h(N).H(S.e0).V(L.INT, 3).uuu();
		t = t/**/				.n(N).H(S.e0).V(L.INT, 4).uu();
		t = t/**/		.n(N).H(S.e0).V(L.INT, 5).uuu();
		t = t/**/.N(null, L.INDR, 5.1, 5.6, -1).U();
	}

	[TestMethod]
	public void RecoverLine()
	{
		var t = Parse(@"
			1 *>
						2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1).N(S.line, err: -4);
		t = t/**/		.n(S.nestr).h(N).H(S.e0).V(L.INT, 2).uuuu();
		t = t/**/.n(null, L.GT, 2.7, 2.8, -1).H(S.e53, err: 1).uU();
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
		t = t/**/		.n(N).h(S.e2, L.NOT).H(S.e0).V(L.INT, 3).u();
		t = t/**/			.n(S.line, err: -4).Leaf();
		t = t/**/			.n(N).h(S.e2, L.SUB).H(S.e0).V(L.INT, 4).u();
		t = t/**/					.N(S.line, err: -4).uu();
		t = t/**/		.n(N).H(S.e0).V(L.INT, 5).uuu();
		t = t/**/.n(null, L.EOL, 3.8, 4.1, -1).H(S.e53, err: 1).u();
		t = t/**/.n(null, L.EOL, 4.9, 5.1, -1).H(S.e56, err: 1).uU();
	}

	[TestMethod]
	public void PrefixNested()
	{
		var t = Parse(@"
			1
				--2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(N).h(S.e2, L.NOTB);
		t = t/**/			.h(S.e0).V(L.INT, 2).uuuuU();
		t = Parse(@"
			1
				- -2
					*!3
				/+4");
		t = t/**/	.h(B).h(S.e0).V(L.INT, 1);
		t = t/**/		.n(N, L.SUB);
		t = t/**/			.h(S.e2, L.SUB).H(S.e0).V(L.INT, 2).u();
		t = t/**/			.n(N, L.MUL).h(S.e2, L.NOT).H(S.e0).V(L.INT, 3).uuu();
		t = t/**/		.n(N, L.DIV);
		t = t/**/			.h(S.e2, L.ADD).H(S.e0).V(L.INT, 4).uuuuU();
	}

	[TestMethod]
	public void Binary()
	{
		var t = Parse(@"1+2*3-4");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.e56, L.ADD).H(S.e0).V(L.INT, 2);
		t = t/**/					.n(S.e53, L.MUL).H(S.e0).V(L.INT, 3).uu();
		t = t/**/		.n(S.e56, L.SUB).H(S.e0).V(L.INT, 4).uuuU();
		t = Parse(@"1 + 2 * 3 >> 4 % 5 < 6 +| 7 ++ 8");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.e56, L.ADD).H(S.e0).V(L.INT, 2);
		t = t/**/			.n(S.e53, L.MUL).H(S.e0).V(L.INT, 3);
		t = t/**/				.n(S.e43, L.SHR).H(S.e0).V(L.INT, 4).uu();
		t = t/**/			.n(S.e53, L.MOD).H(S.e0).V(L.INT, 5).uu();
		t = t/**/		.n(S.e6, L.LT).H(S.e0).V(L.INT, 6).u();
		t = t/**/		.n(S.e7, L.XOR).H(S.e0).V(L.INT, 7);
		t = t/**/			.n(S.e46, L.XORB).H(S.e0).V(L.INT, 8).uuuuU();
	}

	[TestMethod]
	public void BinaryNested()
	{
		var t = Parse(@"
			1
				-2
				*3+4");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(N, L.SUB).H(S.e0).V(L.INT, 2).u();
		t = t/**/		.n(N, L.MUL).H(S.e0).V(L.INT, 3);
		t = t/**/					.n(S.e56, L.ADD).H(S.e0).V(L.INT, 4).uuuuU();
		t = Parse(@"
			1
				* 2
					- 3
						/ 4
					% 5
				&& 6
			7
				- 8");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(N, L.MUL).H(S.e0).V(L.INT, 2);
		t = t/**/					.n(N, L.SUB).H(S.e0).V(L.INT, 3);
		t = t/**/								.n(N, L.DIV).H(S.e0).V(L.INT, 4).uu();
		t = t/**/					.n(N, L.MOD).H(S.e0).V(L.INT, 5).uu();
		t = t/**/		.n(N, L.ANDB).H(S.e0).V(L.INT, 6).uu();
		t = t/**/	.n(B).H(S.e0).V(L.INT, 7);
		t = t/**/		.n(N, L.SUB).H(S.e0).V(L.INT, 8).uuuU();
	}

	[TestMethod]
	public void RecoverBinNest()
	{
		var t = Parse(@"
			1
				*");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(N, L.MUL).H(S.line, err: -4).uuu();
		t = t/**/.n(null, L.EOL, 3.6, 3.6, -1).H(N, err: 1).N(S.line, err: 1).uU();
		t = Parse(@"
			1
				+
					2
					*3
				/ 4
			-
				5");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(N, L.ADD).H(S.line, err: -4);
		t = t/**/					.n(N).H(S.e0).V(L.INT, 2).u();
		t = t/**/					.n(N, L.MUL).H(S.e0).V(L.INT, 3).uu();
		t = t/**/		.n(N, L.DIV).H(S.e0).V(L.INT, 4).uu();
		t = t/**/	.n(B).H(S.line, err: -4);
		t = t/**/		.n(N).H(S.e0).V(L.INT, 5).uuu();
		t = t/**/.n(null, L.EOL, 3.6, 4.1, -1).H(N, err: 1).N(S.e2, err: 1).N(S.line, err: 1).u();
		t = t/**/.n(null, L.EOL, 7.5, 8.1, -1).H(S.e2, err: 1).uU();
	}

	[TestMethod]
	public void Parathn()
	{
		var t = Parse(@"(1)");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1).uuU();
		t = Parse(@"-(-1)/(2*(3+4))");
		t = t/**/	.h(B).h(S.e2, L.SUB).h(S.e2, L.SUB).H(S.e0).V(L.INT, 1).uu();
		t = t/**/		.n(S.e53, L.DIV).H(S.e0).V(L.INT, 2);
		t = t/**/			.n(S.e53, L.MUL).H(S.e0).V(L.INT, 3);
		t = t/**/				.n(S.e56, L.ADD).H(S.e0).V(L.INT, 4).uuuuuU();
	}

	[TestMethod]
	public void Prefix()
	{
		var t = Parse(@"1- -2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(S.e56, v: L.SUB);
		t = t/**/			.h(S.e2, L.SUB).H(S.e0).V(L.INT, 2).uuuuU();
		t = Parse(@"1--2");
		t = t/**/	.h(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.n(F).h(S.F2, L.NOTB).H(S.e0).V(L.INT, 2).uuuu();
		t = t/**/.N(null, L.NOTB, 1.2, 1.4, -1).U();
	}

	[TestMethod]
	public void Feed()
	{
		var t = Parse(@"a 1 * 2");
		t = t/**/	.h(B).H(S.e0).V(L.NAME, "a");
		t = t/**/		.n(F).H(S.e0).V(L.INT, 1).n(S.f53, L.MUL).H(S.e0).V(L.INT, 2).uuuuU();
		t = Parse(@"a 1 + 2");
		t = t/**/	.h(B).H(S.e0).V(L.NAME, "a");
		t = t/**/		.n(F).H(S.e0).V(L.INT, 1).n(S.f56, L.ADD).H(S.e0).V(L.INT, 2).uuuuU();
		t = Parse(@"0 * a 1 * 2 3 4 == 5");
		t = t/**/	.h(B).H(S.e0).V(L.INT, "0").n(S.e53, L.MUL);
		t = t/**/		.H(S.e0).V(L.NAME, "a");
		t = t/**/		.n(F).H(S.e0).V(L.INT, 1).n(S.f53, L.MUL).H(S.e0).V(L.INT, 2).uu();
		t = t/**/		.n(F).H(S.e0).V(L.INT, 3).u();
		t = t/**/		.n(F).H(S.e0).V(L.INT, 4).n(S.f6, L.EQ).H(S.e0).V(L.INT, 5).uuuuuU();
	}

	[TestMethod]
	public void FeedRun()
	{
		var t = Parse(@"0 == a 1 * 2 3 4 .v / 5 c .");
		t = t/**/	.h(B).H(S.e0).V(L.INT, "0").n(S.e6, L.EQ);
		t = t/**/			.H(S.e0).V(L.NAME, "a");
		t = t/**/			.n(F).H(S.e0).V(L.INT, 1).n(S.f53, L.MUL).H(S.e0).V(L.INT, 2).uu();
		t = t/**/			.n(F).H(S.e0).V(L.INT, 3).u();
		t = t/**/			.n(F).H(S.e0).V(L.INT, 4).u();
		t = t/**/			.N(S.e1, L.RUN).n(S.e53, L.DIV);
		t = t/**/				.H(S.e0).V(L.INT, 5).n(F).H(S.e0).V(L.NAME, "c").u();
		t = t/**/				.N(S.e1, L.RUN).uuuuU();
		t = Parse(@"0 << a 1 * 2 3 4 .v / 5 c .");
		t = t/**/	.h(B).H(S.e0).V(L.INT, "0").n(S.e43, L.SHL);
		t = t/**/			.H(S.e0).V(L.NAME, "a");
		t = t/**/			.n(F).H(S.e0).V(L.INT, 1).n(S.f53, L.MUL).H(S.e0).V(L.INT, 2).uu();
		t = t/**/			.n(F).H(S.e0).V(L.INT, 3).u();
		t = t/**/			.n(F).H(S.e0).V(L.INT, 4).u();
		t = t/**/			.N(S.e1, L.RUN).u();
		t = t/**/		.n(S.e53, L.DIV);
		t = t/**/			.H(S.e0).V(L.INT, 5).n(F).H(S.e0).V(L.NAME, "c").u();
		t = t/**/			.N(S.e1, L.RUN).uuuU();
	}
}
