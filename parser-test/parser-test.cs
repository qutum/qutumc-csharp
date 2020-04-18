//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

#pragma warning disable IDE0059
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser
{
	static class TestExtension
	{
		public static bool Check(this ParserStr p, string input)
			=> p.Load(new ScanStr(input)).Check();

		public static (TreeStr t, ScanStr) Parse(this ParserStr p, string input)
		{
			var t = p.Load(new ScanStr(input)).Parse().Dump();
			using var env = EnvWriter.Begin();
			env.WriteLine($"---- match {p.matchn} / loc {p.locn} = {p.matchn / Math.Max(p.locn, 1)} ----");
			return (t, p.scan);
		}

		public static (TreeStr, ScanStr) Eq(this (TreeStr t, ScanStr s) t,
			string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		{
			AreNotEqual(null, t.t);
			AreEqual(err, t.t.err);
			if (name != null) AreEqual(name, t.t.name);
			if (from != null) AreEqual(from, t.t.from);
			if (to != null) AreEqual(to, t.t.to);
			if (v != null) AreEqual(v, t.t.err == 0 ? t.s.Tokens(t.t.from, t.t.to) : t.t.info);
			return t;
		}

		public static (TreeStr, ScanStr) H(this (TreeStr t, ScanStr s) t,
			string name = null, int? from = null, int? to = null, object v = null, int err = 0)
			=> (t.t.head, t.s).Eq(name, from, to, v, err);
		public static (TreeStr, ScanStr) T(this (TreeStr t, ScanStr s) t,
			string name = null, int? from = null, int? to = null, object v = null, int err = 0)
			=> (t.t.tail, t.s).Eq(name, from, to, v, err);
		public static (TreeStr, ScanStr) N(this (TreeStr t, ScanStr s) t,
			string name = null, int? from = null, int? to = null, object v = null, int err = 0)
			=> (t.t.next, t.s).Eq(name, from, to, v, err);
		public static (TreeStr, ScanStr) P(this (TreeStr t, ScanStr s) t,
			string name = null, int? from = null, int? to = null, object v = null, int err = 0)
			=> (t.t.prev, t.s).Eq(name, from, to, v, err);

		public static (TreeStr, ScanStr) U(this (TreeStr t, ScanStr s) t) => (t.t.up, t.s);
		public static (TreeStr, ScanStr) H0(this (TreeStr t, ScanStr) t) { AreEqual(null, t.t.head); return t; }
		public static (TreeStr, ScanStr) N0(this (TreeStr t, ScanStr) t) { AreEqual(null, t.t.next); return t.U(); }
		public static (TreeStr, ScanStr) P0(this (TreeStr t, ScanStr) t) { AreEqual(null, t.t.prev); return t.U(); }
	}

	[TestClass]
	public class TestParser : IDisposable
	{
		readonly EnvWriter env = EnvWriter.Begin();

		public void Dispose() => env.Dispose();

		[TestMethod]
		public void Term1()
		{
			var p = new ParserStr("S=k");
			IsTrue(p.Check("k"));
			IsFalse(p.Check("")); IsFalse(p.Check("kk")); IsFalse(p.Check("K"));
		}

		[TestMethod]
		public void Term2()
		{
			var p = new ParserStr("S=");
			IsTrue(p.Check("")); IsFalse(p.Check("a"));
		}

		[TestMethod]
		public void Alt1()
		{
			var p = new ParserStr("S=|a");
			IsTrue(p.Check("")); IsTrue(p.Check("a")); IsFalse(p.Check("b"));
		}

		[TestMethod]
		public void Alt2()
		{
			var p = new ParserStr("S=A\nA=a|1|#");
			IsTrue(p.Check("a")); IsTrue(p.Check("1")); IsTrue(p.Check("#"));
			IsFalse(p.Check("")); IsFalse(p.Check("a1")); IsFalse(p.Check("A"));
		}

		[TestMethod]
		public void Con()
		{
			var p = new ParserStr("S = A B|B A \nA = a|1 \nB = b|2");
			IsFalse(p.Check("a")); IsFalse(p.Check("1")); IsFalse(p.Check("b")); IsFalse(p.Check("2"));
			IsTrue(p.Check("ab")); IsTrue(p.Check("ba"));
			IsTrue(p.Check("1b")); IsTrue(p.Check("b1"));
			IsTrue(p.Check("a2")); IsTrue(p.Check("2a"));
			IsTrue(p.Check("12")); IsTrue(p.Check("21"));
			IsFalse(p.Check("aba")); IsFalse(p.Check("12ab"));
		}

		[TestMethod]
		public void Esc()
		{
			var p = new ParserStr(@"S = \ss\tt\r\n\\/ | \|or\=eq\*s\+p\?q");
			IsTrue(p.Check(" s\tt\r\n\\/")); IsFalse(p.Check(" s\tt\r \n\\/"));
			IsTrue(p.Check("|or=eq*s+p?q"));
		}

		[TestMethod]
		public void ErrHint()
		{
			var p = new ParserStr("S=A B =start \n A=1|2 =A12 ==oh||no\n |3 =A3 \n B= =\tempty \n |4 =B4") {
				dump = 3
			};
			IsNull(p.Parse("").t.head);
			IsNull(p.Parse("4").t.head);
			p.Parse("15").H("S", 0, 1, "empty", 1);
		}

		[TestMethod]
		public void LeftRecu()
		{
			var p = new ParserStr("S = S b|A \nA = a");
			IsTrue(p.Check("a")); IsTrue(p.Check("ab")); IsTrue(p.Check("abb")); IsTrue(p.Check("abbb"));
			IsFalse(p.Check("")); IsFalse(p.Check("b")); IsFalse(p.Check("aab")); IsFalse(p.Check("abbba"));
		}

		[TestMethod]
		public void RightRecu()
		{
			var p = new ParserStr("S = a S|B \nB = b");
			IsTrue(p.Check("b")); IsTrue(p.Check("ab")); IsTrue(p.Check("aab")); IsTrue(p.Check("aaaab"));
			IsFalse(p.Check("")); IsFalse(p.Check("a")); IsFalse(p.Check("abb")); IsFalse(p.Check("abbba"));
		}

		[TestMethod]
		public void RightRecuUnopt()
		{
			var p = new ParserStr("S= aa B \nB= A a \nA= a A|a") { dump = 3 };
			IsFalse(p.Check("aa")); IsFalse(p.Check("aaa"));
			IsTrue(p.Check("aaaa")); IsTrue(p.Check("aaaaa")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(49, p.matchn);
			p = new ParserStr("S= aa B \nB= A a \nA= A a|a") { dump = 3 };
			IsTrue(p.Check("aaaaaaa")); AreEqual(29, p.matchn);
			p = new ParserStr("S= aa B \nB= A a \nA= a+") { dump = 3 };
			IsTrue(p.Check("aaaaaaa")); AreEqual(28, p.matchn);
		}

		[TestMethod]
		public void MidRecu1()
		{
			var p = new ParserStr(@"
				S	= If|X =-
				If	= if \s S \s then \s S
					| if \s S \s then \s S \s else \s S
				X	= a|b|c|d|e|f|g|h|i|j") { dump = 3 };
			p.Parse("if a then b").H("If").H(v: "a").N(v: "b").N0();
			p.Parse("if a then b else c").H().H(v: "a").N(v: "b").N(v: "c").N0();
			var t = p.Parse("if a then if b then c").H().H(v: "a");
			t = t/**/									.N().H(v: "b").N(v: "c").N0().N0();
			t = p.Parse("if a then if b then c else d").H().H(v: "a");
			t = t/**/									   .N().H(v: "b").N(v: "c").N0();
			t = t/**/									   .N(v: "d").N0();
			t = p.Parse("if a then if b then c else d else e").H().H(v: "a");
			t = t/**/											  .N().H(v: "b").N(v: "c").N(v: "d").N0();
			t = t/**/											  .N(v: "e").N0();
			t = p.Parse("if a then if b then c else if d then e else f else g");
			t = t/**/.H().H(v: "a");
			t = t/**/	.N().H(v: "b").N(v: "c");
			t = t/**/		 .N().H(v: "d").N(v: "e").N(v: "f").N0().N0();
			t = t/**/	.N(v: "g").N0();
			t = p.Parse("if a then if b then if c then d else e else if g then h else i else j");
			t = t/**/.H().H(v: "a");
			t = t/**/	.N().H(v: "b");
			t = t/**/		.N().H(v: "c").N(v: "d").N(v: "e").N0();
			t = t/**/		.N().H(v: "g").N(v: "h").N(v: "i").N0().N0();
			t = t/**/	.N(v: "j").N0();
			IsFalse(p.Check("if a else b"));
			IsFalse(p.Check("if a then b then c"));
			IsFalse(p.Check("if a then if b else c"));
		}

		[TestMethod]
		public void MidRecu2()
		{
			var p = new ParserStr(@"
				S	= If|X =-
				If	= if \s S \s then \s S
					| if \s S \s then \s S \s else \s S
				X	= a|b|c|d|e|f|g|h|i|j") { dump = 3 };
			p.Parse("if a then b").H("If").H(v: "a").N(v: "b").N0();
			p.Parse("if a then b else c").H().H(v: "a").N(v: "b").N(v: "c").N0();
			var t = p.Parse("if a then if b then c").H().H(v: "a");
			t = t/**/									.N().H(v: "b").N(v: "c").N0().N0();
			t = p.Parse("if a then if b then c else d").H().H(v: "a");
			t = t/**/									   .N().H(v: "b").N(v: "c").N0();
			t = t/**/									   .N(v: "d").N0();
			t = p.Parse("if a then if b then c else d else e").H().H(v: "a");
			t = t/**/											  .N().H(v: "b").N(v: "c").N(v: "d").N0();
			t = t/**/											  .N(v: "e").N0();
			t = p.Parse("if a then if b then c else if d then e else f else g");
			t = t/**/.H().H(v: "a");
			t = t/**/	.N().H(v: "b").N(v: "c");
			t = t/**/		 .N().H(v: "d").N(v: "e").N(v: "f").N0().N0();
			t = t/**/	.N(v: "g").N0();
			t = p.Parse("if a then if b then if c then d else e else if g then h else i else j");
			t = t/**/.H().H(v: "a");
			t = t/**/	.N().H(v: "b");
			t = t/**/		.N().H(v: "c").N(v: "d").N(v: "e").N0();
			t = t/**/		.N().H(v: "g").N(v: "h").N(v: "i").N0().N0();
			t = t/**/	.N(v: "j").N0();
			IsFalse(p.Check("if a else b"));
			IsFalse(p.Check("if a then b then c"));
			IsFalse(p.Check("if a then if b else c"));
		}

		[TestMethod]
		public void DoubleRecu()
		{
			var p = new ParserStr("S=S S|a");
			IsFalse(p.Check(""));
			IsTrue(p.Check("a")); IsTrue(p.Check("aa")); IsTrue(p.Check("aaa")); IsTrue(p.Check("aaaa"));
		}

		[TestMethod]
		public void AddMul()
		{
			var p = new ParserStr(@"
				Expr  = Expr\+Mul | Mul
				Mul   = Mul\*Value | Value
				Value = (Expr) | Num
				Num   = Num Digi | Digi
				Digi  = 0|1|2|3|4|5|6|7|8|9") { dump = 3 };
			IsTrue(p.Check("1")); IsTrue(p.Check("07")); IsTrue(p.Check("(3)")); IsTrue(p.Check("(298)"));
			IsFalse(p.Check("1 3")); IsFalse(p.Check("(2")); IsFalse(p.Check("39210)"));
			IsTrue(p.Check("1*2")); IsTrue(p.Check("073*32")); IsTrue(p.Check("86*1231*787*99"));
			IsFalse(p.Check("1*")); IsFalse(p.Check("*3")); IsFalse(p.Check("1*2*"));
			IsTrue(p.Check("1+2")); IsTrue(p.Check("073+32")); IsTrue(p.Check("86+1231+787+99"));
			IsFalse(p.Check("1+")); IsFalse(p.Check("+3")); IsFalse(p.Check("1+2+"));
			IsTrue(p.Check("1*2+3")); IsTrue(p.Check("1+2*3")); IsTrue(p.Check("7+58*23+882*152*33+89*6"));
			IsFalse(p.Check("1*+3")); IsFalse(p.Check("+3*2")); IsFalse(p.Check("+1*2+6+"));
			IsTrue(p.Check("1*(2+3)")); IsTrue(p.Check("(1+2)*3")); IsTrue(p.Check("(7+58)*(23+882)*152*(33+89)*6"));
			IsTrue(p.Check("(1*2)+3")); IsTrue(p.Check("(1+2*3)")); IsTrue(p.Check("7+(5*23)*(15*33+89)*0"));
			IsTrue(p.Check("(1*(2+5)+2)+3")); IsTrue(p.Check("53+((((1))))+2*3"));
			IsFalse(p.Check("1*(3+)")); IsFalse(p.Check("(3))*2")); IsFalse(p.Check("(5*(2)(6)+8)"));
			IsFalse(p.Check("1*()3+5")); IsFalse(p.Check("(3))*2")); IsFalse(p.Check("(5*(2)(6)+8)"));
		}

		[TestMethod]
		public void AddMulErr()
		{
			var p = new ParserStr(@"
				Expr  = Expr\+Mul | Mul     = expression
				Mul   = Mul\*Value | Value  = expression
				Value = (Expr) | Num        = value
				Num   = Num Digi | Digi     = number
				Digi  = 0|1|2|3|4|5|6|7|8|9 = digit") { dump = 3 };
			p.Parse("(1+2*").H("Mul", 3, 5, "value", 2).N0();
			p.Parse("(*1*2+3)*4").H("Value", 0, 1, "expression", 1).N0();
			p.Parse("(1+2*3))*4").H("Mul", 0, 7, '*', 1).N("Expr", 0, 7, '+', 1).N0();
			p.Parse("(1*2+3").H("Mul", 5, 6, '*', 1).N("Value", 0, 6, ')', 2)
							.N("Expr", 1, 6, '+', 1).N("Num", 5, 6, "digit", 1).N0();
			p.Parse("(1*2+)").H("Expr", 1, 5, "expression", 2).N0();
			p.Parse("()").H("Value", 0, 1, "expression", 1).N0();
		}

		[TestMethod]
		public void Greedy1()
		{
			var p = new ParserStr("S = A B \n A = A 1|1 \n B = 1|B 1") { dump = 3 };
			p.Parse("111").H("A", v: "1").N("B", v: "11");
			p.greedy = true;
			p.Parse("111").H("A", v: "11").N("B", v: "1");
		}

		[TestMethod]
		public void Greedy2()
		{
			var p = new ParserStr("S =A B C D \nA =1|12 \nB =234|3 \nC =5|456 \nD =67|7") {
				dump = 3
			};
			p.Parse("1234567").H("A", v: "1").N("B", v: "234").N("C", v: "5").N("D", v: "67");
			p.greedy = true;
			p.Parse("1234567").H("A", v: "12").N("B", v: "3").N("C", v: "456").N("D", v: "7");
		}

		[TestMethod]
		public void GreedyHint()
		{
			var p = new ParserStr("S = A B =*\n A = A 1|1 \n B = 1|B 1") { dump = 3 };
			p.Parse("111").H("A", v: "11").N("B", v: "1");
			p.greedy = true;
			p.Parse("111").H("A", v: "11").N("B", v: "1");
		}

		[TestMethod]
		public void Empty1()
		{
			var p = new ParserStr("S=a A B \n A=|a+ \n B=A") { greedy = true, dump = 3 };
			IsTrue(p.Check("a")); IsTrue(p.Check("aa"));
			p.Parse("aaa").H("A", v: "aa").N("B", v: "");
		}

		[TestMethod]
		public void Empty2()
		{
			var p = new ParserStr("S=A B \n A=a P \n B=P b \n P=|pq") { dump = 3 };
			IsTrue(p.Check("ab")); IsTrue(p.Check("apqb"));
			IsTrue(p.Check("apqpqb")); IsFalse(p.Check("apqpqpqb"));
		}

		[TestMethod]
		public void Option1()
		{
			var p = new ParserStr("S=A B?a? \n A=a|aa \n B=a") { dump = 3 };
			IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaa"));
			p.Parse("aa").H("A", v: "a").N0();
			p.Parse("aaa").H("A", v: "a").N("B", v: "a").N0();
			p.greedy = true;
			p.Parse("aaa").H("A", v: "aa").N("B", v: "a").N0();
		}

		[TestMethod]
		public void Option2()
		{
			var p = new ParserStr("S=A?B? \n A=B a \n B=b");
			IsTrue(p.Check("")); IsTrue(p.Check("ba")); IsTrue(p.Check("b"));
			IsTrue(p.Check("bab")); IsFalse(p.Check("ab"));
		}

		[TestMethod]
		public void More1()
		{
			var p = new ParserStr("S=a+") { dump = 3 };
			IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(15, p.matchn);
		}

		[TestMethod]
		public void More2()
		{
			var p = new ParserStr("S=A B \n A=a P+ \n B=P+b \n P=pq") { dump = 3 };
			IsFalse(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
			p.Parse("apqpqpqb").H("A", v: "apq").N("B", v: "pqpqb").N0();
			p.greedy = true;
			p.Parse("apqpqpqb").H("A", v: "apqpq").N("B", v: "pqb").N0();
			AreEqual(34, p.matchn);
		}

		[TestMethod]
		public void Any1()
		{
			var p = new ParserStr("S=a*") { dump = 3 };
			IsTrue(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(16, p.matchn);
		}

		[TestMethod]
		public void Any2()
		{
			var p = new ParserStr("S=A B \n A=a P* \n B=P* b \n P=p|q") { dump = 3 };
			IsTrue(p.Check("ab")); IsTrue(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
			p.Parse("apqpqpqb").H("A", 0, 1).N("B").H("P", 1).U().T("P", 6).U().N0();
			AreEqual(107, p.matchn);
			p.greedy = true;
			p.Parse("apqpqpqb").H("A").H("P", 1).U().T("P", 6).U().N("B", v: "b").N0();
			AreEqual(107, p.matchn);
		}

		[TestMethod]
		public void TreeHint()
		{
			var p = new ParserStr("S=A B C\n A=1 =+\n B=1 =-\n C=1") {
				tree = true, dump = 3
			};
			p.Parse("111").H("A").N("C").N0();
			p.tree = false;
			p.Parse("111").H("A").N0();
		}

		[TestMethod]
		public void Recovery1()
		{
			var p = new ParserStr("S=A|S,A? =|\n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") {
				dump = 3
			};
			p.Parse("0,").H("S").H("A").H("M").H("V").N0().N0().N0().N0();
			p.Parse("+").Eq(v: '+', err: -1).H0().N0();
			p.Parse("+0").Eq(v: '+', err: -1).H0().N0();
			p.Parse("+,+0").Eq(v: '+', err: -1).H0().N0();
			p.Parse("0+").H("S", 0, 1).N("S", err: -4).N0().N(to: 2, err: -1).H("A", 0, 2, "M", 2).N0();
			p.Parse("0*").H("S", 0, 1).N("S", err: -4).N0().N(to: 2, err: -1).H("M", 0, 2, "V", 2).N0();
			var t = p.Parse("0#&$");
			t = t/**/	.H("S", 0, 1).N("S", 0, 4, err: -4).N0();
			t = t/**/.N("S", 1, 2, '#', -1);
			t = t/**/	.H("M", 0, 1, '*', 1).N("A", 0, 1, '+', 1).N("S", 0, 1, ',', 1).N0().N0();
		}

		[TestMethod]
		public void Recovery2()
		{
			var p = new ParserStr("S=A|S,A? =|\n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") {
				dump = 3
			};
			var t = p.Parse("0+,1*,2#");
			t = t/**/	.H("S", 0, 7).H("S", 0, 4).H("S", v: "0");
			t = t/**/								.N("S", 0, 3, err: -4).N("A", v: "1").N0();
			t = t/**/				.N("S", 0, 6, err: -4).N("A", v: "2").N0();
			t = t/**/	.N("S", 0, 8, err: -4).N0();
			t = t/**/.N("S", 2, 3, ',', -1);
			t = t/**/	.H("A", 0, 2, "M", 2).N0();
			t = t/**/.N("S", 5, 6, ',', -1);
			t = t/**/	.H("M", 3, 5, "V", 2).N0();
			t = t/**/.N("S", 7, 8, '#', -1);
			t = t/**/	.H("M", 6, 7, '*', 1).N("A", 6, 7, '+', 1).N("S", 0, 7, ',', 1).N0().N0();
			t = p.Parse("0+1*2+");
			t = t/**/	.H("S", 0, 5).H("A", 0, 5).N0().N("S", 0, 6, err: -4).N0();
			t = t/**/.N("S", 6, 6, null, -1);
			t = t/**/	.H("A", 0, 6, "M", 2).N0().N0();
			t = p.Parse("0+1*2+,1");
			t = t/**/	.H("S", 0, 5).H("A", 0, 5).N0().N("S", 0, 7, err: -4);
			t = t/**/	.N("A", 7, 8).N0();
			t = t/**/.N("S", 6, 7, ',', -1);
			t = t/**/	.H("A", 0, 6, "M", 2).N0().N0();
		}

		[TestMethod]
		public void Recovery3()
		{
			var p = new ParserStr("S=A|S,A? =*|\n A=M|A\\+M \n M=V|M\\*V \n V=(A)|N =|\n N=0|1|2|3|4|5") {
				dump = 3
			};
			var t = p.Parse("()");
			t = t/**/	.H("A").H("M").H("V").H("V", 0, 2, err: -4).N0().N0().N0().N0();
			t = t/**/.N("S", 1, 2, ')', -1);
			t = t/**/	.H("V", 0, 1, "A", 1).N0().N0();
			t = p.Parse("0+(),1");
			t = t/**/	.H("S").H("A").H("A").H("M").N0();
			t = t/**/					.N("M").H("V").H("V", 2, 4, err: -4).N0().N0().N0().N0();
			t = t/**/	.N("A").N0();
			t = t/**/.N("S", 3, 4, ')', -1);
			t = t/**/	.H("V", 2, 3, "A", 1).N0().N0();
			t = p.Parse("0,1+(2*),3#");
			t = t/**/	.H("S").H("S").H("S", 0, 1);
			t = t/**/				.N("A").H("A", v: "1");
			t = t/**/						.N("M").H("V", 4, 8).H("A", v: "2");
			t = t/**/											.N("V", 4, 8, err: -4).N0().N0().N0().N0();
			t = t/**/			.N("A", v: "3").N0();
			t = t/**/	.N("S", 0, 11, err: -4).N0();
			t = t/**/.N("S", 7, 8, ')', -1);
			t = t/**/	.H("M", 5, 7, "V", 2).N0();
			t = t/**/.N("S", 10, 11, '#', -1);
			t = t/**/	.H("M", 9, 10, '*', 1).N("A", 9, 10, '+', 1).N("S", 0, 10, ',', 1).N0().N0();
			t = p.Parse("0,1+(2*)4,3+");
			t = t/**/	.H("S").H("S").H("S", 0, 1);
			t = t/**/				.N("A").H("A", v: "1");
			t = t/**/						.N("M").H("V", 4, 8).H("A", v: "2");
			t = t/**/											.N("V", 4, 8, err: -4).N0().N0().N0().N0();
			t = t/**/			.N("S", 0, 10, err: -4).N("A", v: "3").N0();
			t = t/**/	.N("S", 0, 12, err: -4).N0();
			t = t/**/.N("S", 7, 8, ')', -1);
			t = t/**/	.H("M", 5, 7, "V", 2).N0();
			t = t/**/.N("S", 8, 9, '4', -1);
			t = t/**/	.H("M", 4, 8, '*', 1).N("A", 2, 8, '+', 1).N("S", 0, 8, ',', 1).N0();
			t = t/**/.N("S", 12, 12, null, -1);
			t = t/**/	.H("A", 10, 12, "M", 2).N0().N0();
		}

		[TestMethod]
		public void Recovery4()
		{
			var p = new ParserStr("P=S+ \n S=V|{S+} =*|\n V=(N)|N =|\n N=0|1|2|3|4|5") {
				dump = 3
			};
			var t = p.Parse("{()");
			t = t/**/	.H("S").H("S").H("V");
			t = t/**/					.H("V", 1, 3, err: -4).N0().N0();
			t = t/**/			.N("S", 0, 3, err: -4).N0().N0();
			t = t/**/.N("P", 2, 3, ')', -1);
			t = t/**/	.H("V", 1, 2, "N", 1).N0();
			t = t/**/.N("P", 3, 3, null, -1);
			t = t/**/	.H("S", 0, 3, '}', 2).N("S", 0, 3, "S", 1).N0().N0();
			p.recover = 1;
			p.Parse("{{").Eq("P", 2, 2, null, -1).H("S", 1, 2, "S", 1).N0().N0();
			p.recover = 2;
			t = p.Parse("{(");
			t = t/**/	.H("S").H("S").H("V");
			t = t/**/					.H("V", 1, 2, err: -4).N0().N0();
			t = t/**/			.N("S", 0, 2, err: -4).N0().N0();
			t = t/**/.N("P", 2, 2, null, -1);
			t = t/**/	.H("V", 1, 2, "N", 1).N0();
			t = t/**/.N("P", 2, 2, null, -1);
			t = t/**/	.H("V", 1, 2, "N", 1).N("S", 0, 2, '}', 2).N("S", 0, 2, "S", 1).N0().N0();
		}

		[TestMethod]
		public void Recovery5()
		{
			var p = new ParserStr("P=S+ \n S=V|{S+} =*|\n|\\0?\\0} =-| \n V=0|1|2|3|4|5") {
				dump = 3
			};
			var t = p.Parse("{{0}}}}{1}");
			t = t/**/	.H("S", 0, 5, "{{0}}").H("S", 1, 4, "{0}").N0();
			t = t/**/	.N("S", 5, 6, err: -4).N("S", 6, 7, err: -4);
			t = t/**/	.N("S", 7, 10, "{1}").N0();
			t = t/**/.N("P", 5, 6, '}', -1).N("P", 6, 7, '}', -1).N0();
		}
	}
}
