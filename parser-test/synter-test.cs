//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

#pragma warning disable IDE0059
using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser;

using Ser = (SyntStr t, SynterStr s);

static class TestExtension
{
	public static bool Check(this SynterStr s, string input)
		=> s.Begin(new LerStr(input)).Check();

	public static Ser Parse(this SynterStr s, string input)
	{
		var t = s.Begin(new LerStr(input)).Parse().Dump();
		using var env = EnvWriter.Begin();
		env.WriteLine($"---- match {s.matchn} / lexi {s.lexn} = {s.matchn / Math.Max(s.lexn, 1)} ----");
		return (t, s);
	}

	public static Ser Eq(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
	{
		AreNotEqual(null, t.t);
		AreEqual(err, t.t.err);
		if (name != null) AreEqual(name, t.t.name);
		if (from != null) AreEqual(from, t.t.from);
		if (to != null) AreEqual(to, t.t.to);
		if (v != null) AreEqual(v, t.t.err == 0 ? t.s.ler.Lexs(t.t.from, t.t.to) : t.t.info);
		return t;
	}

	public static Ser h(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (t.t.head, t.s).Eq(name, from, to, v, err);
	public static Ser t(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (t.t.tail, t.s).Eq(name, from, to, v, err);
	public static Ser n(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (t.t.next, t.s).Eq(name, from, to, v, err);
	public static Ser p(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (t.t.prev, t.s).Eq(name, from, to, v, err);

	public static Ser H(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> h(t, name, from, to, v, err).Leaf();
	public static Ser T(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> TestExtension.t(t, name, from, to, v, err).Leaf();
	public static Ser N(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> n(t, name, from, to, v, err).Leaf();
	public static Ser P(this Ser t,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> p(t, name, from, to, v, err).Leaf();

	public static Ser Leaf(this Ser t) { AreEqual(null, t.t.head); return t; }
	public static Ser U(this Ser t) { AreEqual(null, t.t.next); return (t.t.up, t.s); }
	public static Ser UU(this Ser t) => U(U(t));
	public static Ser UUU(this Ser t) => U(U(U(t)));
	public static Ser UUUU(this Ser t) => U(U(U(U(t))));
}

[TestClass]
public class TestSynter : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	[TestMethod]
	public void Term1()
	{
		var p = new SynterStr("S=k");
		IsTrue(p.Check("k"));
		IsFalse(p.Check("")); IsFalse(p.Check("kk")); IsFalse(p.Check("K"));
	}

	[TestMethod]
	public void Term2()
	{
		var p = new SynterStr("S=");
		IsTrue(p.Check("")); IsFalse(p.Check("a"));
	}

	[TestMethod]
	public void Alt1()
	{
		var p = new SynterStr("S=|a");
		IsTrue(p.Check("")); IsTrue(p.Check("a")); IsFalse(p.Check("b"));
	}

	[TestMethod]
	public void Alt2()
	{
		var p = new SynterStr("S=A\nA=a|1|#");
		IsTrue(p.Check("a")); IsTrue(p.Check("1")); IsTrue(p.Check("#"));
		IsFalse(p.Check("")); IsFalse(p.Check("a1")); IsFalse(p.Check("A"));
	}

	[TestMethod]
	public void Con()
	{
		var p = new SynterStr("S = A B|B A \nA = a|1 \nB = b|2");
		IsFalse(p.Check("a")); IsFalse(p.Check("1")); IsFalse(p.Check("b")); IsFalse(p.Check("2"));
		IsTrue(p.Check("ab")); IsTrue(p.Check("ba"));
		IsTrue(p.Check("1b")); IsTrue(p.Check("b1"));
		IsTrue(p.Check("a2")); IsTrue(p.Check("2a"));
		IsTrue(p.Check("12")); IsTrue(p.Check("21"));
		IsFalse(p.Check("aba")); IsFalse(p.Check("12ab"));
	}

	[TestMethod]
	public void AltPrior1()
	{
		var p = new SynterStr("S=.E =^ \n | E \n E=W|P \n W=a \n P=.W") { dump = 3 };
		p.Parse(".a").h("E").H("W").U();
	}

	[TestMethod]
	public void AltPrior2()
	{
		var p = new SynterStr("S=.E \n | E =^ \n E=W|P \n W=a \n P=.W") { dump = 3 };
		p.Parse(".a").h("E").h("P").H("W").UUU();
	}

	[TestMethod]
	public void Esc()
	{
		var p = new SynterStr(@"S = \ss\tt\r\n\\/ | \|or\=eq\*s\+p\?q");
		IsTrue(p.Check(" s\tt\r\n\\/")); IsFalse(p.Check(" s\tt\r \n\\/"));
		IsTrue(p.Check("|or=eq*s+p?q"));
	}

	[TestMethod]
	public void ErrHint()
	{
		var p = new SynterStr("S=A B =start \n A=1|2 =A12 ==oh||no\n |3 =A3 \n B= =\tempty \n |4 =B4") {
			dump = 3
		};
		IsNull(p.Parse("").t.head);
		IsNull(p.Parse("4").t.head);
		p.Parse("15").H("S", 0, 1, "empty", 1);
	}

	[TestMethod]
	public void LeftRecu()
	{
		var p = new SynterStr("S = S b|A \nA = a");
		IsTrue(p.Check("a")); IsTrue(p.Check("ab")); IsTrue(p.Check("abb")); IsTrue(p.Check("abbb"));
		IsFalse(p.Check("")); IsFalse(p.Check("b")); IsFalse(p.Check("aab")); IsFalse(p.Check("abbba"));
	}

	[TestMethod]
	public void RightRecu()
	{
		var p = new SynterStr("S = a S|B \nB = b");
		IsTrue(p.Check("b")); IsTrue(p.Check("ab")); IsTrue(p.Check("aab")); IsTrue(p.Check("aaaab"));
		IsFalse(p.Check("")); IsFalse(p.Check("a")); IsFalse(p.Check("abb")); IsFalse(p.Check("abbba"));
	}

	[TestMethod]
	public void RightRecuUnopt()
	{
		var p = new SynterStr("S= aa B \nB= A a \nA= a A|a") { dump = 3 };
		IsFalse(p.Check("aa")); IsFalse(p.Check("aaa"));
		IsTrue(p.Check("aaaa")); IsTrue(p.Check("aaaaa")); IsTrue(p.Check("aaaaaa"));
		IsTrue(p.Check("aaaaaaa")); AreEqual(49, p.matchn);
		p = new SynterStr("S= aa B \nB= A a \nA= A a|a") { dump = 3 };
		IsTrue(p.Check("aaaaaaa")); AreEqual(29, p.matchn);
		p = new SynterStr("S= aa B \nB= A a \nA= a+") { dump = 3 };
		IsTrue(p.Check("aaaaaaa")); AreEqual(28, p.matchn);
	}

	[TestMethod]
	public void MidRecu1()
	{
		var p = new SynterStr("""
			S	= If|X =-
			If	= if \s S \s then \s S
				| if \s S \s then \s S \s else \s S
			X	= a|b|c|d|e|f|g|h|i|j
			""") { dump = 3 };
		p.Parse("if a then b").h("If").H(v: "a").N(v: "b").U();
		p.Parse("if a then b else c").h().H(v: "a").N(v: "b").N(v: "c").U();
		var t = p.Parse("if a then if b then c").h().H(v: "a");
		t = t/**/									.n().H(v: "b").N(v: "c").UU();
		t = p.Parse("if a then if b then c else d").h().H(v: "a");
		t = t/**/									   .n().H(v: "b").N(v: "c").U();
		t = t/**/									   .N(v: "d").U();
		t = p.Parse("if a then if b then c else d else e").h().H(v: "a");
		t = t/**/											  .n().H(v: "b").N(v: "c").N(v: "d").U();
		t = t/**/											  .N(v: "e").U();
		t = p.Parse("if a then if b then c else if d then e else f else g");
		t = t/**/.h().H(v: "a");
		t = t/**/	.n().H(v: "b").N(v: "c");
		t = t/**/		 .n().H(v: "d").N(v: "e").N(v: "f").UU();
		t = t/**/	.N(v: "g").U();
		t = p.Parse("if a then if b then if c then d else e else if g then h else i else j");
		t = t/**/.h().H(v: "a");
		t = t/**/	.n().H(v: "b");
		t = t/**/		.n().H(v: "c").N(v: "d").N(v: "e").U();
		t = t/**/		.n().H(v: "g").N(v: "h").N(v: "i").UU();
		t = t/**/	.N(v: "j").U();
		IsFalse(p.Check("if a else b"));
		IsFalse(p.Check("if a then b then c"));
		IsFalse(p.Check("if a then if b else c"));
	}

	[TestMethod]
	public void MidRecu2()
	{
		var p = new SynterStr("""
			S	= If|X =-
			If	= if \s S \s then \s S
				| if \s S \s then \s S \s else \s S
			X	= a|b|c|d|e|f|g|h|i|j
			""") { dump = 3 };
		p.Parse("if a then b").h("If").H(v: "a").N(v: "b").U();
		p.Parse("if a then b else c").h().H(v: "a").N(v: "b").N(v: "c").U();
		var t = p.Parse("if a then if b then c").h().H(v: "a");
		t = t/**/									.n().H(v: "b").N(v: "c").UU();
		t = p.Parse("if a then if b then c else d").h().H(v: "a");
		t = t/**/									   .n().H(v: "b").N(v: "c").U();
		t = t/**/									   .N(v: "d").U();
		t = p.Parse("if a then if b then c else d else e").h().H(v: "a");
		t = t/**/											  .n().H(v: "b").N(v: "c").N(v: "d").U();
		t = t/**/											  .N(v: "e").U();
		t = p.Parse("if a then if b then c else if d then e else f else g");
		t = t/**/.h().H(v: "a");
		t = t/**/	.n().H(v: "b").N(v: "c");
		t = t/**/		 .n().H(v: "d").N(v: "e").N(v: "f").UU();
		t = t/**/	.N(v: "g").U();
		t = p.Parse("if a then if b then if c then d else e else if g then h else i else j");
		t = t/**/.h().H(v: "a");
		t = t/**/	.n().H(v: "b");
		t = t/**/		.n().H(v: "c").N(v: "d").N(v: "e").U();
		t = t/**/		.n().H(v: "g").N(v: "h").N(v: "i").UU();
		t = t/**/	.N(v: "j").U();
		IsFalse(p.Check("if a else b"));
		IsFalse(p.Check("if a then b then c"));
		IsFalse(p.Check("if a then if b else c"));
	}

	[TestMethod]
	public void DoubleRecu()
	{
		var p = new SynterStr("S=S S|a");
		IsFalse(p.Check(""));
		IsTrue(p.Check("a")); IsTrue(p.Check("aa")); IsTrue(p.Check("aaa")); IsTrue(p.Check("aaaa"));
	}

	[TestMethod]
	public void AddMul()
	{
		var p = new SynterStr("""
			Expr  = Expr\+Mul | Mul
			Mul   = Mul\*Value | Value
			Value = (Expr) | Num
			Num   = Num Digi | Digi
			Digi  = 0|1|2|3|4|5|6|7|8|9
			""") { dump = 3 };
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
		var p = new SynterStr("""
			Expr  = Expr\+Mul | Mul     = expression
			Mul   = Mul\*Value | Value  = expression
			Value = (Expr) | Num        = value
			Num   = Num Digi | Digi     = number
			Digi  = 0|1|2|3|4|5|6|7|8|9 = digit
			""") { dump = 3 };
		p.Parse("(1+2*").H("Mul", 3, 5, "value", 2).U();
		p.Parse("(*1*2+3)*4").H("Value", 0, 1, "expression", 1).U();
		p.Parse("(1+2*3))*4").H("Mul", 0, 7, '*', 1).N("Expr", 0, 7, '+', 1).U();
		p.Parse("(1*2+3").H("Mul", 5, 6, '*', 1).N("Value", 0, 6, ')', 2)
						.N("Expr", 1, 6, '+', 1).N("Num", 5, 6, "digit", 1).U();
		p.Parse("(1*2+)").H("Expr", 1, 5, "expression", 2).U();
		p.Parse("()").H("Value", 0, 1, "expression", 1).U();
	}

	[TestMethod]
	public void Greedy1()
	{
		var p = new SynterStr("S = A B \n A = A 1|1 \n B = 1|B 1") { dump = 3 };
		p.Parse("111").H("A", v: "1").n("B", v: "11").H("B", v: "1").UU();
		p.greedy = true;
		p.Parse("111").h("A", v: "11").H("A", v: "1").U().N("B", v: "1").U();
	}

	[TestMethod]
	public void Greedy2()
	{
		var p = new SynterStr("S =A B C D \nA =1|12 \nB =234|3 \nC =5|456 \nD =67|7") {
			dump = 3
		};
		p.Parse("1234567").H("A", v: "1").N("B", v: "234").N("C", v: "5").N("D", v: "67").U();
		p.greedy = true;
		p.Parse("1234567").H("A", v: "12").N("B", v: "3").N("C", v: "456").N("D", v: "7").U();
	}

	[TestMethod]
	public void GreedyHint()
	{
		var p = new SynterStr("S = A B =*\n A = A 1|1 \n B = 1|B 1") { dump = 3 };
		p.Parse("111").h("A", v: "11").H("A", v: "1").U().N("B", v: "1").UU();
		p.greedy = true;
		p.Parse("111").h("A", v: "11").H("A", v: "1").U().N("B", v: "1").UU();
	}

	[TestMethod]
	public void Empty1()
	{
		var p = new SynterStr("S=a A B \n A=|a+ \n B=A") { greedy = true, dump = 3 };
		IsTrue(p.Check("a")); IsTrue(p.Check("aa"));
		p.Parse("aaa").H("A", v: "aa").n("B", v: "").H("A", v: "").UUU();
	}

	[TestMethod]
	public void Empty2()
	{
		var p = new SynterStr("S=A B \n A=a P \n B=P b \n P=|pq") { dump = 3 };
		IsTrue(p.Check("ab")); IsTrue(p.Check("apqb"));
		IsTrue(p.Check("apqpqb")); IsFalse(p.Check("apqpqpqb"));
	}

	[TestMethod]
	public void Option1()
	{
		var p = new SynterStr("S=A B?a? \n A=a|aa \n B=a") { dump = 3 };
		IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaa"));
		p.Parse("aa").H("A", v: "a").U();
		p.Parse("aaa").H("A", v: "a").N("B", v: "a").U();
		p.greedy = true;
		p.Parse("aaa").H("A", v: "aa").N("B", v: "a").U();
	}

	[TestMethod]
	public void Option2()
	{
		var p = new SynterStr("S=A?B? \n A=B a \n B=b");
		IsTrue(p.Check("")); IsTrue(p.Check("ba")); IsTrue(p.Check("b"));
		IsTrue(p.Check("bab")); IsFalse(p.Check("ab"));
	}

	[TestMethod]
	public void More1()
	{
		var p = new SynterStr("S=a+") { dump = 3 };
		IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
		IsTrue(p.Check("aaaaaaa")); AreEqual(15, p.matchn);
	}

	[TestMethod]
	public void More2()
	{
		var p = new SynterStr("S=A B \n A=a P+ \n B=P+b \n P=pq") { dump = 3 };
		IsFalse(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
		p.Parse("apqpqpqb").h("A", v: "apq").n("B", v: "pqpqb").U();
		p.greedy = true;
		p.Parse("apqpqpqb").h("A", v: "apqpq").n("B", v: "pqb").U();
		AreEqual(34, p.matchn);
	}

	[TestMethod]
	public void Any1()
	{
		var p = new SynterStr("S=a*") { dump = 3 };
		IsTrue(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
		IsTrue(p.Check("aaaaaaa")); AreEqual(16, p.matchn);
	}

	[TestMethod]
	public void Any2()
	{
		var p = new SynterStr("S=A B \n A=a P* \n B=P* b \n P=p|q") { dump = 3 };
		IsTrue(p.Check("ab")); IsTrue(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
		p.Parse("apqpqpqb").H("A", 0, 1).n("B").H("P", 1).N().N().N().N().N("P", 6).UU();
		AreEqual(107, p.matchn);
		p.greedy = true;
		p.Parse("apqpqpqb").h("A").H("P", 1).N().N().N().N().N("P", 6).U().N("B", v: "b").U();
		AreEqual(107, p.matchn);
	}

	[TestMethod]
	public void HintSynt1()
	{
		var p = new SynterStr("S=A B C\n A=1 =+\n B=1 =-\n C=1") {
			tree = true, dump = 3
		};
		p.Parse("111").H("A").N("C").U();
		p.tree = false;
		p.Parse("111").H("A").U();
	}

	[TestMethod]
	public void HintSynt2()
	{
		var p = new SynterStr("S=Z*U* \n U=A*V* =+-\n V=B*C* =+-\n Z=z \n A=a \n B=b \n C=c") {
			tree = true, dump = 3
		};
		var t = p.Parse("zabc");
		t = t/**/.H("Z");
		t = t/**/.n("U").H("A");
		t = t/**/		.n("V").H("B");
		t = t/**/				.N("C").UUU();
		t = p.Parse("zab");
		t = t/**/.H("Z");
		t = t/**/.n("U").H("A");
		t = t/**/		.N("B").UU();
		t = p.Parse("zbc");
		t = t/**/.H("Z");
		t = t/**/.n("U").H("B");
		t = t/**/		.N("C").UU();
		t = p.Parse("abc");
		t = t/**/.H("A");
		t = t/**/.n("V").H("B");
		t = t/**/		.N("C").UU();
		p.Parse("ab").H("A").N("B").U();
		p.Parse("bc").H("B").N("C").U();
	}

	[TestMethod]
	public void HintSynt3()
	{
		var p = new SynterStr("S=Z*U* \n U=A*V* =+-\n V=B*C* =+-\n Z=z =-\n A=a \n B=b \n C=c") {
			tree = true, dump = 3
		};
		var t = p.Parse("zabc");
		t = t/**/.H("A");
		t = t/**/.n("V").H("B");
		t = t/**/		.N("C").UU();
		p.Parse("zab").H("A").N("B").U();
		p.Parse("zbc").H("B").N("C").U();
		t = p.Parse("abc");
		t = t/**/.H("A");
		t = t/**/.n("V").H("B");
		t = t/**/		.N("C").UU();
		p.Parse("ab").H("A").N("B").U();
		p.Parse("bc").H("B").N("C").U();
	}

	[TestMethod]
	public void Recovery1()
	{
		var p = new SynterStr("S=A|S,A? =|\n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") {
			dump = 3
		};
		p.Parse("0,").h("S").h("A").h("M").H("V").UUUU();
		p.Parse("+").Eq(v: '+', err: -1).Leaf().U();
		p.Parse("+0").Eq(v: '+', err: -1).Leaf().U();
		p.Parse("+,+0").Eq(v: '+', err: -1).Leaf().U();
		p.Parse("0+").h("S", 0, 1).N("S", err: -4).U().n(to: 2, err: -1).H("A", 0, 2, "M", 2).UU();
		p.Parse("0*").h("S", 0, 1).N("S", err: -4).U().n(to: 2, err: -1).H("M", 0, 2, "V", 2).UU();
		var t = p.Parse("0#&$");
		t = t/**/	.h("S", 0, 1).N("S", 0, 4, err: -4).U();
		t = t/**/.n("S", 1, 2, '#', -1);
		t = t/**/	.h("M", 0, 1, '*', 1).N("A", 0, 1, '+', 1).N("S", 0, 1, ',', 1).UU();
	}

	[TestMethod]
	public void Recovery2()
	{
		var p = new SynterStr("S=A|S,A? =|\n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") {
			dump = 3
		};
		var t = p.Parse("0+,1*,2#");
		t = t/**/	.h("S", 0, 7).h("S", 0, 4).h("S", v: "0");
		t = t/**/								.N("S", 0, 3, err: -4).n("A", v: "1").U();
		t = t/**/				.N("S", 0, 6, err: -4).n("A", v: "2").U();
		t = t/**/	.N("S", 0, 8, err: -4).U();
		t = t/**/.n("S", 2, 3, ',', -1);
		t = t/**/	.H("A", 0, 2, "M", 2).U();
		t = t/**/.n("S", 5, 6, ',', -1);
		t = t/**/	.H("M", 3, 5, "V", 2).U();
		t = t/**/.n("S", 7, 8, '#', -1);
		t = t/**/	.H("M", 6, 7, '*', 1).N("A", 6, 7, '+', 1).N("S", 0, 7, ',', 1).UU();
		t = p.Parse("0+1*2+");
		t = t/**/	.h("S", 0, 5).h("A", 0, 5).U().N("S", 0, 6, err: -4).U();
		t = t/**/.n("S", 6, 6, null, -1);
		t = t/**/	.H("A", 0, 6, "M", 2).UU();
		t = p.Parse("0+1*2+,1");
		t = t/**/	.h("S", 0, 5).h("A", 0, 5).U().N("S", 0, 7, err: -4);
		t = t/**/	.n("A", 7, 8).U();
		t = t/**/.n("S", 6, 7, ',', -1);
		t = t/**/	.H("A", 0, 6, "M", 2).UU();
	}

	[TestMethod]
	public void Recovery3()
	{
		var p = new SynterStr("S=A|S,A? =*|\n A=M|A\\+M \n M=V|M\\*V \n V=(A)|N =|\n N=0|1|2|3|4|5") {
			dump = 3
		};
		var t = p.Parse("()");
		t = t/**/	.h("A").h("M").h("V").H("V", 0, 2, err: -4).UUUU();
		t = t/**/.n("S", 1, 2, ')', -1);
		t = t/**/	.H("V", 0, 1, "A", 1).UU();
		t = p.Parse("0+(),1");
		t = t/**/	.h("S").h("A").h("A").h("M").U();
		t = t/**/					.n("M").h("V").H("V", 2, 4, err: -4).UUUU();
		t = t/**/	.n("A").U();
		t = t/**/.n("S", 3, 4, ')', -1);
		t = t/**/	.H("V", 2, 3, "A", 1).UU();
		t = p.Parse("0,1+(2*),3#");
		t = t/**/	.h("S").h("S").h("S", 0, 1);
		t = t/**/				.n("A").h("A", v: "1");
		t = t/**/						.n("M").h("V", 4, 8).h("A", v: "2");
		t = t/**/											.N("V", 4, 8, err: -4).UUUU();
		t = t/**/			.n("A", v: "3").U();
		t = t/**/	.N("S", 0, 11, err: -4).U();
		t = t/**/.n("S", 7, 8, ')', -1);
		t = t/**/	.H("M", 5, 7, "V", 2).U();
		t = t/**/.n("S", 10, 11, '#', -1);
		t = t/**/	.H("M", 9, 10, '*', 1).N("A", 9, 10, '+', 1).N("S", 0, 10, ',', 1).UU();
		t = p.Parse("0,1+(2*)4,3+");
		t = t/**/	.h("S").h("S").h("S", 0, 1);
		t = t/**/				.n("A").h("A", v: "1");
		t = t/**/						.n("M").h("V", 4, 8).h("A", v: "2");
		t = t/**/											.N("V", 4, 8, err: -4).UUUU();
		t = t/**/			.N("S", 0, 10, err: -4).n("A", v: "3").U();
		t = t/**/	.N("S", 0, 12, err: -4).U();
		t = t/**/.n("S", 7, 8, ')', -1);
		t = t/**/	.H("M", 5, 7, "V", 2).U();
		t = t/**/.n("S", 8, 9, '4', -1);
		t = t/**/	.H("M", 4, 8, '*', 1).N("A", 2, 8, '+', 1).N("S", 0, 8, ',', 1).U();
		t = t/**/.n("S", 12, 12, null, -1);
		t = t/**/	.H("A", 10, 12, "M", 2).UU();
	}

	[TestMethod]
	public void Recovery4()
	{
		var p = new SynterStr("P=S+ \n S=V|{S+} =*|\n V=(N)|N =|\n N=0|1|2|3|4|5") {
			dump = 3
		};
		var t = p.Parse("{()");
		t = t/**/	.h("S").h("S").h("V");
		t = t/**/					.H("V", 1, 3, err: -4).UU();
		t = t/**/			.N("S", 0, 3, err: -4).UU();
		t = t/**/.n("P", 2, 3, ')', -1);
		t = t/**/	.H("V", 1, 2, "N", 1).U();
		t = t/**/.n("P", 3, 3, null, -1);
		t = t/**/	.H("S", 0, 3, '}', 2).N("S", 0, 3, "S", 1).UU();
		p.recover = 1;
		p.Parse("{{").Eq("P", 2, 2, null, -1).H("S", 1, 2, "S", 1).UU();
		p.recover = 2;
		t = p.Parse("{(");
		t = t/**/	.h("S").h("S").h("V");
		t = t/**/					.H("V", 1, 2, err: -4).UU();
		t = t/**/			.N("S", 0, 2, err: -4).UU();
		t = t/**/.n("P", 2, 2, null, -1);
		t = t/**/	.H("V", 1, 2, "N", 1).U();
		t = t/**/.n("P", 2, 2, null, -1);
		t = t/**/	.H("V", 1, 2, "N", 1).N("S", 0, 2, '}', 2).N("S", 0, 2, "S", 1).UU();
	}

	[TestMethod]
	public void Recovery5()
	{
		var p = new SynterStr("P=S+ \n S=V|{S+} =*|\n|\\0?\\0} =-| \n V=0|1|2|3|4|5") {
			dump = 3
		};
		var t = p.Parse("{{0}}}}{1}");
		t = t/**/	.h("S", 0, 5, "{{0}}").h("S", 1, 4, "{0}").U();
		t = t/**/	.N("S", 5, 6, err: -4).N("S", 6, 7, err: -4).n("S", 7, 10, "{1}").U();
		t = t/**/.n("P", 5, 6, '}', -1).n("P", 6, 7, '}', -1).U();
	}
}
