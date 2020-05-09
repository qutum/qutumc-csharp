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

namespace qutum.test.syntax
{
	static class TestExtension
	{
		public static (Tree, Parser) Eq(this (Tree t, Parser p) t,
			Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
		{
			AreNotEqual(null, t.t);
			AreEqual(err, t.t.err);
			if (name != null) AreEqual(name, t.t.name);
			var (fl, fc, tl, tc) = t.p.scan.LineCol(t.t.from, t.t.to);
			if (from != null) AreEqual($"{from}", $"{fl}.{fc}");
			if (to != null) AreEqual($"{to}", $"{tl}.{tc}");
			if (v != null)
				AreEqual(v, v is Lex && t.t.info is Token<Lex> tk ? tk.key : t.t.info);
			return t;
		}
		public static (Tree, Parser) V(this (Tree t, Parser p) t, params object[] vs)
		{
			Token<Lex>[] s = new Token<Lex>[vs.Length];
			int n = 0;
			for (int x = 0; x < vs.Length; x++)
				if (vs[x] is Lex l)
					s[n++] = new Token<Lex> { key = l };
				else
					s[n - 1].value = vs[x];
			AreEqual(t.p.dumper(s.AsSeg(0, n)),
				t.p.dumper(t.t.from >= 0 ? t.p.scan.Tokens(t.t.from, t.t.to)
				: t.p.scan.errs.GetRange(~t.t.from, ~t.t.to - ~t.t.from).ToArray().AsSeg()));
			return t;
		}

		public static (Tree, Parser) H(this (Tree t, Parser p) t,
			Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
			=> (t.t.head, t.p).Eq(name, err, from, to, v);
		public static (Tree, Parser) T(this (Tree t, Parser p) t,
			Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
			=> (t.t.tail, t.p).Eq(name, err, from, to, v);
		public static (Tree, Parser) N(this (Tree t, Parser p) t,
			Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
			=> (t.t.next, t.p).Eq(name, err, from, to, v);
		public static (Tree, Parser) P(this (Tree t, Parser p) t,
			Syn? name = null, int err = 0, double? from = null, double? to = null, object v = null)
			=> (t.t.prev, t.p).Eq(name, err, from, to, v);

		public static (Tree, Parser) U(this (Tree t, Parser s) t) => (t.t.up, t.s);
		public static (Tree, Parser) H0(this (Tree t, Parser) t) { AreEqual(null, t.t.head); return t; }
		public static (Tree, Parser) N0(this (Tree t, Parser) t) { AreEqual(null, t.t.next); return t.U(); }
		public static (Tree, Parser) P0(this (Tree t, Parser) t) { AreEqual(null, t.t.prev); return t.U(); }
	}

	[TestClass]
	public class TestParser : IDisposable
	{
		readonly EnvWriter env = EnvWriter.Begin();

		public void Dispose() => env.Dispose();

		readonly Parser p = new Parser(new Lexer()) { dump = 2 };

		(Tree t, Parser) Parse(string input)
		{
			env.WriteLine(input);
			if (p.scan.scan != null)
				p.scan.Dispose();
			p.scan.Load(new ScanByte(Encoding.UTF8.GetBytes(input)));
			var t = p.Parse().Dump((Func<int, int, (int, int, int, int)>)p.scan.LineCol);
			env.WriteLine($"---- match {p.matchn} / loc {p.locn} = {p.matchn / Math.Max(p.locn, 1)} ----");
			return (t, p);
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
		public void PrefixBlock()
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
		public void PrefixStat()
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
				*3");
			t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
			t = t/**/				.N(Syn.headr).H(Syn.stat, v: Lex.ADD);
			t = t/**/							.H(Syn.e0).V(Lex.INT, 2).N0().N0();
			t = t/**/				.N(Syn.stat, v: Lex.MUL);
			t = t/**/					.H(Syn.e0).V(Lex.INT, 3).N0().N0().N0().N0();
		}

		[TestMethod]
		public void Nested1()
		{
			p.dump = 3;
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
			t = t/**/.N(Syn.all, -1, 4.1, 4.3, v: Lex.DED);
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
			t = t/**/.N(Syn.all, -1, 4.5, 4.5, v: Lex.MUL);
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
			t = t/**/.N(Syn.all, -1, 6.6, 6.6, Lex.DIV);
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
			t = t/**/.N(Syn.all, -1, 6.6, 6.6, Lex.DIV);
			t = t/**/	.H(Syn.nest, 2).N0().N0();
		}

		[TestMethod]
		public void RecoverHeadRight()
		{
			var t = Parse(@"
			1
						+2
					*4
				-3");
			t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
			t = t/**/				.N(Syn.headr).H(Syn.stat, v: Lex.ADD);
			t = t/**/							.H(Syn.e0).V(Lex.INT, 2).N0().N0();
			t = t/**/				.N(Syn.headr, -4);
			t = t/**/				.N(Syn.stat, v: Lex.SUB);
			t = t/**/					.H(Syn.e0).V(Lex.INT, 3).N0().N0().N0();
			t = t/**/.N(Syn.all, -1, 4.6, 4.6, Lex.MUL);
			t = t/**/	.H(Syn.headr, 2, v: Lex.DED).N0().N0();
			t = Parse(@"
			1
						+2
					*4
							/5
						+6
				-3");
			t = t/**/	.H(Syn.Block).H(Syn.e0).V(Lex.INT, 1);
			t = t/**/				.N(Syn.headr).H(Syn.stat, v: Lex.ADD);
			t = t/**/							.H(Syn.e0).V(Lex.INT, 2).N0().N0();
			t = t/**/				.N(Syn.headr, -4);
			t = t/**/				.N(Syn.stat, v: Lex.SUB);
			t = t/**/					.H(Syn.e0).V(Lex.INT, 3).N0().N0().N0();
			t = t/**/.N(Syn.all, -1, 4.6, 4.6, Lex.MUL);
			t = t/**/	.H(Syn.headr, 2, v: Lex.DED).N0().N0();
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
			t = t/**/.N(Syn.all, -1, 2.7, 2.7, Lex.DIV);
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
			t = t/**/.N(Syn.all, -1, 3.5, 3.5, Lex.RP);
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
			t = t/**/.N(Syn.all, -1, 3.6, 3.6, Lex.RP);
			t = t/**/	.H(Syn.stat, 1).N(Syn.line, 1).N0();
			t = t/**/.N(Syn.all, -1, 5.8, 5.8, Lex.EOL);
			t = t/**/	.H(Syn.b53, 1).N0().N0();
		}
	}
}
