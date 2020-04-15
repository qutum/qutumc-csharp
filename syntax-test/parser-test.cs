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
			if (t.t.err != 0 && v != null)
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
			env.WriteLine($"---- largest {p.largest} ----");
			return (t, p);
		}

		[TestMethod]
		public void Blocks()
		{
			var t = Parse(@"
			a
			b");
			t = t/**/	.H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/	.N(Syn.exp).V(Lex.WORD, "b", Lex.EOL).N0().N0();
			t = Parse(@"
					a
				b
				c
			d");
			t = t/**/	.H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/	.N(Syn.exp).V(Lex.WORD, "b", Lex.EOL);
			t = t/**/	.N(Syn.exp).V(Lex.WORD, "c", Lex.EOL);
			t = t/**/	.N(Syn.exp).V(Lex.WORD, "d", Lex.EOL).N0().N0();
		}

		[TestMethod]
		public void Stats()
		{
			var t = Parse(@"
			a
				- 1
				* 2");
			t = t/**/	.H(Syn.istat, v: Lex.MUL);
			t = t/**/		.H(Syn.istat, v: Lex.SUB);
			t = t/**/			.H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/			.N(Syn.exp).V(Lex.INT, 1, Lex.EOL).N0();
			t = t/**/		.N(Syn.exp).V(Lex.INT, 2, Lex.EOL).N0().N0().N0();
		}

		[TestMethod]
		public void HeadRight()
		{
			var t = Parse(@"
			a
					+a
				-b");
		}

		[TestMethod]
		public void Nested1()
		{
			p.dump = 3;
			var t = Parse(@"
			a + b
				*2
				## comment
					<< c
						+ 1
				- d");
			t = Parse(@"
			a
				+ 2

					- b
				* c");
		}

		[TestMethod]
		public void Nested2()
		{
			Parse(@"
			a
				+ b
					* c
				/
					d
						+e
			b
				- c
			");
			Parse(@"
			a
					+ b
				* c
					- d
			b
				/ c
			");
		}

		[TestMethod]
		public void RecoverBinary1()
		{
			var t = Parse(@"
			a
				+");
			t = t/**/	.H(Syn.istat, v: Lex.ADD);
			t = t/**/		.H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/		.N(Syn.right, -4).N0().N0();
			t = t/**/.N(Syn.all, -1, 3.6, 3.6, Lex.DED);
			t = t/**/	.H(Syn.right, 1).N0().N0();
		}

		[TestMethod]
		public void RecoverBinary2()
		{
			var t = Parse(@"
			a
				+
			c
			d");
			t = t/**/	.H(Syn.istat, v: Lex.ADD);
			t = t/**/		.H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/		.N(Syn.right, -4).N0();
			t = t/**/	.N(Syn.exp).V(Lex.WORD, "c", Lex.EOL);
			t = t/**/	.N(Syn.exp).V(Lex.WORD, "d", Lex.EOL).N0();
			t = t/**/.N(Syn.all, -1, 4.1, 4.3, v: Lex.DED);
			t = t/**/	.H(Syn.right, 1).N0().N0();
		}

		[TestMethod]
		public void RecoverBinary3()
		{
			var t = Parse(@"
			a
				+
				* b
			c");
			t = t/**/	.H(Syn.istat, v: Lex.MUL);
			t = t/**/		.H(Syn.istat, v: Lex.ADD);
			t = t/**/			.H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/			.N(Syn.right, -4).N0();
			t = t/**/		.N(Syn.exp).V(Lex.WORD, "b", Lex.EOL).N0();
			t = t/**/	.N(Syn.exp).V(Lex.WORD, "c", Lex.EOL).N0();
			t = t/**/.N(Syn.all, -1, 4.5, 4.5, v: Lex.MUL);
			t = t/**/	.H(Syn.right, 1).N0().N0();
		}

		[TestMethod]
		public void RecoverStat1()
		{
			var t = Parse(@"
			a
				b
				+ 1");
			t = t/**/	.H(Syn.istat, v: Lex.ADD);
			t = t/**/		.H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/		.N(Syn.istat, -4);
			t = t/**/		.N(Syn.exp).V(Lex.INT, 1, Lex.EOL).N0().N0();
			t = t/**/.N(Syn.all, -1, 3.5, 3.5).V(Lex.WORD, "b");
			t = t/**/	.H(Syn.istat, 1, v: Lex.OPBIN).N0().N0();
		}

		[TestMethod]
		public void RecoverStat2()
		{
			var t = Parse(@"
			a
				b
				+ 1
				c
			d");
			t = t/**/	.H(Syn.istat, v: Lex.ADD);
			t = t/**/		.H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/		.N(Syn.istat, -4);
			t = t/**/		.N(Syn.exp).V(Lex.INT, 1, Lex.EOL).N0();
			t = t/**/	.N(Syn.istat, -4);
			t = t/**/	.N(Syn.exp).V(Lex.WORD, "d", Lex.EOL).N0();
			t = t/**/.N(Syn.all, -1, 3.5, 3.5).V(Lex.WORD, "b");
			t = t/**/	.H(Syn.istat, 1, v: Lex.OPBIN).N0();
			t = t/**/.N(Syn.all, -1, 5.5, 5.5).V(Lex.WORD, "c");
			t = t/**/	.H(Syn.istat, 1, v: Lex.OPBIN).N0().N0();
		}
	}
}
