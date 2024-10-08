//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
#pragma warning disable IDE0059 // Unnecessary assignment
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using qutum.syntax;
using System;
using System.Linq;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax;

using L = Lex;
using S = Syn;
using Ser = (Synt t, Synter s);

file static class Extension
{
	internal struct Dec()
	{
		internal decimal d;
		public static implicit operator Dec(decimal d) => new() { d = d };
		public static implicit operator Dec(double d) => new() { d = (decimal)d };
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0305:Simplify collection initialization")]
	public static Ser Eq(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
	{
		AreNotEqual(null, s.t);
		AreEqual(err, s.t.err);
		if (name != null) AreEqual(name, s.t.name);
		var (line, col) = s.s.ler.LineCol(s.t.j);
		if (j.on != null) AreEqual($"{j.on?.d}", $"{line.on}.{col.on}");
		if (j.via != null) AreEqual($"{j.via?.d}", $"{line.to}.{col.via}");
		if (err != 0 && d is string aim && s.t.info?.ToString() is string test) {
			var ts = test.Split(SerMaker<char, string>.ErrMore);
			var As = aim.Split("  ");
			if ((As.Length < 3 ? ts.Length != As.Length : ts.Length < As.Length)
				|| !As.Zip(ts).All(ea => ea.First.Split(' ').ToCount().ToHashSet()
					.IsSubsetOf(ea.Second.Split(' ').ToCount().ToHashSet())))
				Fail($"Expected Error <{aim}> Actual <{test.Replace("\n", "  ")}>");
		}
		else if (d is (L key, object value))
			D(s, key, value);
		else if (d != null)
			AreEqual(d, d is L && s.t.info is Lexi<L> l ? l.key : s.t.info);
		return s;
	}
	public static Ser D(this Ser s, params object[] Ds)
	{
		var ds = new Lexi<L>[Ds.Length];
		int n = 0;
		for (int x = 0; x < Ds.Length; x++)
			if (Ds[x] is L l)
				ds[n++] = new() { key = l };
			else
				ds[n - 1].value = Ds[x];
		AreEqual(s.s.dumper(ds.Seg((0, n))),
			s.s.dumper(s.t.j.on >= 0 ? s.s.ler.Lexs(s.t.j)
				: s.s.ler.errs.GetRange(~s.t.j.on, -s.t.j.size).ToArray().Seg()));
		return s;
	}

	public static Ser h(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> (s.t.head, s.s).Eq(name, d, j, err).Vine();
	public static Ser t(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> (s.t.tail, s.s).Eq(name, d, j, err).Vine();
	public static Ser n(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> (s.t.next, s.s).Eq(name, d, j, err).Vine();
	public static Ser p(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> (s.t.prev, s.s).Eq(name, d, j, err).Vine();

	public static Ser H(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> (s.t.head, s.s).Eq(name, d, j, err).Leaf();
	public static Ser T(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> (s.t.tail, s.s).Eq(name, d, j, err).Leaf();
	public static Ser N(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> (s.t.next, s.s).Eq(name, d, j, err).Leaf();
	public static Ser P(this Ser s,
		S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> (s.t.prev, s.s).Eq(name, d, j, err).Leaf();

	public static Ser Vine(this Ser s) { AreNotEqual(null, s.t.head); return s; }
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

	public static Ser e(this Ser s, int err = -1) => n(s, err: err);

	public static Ser nr(this Ser s) => s.n(S.nestr);
	public static Ser j(this Ser s, object d = null, Jov<Dec?> j = default, int err = 0)
		=> s.n(S.junc, d, j, err).h(S.block);
}

[TestClass]
public class TestSynter : IDisposable
{
	readonly EnvWriter env = EnvWriter.Use();

	public void Dispose() => env.Dispose();

	readonly Synter ser = new Synter().Begin(new());

	Ser Parse(string read)
	{
		env.WriteLine(read);
		ser.ler.Dispose();
		ser.ler.Begin(new LerByte(Encoding.UTF8.GetBytes(read)));
		var t = ser.Parse();
		if (ser.dump >= 4)
			env.WriteLine(ser.ler.Dumper(false));
		return (t.Dump(ser.Dumper), ser);
	}
	public const S B = S.block, N = S.nest, J = S.junc, E = S.exp, P = S.phr, I = S.inp;

	[TestMethod]
	public void Block()
	{
		var t = Parse("").Eq(S.qutum).U();
		t = Parse("""
			1
			2
			 \##\ 3
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1)).u();
		t = t/**/	.n(B).H(P, (L.INT, 2)).u();
		t = t/**/	.n(B).H(P, (L.INT, 3)).uuU();
	}

	[TestMethod]
	public void BlockRecovery()
	{
		var t = Parse("""
					1
				\##\ 2
			""");
		t = t/**/	.h(B).H(S.sen, err: 1);
		t = t/**/		.nr().h(N).H(P, (L.INT, 1)).uu();
		t = t/**/		.n(N).H(P, (L.INT, 2)).uuu();
		t = t/**/.e().H(null, "block sentence", (1.1, 1.1), -1).uU();
	}

	[TestMethod]
	public void Junction()
	{
		var t = Parse("""
			1
			+ 2
			/ 3
			4
			5
			*6
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.j(L.ADD).H(P, (L.INT, 2)).uu();
		t = t/**/		.j(L.DIV).H(P, (L.INT, 3)).uuu();
		t = t/**/	.n(B).H(P, (L.INT, 4)).u();
		t = t/**/	.n(B).H(P, (L.INT, 5));
		t = t/**/		.j(L.MUL).H(P, (L.INT, 6)).uuuuU();
	}

	[TestMethod]
	public void JunctionRecover()
	{
		var t = Parse("""
			+ 2
			/ 3
			4
			""");
		t = t/**/	.h(B).H(S.sen, err: 1);
		t = t/**/		.j(L.ADD).H(P, (L.INT, 2)).uu();
		t = t/**/		.j(L.DIV).H(P, (L.INT, 3)).uuu();
		t = t/**/	.n(B).H(P, (L.INT, 4)).uu();
		t = t/**/.e().H(null, "block sentence", (1.1, 1.1), -1).uU();
	}

	[TestMethod]
	public void NestBlock()
	{
		var t = Parse("""
			1
				2
					3
				4
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(N).H(P, (L.INT, 2));
		t = t/**/			.n(N).H(P, (L.INT, 3)).uu();
		t = t/**/		.n(N).H(P, (L.INT, 4)).uuuU();
		t = Parse("""
			1
				2
				3
					4
						5
					6
			7
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(N).H(P, (L.INT, 2)).u();
		t = t/**/		.n(N).H(P, (L.INT, 3));
		t = t/**/			.n(N).H(P, (L.INT, 4));
		t = t/**/				.n(N).H(P, (L.INT, 5)).uu();
		t = t/**/			.n(N).H(P, (L.INT, 6)).uuu();
		t = t/**/	.n(B).H(P, (L.INT, 7)).uuU();
	}

	[TestMethod]
	public void NestJunc()
	{
		var t = Parse("""
			1
				2
				/ 3
				4
			5
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(N).H(P, (L.INT, 2));
		t = t/**/			.j(L.DIV).H(P, (L.INT, 3)).uuu();
		t = t/**/		.n(N).H(P, (L.INT, 4)).uu();
		t = t/**/	.n(B).H(P, (L.INT, 5)).uuU();
		t = Parse("""
			1
				2
				/ 3
				4
			+	-5
					6
				* 7
				8
			9
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(N).H(P, (L.INT, 2));
		t = t/**/			.j(L.DIV).H(P, (L.INT, 3)).uuu();
		t = t/**/		.n(N).H(P, (L.INT, 4)).u();
		t = t/**/		.j(L.ADD).h(E, L.NEGA).H(P, (L.INT, 5)).u();
		t = t/**/				.n(N).H(P, (L.INT, 6)).u();
		t = t/**/				.j(L.MUL).H(P, (L.INT, 7)).uuu();
		t = t/**/			.n(B).H(P, (L.INT, 8)).uuu();
		t = t/**/	.n(B).H(P, (L.INT, 9)).uuU();
	}

	[TestMethod]
	public void NestRecover()
	{
		var t = Parse("""
			1
				)
					]
				2
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(N).H(S.sen, err: 1);
		t = t/**/			.n(N).H(S.sen, err: 1).uu();
		t = t/**/		.n(N).H(P, (L.INT, 2)).uuu();
		t = t/**/.e().H(null, "sentence expression", (2.2, 2.3), -1);
		t = t/**/	.N(null, "sentence expression", (3.3, 3.4), -1).uU();
		t = Parse("""
			1
				-
						2
				3
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(N).H(S.sen, err: 1);
		t = t/**/			.j(L.SUB).H(S.sen, err: 1);
		t = t/**/					.n(N).H(P, (L.INT, 2)).uuuu();
		t = t/**/		.n(N).H(P, (L.INT, 3)).uuu();
		t = t/**/.e().H(null, "sentence expression", (2.2, 2.2), -1);
		t = t/**/	.N(null, "junction block", (3.1, 3.1), -1).uU();
	}

	[TestMethod]
	public void NestRight()
	{
		var t = Parse("""
			1
					2
						3
					4
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.nr().h(N).H(P, (L.INT, 2));
		t = t/**/				.n(N).H(P, (L.INT, 3)).uu();
		t = t/**/			.n(N).H(P, (L.INT, 4)).uuuuU();
		t = Parse("""
			1
						2
								3
					4
				5
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.nr().h(N).H(P, (L.INT, 2));
		t = t/**/				.nr().h(N).H(P, (L.INT, 3)).uuu();
		t = t/**/			.n(N).H(P, (L.INT, 4)).uu();
		t = t/**/		.n(N).H(P, (L.INT, 5)).uuu();
		t = t/**/.e(-3).H(null, L.INDR, (4.1, 4.3), -3).uU();
	}

	[TestMethod]
	public void NestRightJunc()
	{
		var t = Parse("""
			1
					2
					/ 3
					4
				5
				*6
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.nr().h(N).H(P, (L.INT, 2));
		t = t/**/				.j(L.DIV).H(P, (L.INT, 3)).uuu();
		t = t/**/			.n(N).H(P, (L.INT, 4)).uu();
		t = t/**/		.n(N).H(P, (L.INT, 5));
		t = t/**/			.j(L.MUL).H(P, (L.INT, 6)).uuuuuU();
		t = Parse("""
			1
			+	a
						2
						/ 3
						4
					5
					*6
				/7
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.j(L.ADD).H(P, (L.NAME, "a"));
		t = t/**/				.nr().h(N).H(P, (L.INT, 2));
		t = t/**/						.j(L.DIV).H(P, (L.INT, 3)).uuu();
		t = t/**/					.n(N).H(P, (L.INT, 4)).uu();
		t = t/**/				.n(N).H(P, (L.INT, 5));
		t = t/**/					.j(L.MUL).H(P, (L.INT, 6)).uuu();
		t = t/**/				.j(L.DIV).H(P, (L.INT, 7)).uu().uuuuU();
	}

	[TestMethod]
	public void LineRecover()
	{
		var t = Parse("""
			1 *>
						2
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1)).N(S.sen, err: 1);
		t = t/**/		.nr().h(N).H(P, (L.INT, 2)).uuuu();
		t = t/**/.e().H(null, "arithmetic expression", (1.4, 1.5), -1).uU();
	}

	[TestMethod]
	public void Prefix()
	{
		var t = Parse(@"1- -2");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(E, L.SUB);
		t = t/**/			.h(E, L.NEGA).H(P, (L.INT, 2)).uuuuU();
		t = Parse(@"1--2");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(I).h(E, L.NOTB).H(P, (L.INT, 2)).uuuu();
		t = t/**/.e(-3).H(null, L.NOTB, (1.2, 1.4), -3).uU();
	}

	[TestMethod]
	public void PrefixJunc()
	{
		var t = Parse("""
			1
				+2
			--3
			- -4
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(N).h(E, L.POSI).H(P, (L.INT, 2)).uuu();
		t = t/**/	.n(B).h(E, L.NOTB).H(P, (L.INT, 3)).u();
		t = t/**/		.j(L.SUB).h(E, L.NEGA).H(P, (L.INT, 4)).uuuuuU();
	}

	[TestMethod]
	public void Postfix()
	{
		var t = Parse(@"0 == a, 1 * 2, 3, .d / 5, c .");
		t = t/**/	.h(B).H(P, (L.INT, "0")).n(E, L.EQ);
		t = t/**/			.H(P, (L.NAME, "a"));
		t = t/**/			.n(I).H(P, (L.INT, 1)).n(E, L.MUL).H(P, (L.INT, 2)).uu();
		t = t/**/			.n(I).H(P, (L.INT, 3)).u();
		t = t/**/			.N(P, (L.RUN, "d"));
		t = t/**/			.n(E, L.DIV).H(P, (L.INT, 5));
		t = t/**/						.n(I).H(P, (L.NAME, "c")).N(P, L.RUN).uuuuuU();
		t = Parse(@"0 << a 1 * 2 3. 4 .d / 5 c .");
		t = t/**/	.h(B).H(P, (L.INT, "0"));
		t = t/**/		.n(E, L.SHL).H(P, (L.NAME, "a")).n(I).H(P, (L.INT, 1)).uu();
		t = t/**/		.n(E, L.MUL).H(P, (L.INT, 2));
		t = t/**/					.n(I).H(P, (L.INT, 3)).N(P, (L.RUND, "")).u();
		t = t/**/					.n(I).H(P, (L.INT, 4)).u();
		t = t/**/					.N(P, L.RUN).u();
		t = t/**/		.n(E, L.DIV).H(P, (L.INT, 5)).n(I).H(P, (L.NAME, "c")).u();
		t = t/**/					.N(P, L.RUN).uuuU();
		t = Parse(@"0 << a,1 * 2 3. 4 .d / 5 c .");
		t = t/**/	.h(B).H(P, (L.INT, "0"));
		t = t/**/		.n(E, L.SHL).H(P, (L.NAME, "a"));
		t = t/**/					.n(I).H(P, (L.INT, 1));
		t = t/**/					.n(E, L.MUL).H(P, (L.INT, 2));
		t = t/**/								.n(I).H(P, (L.INT, 3)).N(P, (L.RUND, "")).u();
		t = t/**/								.n(I).H(P, (L.INT, 4)).u();
		t = t/**/								.N(P, L.RUN).u();
		t = t/**/					.n(E, L.DIV).H(P, (L.INT, 5)).n(I).H(P, (L.NAME, "c")).u();
		t = t/**/								.N(P, L.RUN).uuuuuU();
	}

	[TestMethod]
	public void PostfixJunc()
	{
		var t = Parse("""
			1
			.a. .b
				2
			. 3
				. 4
				5
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(J).H(S.jpost, (L.RUN, "a"));
		t = t/**/			.N(S.jpost, (L.RUN, "")).N(S.jpost, (L.RUN, "b"));
		t = t/**/			.n(B).H(P, (L.INT, 2)).uu();
		t = t/**/		.n(J).H(S.jpost, (L.RUN, ""));
		t = t/**/			.n(B).H(P, (L.INT, 3));
		t = t/**/				.n(J).H(S.jpost, (L.RUN, ""));
		t = t/**/					.n(B).H(P, (L.INT, 4)).uuu();
		t = t/**/			.n(B).H(P, (L.INT, 5)).uuuuU();
	}

	[TestMethod]
	public void PostfixJuncRecover()
	{
		var t = Parse("""
			1
			.a. .b + 2
				3
				.c
					.
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(J).H(S.jpost, (L.RUN, "a"));
		t = t/**/			.N(S.jpost, (L.RUN, "")).N(S.jpost, (L.RUN, "b"));
		t = t/**/			.n(B).H(S.sen, err: 1).u();
		t = t/**/			.n(B).H(P, (L.INT, 3));
		t = t/**/				.n(J).H(S.jpost, (L.RUN, "c"));
		t = t/**/					.n(B).H(S.sen, err: 1);
		t = t/**/						.n(J).H(S.jpost, (L.RUN, "")).uuuu().uuu();
		t = t/**/.e().H(null, "block sentence", (2.8, 2.9), -1);
		t = t/**/	.N(null, "block sentence", (5.3, 5.3), -1).uU();
	}

	[TestMethod]
	public void Binary()
	{
		var t = Parse(@"1+2*3-4");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(E, L.ADD).H(P, (L.INT, 2));
		t = t/**/					.n(E, L.MUL).H(P, (L.INT, 3)).uu();
		t = t/**/		.n(E, L.SUB).H(P, (L.INT, 4)).uuuU();
		t = Parse(@"1 + 2 * 3 >> 4 % 5 < 6 != 7 ++ 8");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(E, L.ADD).H(P, (L.INT, 2));
		t = t/**/			.n(E, L.MUL).H(P, (L.INT, 3));
		t = t/**/				.n(E, L.SHR).H(P, (L.INT, 4)).uu();
		t = t/**/			.n(E, L.MOD).H(P, (L.INT, 5)).uu();
		t = t/**/		.n(E, L.LT).H(P, (L.INT, 6)).u();
		t = t/**/		.n(E, L.XOR).H(P, (L.INT, 7));
		t = t/**/			.n(E, L.XORB).H(P, (L.INT, 8)).uuuuU();
	}

	[TestMethod]
	public void BinaryJunc()
	{
		var t = Parse("""
			1
			- 2
			*3+4
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.j(L.SUB).H(P, (L.INT, 2)).uu();
		t = t/**/		.j(L.MUL).H(P, (L.INT, 3));
		t = t/**/					.n(E, L.ADD).H(P, (L.INT, 4)).uuuuuU();
		t = Parse("""
			1
			*	2
				,	3
					/ 4
				%	5
			&&	6
			7
			-	8
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.j(L.MUL).H(P, (L.INT, 2));
		t = t/**/				.j(L.INP).H(P, (L.INT, 3));
		t = t/**/						.j(L.DIV).H(P, (L.INT, 4)).uuuu();
		t = t/**/				.j(L.MOD).H(P, (L.INT, 5)).uuuu();
		t = t/**/		.j(L.ANDB).H(P, (L.INT, 6)).uuu();
		t = t/**/	.n(B).H(P, (L.INT, 7));
		t = t/**/		.j(L.SUB).H(P, (L.INT, 8)).uuuuU();
	}

	[TestMethod]
	public void BinaryJuncRecover()
	{
		var t = Parse("""
			1
			*
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.N(J, err: 1).uu();
		t = t/**/.e().H(null, "junction block", (2.2, 2.2), -1).uU();
		t = Parse("""
			1
				+
					*2/
					3
				/ 4-
			-
				!&5
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(N).H(S.sen, err: 1);
		t = t/**/			.j(L.ADD).H(S.sen, err: 1);
		t = t/**/					.j(L.MUL).H(P, (L.INT, 2)).N(S.sen, err: 1).uuu();
		t = t/**/				.n(B).H(P, (L.INT, 3)).uu();
		t = t/**/			.j(L.DIV).H(P, (L.INT, 4)).N(S.sen, err: 1).uuu();
		t = t/**/		.j(L.SUB).H(S.sen, err: 1).uuuu();
		t = t/**/.e().H(null, "sentence expression", (2.2, 2.2), -1);
		t = t/**/	.N(null, "junction block", (3.3, 3.3), -1);
		t = t/**/	.N(null, "arithmetic operator expression", (3.6, 4.1), -1);
		t = t/**/	.N(null, "arithmetic operator expression", (5.6, 6.1), -1);
		t = t/**/	.N(null, "prefix operator expression", (7.3, 7.4), -1).uU();
	}

	[TestMethod]
	public void Parenth()
	{
		var t = Parse(@"(1)");
		t = t/**/	.h(B).H(P, (L.INT, 1)).uuU();
		t = Parse(@"-(-1)/(2*(3+4))");
		t = t/**/	.h(B).h(E, L.NEGA).h(E, L.NEGA).H(P, (L.INT, 1)).uu();
		t = t/**/		.n(E, L.DIV).H(P, (L.INT, 2));
		t = t/**/			.n(E, L.MUL).H(P, (L.INT, 3));
		t = t/**/				.n(E, L.ADD).H(P, (L.INT, 4)).uuuuuU();
	}

	[TestMethod]
	public void ParenthRecover()
	{
		var t = Parse(@"(1");
		t = t/**/	.h(B).H(P, (L.INT, 1)).N(S.phr, err: 1).uu();
		t = t/**/.e().H(null, "parenth RP", (1.3, 1.3), -1).uU();
		t = Parse(@"-(-1/(2+)3)/)");
		t = t/**/	.h(B).h(E, L.NEGA).h(E, L.NEGA).H(P, (L.INT, 1)).u();
		t = t/**/					.n(E, L.DIV).H(P, (L.INT, 2)).N(S.phr, err: 1);
		t = t/**/								.n(I).H(P, (L.INT, 3)).uuu();
		t = t/**/		.N(S.sen, err: 1).uu();
		t = t/**/.e().H(S.phr, "arithmetic expression", (1.9, 1.10m), -1);
		t = t/**/	.N(S.sen, "arithmetic expression", (1.13, 1.14), -1).uU();
		t = Parse(@"( 1*[ 2 / [(3+4] - 5");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.n(E, L.MUL).h(S.squ, err: 1);
		t = t/**/				.H(P, (L.INT, 2));
		t = t/**/				.n(E, L.DIV).h(S.squ).H(P, (L.INT, 3));
		t = t/**/									.n(E, L.ADD).H(P, (L.INT, 4)).u();
		t = t/**/									.N(P, err: 1).uu();
		t = t/**/				.n(E, L.SUB).H(P, (L.INT, 5)).uuu();
		t = t/**/		.N(P, err: 1).uu();
		t = t/**/.e().H(null, "parenth RP", (1.16, 1.16), -1);
		t = t/**/	.N(null, "square bracket RSB", (1.21, 1.21), -1);
		t = t/**/	.N(null, "parenth RP", (1.21, 1.21), -1).uU();
	}

	[TestMethod]
	public void BracketJunc()
	{
		var t = Parse("""
			1
			[2]
				3
				*4
				5
			[6]
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.j(L.LSB).H(P, (L.INT, 2)).u();
		t = t/**/			.n(B).H(P, (L.INT, 3));
		t = t/**/				.j(L.MUL).H(P, (L.INT, 4)).uuu();
		t = t/**/			.n(B).H(P, (L.INT, 5)).uu();
		t = t/**/		.j(L.LSB).H(P, (L.INT, 6)).uuuuU();
	}

	[TestMethod]
	public void BracketJuncRecover()
	{
		var t = Parse("""
			1
			[
			[]
			2
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.j(L.LSB).H(S.Jsqu, err: 1).uu();
		t = t/**/		.j(L.LSB).H(S.Jsqu, err: 1).uuu();
		t = t/**/	.n(B).H(P, (L.INT, 2)).uu();
		t = t/**/.e().H(null, "bracket junction expression", (2.2, 3.1), -1);
		t = t/**/	.N(null, "bracket junction expression", (3.2, 3.3), -1).uU();
		t = Parse("""
			1
			[2] 3
				[4] /5
			[6
				[] 7
				8
			""");
		t = t/**/	.h(B).H(P, (L.INT, 1));
		t = t/**/		.j(L.LSB).H(P, (L.INT, 2));
		t = t/**/				.N(S.Jsqu, err: 1);
		t = t/**/				.j(L.LSB).H(P, (L.INT, 4));
		t = t/**/						.N(S.Jsqu, err: 1).uuuu();
		t = t/**/		.j(L.LSB).H(P, (L.INT, 6)).N(S.Jsqu, err: 1);
		t = t/**/				.j(L.LSB).H(S.Jsqu, err: 1).uuu();
		t = t/**/			.n(B).H(P, (L.INT, 8)).uuuu();
		t = t/**/.e().H(null, "bracket junction EOL", (2.5, 2.6), -1);
		t = t/**/	.N(null, "bracket junction EOL", (3.6, 3.7), -1);
		t = t/**/	.N(null, "bracket junction RSB", (4.3, 5.1), -1);
		t = t/**/	.N(null, "bracket junction expression", (5.3, 5.4), -1).uU();
	}

	[TestMethod]
	public void Input1()
	{
		var t = Parse(@"a,1,2");
		t = t/**/	.h(B).H(P, (L.NAME, "a"));
		t = t/**/		.n(I).H(P, (L.INT, 1)).u().n(I).H(P, (L.INT, 2)).uuuU();
		t = Parse(@"a, 1 * 2");
		t = t/**/	.h(B).H(P, (L.NAME, "a"));
		t = t/**/		.n(I).H(P, (L.INT, 1)).n(E, L.MUL).H(P, (L.INT, 2)).uuuuU();
		t = Parse(@"0 * a, 1 * 2, 3, 4 == 5");
		t = t/**/	.h(B).H(P, (L.INT, "0")).n(E, L.MUL);
		t = t/**/			.H(P, (L.NAME, "a"));
		t = t/**/			.n(I).H(P, (L.INT, 1)).n(E, L.MUL).H(P, (L.INT, 2)).uu();
		t = t/**/			.n(I).H(P, (L.INT, 3)).u();
		t = t/**/			.n(I).H(P, (L.INT, 4)).n(E, L.EQ).H(P, (L.INT, 5)).uuuuuU();
	}

	[TestMethod]
	public void Input2()
	{
		var t = Parse(@"a 1 2");
		t = t/**/	.h(B).H(P, (L.NAME, "a"));
		t = t/**/		.n(I).H(P, (L.INT, 1)).u().n(I).H(P, (L.INT, 2)).uuuU();
		t = Parse(@"a 1 * 2");
		t = t/**/	.h(B).H(P, (L.NAME, "a"));
		t = t/**/		.n(I).H(P, (L.INT, 1)).u().n(E, L.MUL).H(P, (L.INT, 2)).uuuU();
		t = Parse(@"0 * a 1 * 2 3 4 == 5");
		t = t/**/	.h(B).H(P, (L.INT, "0"));
		t = t/**/		.n(E, L.MUL).H(P, (L.NAME, "a")).n(I).H(P, (L.INT, 1)).uu();
		t = t/**/		.n(E, L.MUL).H(P, (L.INT, 2));
		t = t/**/					.n(I).H(P, (L.INT, 3)).u().n(I).H(P, (L.INT, 4)).uu();
		t = t/**/		.n(E, L.EQ).H(P, (L.INT, 5)).uuuU();
	}

	[TestMethod]
	public void Input3()
	{
		var t = Parse(@"a 1,2 3");
		t = t/**/	.h(B).H(P, (L.NAME, "a"));
		t = t/**/		.n(I).H(P, (L.INT, 1)).u();
		t = t/**/		.n(I).H(P, (L.INT, 2)).n(I).H(P, (L.INT, 3)).uuuuU();
		t = Parse(@"a,1 2,3");
		t = t/**/	.h(B).H(P, (L.NAME, "a"));
		t = t/**/		.n(I).H(P, (L.INT, 1)).n(I).H(P, (L.INT, 2)).uu();
		t = t/**/		.n(I).H(P, (L.INT, 3)).uuuU();
		t = Parse(@"0 * a 1 2,3 * b 4,5");
		t = t/**/	.h(B).H(P, (L.INT, "0")).n(E, L.MUL);
		t = t/**/			.H(P, (L.NAME, "a"));
		t = t/**/			.n(I).H(P, (L.INT, 1)).u().n(I).H(P, (L.INT, 2)).u();
		t = t/**/			.n(I).H(P, (L.INT, 3)).n(E, L.MUL);
		t = t/**/						.H(P, (L.NAME, "b")).n(I).H(P, (L.INT, 4)).uuu();
		t = t/**/			.n(I).H(P, (L.INT, 5)).uuuuU();
	}

	[TestMethod]
	public void Input4()
	{
		var t = Parse(@"a,");
		t = t/**/	.h(B).H(P, (L.NAME, "a")).uuU();
		t = Parse(@"a,1,");
		t = t/**/	.h(B).H(P, (L.NAME, "a")).n(I).H(P, (L.INT, 1)).uuuU();
		t = Parse(@"a 1,");
		t = t/**/	.h(B).H(P, (L.NAME, "a")).n(I).H(P, (L.INT, 1)).uuuU();
		t = Parse(@"(0 * (a) 1,2 3,)");
		t = t/**/	.h(B).H(P, (L.INT, "0")).n(E, L.MUL);
		t = t/**/			.H(P, (L.NAME, "a"));
		t = t/**/			.n(I).H(P, (L.INT, 1)).u();
		t = t/**/			.n(I).H(P, (L.INT, 2)).n(I).H(P, (L.INT, 3)).uuuuuU();
	}
}
