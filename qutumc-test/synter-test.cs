//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
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

public readonly struct Ser(Synt t, Synter s)

{
	public struct Dec()
	{
		internal decimal d;
		public static implicit operator Dec(decimal d) => new() { d = d };
		public static implicit operator Dec(double d) => new() { d = (decimal)d };
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0305:Simplify collection initialization")]
	public readonly Ser Eq(S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
	{
		AreNotEqual(null, t);
		AreEqual(err, t.err);
		if (name != null) AreEqual(name, t.name);
		var (line, col) = s.ler.LineCol(t.j);
		if (j.on != null) AreEqual($"{j.on?.d}", $"{line.on}.{col.on}");
		if (j.via != null) AreEqual($"{j.via?.d}", $"{line.to}.{col.via}");
		if (err != 0 && d is string aim && t.info?.ToString() is string test) {
			var ts = test.Split(SerMaker<char, string>.ErrMore);
			var As = aim.Split("  ");
			if ((As.Length < 3 ? ts.Length != As.Length : ts.Length < As.Length)
				|| !As.Zip(ts).All(ea => ea.First.Split(' ').ToCount().ToHashSet()
					.IsSubsetOf(ea.Second.Split(' ').ToCount().ToHashSet())))
				Fail($"Expected Error <{aim}> Actual <{test.Replace("\n", "  ")}>");
		}
		else if (d is (L key, object value))
			D(key, value);
		else if (d != null)
			AreEqual(d, d is L && t.info is Lexi<L> l ? l.key : t.info);
		return this;
	}
	public Ser D(params object[] Ds)
	{
		var ds = new Lexi<L>[Ds.Length];
		int n = 0;
		for (int x = 0; x < Ds.Length; x++)
			if (Ds[x] is L l)
				ds[n++] = new() { key = l };
			else
				ds[n - 1].value = Ds[x];
		AreEqual(s.dumper(ds.Seg((0, n))),
			s.dumper(t.j.on >= 0 ? s.ler.Lexs(t.j)
				: s.ler.errs.GetRange(~t.j.on, -t.j.size).ToArray().Seg()));
		return this;
	}

	public Ser h(S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> new Ser(t.head, s).Eq(name, d, j, err).Vine;
	public Ser n(S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> new Ser(t.next, s).Eq(name, d, j, err).Vine;

	public Ser H(S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> new Ser(t.head, s).Eq(name, d, j, err).Leaf;
	public Ser N(S? name = null, object d = null, Jov<Dec?> j = default, int err = 0)
		=> new Ser(t.next, s).Eq(name, d, j, err).Leaf;

	public Ser Vine { get { AreNotEqual(null, t.head); return this; } }
	public Ser Leaf { get { AreEqual(null, t.head); return this; } }
	public Ser u { get { AreEqual(null, t.next); AreNotEqual(null, t.up); return new Ser(t.up, s); } }
	public Ser U { get { AreEqual(null, t.next); AreEqual(null, t.up); return new Ser(t.up, s); } }
	public Ser uu => u.u;
	public Ser uU => u.U;
	public Ser uuu => u.u.u;
	public Ser uuU => u.u.U;
	public Ser uuuu => u.u.u.u;
	public Ser uuuU => u.u.u.U;
	public Ser uuuuU => u.u.u.u.U;
	public Ser uuuuuU => u.u.u.u.u.U;

	public Ser e(int err = -1) => n(err: err);

	public Ser j(object d = null, Jov<Dec?> j = default, int err = 0)
		=> n(S.junc, d, j, err).h(S.block);
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
		return new(t.Dump(ser.Dumper), ser);
	}
	public const S B = S.block, N = S.nook, Nr = S.nookr, J = S.junc,
					E = S.exp, P = S.phr, I = S.inp;

	[TestMethod]
	public void Block()
	{
		var _ = Parse("").Eq(S.qutum).U;
		_ = Parse("""
			1
			2
			 \##\ 3
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1)).u;
		_ = _/**/	.n(B).H(P, (L.INT, 2)).u;
		_ = _/**/	.n(B).H(P, (L.INT, 3)).uuU;
	}

	[TestMethod]
	public void BlockRecovery()
	{
		var _ = Parse("""
					1
				\##\ 2
			""");
		_ = _/**/	.h(B).H(S.sen, err: 1);
		_ = _/**/		.n(Nr).h(N).H(P, (L.INT, 1)).uu;
		_ = _/**/		.n(N).H(P, (L.INT, 2)).uuu;
		_ = _/**/.e().H(null, "block sentence", (1.1, 1.1), -1).uU;
	}

	[TestMethod]
	public void Junction()
	{
		var _ = Parse("""
			1
			+ 2
			/ 3
			4
			5
			*6
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.j(L.ADD).H(P, (L.INT, 2)).uu;
		_ = _/**/		.j(L.DIV).H(P, (L.INT, 3)).uuu;
		_ = _/**/	.n(B).H(P, (L.INT, 4)).u;
		_ = _/**/	.n(B).H(P, (L.INT, 5));
		_ = _/**/		.j(L.MUL).H(P, (L.INT, 6)).uuuuU;
	}

	[TestMethod]
	public void JunctionRecover()
	{
		var _ = Parse("""
			+ 2
			/ 3
			4
			""");
		_ = _/**/	.h(B).H(S.sen, err: 1);
		_ = _/**/		.j(L.ADD).H(P, (L.INT, 2)).uu;
		_ = _/**/		.j(L.DIV).H(P, (L.INT, 3)).uuu;
		_ = _/**/	.n(B).H(P, (L.INT, 4)).uu;
		_ = _/**/.e().H(null, "block sentence", (1.1, 1.1), -1).uU;
	}

	[TestMethod]
	public void NookBlock()
	{
		var _ = Parse("""
			1
				2
					3
				4
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(N).H(P, (L.INT, 2));
		_ = _/**/			.n(N).H(P, (L.INT, 3)).uu;
		_ = _/**/		.n(N).H(P, (L.INT, 4)).uuuU;
		_ = Parse("""
			1
				2
				3
					4
						5
					6
			7
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(N).H(P, (L.INT, 2)).u;
		_ = _/**/		.n(N).H(P, (L.INT, 3));
		_ = _/**/			.n(N).H(P, (L.INT, 4));
		_ = _/**/				.n(N).H(P, (L.INT, 5)).uu;
		_ = _/**/			.n(N).H(P, (L.INT, 6)).uuu;
		_ = _/**/	.n(B).H(P, (L.INT, 7)).uuU;
	}

	[TestMethod]
	public void NookJunc()
	{
		var _ = Parse("""
			1
				2
				/ 3
				4
			5
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(N).H(P, (L.INT, 2));
		_ = _/**/			.j(L.DIV).H(P, (L.INT, 3)).uuu;
		_ = _/**/		.n(N).H(P, (L.INT, 4)).uu;
		_ = _/**/	.n(B).H(P, (L.INT, 5)).uuU;
		_ = Parse("""
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
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(N).H(P, (L.INT, 2));
		_ = _/**/			.j(L.DIV).H(P, (L.INT, 3)).uuu;
		_ = _/**/		.n(N).H(P, (L.INT, 4)).u;
		_ = _/**/		.j(L.ADD).h(E, L.NEGA).H(P, (L.INT, 5)).u;
		_ = _/**/				.n(N).H(P, (L.INT, 6)).u;
		_ = _/**/				.j(L.MUL).H(P, (L.INT, 7)).uuu;
		_ = _/**/			.n(B).H(P, (L.INT, 8)).uuu;
		_ = _/**/	.n(B).H(P, (L.INT, 9)).uuU;
	}

	[TestMethod]
	public void NookRecover()
	{
		var _ = Parse("""
			1
				)
					]
				2
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(N).H(S.sen, err: 1);
		_ = _/**/			.n(N).H(S.sen, err: 1).uu;
		_ = _/**/		.n(N).H(P, (L.INT, 2)).uuu;
		_ = _/**/.e().H(null, "sentence expression", (2.2, 2.3), -1);
		_ = _/**/	.N(null, "sentence expression", (3.3, 3.4), -1).uU;
		_ = Parse("""
			1
				-
						2
				3
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(N).H(S.sen, err: 1);
		_ = _/**/			.j(L.SUB).H(S.sen, err: 1);
		_ = _/**/					.n(N).H(P, (L.INT, 2)).uuuu;
		_ = _/**/		.n(N).H(P, (L.INT, 3)).uuu;
		_ = _/**/.e().H(null, "sentence expression", (2.2, 2.2), -1);
		_ = _/**/	.N(null, "junction block", (3.1, 3.1), -1).uU;
	}

	[TestMethod]
	public void NookRight()
	{
		var _ = Parse("""
			1
					2
						3
					4
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(Nr).h(N).H(P, (L.INT, 2));
		_ = _/**/				.n(N).H(P, (L.INT, 3)).uu;
		_ = _/**/			.n(N).H(P, (L.INT, 4)).uuuuU;
		_ = Parse("""
			1
						2
								3
					4
				5
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(Nr).h(N).H(P, (L.INT, 2));
		_ = _/**/				.n(Nr).h(N).H(P, (L.INT, 3)).uuu;
		_ = _/**/			.n(N).H(P, (L.INT, 4)).uu;
		_ = _/**/		.n(N).H(P, (L.INT, 5)).uuu;
		_ = _/**/.e(-3).H(null, L.INDR, (4.1, 4.3), -3).uU;
	}

	[TestMethod]
	public void NookRightJunc()
	{
		var _ = Parse("""
			1
					2
					/ 3
					4
				5
				*6
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(Nr).h(N).H(P, (L.INT, 2));
		_ = _/**/				.j(L.DIV).H(P, (L.INT, 3)).uuu;
		_ = _/**/			.n(N).H(P, (L.INT, 4)).uu;
		_ = _/**/		.n(N).H(P, (L.INT, 5));
		_ = _/**/			.j(L.MUL).H(P, (L.INT, 6)).uuuuuU;
		_ = Parse("""
			1
			+	a
						2
						/ 3
						4
					5
					*6
				/7
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.j(L.ADD).H(P, (L.NAME, "a"));
		_ = _/**/				.n(Nr).h(N).H(P, (L.INT, 2));
		_ = _/**/						.j(L.DIV).H(P, (L.INT, 3)).uuu;
		_ = _/**/					.n(N).H(P, (L.INT, 4)).uu;
		_ = _/**/				.n(N).H(P, (L.INT, 5));
		_ = _/**/					.j(L.MUL).H(P, (L.INT, 6)).uuu;
		_ = _/**/				.j(L.DIV).H(P, (L.INT, 7)).uu.uuuuU;
	}

	[TestMethod]
	public void LineRecover()
	{
		var _ = Parse("""
			1 *>
						2
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1)).N(S.sen, err: 1);
		_ = _/**/		.n(Nr).h(N).H(P, (L.INT, 2)).uuuu;
		_ = _/**/.e().H(null, "arithmetic expression", (1.4, 1.5), -1).uU;
	}

	[TestMethod]
	public void Prefix()
	{
		var _ = Parse(@"1- -+2");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(E, L.SUB);
		_ = _/**/			.h(E, L.NEGA).h(E, L.POSI).H(P, (L.INT, 2)).uuuuuU;
		_ = Parse(@"1--+2");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(I);
		_ = _/**/			.h(E, L.NOTB).h(E, L.POSI).H(P, (L.INT, 2)).uu.uuu;
		_ = _/**/.e(-3).H(null, L.NOTB, (1.2, 1.4), -3).uU;
	}

	[TestMethod]
	public void PrefixJunc()
	{
		var _ = Parse("""
			1
				+2
			--3
			- -4
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(N).h(E, L.POSI).H(P, (L.INT, 2)).uuu;
		_ = _/**/	.n(B).h(E, L.NOTB).H(P, (L.INT, 3)).u;
		_ = _/**/		.j(L.SUB).h(E, L.NEGA).H(P, (L.INT, 4)).uuuuuU;
	}

	[TestMethod]
	public void Postfix()
	{
		var _ = Parse(@"0 == a, 1 * 2, 3, .d / 5, c .");
		_ = _/**/	.h(B).H(P, (L.INT, "0")).n(E, L.EQ);
		_ = _/**/			.H(P, (L.NAME, "a"));
		_ = _/**/			.n(I).H(P, (L.INT, 1)).n(E, L.MUL).H(P, (L.INT, 2)).uu;
		_ = _/**/			.n(I).H(P, (L.INT, 3)).u;
		_ = _/**/			.N(P, (L.RUN, "d"));
		_ = _/**/			.n(E, L.DIV).H(P, (L.INT, 5));
		_ = _/**/						.n(I).H(P, (L.NAME, "c")).N(P, L.RUN).uuuuuU;
		_ = Parse(@"0 << a 1 * 2 3. 4 .d / 5 c .");
		_ = _/**/	.h(B).H(P, (L.INT, "0"));
		_ = _/**/		.n(E, L.SHL).H(P, (L.NAME, "a")).n(I).H(P, (L.INT, 1)).uu;
		_ = _/**/		.n(E, L.MUL).H(P, (L.INT, 2));
		_ = _/**/					.n(I).H(P, (L.INT, 3)).N(P, (L.RUND, "")).u;
		_ = _/**/					.n(I).H(P, (L.INT, 4)).u;
		_ = _/**/					.N(P, L.RUN).u;
		_ = _/**/		.n(E, L.DIV).H(P, (L.INT, 5)).n(I).H(P, (L.NAME, "c")).u;
		_ = _/**/					.N(P, L.RUN).uuuU;
		_ = Parse(@"0 << a,1 * 2 3. 4 .d / 5 c .");
		_ = _/**/	.h(B).H(P, (L.INT, "0"));
		_ = _/**/		.n(E, L.SHL).H(P, (L.NAME, "a"));
		_ = _/**/					.n(I).H(P, (L.INT, 1));
		_ = _/**/					.n(E, L.MUL).H(P, (L.INT, 2));
		_ = _/**/								.n(I).H(P, (L.INT, 3)).N(P, (L.RUND, "")).u;
		_ = _/**/								.n(I).H(P, (L.INT, 4)).u;
		_ = _/**/								.N(P, L.RUN).u;
		_ = _/**/					.n(E, L.DIV).H(P, (L.INT, 5)).n(I).H(P, (L.NAME, "c")).u;
		_ = _/**/								.N(P, L.RUN).uuuuuU;
	}

	[TestMethod]
	public void PostfixJunc()
	{
		var _ = Parse("""
			1
			.a. .b
				2
			. 3
				. 4
				5
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(J).H(S.jpost, (L.RUN, "a"));
		_ = _/**/			.N(S.jpost, (L.RUN, "")).N(S.jpost, (L.RUN, "b"));
		_ = _/**/			.n(B).H(P, (L.INT, 2)).uu;
		_ = _/**/		.n(J).H(S.jpost, (L.RUN, ""));
		_ = _/**/			.n(B).H(P, (L.INT, 3));
		_ = _/**/				.n(J).H(S.jpost, (L.RUN, ""));
		_ = _/**/					.n(B).H(P, (L.INT, 4)).uuu;
		_ = _/**/			.n(B).H(P, (L.INT, 5)).uuuuU;
	}

	[TestMethod]
	public void PostfixJuncRecover()
	{
		var _ = Parse("""
			1
			.a. .b + 2
				3
				.c
					.
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(J).H(S.jpost, (L.RUN, "a"));
		_ = _/**/			.N(S.jpost, (L.RUN, "")).N(S.jpost, (L.RUN, "b"));
		_ = _/**/			.n(B).H(S.sen, err: 1).u;
		_ = _/**/			.n(B).H(P, (L.INT, 3));
		_ = _/**/				.n(J).H(S.jpost, (L.RUN, "c"));
		_ = _/**/					.n(B).H(S.sen, err: 1);
		_ = _/**/						.n(J).H(S.jpost, (L.RUN, "")).uuuu.uuu;
		_ = _/**/.e().H(null, "block sentence", (2.8, 2.9), -1);
		_ = _/**/	.N(null, "block sentence", (5.3, 5.3), -1).uU;
	}

	[TestMethod]
	public void Binary()
	{
		var _ = Parse(@"1+2*3-4");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(E, L.ADD).H(P, (L.INT, 2));
		_ = _/**/					.n(E, L.MUL).H(P, (L.INT, 3)).uu;
		_ = _/**/		.n(E, L.SUB).H(P, (L.INT, 4)).uuuU;
		_ = Parse(@"1 + 2 * 3 >> 4 % 5 < 6 != 7 ++ 8");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(E, L.ADD).H(P, (L.INT, 2));
		_ = _/**/			.n(E, L.MUL).H(P, (L.INT, 3));
		_ = _/**/				.n(E, L.SHR).H(P, (L.INT, 4)).uu;
		_ = _/**/			.n(E, L.MOD).H(P, (L.INT, 5)).uu;
		_ = _/**/		.n(E, L.LT).H(P, (L.INT, 6)).u;
		_ = _/**/		.n(E, L.XOR).H(P, (L.INT, 7));
		_ = _/**/			.n(E, L.XORB).H(P, (L.INT, 8)).uuuuU;
	}

	[TestMethod]
	public void BinaryJunc()
	{
		var _ = Parse("""
			1
			- 2
			*3+4
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.j(L.SUB).H(P, (L.INT, 2)).uu;
		_ = _/**/		.j(L.MUL).H(P, (L.INT, 3));
		_ = _/**/					.n(E, L.ADD).H(P, (L.INT, 4)).uuuuuU;
		_ = Parse("""
			1
			*	2
				,	3
					/ 4
				%	5
			&&	6
			7
			-	8
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.j(L.MUL).H(P, (L.INT, 2));
		_ = _/**/				.j(L.INP).H(P, (L.INT, 3));
		_ = _/**/						.j(L.DIV).H(P, (L.INT, 4)).uuuu;
		_ = _/**/				.j(L.MOD).H(P, (L.INT, 5)).uuuu;
		_ = _/**/		.j(L.ANDB).H(P, (L.INT, 6)).uuu;
		_ = _/**/	.n(B).H(P, (L.INT, 7));
		_ = _/**/		.j(L.SUB).H(P, (L.INT, 8)).uuuuU;
	}

	[TestMethod]
	public void BinaryJuncRecover()
	{
		var _ = Parse("""
			1
			*
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.N(J, err: 1).uu;
		_ = _/**/.e().H(null, "junction block", (2.2, 2.2), -1).uU;
		_ = Parse("""
			1
				+
					*2/
					3
				/ 4-
			-
				!&5
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(N).H(S.sen, err: 1);
		_ = _/**/			.j(L.ADD).H(S.sen, err: 1);
		_ = _/**/					.j(L.MUL).H(P, (L.INT, 2)).N(S.sen, err: 1).uuu;
		_ = _/**/				.n(B).H(P, (L.INT, 3)).uu;
		_ = _/**/			.j(L.DIV).H(P, (L.INT, 4)).N(S.sen, err: 1).uuu;
		_ = _/**/		.j(L.SUB).H(S.sen, err: 1).uuuu;
		_ = _/**/.e().H(null, "sentence expression", (2.2, 2.2), -1);
		_ = _/**/	.N(null, "junction block", (3.3, 3.3), -1);
		_ = _/**/	.N(null, "arithmetic operator expression", (3.6, 4.1), -1);
		_ = _/**/	.N(null, "arithmetic operator expression", (5.6, 6.1), -1);
		_ = _/**/	.N(null, "prefix operator expression", (7.3, 7.4), -1).uU;
	}

	[TestMethod]
	public void Parenth()
	{
		var _ = Parse(@"(1)");
		_ = _/**/	.h(B).H(P, (L.INT, 1)).uuU;
		_ = Parse(@"-(-1)/(2*(3+4))");
		_ = _/**/	.h(B).h(E, L.NEGA).h(E, L.NEGA).H(P, (L.INT, 1)).uu;
		_ = _/**/		.n(E, L.DIV).H(P, (L.INT, 2));
		_ = _/**/			.n(E, L.MUL).H(P, (L.INT, 3));
		_ = _/**/				.n(E, L.ADD).H(P, (L.INT, 4)).uuuuuU;
	}

	[TestMethod]
	public void ParenthRecover()
	{
		var _ = Parse(@"(1");
		_ = _/**/	.h(B).H(P, (L.INT, 1)).N(S.phr, err: 1).uu;
		_ = _/**/.e().H(null, "parenth RP", (1.3, 1.3), -1).uU;
		_ = Parse(@"-(-1/(2+)3)/)");
		_ = _/**/	.h(B).h(E, L.NEGA).h(E, L.NEGA).H(P, (L.INT, 1)).u;
		_ = _/**/					.n(E, L.DIV).H(P, (L.INT, 2)).N(S.phr, err: 1);
		_ = _/**/								.n(I).H(P, (L.INT, 3)).uuu;
		_ = _/**/		.N(S.sen, err: 1).uu;
		_ = _/**/.e().H(S.phr, "arithmetic expression", (1.9, 1.10m), -1);
		_ = _/**/	.N(S.sen, "arithmetic expression", (1.13, 1.14), -1).uU;
		_ = Parse(@"( 1*[ 2 / [(3+4] - 5");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.n(E, L.MUL).h(S.squ, err: 1);
		_ = _/**/				.H(P, (L.INT, 2));
		_ = _/**/				.n(E, L.DIV).h(S.squ).H(P, (L.INT, 3));
		_ = _/**/									.n(E, L.ADD).H(P, (L.INT, 4)).u;
		_ = _/**/									.N(P, err: 1).uu;
		_ = _/**/				.n(E, L.SUB).H(P, (L.INT, 5)).uuu;
		_ = _/**/		.N(P, err: 1).uu;
		_ = _/**/.e().H(null, "parenth RP", (1.16, 1.16), -1);
		_ = _/**/	.N(null, "square bracket RSB", (1.21, 1.21), -1);
		_ = _/**/	.N(null, "parenth RP", (1.21, 1.21), -1).uU;
	}

	[TestMethod]
	public void BracketJunc()
	{
		var _ = Parse("""
			1
			[2]
				3
				*4
				5
			[6]
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.j(L.LSB).H(P, (L.INT, 2)).u;
		_ = _/**/			.n(B).H(P, (L.INT, 3));
		_ = _/**/				.j(L.MUL).H(P, (L.INT, 4)).uuu;
		_ = _/**/			.n(B).H(P, (L.INT, 5)).uu;
		_ = _/**/		.j(L.LSB).H(P, (L.INT, 6)).uuuuU;
	}

	[TestMethod]
	public void BracketJuncRecover()
	{
		var _ = Parse("""
			1
			[
			[]
			2
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.j(L.LSB).H(S.Jsqu, err: 1).uu;
		_ = _/**/		.j(L.LSB).H(S.Jsqu, err: 1).uuu;
		_ = _/**/	.n(B).H(P, (L.INT, 2)).uu;
		_ = _/**/.e().H(null, "bracket junction expression", (2.2, 3.1), -1);
		_ = _/**/	.N(null, "bracket junction expression", (3.2, 3.3), -1).uU;
		_ = Parse("""
			1
			[2] 3
				[4] /5
			[6
				[] 7
				8
			""");
		_ = _/**/	.h(B).H(P, (L.INT, 1));
		_ = _/**/		.j(L.LSB).H(P, (L.INT, 2));
		_ = _/**/				.N(S.Jsqu, err: 1);
		_ = _/**/				.j(L.LSB).H(P, (L.INT, 4));
		_ = _/**/						.N(S.Jsqu, err: 1).uuuu;
		_ = _/**/		.j(L.LSB).H(P, (L.INT, 6)).N(S.Jsqu, err: 1);
		_ = _/**/				.j(L.LSB).H(S.Jsqu, err: 1).uuu;
		_ = _/**/			.n(B).H(P, (L.INT, 8)).uuuu;
		_ = _/**/.e().H(null, "bracket junction EOL", (2.5, 2.6), -1);
		_ = _/**/	.N(null, "bracket junction EOL", (3.6, 3.7), -1);
		_ = _/**/	.N(null, "bracket junction RSB", (4.3, 5.1), -1);
		_ = _/**/	.N(null, "bracket junction expression", (5.3, 5.4), -1).uU;
	}

	[TestMethod]
	public void Input1()
	{
		var _ = Parse(@"a,1,2");
		_ = _/**/	.h(B).H(P, (L.NAME, "a"));
		_ = _/**/		.n(I).H(P, (L.INT, 1)).u.n(I).H(P, (L.INT, 2)).uuuU;
		_ = Parse(@"a, 1 * 2");
		_ = _/**/	.h(B).H(P, (L.NAME, "a"));
		_ = _/**/		.n(I).H(P, (L.INT, 1)).n(E, L.MUL).H(P, (L.INT, 2)).uuuuU;
		_ = Parse(@"0 * a, 1 * 2, 3, 4 == 5");
		_ = _/**/	.h(B).H(P, (L.INT, "0")).n(E, L.MUL);
		_ = _/**/			.H(P, (L.NAME, "a"));
		_ = _/**/			.n(I).H(P, (L.INT, 1)).n(E, L.MUL).H(P, (L.INT, 2)).uu;
		_ = _/**/			.n(I).H(P, (L.INT, 3)).u;
		_ = _/**/			.n(I).H(P, (L.INT, 4)).n(E, L.EQ).H(P, (L.INT, 5)).uuuuuU;
	}

	[TestMethod]
	public void Input2()
	{
		var _ = Parse(@"a 1 2");
		_ = _/**/	.h(B).H(P, (L.NAME, "a"));
		_ = _/**/		.n(I).H(P, (L.INT, 1)).u.n(I).H(P, (L.INT, 2)).uuuU;
		_ = Parse(@"a 1 * 2");
		_ = _/**/	.h(B).H(P, (L.NAME, "a"));
		_ = _/**/		.n(I).H(P, (L.INT, 1)).u.n(E, L.MUL).H(P, (L.INT, 2)).uuuU;
		_ = Parse(@"0 * a 1 * 2 3 4 == 5");
		_ = _/**/	.h(B).H(P, (L.INT, "0"));
		_ = _/**/		.n(E, L.MUL).H(P, (L.NAME, "a")).n(I).H(P, (L.INT, 1)).uu;
		_ = _/**/		.n(E, L.MUL).H(P, (L.INT, 2));
		_ = _/**/					.n(I).H(P, (L.INT, 3)).u.n(I).H(P, (L.INT, 4)).uu;
		_ = _/**/		.n(E, L.EQ).H(P, (L.INT, 5)).uuuU;
	}

	[TestMethod]
	public void Input3()
	{
		var _ = Parse(@"a 1,2 3");
		_ = _/**/	.h(B).H(P, (L.NAME, "a"));
		_ = _/**/		.n(I).H(P, (L.INT, 1)).u;
		_ = _/**/		.n(I).H(P, (L.INT, 2)).n(I).H(P, (L.INT, 3)).uuuuU;
		_ = Parse(@"a,1 2,3");
		_ = _/**/	.h(B).H(P, (L.NAME, "a"));
		_ = _/**/		.n(I).H(P, (L.INT, 1)).n(I).H(P, (L.INT, 2)).uu;
		_ = _/**/		.n(I).H(P, (L.INT, 3)).uuuU;
		_ = Parse(@"0 * a 1 2,3 * b 4,5");
		_ = _/**/	.h(B).H(P, (L.INT, "0")).n(E, L.MUL);
		_ = _/**/			.H(P, (L.NAME, "a"));
		_ = _/**/			.n(I).H(P, (L.INT, 1)).u.n(I).H(P, (L.INT, 2)).u;
		_ = _/**/			.n(I).H(P, (L.INT, 3)).n(E, L.MUL);
		_ = _/**/						.H(P, (L.NAME, "b")).n(I).H(P, (L.INT, 4)).uuu;
		_ = _/**/			.n(I).H(P, (L.INT, 5)).uuuuU;
	}

	[TestMethod]
	public void Input4()
	{
		var _ = Parse(@"a,");
		_ = _/**/	.h(B).H(P, (L.NAME, "a")).uuU;
		_ = Parse(@"a,1,");
		_ = _/**/	.h(B).H(P, (L.NAME, "a")).n(I).H(P, (L.INT, 1)).uuuU;
		_ = Parse(@"a 1,");
		_ = _/**/	.h(B).H(P, (L.NAME, "a")).n(I).H(P, (L.INT, 1)).uuuU;
		_ = Parse(@"(0 * (a) 1,2 3,)");
		_ = _/**/	.h(B).H(P, (L.INT, "0")).n(E, L.MUL);
		_ = _/**/			.H(P, (L.NAME, "a"));
		_ = _/**/			.n(I).H(P, (L.INT, 1)).u;
		_ = _/**/			.n(I).H(P, (L.INT, 2)).n(I).H(P, (L.INT, 3)).uuuuuU;
	}
}
