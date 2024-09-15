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
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
	{
		AreNotEqual(null, s.t);
		AreEqual(err, s.t.err);
		if (name != null) AreEqual(name, s.t.name);
		var (fl, fc, tl, tc) = s.s.ler.LineCol(s.t.from, s.t.to);
		if (from != null) AreEqual($"{from?.d}", $"{fl}.{fc}");
		if (to != null) AreEqual($"{to?.d}", $"{tl}.{tc}");
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
		AreEqual(s.s.dumper(ds.Seg(0, n)),
			s.s.dumper(s.t.from >= 0 ? s.s.ler.Lexs(s.t.from, s.t.to)
				: s.s.ler.errs.GetRange(~s.t.from, ~s.t.to - ~s.t.from).ToArray().Seg()));
		return s;
	}

	public static Ser h(this Ser s,
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
		=> (s.t.head, s.s).Eq(name, d, from, to, err).Vine();
	public static Ser t(this Ser s,
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
		=> (s.t.tail, s.s).Eq(name, d, from, to, err).Vine();
	public static Ser n(this Ser s,
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
		=> (s.t.next, s.s).Eq(name, d, from, to, err).Vine();
	public static Ser p(this Ser s,
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
		=> (s.t.prev, s.s).Eq(name, d, from, to, err).Vine();

	public static Ser H(this Ser s,
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
		=> (s.t.head, s.s).Eq(name, d, from, to, err).Leaf();
	public static Ser T(this Ser s,
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
		=> (s.t.tail, s.s).Eq(name, d, from, to, err).Leaf();
	public static Ser N(this Ser s,
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
		=> (s.t.next, s.s).Eq(name, d, from, to, err).Leaf();
	public static Ser P(this Ser s,
		S? name = null, object d = null, Dec? from = null, Dec? to = null, int err = 0)
		=> (s.t.prev, s.s).Eq(name, d, from, to, err).Leaf();

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
}

[TestClass]
public class TestSynter : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	readonly Synter ser = new Synter().Begin(new());

	Ser Parse(string read)
	{
		env.WriteLine(read);
		ser.ler.Dispose();
		ser.ler.Begin(new LerByte(Encoding.UTF8.GetBytes(read)));
		return (ser.Parse().Dump(ser.Dumper), ser);
	}
	public const S B = S.Block, N = S.nest, E = S.exp, Eh = S.exph, I = S.inp;

	[TestMethod]
	public void Blocks()
	{
		var t = Parse("").Eq(S.all).U();
		t = Parse(@"
			1
			2");
		t = t/**/	.h(B).H(Eh, (L.INT, 1)).u();
		t = t/**/	.n(B).H(Eh, (L.INT, 2)).uuU();
		t = Parse(@"
				1
		\##\ 2
				3
	\##\ 4
	5
6");
		t = t/**/	.h(B).H(Eh, (L.INT, 1)).u();
		t = t/**/	.n(B).H(Eh, (L.INT, 2)).u();
		t = t/**/	.n(B).H(Eh, (L.INT, 3)).u();
		t = t/**/	.n(B).H(Eh, (L.INT, 4)).u();
		t = t/**/	.n(B).H(Eh, (L.INT, 5)).u();
		t = t/**/	.n(B).H(Eh, (L.INT, 6)).uu();
		t = t/**/.e(-3).H(null, L.INDR, 3.1, 3.3, -3).uU();
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
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N).H(Eh, (L.INT, 2));
		t = t/**/			.n(N).H(Eh, (L.INT, 3)).uu();
		t = t/**/		.n(N).H(Eh, (L.INT, 4)).uuuU();
		t = Parse(@"
			1
				2
				3
					4
						5
					6
			7");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N).H(Eh, (L.INT, 2)).u();
		t = t/**/		.n(N).H(Eh, (L.INT, 3));
		t = t/**/			.n(N).H(Eh, (L.INT, 4));
		t = t/**/				.n(N).H(Eh, (L.INT, 5)).uu();
		t = t/**/			.n(N).H(Eh, (L.INT, 6)).uuu();
		t = t/**/	.n(B).H(Eh, (L.INT, 7)).uuU();
	}

	[TestMethod]
	public void RecoverNested()
	{
		var t = Parse(@"
			1
				)
				2");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N).H(S.line, err: 1).u();
		t = t/**/		.n(N).H(Eh, (L.INT, 2)).uuu();
		t = t/**/.e().H(null, "line expression", 3.5, 3.6, -1).uU();
	}

	[TestMethod]
	public void NestedRight()
	{
		var t = Parse(@"
			1
					2
						3
					4");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(S.nestr).h(N);
		t = t/**/					.H(Eh, (L.INT, 2));
		t = t/**/					.n(N).H(Eh, (L.INT, 3)).uu();
		t = t/**/				.n(N).H(Eh, (L.INT, 4)).uuuuU();
		t = Parse(@"
			1
						2
								3
					4
				5");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(S.nestr).h(N).H(Eh, (L.INT, 2));
		t = t/**/						.n(S.nestr).h(N).H(Eh, (L.INT, 3)).uuu();
		t = t/**/				.n(N).H(Eh, (L.INT, 4)).uu();
		t = t/**/		.n(N).H(Eh, (L.INT, 5)).uuu();
		t = t/**/.e(-3).H(null, L.INDR, 5.1, 5.6, -3).uU();
	}

	[TestMethod]
	public void RecoverLine()
	{
		var t = Parse(@"
			1 *>
						2");
		t = t/**/	.h(B).H(Eh, (L.INT, 1)).N(S.line, err: 1);
		t = t/**/		.n(S.nestr).h(N).H(Eh, (L.INT, 2)).uuuu();
		t = t/**/.e().H(null, "arithmetic expression", 2.7, 2.8, -1).uU();
	}

	[TestMethod]
	public void RecoverExp()
	{
		var t = Parse(@"
			1
				!3/
					- 4-
				5");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N).h(E, L.NOT).H(Eh, (L.INT, 3)).u();
		t = t/**/			.N(S.line, err: 1);
		t = t/**/			.n(N, L.SUB).H(Eh, (L.INT, 4)).N(S.line, err: 1).uu();
		t = t/**/		.n(N).H(Eh, (L.INT, 5)).uuu();
		t = t/**/.e().H(null, "arithmetic expression", 3.8, 4.1, -1);
		t = t/**/	.N(null, "arithmetic expression", 4.10m, 5.1, -1).uU();
	}

	[TestMethod]
	public void PrefixNested()
	{
		var t = Parse(@"
			1
				--2
				- 3");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N).h(E, L.NOTB).H(Eh, (L.INT, 2)).uu();
		t = t/**/		.n(N, L.SUB).H(Eh, (L.INT, 3)).uuuU();
		t = Parse(@"
			1
				- -2
					*!3
				/+4");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N, L.SUB);
		t = t/**/			.h(E, L.NEGA).H(Eh, (L.INT, 2)).u();
		t = t/**/			.n(N, L.MUL).h(E, L.NOT).H(Eh, (L.INT, 3)).uuu();
		t = t/**/		.n(N, L.DIV);
		t = t/**/			.h(E, L.POSI).H(Eh, (L.INT, 4)).uuuuU();
	}

	[TestMethod]
	public void Binary()
	{
		var t = Parse(@"1+2*3-4");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(E, L.ADD).H(Eh, (L.INT, 2));
		t = t/**/					.n(E, L.MUL).H(Eh, (L.INT, 3)).uu();
		t = t/**/		.n(E, L.SUB).H(Eh, (L.INT, 4)).uuuU();
		t = Parse(@"1 + 2 * 3 >> 4 % 5 < 6 != 7 ++ 8");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(E, L.ADD).H(Eh, (L.INT, 2));
		t = t/**/			.n(E, L.MUL).H(Eh, (L.INT, 3));
		t = t/**/				.n(E, L.SHR).H(Eh, (L.INT, 4)).uu();
		t = t/**/			.n(E, L.MOD).H(Eh, (L.INT, 5)).uu();
		t = t/**/		.n(E, L.LT).H(Eh, (L.INT, 6)).u();
		t = t/**/		.n(E, L.XOR).H(Eh, (L.INT, 7));
		t = t/**/			.n(E, L.XORB).H(Eh, (L.INT, 8)).uuuuU();
	}

	[TestMethod]
	public void BinaryNested()
	{
		var t = Parse(@"
			1
				- 2
				*3+4");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N, L.SUB).H(Eh, (L.INT, 2)).u();
		t = t/**/		.n(N, L.MUL).H(Eh, (L.INT, 3));
		t = t/**/					.n(E, L.ADD).H(Eh, (L.INT, 4)).uuuuU();
		t = Parse(@"
			1
				* 2
					- 3
						/ 4
					% 5
				&& 6
			7
				- 8");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N, L.MUL).H(Eh, (L.INT, 2));
		t = t/**/					.n(N, L.SUB).H(Eh, (L.INT, 3));
		t = t/**/								.n(N, L.DIV).H(Eh, (L.INT, 4)).uu();
		t = t/**/					.n(N, L.MOD).H(Eh, (L.INT, 5)).uu();
		t = t/**/		.n(N, L.ANDB).H(Eh, (L.INT, 6)).uu();
		t = t/**/	.n(B).H(Eh, (L.INT, 7));
		t = t/**/		.n(N, L.SUB).H(Eh, (L.INT, 8)).uuuU();
	}

	[TestMethod]
	public void RecoverBinNest()
	{
		var t = Parse(@"
			1
				*");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N, L.MUL).H(S.line, err: 1).uuu();
		t = t/**/.e().H(null, "nested binary block block", 3.6, 3.6, -1).uU();
		t = Parse(@"
			1
				+
					2
					*3
				/ 4
			-
				5");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(N, L.ADD).H(S.line, err: 1);
		t = t/**/					.n(N).H(Eh, (L.INT, 2)).u();
		t = t/**/					.n(N, L.MUL).H(Eh, (L.INT, 3)).uu();
		t = t/**/		.n(N, L.DIV).H(Eh, (L.INT, 4)).uu();
		t = t/**/	.n(B).H(S.line, err: 1);
		t = t/**/		.n(N).H(Eh, (L.INT, 5)).uuu();
		t = t/**/.e().H(null, "nested binary block block", 3.6, 4.1, -1);
		t = t/**/	.N(null, "line expression", 7.4, 7.5, -1).uU();
	}

	[TestMethod]
	public void Parath()
	{
		var t = Parse(@"(1)");
		t = t/**/	.h(B).H(Eh, (L.INT, 1)).uuU();
		t = Parse(@"-(-1)/(2*(3+4))");
		t = t/**/	.h(B).h(E, L.NEGA).h(E, L.NEGA).H(Eh, (L.INT, 1)).uu();
		t = t/**/		.n(E, L.DIV).H(Eh, (L.INT, 2));
		t = t/**/			.n(E, L.MUL).H(Eh, (L.INT, 3));
		t = t/**/				.n(E, L.ADD).H(Eh, (L.INT, 4)).uuuuuU();
	}

	[TestMethod]
	public void Prefix()
	{
		var t = Parse(@"1- -2");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(E, d: L.SUB);
		t = t/**/			.h(E, L.NEGA).H(Eh, (L.INT, 2)).uuuuU();
		t = Parse(@"1--2");
		t = t/**/	.h(B).H(Eh, (L.INT, 1));
		t = t/**/		.n(I).h(E, L.NOTB).H(Eh, (L.INT, 2)).uuuu();
		t = t/**/.e(-3).H(null, L.NOTB, 1.2, 1.4, -3).uU();
	}

	[TestMethod]
	public void Input1()
	{
		var t = Parse(@"a,1,2");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a"));
		t = t/**/		.n(I).H(Eh, (L.INT, 1)).u().n(I).H(Eh, (L.INT, 2)).uuuU();
		t = Parse(@"a, 1 * 2");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a"));
		t = t/**/		.n(I).H(Eh, (L.INT, 1)).n(E, L.MUL).H(Eh, (L.INT, 2)).uuuuU();
		t = Parse(@"0 * a, 1 * 2, 3, 4 == 5");
		t = t/**/	.h(B).H(Eh, (L.INT, "0")).n(E, L.MUL);
		t = t/**/			.H(Eh, (L.NAME, "a"));
		t = t/**/			.n(I).H(Eh, (L.INT, 1)).n(E, L.MUL).H(Eh, (L.INT, 2)).uu();
		t = t/**/			.n(I).H(Eh, (L.INT, 3)).u();
		t = t/**/			.n(I).H(Eh, (L.INT, 4)).n(E, L.EQ).H(Eh, (L.INT, 5)).uuuuuU();
	}

	[TestMethod]
	public void Input2()
	{
		var t = Parse(@"a 1 2");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a"));
		t = t/**/		.n(I).H(Eh, (L.INT, 1)).u().n(I).H(Eh, (L.INT, 2)).uuuU();
		t = Parse(@"a 1 * 2");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a"));
		t = t/**/		.n(I).H(Eh, (L.INT, 1)).u().n(E, L.MUL).H(Eh, (L.INT, 2)).uuuU();
		t = Parse(@"0 * a 1 * 2 3 4 == 5");
		t = t/**/	.h(B).H(Eh, (L.INT, "0"));
		t = t/**/		.n(E, L.MUL).H(Eh, (L.NAME, "a")).n(I).H(Eh, (L.INT, 1)).uu();
		t = t/**/		.n(E, L.MUL).H(Eh, (L.INT, 2));
		t = t/**/					.n(I).H(Eh, (L.INT, 3)).u().n(I).H(Eh, (L.INT, 4)).uu();
		t = t/**/		.n(E, L.EQ).H(Eh, (L.INT, 5)).uuuU();
	}

	[TestMethod]
	public void Input3()
	{
		var t = Parse(@"a 1,2 3");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a"));
		t = t/**/		.n(I).H(Eh, (L.INT, 1)).u();
		t = t/**/		.n(I).H(Eh, (L.INT, 2)).n(I).H(Eh, (L.INT, 3)).uuuuU();
		t = Parse(@"a,1 2,3");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a"));
		t = t/**/		.n(I).H(Eh, (L.INT, 1)).n(I).H(Eh, (L.INT, 2)).uu();
		t = t/**/		.n(I).H(Eh, (L.INT, 3)).uuuU();
		t = Parse(@"0 * a 1 2,3 * b 4,5");
		t = t/**/	.h(B).H(Eh, (L.INT, "0")).n(E, L.MUL);
		t = t/**/			.H(Eh, (L.NAME, "a"));
		t = t/**/			.n(I).H(Eh, (L.INT, 1)).u().n(I).H(Eh, (L.INT, 2)).u();
		t = t/**/			.n(I).H(Eh, (L.INT, 3)).n(E, L.MUL);
		t = t/**/						.H(Eh, (L.NAME, "b")).n(I).H(Eh, (L.INT, 4)).uuu();
		t = t/**/			.n(I).H(Eh, (L.INT, 5)).uuuuU();
	}

	[TestMethod]
	public void Input4()
	{
		var t = Parse(@"a,");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a")).uuU();
		t = Parse(@"a,1,");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a")).n(I).H(Eh, (L.INT, 1)).uuuU();
		t = Parse(@"a 1,");
		t = t/**/	.h(B).H(Eh, (L.NAME, "a")).n(I).H(Eh, (L.INT, 1)).uuuU();
		t = Parse(@"(0 * a 1,2 3,)");
		t = t/**/	.h(B).H(Eh, (L.INT, "0")).n(E, L.MUL);
		t = t/**/			.H(Eh, (L.NAME, "a"));
		t = t/**/			.n(I).H(Eh, (L.INT, 1)).u();
		t = t/**/			.n(I).H(Eh, (L.INT, 2)).n(I).H(Eh, (L.INT, 3)).uuuuuU();
	}

	[TestMethod]
	public void Postfix()
	{
		var t = Parse(@"0 == a, 1 * 2, 3, .d / 5, c .");
		t = t/**/	.h(B).H(Eh, (L.INT, "0")).n(E, L.EQ);
		t = t/**/			.H(Eh, (L.NAME, "a"));
		t = t/**/			.n(I).H(Eh, (L.INT, 1)).n(E, L.MUL).H(Eh, (L.INT, 2)).uu();
		t = t/**/			.n(I).H(Eh, (L.INT, 3)).u();
		t = t/**/			.N(Eh, (L.RUN, "d"));
		t = t/**/			.n(E, L.DIV).H(Eh, (L.INT, 5));
		t = t/**/						.n(I).H(Eh, (L.NAME, "c")).N(Eh, L.RUN).uuuuuU();
		t = Parse(@"0 << a 1 * 2 3. 4 .d / 5 c .");
		t = t/**/	.h(B).H(Eh, (L.INT, "0"));
		t = t/**/		.n(E, L.SHL).H(Eh, (L.NAME, "a")).n(I).H(Eh, (L.INT, 1)).uu();
		t = t/**/		.n(E, L.MUL).H(Eh, (L.INT, 2));
		t = t/**/					.n(I).H(Eh, (L.INT, 3)).N(Eh, (L.RUNH, "")).u();
		t = t/**/					.n(I).H(Eh, (L.INT, 4)).u();
		t = t/**/					.N(Eh, L.RUN).u();
		t = t/**/		.n(E, L.DIV).H(Eh, (L.INT, 5)).n(I).H(Eh, (L.NAME, "c")).u();
		t = t/**/					.N(Eh, L.RUN).uuuU();
	}
}
