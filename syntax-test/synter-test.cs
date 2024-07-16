//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

#pragma warning disable IDE0059
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using qutum.syntax;
using System;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax;

using Ser = (Synt t, Synter s);

static class TestExtension
{
	public static Ser Eq(this Ser t,
		Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
	{
		AreNotEqual(null, t.t);
		AreEqual(err, t.t.err);
		if (name != null) AreEqual(name, t.t.name);
		var (fl, fc, tl, tc) = t.s.ler.LineCol(t.t.from, t.t.to);
		if (from != null) AreEqual($"{from}", $"{fl}.{fc}");
		if (to != null) AreEqual($"{to}", $"{tl}.{tc}");
		if (v != null)
			AreEqual(v, v is Lex && t.t.info is Lexi<Lex> l ? l.key : t.t.info);
		return t;
	}
	public static Ser V(this Ser t, params object[] vs)
	{
		Lexi<Lex>[] s = new Lexi<Lex>[vs.Length];
		int n = 0;
		for (int x = 0; x < vs.Length; x++)
			if (vs[x] is Lex l)
				s[n++] = new Lexi<Lex> { key = l };
			else
				s[n - 1].value = vs[x];
		AreEqual(t.s.dumper(s.Seg(0, n)),
			t.s.dumper(t.t.from >= 0 ? t.s.ler.Lexs(t.t.from, t.t.to)
				: t.s.ler.errs.GetRange(~t.t.from, ~t.t.to - ~t.t.from).ToArray().Seg()));
		return t;
	}

	public static Ser H(this Ser t,
		Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		=> (t.t.head, t.s).Eq(name, err, from, to, v);
	public static Ser T(this Ser t,
		Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		=> (t.t.tail, t.s).Eq(name, err, from, to, v);
	public static Ser N(this Ser t,
		Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		=> (t.t.next, t.s).Eq(name, err, from, to, v);
	public static Ser P(this Ser t,
		Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		=> (t.t.prev, t.s).Eq(name, err, from, to, v);

	public static Ser H0(this Ser t) { AreEqual(null, t.t.head); return t; }
	public static Ser N0(this Ser t) { AreEqual(null, t.t.next); return (t.t.up, t.s); }
	public static Ser P0(this Ser t) { AreEqual(null, t.t.prev); return (t.t.up, t.s); }
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

	[TestMethod]
	public void Blocks()
	{
		var t = Parse("").Eq(Syn.all, 0).H0().N0();
		t = Parse(@"
			1
			2");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1).N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 2).N0().N0().N0();
		t = Parse(@"
				1
				2
	3
4");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1).N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 2).N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 3).N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 4).N0().N0().N0();
	}

	[TestMethod]
	public void Binary()
	{
		var t = Parse(@"
			1
				-2
				*3/4");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.SUB);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2).N0();
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 3);
		t = t/**/					.N(Syn.b53, v: Lex.DIV);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 4).N0().N0().N0().N0().N0();
	}

	[TestMethod]
	public void Prefix1()
	{
		var t = Parse(@"
			-
				1");
		t = t/**/	.H(Syn.Block).H(Syn.pre, v: Lex.SUB);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 1).N0().N0().N0().N0();
		t = Parse(@"
			-
				-1
				* --
					2");
		t = t/**/	.H(Syn.Block).H(Syn.pre, v: Lex.SUB);
		t = t/**/					.H(Syn.e2, v: Lex.SUB).H(Syn.e0).V(Lex.INT, 1).N0().N0();
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.pre, v: Lex.BNOT);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 2).N0().N0().N0().N0().N0();
	}

	[TestMethod]
	public void Prefix2()
	{
		var t = Parse(@"
			1
				--2");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.e2p, v: Lex.BNOT);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2).N0().N0().N0();
		t = Parse(@"
			1
				--2
					*3
				/4");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat);
		t = t/**/					.H(Syn.e2p, v: Lex.BNOT).H(Syn.e0).V(Lex.INT, 2).N0();
		t = t/**/					.N(Syn.stat, v: Lex.MUL).H(Syn.e0).V(Lex.INT, 3).N0().N0();
		t = t/**/				.N(Syn.stat, v: Lex.DIV);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 4).N0().N0().N0().N0();
	}

	[TestMethod]
	public void HeadRight()
	{
		var t = Parse(@"
			1
					+2
				*3");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.headr).H(Syn.stat, v: Lex.ADD);
		t = t/**/							.H(Syn.e0).V(Lex.INT, 2).N0().N0();
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 3).N0().N0().N0().N0();
		t = Parse(@"
			1
						+2
					-3
				*4");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.headr).H(Syn.stat, v: Lex.ADD).H(Syn.e0).V(Lex.INT, 2).N0();
		t = t/**/							.N(Syn.stat, v: Lex.SUB).H(Syn.e0).V(Lex.INT, 3).N0().N0();
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 4).N0().N0().N0();
		t = t/**/	.N(null, -1, 4.1, 4.6, Lex.INDR).N0();
	}

	[TestMethod]
	public void Nested1()
	{
		ser.dump = 3;
		var t = Parse(@"
			1
				2
					3
				* 4");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2);
		t = t/**/					.N(Syn.e0).V(Lex.INT, 3).N0();
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 4).N0().N0().N0().N0();
		t = Parse(@"
			1
				*2
					-3
						+4
				- 5");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2);
		t = t/**/					.N(Syn.stat, v: Lex.SUB);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 3);
		t = t/**/						.N(Syn.stat, v: Lex.ADD);
		t = t/**/							.H(Syn.e0).V(Lex.INT, 4).N0().N0().N0();
		t = t/**/				.N(Syn.stat, v: Lex.SUB);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 5).N0().N0().N0().N0();
	}

	[TestMethod]
	public void Nested2()
	{
		var t = Parse(@"
			1
				+ 2
					* 3
					/ 4
						+5
			6
				- 7");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2);
		t = t/**/					.N(Syn.stat, v: Lex.MUL);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 3).N0();
		t = t/**/					.N(Syn.stat, v: Lex.DIV);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 4);
		t = t/**/						.N(Syn.stat, v: Lex.ADD);
		t = t/**/							.H(Syn.e0).V(Lex.INT, 5).N0().N0().N0().N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 6);
		t = t/**/				.N(Syn.stat, v: Lex.SUB);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 7).N0().N0().N0().N0();
		t = Parse(@"
			1
				+
					2
						* 3
						/ 4
							+5
			6
				-
					7");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2);
		t = t/**/					.N(Syn.stat, v: Lex.MUL);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 3).N0();
		t = t/**/					.N(Syn.stat, v: Lex.DIV);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 4);
		t = t/**/						.N(Syn.stat, v: Lex.ADD);
		t = t/**/							.H(Syn.e0).V(Lex.INT, 5).N0().N0().N0().N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 6);
		t = t/**/				.N(Syn.stat, v: Lex.SUB);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 7).N0().N0().N0().N0();
	}

	[TestMethod]
	public void RecoverRight1()
	{
		var t = Parse(@"
			1
				+");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.right, -4).N0().N0().N0();
		t = t/**/.N(Syn.all, -1, 3.6, 3.6, Lex.DED);
		t = t/**/	.H(Syn.right, 1).N0().N0();
	}

	[TestMethod]
	public void RecoverRight2()
	{
		var t = Parse(@"
			1
				+
			2
			3");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.right, -4).N0().N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 2).N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 3).N0().N0();
		t = t/**/.N(Syn.all, -1, 4.1, 4.1, v: Lex.DED);
		t = t/**/	.H(Syn.right, 1).N0().N0();
	}

	[TestMethod]
	public void RecoverRight3()
	{
		var t = Parse(@"
			1
				+
				* 2
			3");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.right, -4).N0();
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2).N0().N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 3).N0().N0();
		t = t/**/.N(Syn.all, -1, 4.5, 4.6, v: Lex.MUL);
		t = t/**/	.H(Syn.right, 1).N0().N0();
	}

	[TestMethod]
	public void RecoverBlock()
	{
		var t = Parse(@"
			1
				+
					2
						* 3
					/ 6
				- 4
			5");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2);
		t = t/**/					.N(Syn.stat, v: Lex.MUL);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 3).N0();
		t = t/**/					.N(Syn.nest, -4).N0();
		t = t/**/				.N(Syn.stat, v: Lex.SUB);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 4).N0().N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 5).N0().N0();
		t = t/**/.N(Syn.all, -1, 6.6, 6.7, Lex.DIV);
		t = t/**/	.H(Syn.nest, 2).N0().N0();
		t = Parse(@"
			1
				+
					2
						* 3
					/ 6
							/ 7
						* 8
				- 4
			5");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2);
		t = t/**/					.N(Syn.stat, v: Lex.MUL);
		t = t/**/						.H(Syn.e0).V(Lex.INT, 3).N0();
		t = t/**/					.N(Syn.nest, -4).N0();
		t = t/**/				.N(Syn.stat, v: Lex.SUB);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 4).N0().N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 5).N0().N0();
		t = t/**/.N(Syn.all, -1, 6.6, 6.7, Lex.DIV);
		t = t/**/	.H(Syn.nest, 2).N0().N0();
	}

	[TestMethod]
	public void RecoverLine()
	{
		var t = Parse(@"
			1 */
						+2
				*3");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1).N(Syn.line, -4);
		t = t/**/				.N(Syn.headr).H(Syn.stat, v: Lex.ADD);
		t = t/**/							.H(Syn.e0).V(Lex.INT, 2).N0().N0();
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 3).N0().N0().N0();
		t = t/**/.N(Syn.all, -1, 2.7, 2.8, Lex.DIV);
		t = t/**/	.H(Syn.b53, 1).N0().N0();
	}

	[TestMethod]
	public void RecoverStat1()
	{
		var t = Parse(@"
			1
				)
				+ 2");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.linep, -4);
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2).N0().N0().N0();
		t = t/**/.N(Syn.all, -1, 3.5, 3.6, Lex.RP);
		t = t/**/	.H(Syn.linep, 1).N0().N0();
	}

	[TestMethod]
	public void RecoverStat2()
	{
		var t = Parse(@"
			1
				*)
				+2
				-3/
			4");
		t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
		t = t/**/				.N(Syn.stat, v: Lex.MUL);
		t = t/**/					.H(Syn.line, -4).N0();
		t = t/**/				.N(Syn.stat, v: Lex.ADD);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 2).N0();
		t = t/**/				.N(Syn.stat, v: Lex.SUB);
		t = t/**/					.H(Syn.e0).V(Lex.INT, 3).N(Syn.line, -4).N0().N0();
		t = t/**/	.N(Syn.Block).H(Syn.e0).V(Lex.INT, 4).N0().N0();
		t = t/**/.N(Syn.all, -1, 3.6, 3.7, Lex.RP);
		t = t/**/	.H(Syn.stat, 1).N(Syn.line, 1).N0();
		t = t/**/.N(Syn.all, -1, 5.8, 6.1, Lex.EOL);
		t = t/**/	.H(Syn.b53, 1).N0().N0();
	}
}
