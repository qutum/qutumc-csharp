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
		S? name = null, int err = 0, double? from = null, double? to = null, object v = null)
	{
		AreNotEqual(null, t.t);
		AreEqual(err, t.t.err);
		if (name != null) AreEqual(name, t.t.name);
		var (fl, fc, tl, tc) = t.s.ler.LineCol(t.t.from, t.t.to);
		if (from != null) AreEqual($"{from}", $"{fl}.{fc}");
		if (to != null) AreEqual($"{to}", $"{tl}.{tc}");
		if (v != null)
			AreEqual(v, v is L && t.t.info is Lexi<L> l ? l.key : t.t.info);
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

	public static Ser H(this Ser t,
		S? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		=> (t.t.head, t.s).Eq(name, err, from, to, v);
	public static Ser T(this Ser t,
		S? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		=> (t.t.tail, t.s).Eq(name, err, from, to, v);
	public static Ser N(this Ser t,
		S? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		=> (t.t.next, t.s).Eq(name, err, from, to, v);
	public static Ser P(this Ser t,
		S? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		=> (t.t.prev, t.s).Eq(name, err, from, to, v);

	public static Ser H0(this Ser t) { AreEqual(null, t.t.head); return t; }
	public static Ser N0(this Ser t) { AreEqual(null, t.t.next); return (t.t.up, t.s); }
	public static Ser P0(this Ser t) { AreEqual(null, t.t.prev); return (t.t.up, t.s); }
	public static Ser HN0(this Ser t) { H0(t); AreEqual(null, t.t.next); return (t.t.up, t.s); }
	public static Ser HP0(this Ser t) { H0(t); AreEqual(null, t.t.prev); return (t.t.up, t.s); }
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
		var t = Parse("").Eq(S.all, 0).HN0();
		t = Parse(@"
			1
			2");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1).HN0();
		t = t/**/	.N(B).H(S.e0).V(L.INT, 2).HN0().N0().N0();
		t = Parse(@"
				1
		\##\ 2
				3
	\##\ 4
	5
6");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1).HN0();
		t = t/**/	.N(B).H(S.e0).V(L.INT, 2).HN0();
		t = t/**/	.N(B).H(S.e0).V(L.INT, 3).HN0();
		t = t/**/	.N(B).H(S.e0).V(L.INT, 4).HN0();
		t = t/**/	.N(B).H(S.e0).V(L.INT, 5).HN0();
		t = t/**/	.N(B).H(S.e0).V(L.INT, 6).HN0().N0();
		t = t/**/.N(null, -1, 3.1, 3.3, L.INDR).HN0();
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
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest).H(S.e0).V(L.INT, 2);
		t = t/**/				.N(S.nest).H(S.e0).V(L.INT, 3).HN0().N0();
		t = t/**/		.N(S.nest).H(S.e0).V(L.INT, 4).HN0().N0().N0().N0();
		t = Parse(@"
			1
				2
				3
					4
						5
					6
			7");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest).H(S.e0).V(L.INT, 2).HN0();
		t = t/**/		.N(S.nest).H(S.e0).V(L.INT, 3);
		t = t/**/			.N(S.nest).H(S.e0).V(L.INT, 4);
		t = t/**/				.N(S.nest).H(S.e0).V(L.INT, 5).HN0().N0();
		t = t/**/			.N(S.nest).H(S.e0).V(L.INT, 6).HN0().N0().N0();
		t = t/**/	.N(B).H(S.e0).V(L.INT, 7).HN0().N0();
	}

	[TestMethod]
	public void NestedRight()
	{
		var t = Parse(@"
			1
					2
						3
					4");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nestr).H(S.nest);
		t = t/**/						.H(S.e0).V(L.INT, 2);
		t = t/**/						.N(S.nest).H(S.e0).V(L.INT, 3).HN0().N0();
		t = t/**/				.N(S.nest).H(S.e0).V(L.INT, 4).HN0().N0().N0();
		t = Parse(@"
			1
						2
								3
					4
				5");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nestr).H(S.nest).H(S.e0).V(L.INT, 2);
		t = t/**/						.N(S.nestr).H(S.nest).H(S.e0).V(L.INT, 3).HN0().N0().N0();
		t = t/**/				.N(S.nest).H(S.e0).V(L.INT, 4).HN0().N0();
		t = t/**/		.N(S.nest).H(S.e0).V(L.INT, 5).HN0().N0().N0();
		t = t/**/.N(null, -1, 5.1, 5.6, L.INDR).HN0();
	}

	[TestMethod]
	public void Binary1()
	{
		var t = Parse(@"1+2*3-4");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.e56, v: L.ADD).H(S.e0).V(L.INT, 2);
		t = t/**/					.N(S.e53, v: L.MUL).H(S.e0).V(L.INT, 3).HN0().N0();
		t = t/**/		.N(S.e56, v: L.SUB).H(S.e0).V(L.INT, 4).HN0().N0().N0();
		t = Parse(@"1+2*3-4");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.e56, v: L.ADD).H(S.e0).V(L.INT, 2);
		t = t/**/					.N(S.e53, v: L.MUL).H(S.e0).V(L.INT, 3).HN0().N0();
		t = t/**/		.N(S.e56, v: L.SUB).H(S.e0).V(L.INT, 4).HN0().N0().N0();
	}

	[TestMethod]
	public void BinaryNested()
	{
		var t = Parse(@"
			1
				-2
				*3+4");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest, v: L.SUB).H(S.e0).V(L.INT, 2).HN0();
		t = t/**/		.N(S.nest, v: L.MUL).H(S.e0).V(L.INT, 3);
		t = t/**/					.N(S.e56, v: L.ADD).H(S.e0).V(L.INT, 4).HN0().N0().N0().N0().N0();
		t = Parse(@"
			1
				+ 2
					* 3
					/ 4
						+5
			6
				- 7");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest, v: L.ADD).H(S.e0).V(L.INT, 2);
		t = t/**/						.N(S.nest, v: L.MUL).H(S.e0).V(L.INT, 3).HN0();
		t = t/**/						.N(S.nest, v: L.DIV).H(S.e0).V(L.INT, 4);
		t = t/**/									.N(S.nest, v: L.ADD);
		t = t/**/										.H(S.e0).V(L.INT, 5).HN0().N0().N0().N0();
		t = t/**/	.N(B).H(S.e0).V(L.INT, 6);
		t = t/**/		.N(S.nest, v: L.SUB);
		t = t/**/			.H(S.e0).V(L.INT, 7).HN0().N0().N0().N0();
	}

	[TestMethod]
	public void Prefix()
	{
		var t = Parse(@"1- -2");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.e56, v: L.SUB);
		t = t/**/			.H(S.e2, v: L.SUB).H(S.e0).V(L.INT, 2).HN0().N0().N0().N0();
		t = Parse(@"1--2");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.feed);
		t = t/**/			.H(S.f2p, v: L.NOTB).H(S.e0).V(L.INT, 2).HN0().N0().N0().N0();
		t = t/**/.N(null, -1, 1.2, 1.4, L.NOTB).HN0();
	}

	[TestMethod]
	public void PrefixNested()
	{
		var t = Parse(@"
			1
				--2");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest).H(S.e2, v: L.NOTB);
		t = t/**/				.H(S.e0).V(L.INT, 2).HN0().N0().N0().N0();
		t = Parse(@"
			1
				- -2
					*!3
				/+4");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest, v: L.SUB);
		t = t/**/			.H(S.e2, v: L.SUB).H(S.e0).V(L.INT, 2).HN0();
		t = t/**/			.N(S.nest, v: L.MUL).H(S.e2, v: L.NOT).H(S.e0).V(L.INT, 3).HN0().N0().N0();
		t = t/**/		.N(S.nest, v: L.DIV);
		t = t/**/			.H(S.e2, v: L.ADD).H(S.e0).V(L.INT, 4).HN0().N0().N0().N0();
	}

	[TestMethod]
	public void RecoverNested()
	{
		var t = Parse(@"
			1
				)
				2");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest).H(S.line, -4).HN0();
		t = t/**/		.N(S.nest).H(S.e0).V(L.INT, 2).HN0().N0().N0();
		t = t/**/.N(null, -1, 3.5, 3.6, L.RP);
		t = t/**/	.H(S.nests, 1).N(S.line, 1).HN0().N0();
	}

	[TestMethod]
	public void RecoverBinNest()
	{
		var t = Parse(@"
			1
				*");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest, v: L.MUL);
		t = t/**/			.H(S.line, -4).HN0().N0().N0();
		t = t/**/.N(null, -1, 3.6, 3.6, L.EOL).H(S.nest, 1).HP0().N0();
		t = Parse(@"
			1
				+
					2
					*3
				/ 4
			-
				5");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest, v: L.ADD).H(S.line, -4);
		t = t/**/							.N(S.nest).H(S.e0).V(L.INT, 2).HN0();
		t = t/**/							.N(S.nest, v: L.MUL).H(S.e0).V(L.INT, 3).HN0().N0();
		t = t/**/		.N(S.nest, v: L.DIV).H(S.e0).V(L.INT, 4).HN0().N0();
		t = t/**/	.N(B).H(S.line, -4);
		t = t/**/		.N(S.nest).H(S.e0).V(L.INT, 5).HN0().N0().N0();
		t = t/**/.N(null, -1, 3.6, 4.1, L.EOL).H(S.nest, 1).HP0();
		t = t/**/.N(null, -1, 7.5, 8.1, L.EOL).H(S.e2, 1).HP0().N0();
	}

	[TestMethod]
	public void RecoverLine()
	{
		var t = Parse(@"
			1 *>
						2");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1).N(S.line, -4);
		t = t/**/		.N(S.nestr).H(S.nest).H(S.e0).V(L.INT, 2).HN0().N0().N0().N0();
		t = t/**/.N(null, -1, 2.7, 2.8, L.GT);
		t = t/**/	.H(S.e53, 1).HN0().N0();
	}

	[TestMethod]
	public void RecoverExp()
	{
		var t = Parse(@"
			1
				!3/
					-4-
				5");
		t = t/**/	.H(B).H(S.e0).V(L.INT, 1);
		t = t/**/		.N(S.nest).H(S.e2, v: L.NOT).H(S.e0).V(L.INT, 3).HN0();
		t = t/**/				.N(S.line, -4).H0();
		t = t/**/				.N(S.nest).H(S.e2, v: L.SUB).H(S.e0).V(L.INT, 4).HN0();
		t = t/**/						.N(S.line, -4).HN0().N0();
		t = t/**/		.N(S.nest).H(S.e0).V(L.INT, 5).HN0().N0().N0();
		t = t/**/.N(null, -1, 3.8, 4.1, L.EOL).H(S.e53, 1).HN0();
		t = t/**/.N(null, -1, 4.9, 5.1, L.EOL).H(S.e56, 1).HN0().N0();
	}
}
