//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using qutum.syntax;
using System;
using System.Linq;
using System.Numerics;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax;

using K = Lek;
using N = Syn;

public readonly struct Ser(Synt t, Synter s)

{
	public struct Dec() : IAdditionOperators<Dec, int, Dec>, ISubtractionOperators<Dec, Dec, Dec>,
		IComparable<Dec>, IUnaryPlusOperators<Dec, int>
	{
		internal decimal d;
		public static implicit operator Dec(decimal d) => new() { d = d };
		public static implicit operator Dec(double d) => new() { d = (decimal)d };

		public static Dec operator +(Dec a, int b) => a.d + b;
		public static Dec operator -(Dec a, Dec b) => a.d - b.d;
		public readonly int CompareTo(Dec that) => int.CreateSaturating(d - that.d);
		public static int operator +(Dec a) => (int)a.d;
	}

	public readonly Ser Eq(N? name = null, object d = null, Jov<Dec>? j = null, int err = 0)
	{
		IsNotNull(t);
		AreEqual(err, t.err);
		if (name != null) AreEqual(name, t.name);
		var (line, col) = s.ler.LineCol(t.j);
		if (j?.on.d >= 0) AreEqual($"{j?.on.d}", $"{line.on}.{col.on}");
		if (j?.via.d >= 0) AreEqual($"{j?.via.d}", $"{line.to}.{col.via}");
		if (err != 0 && d is string aim && t.info?.ToString() is string test) {
			var ts = test.Split(SerMaker<char, string>.ErrMore);
			var As = aim.Split("  ");
			if ((As.Length < 3 ? ts.Length != As.Length : ts.Length < As.Length)
				|| !As.Zip(ts).All(ea => ea.First.Split(' ').ToCount().ToHashSet()
					.IsSubsetOf(ea.Second.Split(' ').ToCount().ToHashSet())))
				Fail($"Expected Error <{aim}> Actual <{test.Replace("\n", "  ")}>");
		}
		else if (d is (K key, object value))
			D(key, value);
		else if (d != null)
			AreEqual(d, d is K && t.info is Lexi<K> l ? l.key : t.info);
		return this;
	}
	public Ser D(K key, object value)
	{
		AreEqual(s.dumper(new Lexi<K>() {
			key = key,
			value = "".Equals(value) && t.info is Lexi<K> l && l.value == null ? null : value
		}), s.dumper(t.info));
		return this;
	}
	public Ser D(params object[] Ds)
	{
		var ds = new Arr<int, Lexi<K>>(Ds.Length, default);
		for (int x = 0; x < Ds.Length; x++)
			if (Ds[x] is K l)
				ds.s[ds.j.via++] = new() { key = l };
			else
				ds[ds.j.via - 1].value = Ds[x];
		AreEqual(s.dumper(ds), // TODO SerMaker dump lexis
			s.dumper(t.j.on.zpos ? s.ler.Lexs(t.j) : [.. s.ler.errs.GetRange(+~t.j.on, -+t.j.size)]));
		return this;
	}

	public Ser h(N? name = null, object d = null, Jov<Dec>? j = null, int err = 0)
		=> new Ser(t.head, s).Eq(name, d, j, err).Vine;
	public Ser n(N? name = null, object d = null, Jov<Dec>? j = null, int err = 0)
		=> new Ser(t.next, s).Eq(name, d, j, err).Vine;

	public Ser H(N? name = null, object d = null, Jov<Dec>? j = null, int err = 0)
		=> new Ser(t.head, s).Eq(name, d, j, err).Leaf;
	public Ser N(N? name = null, object d = null, Jov<Dec>? j = null, int err = 0)
		=> new Ser(t.next, s).Eq(name, d, j, err).Leaf;

	public Ser Vine { get { IsNotNull(t.head); return this; } }
	public Ser Leaf { get { IsNull(t.head); return this; } }
	public Ser u { get { IsNull(t.next); IsNotNull(t.up); return new Ser(t.up, s); } }
	public Ser U { get { IsNull(t.next); IsNull(t.up); return new Ser(t.up, s); } }
	public Ser uu => u.u;
	public Ser uU => u.U;
	public Ser uuu => u.u.u;
	public Ser uuU => u.u.U;
	public Ser uuuu => u.u.u.u;
	public Ser uuuU => u.u.u.U;
	public Ser uuuuU => u.u.u.u.U;
	public Ser uuuuuU => u.u.u.u.u.U;

	public Ser e(int err = -1) => n(err: err);

	public Ser j(object d = null, Jov<Dec>? j = null, int err = 0)
		=> n(Syn.junc, d, j, err).h(Syn.block);
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
	public const N B = N.block, N = N.nook, Nr = N.nookr, J = N.junc,
					E = N.exp, P = N.phr, I = N.inp;

	[TestMethod]
	public void Block()
	{
		var _ = Parse("").Eq(N.qutum).U;
		_ = Parse("""
			1
			2
			 \##\ 3
			""");
		_ = _/**/	.h(B).H(P, (K.INT, 1)).u;
		_ = _/**/	.n(B).H(P, (K.INT, 2)).u;
		_ = _/**/	.n(B).H(P, (K.INT, 3)).uuU;
	}

	[TestMethod]
	public void BlockRecovery()
	{
		var _ = Parse("""
					1
				\##\ 2
			""");
		_ = _/**/	.h(B).H(N.sen, err: 1);
		_ = _/**/		.n(Nr).h(N).H(P, (K.INT, 1)).uu;
		_ = _/**/		.n(N).H(P, (K.INT, 2)).uuu;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.j(K.ADD).H(P, (K.INT, 2)).uu;
		_ = _/**/		.j(K.DIV).H(P, (K.INT, 3)).uuu;
		_ = _/**/	.n(B).H(P, (K.INT, 4)).u;
		_ = _/**/	.n(B).H(P, (K.INT, 5));
		_ = _/**/		.j(K.MUL).H(P, (K.INT, 6)).uuuuU;
	}

	[TestMethod]
	public void JunctionRecover()
	{
		var _ = Parse("""
			+ 2
			/ 3
			4
			""");
		_ = _/**/	.h(B).H(N.sen, err: 1);
		_ = _/**/		.j(K.ADD).H(P, (K.INT, 2)).uu;
		_ = _/**/		.j(K.DIV).H(P, (K.INT, 3)).uuu;
		_ = _/**/	.n(B).H(P, (K.INT, 4)).uu;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(N).H(P, (K.INT, 2));
		_ = _/**/			.n(N).H(P, (K.INT, 3)).uu;
		_ = _/**/		.n(N).H(P, (K.INT, 4)).uuuU;
		_ = Parse("""
			1
				2
				3
					4
						5
					6
			7
			""");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(N).H(P, (K.INT, 2)).u;
		_ = _/**/		.n(N).H(P, (K.INT, 3));
		_ = _/**/			.n(N).H(P, (K.INT, 4));
		_ = _/**/				.n(N).H(P, (K.INT, 5)).uu;
		_ = _/**/			.n(N).H(P, (K.INT, 6)).uuu;
		_ = _/**/	.n(B).H(P, (K.INT, 7)).uuU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(N).H(P, (K.INT, 2));
		_ = _/**/			.j(K.DIV).H(P, (K.INT, 3)).uuu;
		_ = _/**/		.n(N).H(P, (K.INT, 4)).uu;
		_ = _/**/	.n(B).H(P, (K.INT, 5)).uuU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(N).H(P, (K.INT, 2));
		_ = _/**/			.j(K.DIV).H(P, (K.INT, 3)).uuu;
		_ = _/**/		.n(N).H(P, (K.INT, 4)).u;
		_ = _/**/		.j(K.ADD).h(E, K.NEGA).H(P, (K.INT, 5)).u;
		_ = _/**/				.n(N).H(P, (K.INT, 6)).u;
		_ = _/**/				.j(K.MUL).H(P, (K.INT, 7)).uuu;
		_ = _/**/			.n(B).H(P, (K.INT, 8)).uuu;
		_ = _/**/	.n(B).H(P, (K.INT, 9)).uuU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(N).H(N.sen, err: 1);
		_ = _/**/			.n(N).H(N.sen, err: 1).uu;
		_ = _/**/		.n(N).H(P, (K.INT, 2)).uuu;
		_ = _/**/.e().H(null, "sentence expression", (2.2, 2.3), -1);
		_ = _/**/	.N(null, "sentence expression", (3.3, 3.4), -1).uU;
		_ = Parse("""
			1
				-
						2
				3
			""");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(N).H(N.sen, err: 1);
		_ = _/**/			.j(K.SUB).H(N.sen, err: 1);
		_ = _/**/					.n(N).H(P, (K.INT, 2)).uuuu;
		_ = _/**/		.n(N).H(P, (K.INT, 3)).uuu;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(Nr).h(N).H(P, (K.INT, 2));
		_ = _/**/				.n(N).H(P, (K.INT, 3)).uu;
		_ = _/**/			.n(N).H(P, (K.INT, 4)).uuuuU;
		_ = Parse("""
			1
						2
								3
					4
				5
			""");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(Nr).h(N).H(P, (K.INT, 2));
		_ = _/**/				.n(Nr).h(N).H(P, (K.INT, 3)).uuu;
		_ = _/**/			.n(N).H(P, (K.INT, 4)).uu;
		_ = _/**/		.n(N).H(P, (K.INT, 5)).uuu;
		_ = _/**/.e(-3).H(null, K.INDR, (4.1, 4.3), -3).uU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(Nr).h(N).H(P, (K.INT, 2));
		_ = _/**/				.j(K.DIV).H(P, (K.INT, 3)).uuu;
		_ = _/**/			.n(N).H(P, (K.INT, 4)).uu;
		_ = _/**/		.n(N).H(P, (K.INT, 5));
		_ = _/**/			.j(K.MUL).H(P, (K.INT, 6)).uuuuuU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.j(K.ADD).H(P, (K.NAME, "a"));
		_ = _/**/				.n(Nr).h(N).H(P, (K.INT, 2));
		_ = _/**/						.j(K.DIV).H(P, (K.INT, 3)).uuu;
		_ = _/**/					.n(N).H(P, (K.INT, 4)).uu;
		_ = _/**/				.n(N).H(P, (K.INT, 5));
		_ = _/**/					.j(K.MUL).H(P, (K.INT, 6)).uuu;
		_ = _/**/				.j(K.DIV).H(P, (K.INT, 7)).uu.uuuuU;
	}

	[TestMethod]
	public void LineRecover()
	{
		var _ = Parse("""
			1 *>
						2
			""");
		_ = _/**/	.h(B).H(P, (K.INT, 1)).N(N.sen, err: 1);
		_ = _/**/		.n(Nr).h(N).H(P, (K.INT, 2)).uuuu;
		_ = _/**/.e().H(null, "arithmetic expression", (1.4, 1.5), -1).uU;
	}

	[TestMethod]
	public void Prefix()
	{
		var _ = Parse(@"1- -+2");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(E, K.SUB);
		_ = _/**/			.h(E, K.NEGA).h(E, K.POSI).H(P, (K.INT, 2)).uuuuuU;
		_ = Parse(@"1--+2");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(I);
		_ = _/**/			.h(E, K.NOTB).h(E, K.POSI).H(P, (K.INT, 2)).uu.uuu;
		_ = _/**/.e(-3).H(null, K.NOTB, (1.2, 1.4), -3).uU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(N).h(E, K.POSI).H(P, (K.INT, 2)).uuu;
		_ = _/**/	.n(B).h(E, K.NOTB).H(P, (K.INT, 3)).u;
		_ = _/**/		.j(K.SUB).h(E, K.NEGA).H(P, (K.INT, 4)).uuuuuU;
	}

	[TestMethod]
	public void Postfix()
	{
		var _ = Parse(@"0 == a, 1 * 2, 3, .d / 5, c .");
		_ = _/**/	.h(B).H(P, (K.INT, "0")).n(E, K.EQ);
		_ = _/**/			.H(P, (K.NAME, "a"));
		_ = _/**/			.n(I).H(P, (K.INT, 1)).n(E, K.MUL).H(P, (K.INT, 2)).uu;
		_ = _/**/			.n(I).H(P, (K.INT, 3)).u;
		_ = _/**/			.N(P, (K.RUN, "d"));
		_ = _/**/			.n(E, K.DIV).H(P, (K.INT, 5));
		_ = _/**/						.n(I).H(P, (K.NAME, "c")).N(P, K.RUN).uuuuuU;
		_ = Parse(@"0 << a 1 * 2 3. 4 .d.e 6 / 5 c .");
		_ = _/**/	.h(B).H(P, (K.INT, "0"));
		_ = _/**/		.n(E, K.SHL).H(P, (K.NAME, "a")).n(I).H(P, (K.INT, 1)).uu;
		_ = _/**/		.n(E, K.MUL).H(P, (K.INT, 2));
		_ = _/**/					.n(I).H(P, (K.INT, 3)).N(P, (K.RUND, "")).u;
		_ = _/**/					.n(I).H(P, (K.INT, 4)).u;
		_ = _/**/					.N(P, (K.RUN, "d")).N(P, (K.RUND, "e"));
		_ = _/**/					.n(I).H(P, (K.INT, 6)).uu;
		_ = _/**/		.n(E, K.DIV).H(P, (K.INT, 5)).n(I).H(P, (K.NAME, "c")).u;
		_ = _/**/					.N(P, K.RUN).uuuU;
		_ = Parse(@"0 << a,1 * 2 3. 4 .d / 5 c .");
		_ = _/**/	.h(B).H(P, (K.INT, "0"));
		_ = _/**/		.n(E, K.SHL).H(P, (K.NAME, "a"));
		_ = _/**/					.n(I).H(P, (K.INT, 1));
		_ = _/**/					.n(E, K.MUL).H(P, (K.INT, 2));
		_ = _/**/								.n(I).H(P, (K.INT, 3)).N(P, (K.RUND, "")).u;
		_ = _/**/								.n(I).H(P, (K.INT, 4)).u;
		_ = _/**/								.N(P, K.RUN).u;
		_ = _/**/					.n(E, K.DIV).H(P, (K.INT, 5)).n(I).H(P, (K.NAME, "c")).u;
		_ = _/**/								.N(P, K.RUN).uuuuuU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(J).H(N.jpost, (K.RUN, "a"));
		_ = _/**/			.N(N.jpost, (K.RUN, "")).N(N.jpost, (K.RUN, "b"));
		_ = _/**/			.n(B).H(P, (K.INT, 2)).uu;
		_ = _/**/		.n(J).H(N.jpost, (K.RUN, ""));
		_ = _/**/			.n(B).H(P, (K.INT, 3));
		_ = _/**/				.n(J).H(N.jpost, (K.RUN, ""));
		_ = _/**/					.n(B).H(P, (K.INT, 4)).uuu;
		_ = _/**/			.n(B).H(P, (K.INT, 5)).uuuuU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(J).H(N.jpost, (K.RUN, "a"));
		_ = _/**/			.N(N.jpost, (K.RUN, "")).N(N.jpost, (K.RUN, "b"));
		_ = _/**/			.n(B).H(N.sen, err: 1).u;
		_ = _/**/			.n(B).H(P, (K.INT, 3));
		_ = _/**/				.n(J).H(N.jpost, (K.RUN, "c"));
		_ = _/**/					.n(B).H(N.sen, err: 1);
		_ = _/**/						.n(J).H(N.jpost, (K.RUN, "")).uuuu.uuu;
		_ = _/**/.e().H(null, "block sentence", (2.8, 2.9), -1);
		_ = _/**/	.N(null, "block sentence", (5.3, 5.3), -1).uU;
	}

	[TestMethod]
	public void Binary()
	{
		var _ = Parse(@"1+2*3-4");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(E, K.ADD).H(P, (K.INT, 2));
		_ = _/**/					.n(E, K.MUL).H(P, (K.INT, 3)).uu;
		_ = _/**/		.n(E, K.SUB).H(P, (K.INT, 4)).uuuU;
		_ = Parse(@"1 + 2 * 3 >> 4 % 5 < 6 != 7 ++ 8");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(E, K.ADD).H(P, (K.INT, 2));
		_ = _/**/			.n(E, K.MUL).H(P, (K.INT, 3));
		_ = _/**/				.n(E, K.SHR).H(P, (K.INT, 4)).uu;
		_ = _/**/			.n(E, K.MOD).H(P, (K.INT, 5)).uu;
		_ = _/**/		.n(E, K.LT).H(P, (K.INT, 6)).u;
		_ = _/**/		.n(E, K.XOR).H(P, (K.INT, 7));
		_ = _/**/			.n(E, K.XORB).H(P, (K.INT, 8)).uuuuU;
	}

	[TestMethod]
	public void BinaryJunc()
	{
		var _ = Parse("""
			1
			- 2
			*3+4
			""");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.j(K.SUB).H(P, (K.INT, 2)).uu;
		_ = _/**/		.j(K.MUL).H(P, (K.INT, 3));
		_ = _/**/					.n(E, K.ADD).H(P, (K.INT, 4)).uuuuuU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.j(K.MUL).H(P, (K.INT, 2));
		_ = _/**/				.j(K.INP).H(P, (K.INT, 3));
		_ = _/**/						.j(K.DIV).H(P, (K.INT, 4)).uuuu;
		_ = _/**/				.j(K.MOD).H(P, (K.INT, 5)).uuuu;
		_ = _/**/		.j(K.ANDB).H(P, (K.INT, 6)).uuu;
		_ = _/**/	.n(B).H(P, (K.INT, 7));
		_ = _/**/		.j(K.SUB).H(P, (K.INT, 8)).uuuuU;
	}

	[TestMethod]
	public void BinaryJuncRecover()
	{
		var _ = Parse("""
			1
			*
			""");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(N).H(N.sen, err: 1);
		_ = _/**/			.j(K.ADD).H(N.sen, err: 1);
		_ = _/**/					.j(K.MUL).H(P, (K.INT, 2)).N(N.sen, err: 1).uuu;
		_ = _/**/				.n(B).H(P, (K.INT, 3)).uu;
		_ = _/**/			.j(K.DIV).H(P, (K.INT, 4)).N(N.sen, err: 1).uuu;
		_ = _/**/		.j(K.SUB).H(N.sen, err: 1).uuuu;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1)).uuU;
		_ = Parse(@"-(-1)/(2*(3+4))");
		_ = _/**/	.h(B).h(E, K.NEGA).h(E, K.NEGA).H(P, (K.INT, 1)).uu;
		_ = _/**/		.n(E, K.DIV).H(P, (K.INT, 2));
		_ = _/**/			.n(E, K.MUL).H(P, (K.INT, 3));
		_ = _/**/				.n(E, K.ADD).H(P, (K.INT, 4)).uuuuuU;
	}

	[TestMethod]
	public void ParenthRecover()
	{
		var _ = Parse(@"(1");
		_ = _/**/	.h(B).H(P, (K.INT, 1)).N(N.phr, err: 1).uu;
		_ = _/**/.e().H(null, "parenth RP", (1.3, 1.3), -1).uU;
		_ = Parse(@"-(-1/(2+)3)/)");
		_ = _/**/	.h(B).h(E, K.NEGA).h(E, K.NEGA).H(P, (K.INT, 1)).u;
		_ = _/**/					.n(E, K.DIV).H(P, (K.INT, 2)).N(N.phr, err: 1);
		_ = _/**/								.n(I).H(P, (K.INT, 3)).uuu;
		_ = _/**/		.N(N.sen, err: 1).uu;
		_ = _/**/.e().H(N.phr, "arithmetic expression", (1.9, 1.10m), -1);
		_ = _/**/	.N(N.sen, "arithmetic expression", (1.13, 1.14), -1).uU;
		_ = Parse(@"( 1*[ 2 / [(3+4] - 5");
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.n(E, K.MUL).h(N.squ, err: 1);
		_ = _/**/				.H(P, (K.INT, 2));
		_ = _/**/				.n(E, K.DIV).h(N.squ).H(P, (K.INT, 3));
		_ = _/**/									.n(E, K.ADD).H(P, (K.INT, 4)).u;
		_ = _/**/									.N(P, err: 1).uu;
		_ = _/**/				.n(E, K.SUB).H(P, (K.INT, 5)).uuu;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.j(K.LSB).H(P, (K.INT, 2)).u;
		_ = _/**/			.n(B).H(P, (K.INT, 3));
		_ = _/**/				.j(K.MUL).H(P, (K.INT, 4)).uuu;
		_ = _/**/			.n(B).H(P, (K.INT, 5)).uu;
		_ = _/**/		.j(K.LSB).H(P, (K.INT, 6)).uuuuU;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.j(K.LSB).H(N.Jsqu, err: 1).uu;
		_ = _/**/		.j(K.LSB).H(N.Jsqu, err: 1).uuu;
		_ = _/**/	.n(B).H(P, (K.INT, 2)).uu;
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
		_ = _/**/	.h(B).H(P, (K.INT, 1));
		_ = _/**/		.j(K.LSB).H(P, (K.INT, 2));
		_ = _/**/				.N(N.Jsqu, err: 1);
		_ = _/**/				.j(K.LSB).H(P, (K.INT, 4));
		_ = _/**/						.N(N.Jsqu, err: 1).uuuu;
		_ = _/**/		.j(K.LSB).H(P, (K.INT, 6)).N(N.Jsqu, err: 1);
		_ = _/**/				.j(K.LSB).H(N.Jsqu, err: 1).uuu;
		_ = _/**/			.n(B).H(P, (K.INT, 8)).uuuu;
		_ = _/**/.e().H(null, "bracket junction EOL", (2.5, 2.6), -1);
		_ = _/**/	.N(null, "bracket junction EOL", (3.6, 3.7), -1);
		_ = _/**/	.N(null, "bracket junction RSB", (4.3, 5.1), -1);
		_ = _/**/	.N(null, "bracket junction expression", (5.3, 5.4), -1).uU;
	}

	[TestMethod]
	public void Input1()
	{
		var _ = Parse(@"a,1,2");
		_ = _/**/	.h(B).H(P, (K.NAME, "a"));
		_ = _/**/		.n(I).H(P, (K.INT, 1)).u.n(I).H(P, (K.INT, 2)).uuuU;
		_ = Parse(@"a, 1 * 2");
		_ = _/**/	.h(B).H(P, (K.NAME, "a"));
		_ = _/**/		.n(I).H(P, (K.INT, 1)).n(E, K.MUL).H(P, (K.INT, 2)).uuuuU;
		_ = Parse(@"0 * a, 1 * 2, 3, 4 == 5");
		_ = _/**/	.h(B).H(P, (K.INT, "0"));
		_ = _/**/		.n(E, K.MUL).H(P, (K.NAME, "a"));
		_ = _/**/					.n(I).H(P, (K.INT, 1)).n(E, K.MUL).H(P, (K.INT, 2)).uu;
		_ = _/**/					.n(I).H(P, (K.INT, 3)).u;
		_ = _/**/					.n(I).H(P, (K.INT, 4)).n(E, K.EQ).H(P, (K.INT, 5)).uuuuuU;
	}

	[TestMethod]
	public void Input2()
	{
		var _ = Parse(@"a 1 2");
		_ = _/**/	.h(B).H(P, (K.NAME, "a"));
		_ = _/**/		.n(I).H(P, (K.INT, 1)).u.n(I).H(P, (K.INT, 2)).uuuU;
		_ = Parse(@"a 1 * 2");
		_ = _/**/	.h(B).H(P, (K.NAME, "a"));
		_ = _/**/		.n(I).H(P, (K.INT, 1)).u.n(E, K.MUL).H(P, (K.INT, 2)).uuuU;
		_ = Parse(@"0 * a 1 * 2 3 4 == 5");
		_ = _/**/	.h(B).H(P, (K.INT, "0"));
		_ = _/**/		.n(E, K.MUL).H(P, (K.NAME, "a")).n(I).H(P, (K.INT, 1)).uu;
		_ = _/**/		.n(E, K.MUL).H(P, (K.INT, 2));
		_ = _/**/					.n(I).H(P, (K.INT, 3)).u.n(I).H(P, (K.INT, 4)).uu;
		_ = _/**/		.n(E, K.EQ).H(P, (K.INT, 5)).uuuU;
	}

	[TestMethod]
	public void Input3()
	{
		var _ = Parse(@"a 1,2 3");
		_ = _/**/	.h(B).H(P, (K.NAME, "a"));
		_ = _/**/		.n(I).H(P, (K.INT, 1)).u;
		_ = _/**/		.n(I).H(P, (K.INT, 2)).n(I).H(P, (K.INT, 3)).uuuuU;
		_ = Parse(@"a,1 2,3");
		_ = _/**/	.h(B).H(P, (K.NAME, "a"));
		_ = _/**/		.n(I).H(P, (K.INT, 1)).n(I).H(P, (K.INT, 2)).uu;
		_ = _/**/		.n(I).H(P, (K.INT, 3)).uuuU;
		_ = Parse(@"0 * a 1 2,3 * b 4,5");
		_ = _/**/	.h(B).H(P, (K.INT, "0")).n(E, K.MUL);
		_ = _/**/			.H(P, (K.NAME, "a"));
		_ = _/**/			.n(I).H(P, (K.INT, 1)).u.n(I).H(P, (K.INT, 2)).u;
		_ = _/**/			.n(I).H(P, (K.INT, 3)).n(E, K.MUL);
		_ = _/**/						.H(P, (K.NAME, "b")).n(I).H(P, (K.INT, 4)).uuu;
		_ = _/**/			.n(I).H(P, (K.INT, 5)).uuuuU;
	}

	[TestMethod]
	public void Input4()
	{
		var _ = Parse(@"a,");
		_ = _/**/	.h(B).H(P, (K.NAME, "a")).uuU;
		_ = Parse(@"a,1,");
		_ = _/**/	.h(B).H(P, (K.NAME, "a")).n(I).H(P, (K.INT, 1)).uuuU;
		_ = Parse(@"a 1,");
		_ = _/**/	.h(B).H(P, (K.NAME, "a")).n(I).H(P, (K.INT, 1)).uuuU;
		_ = Parse(@"(0 * (a) 1,2 3,)");
		_ = _/**/	.h(B).H(P, (K.INT, "0")).n(E, K.MUL);
		_ = _/**/			.H(P, (K.NAME, "a"));
		_ = _/**/			.n(I).H(P, (K.INT, 1)).u;
		_ = _/**/			.n(I).H(P, (K.INT, 2)).n(I).H(P, (K.INT, 3)).uuuuuU;
	}
}
