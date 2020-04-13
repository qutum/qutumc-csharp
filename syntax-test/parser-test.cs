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
			if (t.t.err != 0 && v != null) AreEqual(v, t.t.info);
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
		public void RecoverBinary()
		{
			var t = Parse("a\n\t+");
			t = t/**/	.H(Syn.block).H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/				.N(Syn.opb).V(Lex.ADD).N(Syn.bin, -5).N0().N0();
			t = t/**/.N(Syn.all, -1, 2.3).V(Lex.DED, 0);
			t = t/**/				.H(Syn.bin, 2).N0().N0();
			t = Parse("\t\ta\n\t\t\t+\n\t\tc\n\t\td");
			t = t/**/	.H(Syn.block).H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/				.N(Syn.opb).V(Lex.ADD).N(Syn.bin, -5).N0();
			t = t/**/	.N(Syn.block).H(Syn.exp).V(Lex.WORD, "c", Lex.EOL).N0();
			t = t/**/	.N(Syn.block).H(Syn.exp).V(Lex.WORD, "d", Lex.EOL).N0().N0();
			t = t/**/.N(Syn.all, -1, 3.1, 3.2).V(Lex.DED, 2);
			t = t/**/				.H(Syn.bin, 2).N0().N0();
			t = Parse("a\n\t+\n\t* b\nc");
			t = t/**/	.H(Syn.block).H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/				.N(Syn.opb).V(Lex.ADD).N(Syn.bin, -5);
			t = t/**/				.N(Syn.opb).V(Lex.MUL);
			t = t/**/				.N(Syn.block).H(Syn.exp).V(Lex.WORD, "b", Lex.EOL).N0().N0();
			t = t/**/	.N(Syn.block).H(Syn.exp).V(Lex.WORD, "c", Lex.EOL).N0().N0();
			t = t/**/.N(Syn.all, -1, 3.2).V(Lex.MUL);
			t = t/**/				.H(Syn.bin, 2).N0().N0();
		}

		[TestMethod]
		public void RecoverStat()
		{
			var t = Parse("a\n\tb\n\t+ 1");
			t = t/**/	.H(Syn.block).H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/				.N(Syn.stat, -3);
			t = t/**/				.N(Syn.opb).V(Lex.ADD);
			t = t/**/				.N(Syn.block).H(Syn.exp).V(Lex.INT, 1, Lex.EOL).N0().N0().N0();
			t = t/**/.N(Syn.all, -1, 2.3).V(Lex.EOL).N0();
			t = Parse("a\n\tb\n\t+ 1\n\tc\nd");
			t = t/**/	.H(Syn.block).H(Syn.exp).V(Lex.WORD, "a", Lex.EOL);
			t = t/**/				.N(Syn.stat, -3);
			t = t/**/				.N(Syn.opb).V(Lex.ADD);
			t = t/**/				.N(Syn.block).H(Syn.exp).V(Lex.INT, 1, Lex.EOL).N0();
			t = t/**/				.N(Syn.stat, -3).N0();
			t = t/**/	.N(Syn.block).H(Syn.exp).V(Lex.WORD, "d", Lex.EOL).N0().N0();
			t = t/**/.N(Syn.all, -1, 2.3).V(Lex.EOL);
			t = t/**/.N(Syn.all, -1, 4.3).V(Lex.EOL).N0();
		}
	}
}
