//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using qutum.parser.earley;
using System;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser.earley;

public readonly struct Ser(EsynStr t, EarleyStr s)
{
	public readonly Ser Eq(string name = null, Jov<int?> j = default, object d = null, int err = 0)
	{
		AreNotEqual(null, t);
		AreEqual(err, t.err);
		if (name != null) AreEqual(name, t.name);
		if (j.on != null) AreEqual(j.on, t.j.on);
		if (j.via != null) AreEqual(j.via, t.j.via);
		if (d != null) AreEqual(d, t.err == 0 ? s.ler.Lexs(t.j) : t.info);
		return this;
	}

	public Ser h(string name = null, Jov<int?> j = default, object d = null, int err = 0)
		=> new Ser(t.head, s).Eq(name, j, d, err).Vine;
	public Ser n(string name = null, Jov<int?> j = default, object d = null, int err = 0)
		=> new Ser(t.next, s).Eq(name, j, d, err).Vine;

	public Ser H(string name = null, Jov<int?> j = default, object d = null, int err = 0)
		=> new Ser(t.head, s).Eq(name, j, d, err).Leaf;
	public Ser N(string name = null, Jov<int?> j = default, object d = null, int err = 0)
		=> new Ser(t.next, s).Eq(name, j, d, err).Leaf;

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
}

[TestClass]
public class TestParserEarley : IDisposable
{
	readonly EnvWriter env = EnvWriter.Use();

	public void Dispose() => env.Dispose();

	EarleyStr ser;

	void NewSer(string grammar) => ser = new EarleyStr(grammar) { dump = 3 };

	public void True(string read) => IsTrue(ser.Begin(new LerStr(read)).Check());
	public void False(string read) => IsFalse(ser.Begin(new LerStr(read)).Check());

	public Ser Parse(string read)
	{
		var t = ser.Begin(new LerStr(read)).Parse().Dump();
		using var env = EnvWriter.Use();
		env.WriteLine($"---- {ser.matchz} match/lexi {ser.lexz} = {ser.matchz / int.Max(ser.lexz, 1)} ----");
		return new(t, ser);
	}

	[TestMethod]
	public void Term1()
	{
		NewSer("S=k");
		True("k");
		False(""); False("kk"); False("K");
	}

	[TestMethod]
	public void Term2()
	{
		NewSer("S=");
		True(""); False("a");
	}

	[TestMethod]
	public void Alt1()
	{
		NewSer("S=|a");
		True(""); True("a"); False("b");
	}

	[TestMethod]
	public void Alt2()
	{
		NewSer("S=A\nA=a|1|#");
		True("a"); True("1"); True("#");
		False(""); False("a1"); False("A");
	}

	[TestMethod]
	public void Con()
	{
		NewSer("S = A B|B A \nA = a|1 \nB = b|2");
		False("a"); False("1"); False("b"); False("2");
		True("ab"); True("ba");
		True("1b"); True("b1");
		True("a2"); True("2a");
		True("12"); True("21");
		False("aba"); False("12ab");
	}

	[TestMethod]
	public void AltPrior1()
	{
		NewSer("S=.E =^ \n | E \n E=W|P \n W=a \n P=.W");
		_ = Parse(".a").h("E").H("W").uuU;
	}

	[TestMethod]
	public void AltPrior2()
	{
		NewSer("S=.E \n | E =^ \n E=W|P \n W=a \n P=.W");
		_ = Parse(".a").h("E").h("P").H("W").uuuU;
	}

	[TestMethod]
	public void Esc()
	{
		NewSer(@"S = \ss\tt\r\n\\/ | \|or\=eq\*s\+p\?q");
		True(" s\tt\r\n\\/"); False(" s\tt\r \n\\/");
		True("|or=eq*s+p?q");
	}

	[TestMethod]
	public void ErrHint()
	{
		NewSer("S=A B =start \n A=1|2 =A12 ==oh||no\n |3 =A3 \n B= =\tempty \n |4 =B4");
		_ = Parse("").Leaf;
		_ = Parse("4").Leaf;
		Parse("15").H("S", (0, 1), "empty", 1);
	}

	[TestMethod]
	public void LeftRecu()
	{
		NewSer("S = S b|A \nA = a");
		True("a"); True("ab"); True("abb"); True("abbb");
		False(""); False("b"); False("aab"); False("abbba");
	}

	[TestMethod]
	public void RightRecu()
	{
		NewSer("S = a S|B \nB = b");
		True("b"); True("ab"); True("aab"); True("aaaab");
		False(""); False("a"); False("abb"); False("abbba");
	}

	[TestMethod]
	public void RightRecuUnopt()
	{
		NewSer("S= aa B \nB= A a \nA= a A|a");
		False("aa"); False("aaa");
		True("aaaa"); True("aaaaa"); True("aaaaaa");
		True("aaaaaaa"); AreEqual(49, ser.matchz);
		NewSer("S= aa B \nB= A a \nA= A a|a");
		True("aaaaaaa"); AreEqual(29, ser.matchz);
		NewSer("S= aa B \nB= A a \nA= a+");
		True("aaaaaaa"); AreEqual(28, ser.matchz);
	}

	[TestMethod]
	public void MidRecu()
	{
		NewSer("""
			S	= If|X =-
			If	= if \s S \s then \s S
				| if \s S \s then \s S \s else \s S
			X	= a|b|c|d|e|f|g|h|i|j
			""");
		var _ = Parse("if a then b").h("If").H(d: "a").N(d: "b").uuU;
		_ = Parse("if a then b else c").h().H(d: "a").N(d: "b").N(d: "c").uuU;
		_ = Parse("if a then if b then c").h().H(d: "a");
		_ = _/**/									.n().H(d: "b").N(d: "c").uuuU;
		_ = Parse("if a then if b then c else d").h().H(d: "a");
		_ = _/**/									   .n().H(d: "b").N(d: "c").u;
		_ = _/**/									   .N(d: "d").uuU;
		_ = Parse("if a then if b then c else d else e").h().H(d: "a");
		_ = _/**/											  .n().H(d: "b").N(d: "c").N(d: "d").u;
		_ = _/**/											  .N(d: "e").uuU;
		_ = Parse("if a then if b then c else if d then e else f else g");
		_ = _/**/.h().H(d: "a");
		_ = _/**/	.n().H(d: "b").N(d: "c");
		_ = _/**/		 .n().H(d: "d").N(d: "e").N(d: "f").uu;
		_ = _/**/	.N(d: "g").uuU;
		_ = Parse("if a then if b then if c then d else e else if g then h else i else j");
		_ = _/**/.h().H(d: "a");
		_ = _/**/	.n().H(d: "b");
		_ = _/**/		.n().H(d: "c").N(d: "d").N(d: "e").u;
		_ = _/**/		.n().H(d: "g").N(d: "h").N(d: "i").uu;
		_ = _/**/	.N(d: "j").uuU;
		False("if a else b");
		False("if a then b then c");
		False("if a then if b else c");
	}

	[TestMethod]
	public void DoubleRecu()
	{
		NewSer("S=S S|a");
		False("");
		True("a"); True("aa"); True("aaa"); True("aaaa");
	}

	[TestMethod]
	public void AddMul()
	{
		NewSer("""
			Expr  = Expr\+Mul | Mul
			Mul   = Mul\*Value | Value
			Value = (Expr) | Num
			Num   = Num Digi | Digi
			Digi  = 0|1|2|3|4|5|6|7|8|9
			""");
		True("1"); True("07"); True("(3)"); True("(298)");
		False("1 3"); False("(2"); False("39210)");
		True("1*2"); True("073*32"); True("86*1231*787*99");
		False("1*"); False("*3"); False("1*2*");
		True("1+2"); True("073+32"); True("86+1231+787+99");
		False("1+"); False("+3"); False("1+2+");
		True("1*2+3"); True("1+2*3"); True("7+58*23+882*152*33+89*6");
		False("1*+3"); False("+3*2"); False("+1*2+6+");
		True("1*(2+3)"); True("(1+2)*3"); True("(7+58)*(23+882)*152*(33+89)*6");
		True("(1*2)+3"); True("(1+2*3)"); True("7+(5*23)*(15*33+89)*0");
		True("(1*(2+5)+2)+3"); True("53+((((1))))+2*3");
		False("1*(3+)"); False("(3))*2"); False("(5*(2)(6)+8)");
		False("1*()3+5"); False("(3))*2"); False("(5*(2)(6)+8)");
	}

	[TestMethod]
	public void AddMulErr()
	{
		NewSer("""
			Expr  = Expr\+Mul | Mul     = expression
			Mul   = Mul\*Value | Value  = expression
			Value = (Expr) | Num        = value
			Num   = Num Digi | Digi     = number
			Digi  = 0|1|2|3|4|5|6|7|8|9 = digit
			""");
		_ = Parse("(1+2*").H("Mul", (3, 5), "value", 2).uU;
		_ = Parse("(*1*2+3)*4").H("Value", (0, 1), "expression", 1).uU;
		_ = Parse("(1+2*3))*4").H("Mul", (0, 7), '*', 1).N("Expr", (0, 7), '+', 1).uU;
		_ = Parse("(1*2+3").H("Mul", (5, 6), '*', 1).N("Value", (0, 6), ')', 2)
						.N("Expr", (1, 6), '+', 1).N("Num", (5, 6), "digit", 1).uU;
		_ = Parse("(1*2+)").H("Expr", (1, 5), "expression", 2).uU;
		_ = Parse("()").H("Value", (0, 1), "expression", 1).uU;
	}

	[TestMethod]
	public void Greedy1()
	{
		NewSer("S = A B \n A = A 1|1 \n B = 1|B 1");
		_ = Parse("111").H("A", d: "1").n("B", d: "11").H("B", d: "1").uuU;
		ser.greedy = true;
		_ = Parse("111").h("A", d: "11").H("A", d: "1").u.N("B", d: "1").uU;
	}

	[TestMethod]
	public void Greedy2()
	{
		NewSer("S =A B C D \nA =1|12 \nB =234|3 \nC =5|456 \nD =67|7");
		_ = Parse("1234567").H("A", d: "1").N("B", d: "234").N("C", d: "5").N("D", d: "67").uU;
		ser.greedy = true;
		_ = Parse("1234567").H("A", d: "12").N("B", d: "3").N("C", d: "456").N("D", d: "7").uU;
	}

	[TestMethod]
	public void GreedyHint()
	{
		NewSer("S = A B =*\n A = A 1|1 \n B = 1|B 1");
		_ = Parse("111").h("A", d: "11").H("A", d: "1").u.N("B", d: "1").uU;
		ser.greedy = true;
		_ = Parse("111").h("A", d: "11").H("A", d: "1").u.N("B", d: "1").uU;
	}

	[TestMethod]
	public void Empty1()
	{
		NewSer("S=a A B \n A=|a+ \n B=A");
		ser.greedy = true;
		True("a"); True("aa");
		_ = Parse("aaa").H("A", d: "aa").n("B", d: "").H("A", d: "").uuU;
	}

	[TestMethod]
	public void Empty2()
	{
		NewSer("S=A B \n A=a P \n B=P b \n P=|pq");
		True("ab"); True("apqb");
		True("apqpqb"); False("apqpqpqb");
	}

	[TestMethod]
	public void Option1()
	{
		NewSer("S=A B?a? \n A=a|aa \n B=a");
		False(""); True("a"); True("aaaa");
		_ = Parse("aa").H("A", d: "a").uU;
		_ = Parse("aaa").H("A", d: "a").N("B", d: "a").uU;
		ser.greedy = true;
		_ = Parse("aaa").H("A", d: "aa").N("B", d: "a").uU;
	}

	[TestMethod]
	public void Option2()
	{
		NewSer("S=A?B? \n A=B a \n B=b");
		True(""); True("ba"); True("b");
		True("bab"); False("ab");
	}

	[TestMethod]
	public void More1()
	{
		NewSer("S=a+");
		False(""); True("a"); True("aaaaaa");
		True("aaaaaaa"); AreEqual(15, ser.matchz);
	}

	[TestMethod]
	public void More2()
	{
		NewSer("S=A B \n A=a P+ \n B=P+b \n P=pq");
		False("apqb"); True("apqpqb");
		_ = Parse("apqpqpqb").h("A", d: "apq").n("B", d: "pqpqb").uU;
		ser.greedy = true;
		_ = Parse("apqpqpqb").h("A", d: "apqpq").n("B", d: "pqb").uU;
		AreEqual(34, ser.matchz);
	}

	[TestMethod]
	public void Any1()
	{
		NewSer("S=a*");
		True(""); True("a"); True("aaaaaa");
		True("aaaaaaa"); AreEqual(16, ser.matchz);
	}

	[TestMethod]
	public void Any2()
	{
		NewSer("S=A B \n A=a P* \n B=P* b \n P=p|q");
		True("ab"); True("apqb"); True("apqpqb");
		_ = Parse("apqpqpqb").H("A", (0, 1)).n("B").H("P", (1, null)).N().N().N().N().N("P", (6, null)).uuU;
		AreEqual(107, ser.matchz);
		ser.greedy = true;
		_ = Parse("apqpqpqb").h("A").H("P", (1, null)).N().N().N().N().N("P", (6, null)).u
			.N("B", d: "b").uU;
		AreEqual(107, ser.matchz);
	}

	[TestMethod]
	public void HintSynt1()
	{
		NewSer("S=A B C\n A=1 =+\n B=1 =-\n C=1");
		ser.tree = true;
		_ = Parse("111").H("A").N("C").uU;
		ser.tree = false;
		_ = Parse("111").H("A").uU;
	}

	[TestMethod]
	public void HintSynt2()
	{
		NewSer("S=Z*U* \n U=A*V* =+-\n V=B*C* =+-\n Z=z \n A=a \n B=b \n C=c");
		ser.tree = true;
		var _ = Parse("zabc");
		_ = _/**/.H("Z");
		_ = _/**/.n("U").H("A");
		_ = _/**/		.n("V").H("B");
		_ = _/**/				.N("C").uuuU;
		_ = Parse("zab");
		_ = _/**/.H("Z");
		_ = _/**/.n("U").H("A");
		_ = _/**/		.N("B").uuU;
		_ = Parse("zbc");
		_ = _/**/.H("Z");
		_ = _/**/.n("U").H("B");
		_ = _/**/		.N("C").uuU;
		_ = Parse("abc");
		_ = _/**/.H("A");
		_ = _/**/.n("V").H("B");
		_ = _/**/		.N("C").uuU;
		_ = Parse("ab").H("A").N("B").uU;
		_ = Parse("bc").H("B").N("C").uU;
	}

	[TestMethod]
	public void HintSynt3()
	{
		NewSer("S=Z*U* \n U=A*V* =+-\n V=B*C* =+-\n Z=z =-\n A=a \n B=b \n C=c");
		ser.tree = true;
		var _ = Parse("zabc");
		_ = _/**/.H("A");
		_ = _/**/.n("V").H("B");
		_ = _/**/		.N("C").uuU;
		_ = Parse("zab").H("A").N("B").uU;
		_ = Parse("zbc").H("B").N("C").uU;
		_ = Parse("abc");
		_ = _/**/.H("A");
		_ = _/**/.n("V").H("B");
		_ = _/**/		.N("C").uuU;
		_ = Parse("ab").H("A").N("B").uU;
		_ = Parse("bc").H("B").N("C").uU;
	}

	[TestMethod]
	public void Recovery1()
	{
		NewSer("S=A|S,A? =|\n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5");
		var _ = Parse("0,").h("S").h("A").h("M").H("V").uuuuU;
		_ = Parse("+").Eq(d: '+', err: -1).Leaf.U;
		_ = Parse("+0").Eq(d: '+', err: -1).Leaf.U;
		_ = Parse("+,+0").Eq(d: '+', err: -1).Leaf.U;
		_ = Parse("0+").h("S", (0, 1)).N("S", err: -4).u.n(j: (null, 2), err: -1).H("A", (0, 2), "M", 2).uU;
		_ = Parse("0*").h("S", (0, 1)).N("S", err: -4).u.n(j: (null, 2), err: -1).H("M", (0, 2), "V", 2).uU;
		_ = Parse("0#&$");
		_ = _/**/	.h("S", (0, 1)).N("S", (0, 4), err: -4).u;
		_ = _/**/.n("S", (1, 2), '#', -1);
		_ = _/**/	.H("M", (0, 1), '*', 1).N("A", (0, 1), '+', 1).N("S", (0, 1), ',', 1).uU;
	}

	[TestMethod]
	public void Recovery2()
	{
		NewSer("S=A|S,A? =|\n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5");
		var _ = Parse("0+,1*,2#");
		_ = _/**/	.h("S", (0, 7)).h("S", (0, 4)).h("S", d: "0");
		_ = _/**/								.N("S", (0, 3), err: -4).n("A", d: "1").u;
		_ = _/**/				.N("S", (0, 6), err: -4).n("A", d: "2").u;
		_ = _/**/	.N("S", (0, 8), err: -4).u;
		_ = _/**/.n("S", (2, 3), ',', -1);
		_ = _/**/	.H("A", (0, 2), "M", 2).u;
		_ = _/**/.n("S", (5, 6), ',', -1);
		_ = _/**/	.H("M", (3, 5), "V", 2).u;
		_ = _/**/.n("S", (7, 8), '#', -1);
		_ = _/**/	.H("M", (6, 7), '*', 1).N("A", (6, 7), '+', 1).N("S", (0, 7), ',', 1).uU;
		_ = Parse("0+1*2+");
		_ = _/**/	.h("S", (0, 5)).h("A", (0, 5)).u.N("S", (0, 6), err: -4).u;
		_ = _/**/.n("S", (6, 6), null, -1);
		_ = _/**/	.H("A", (0, 6), "M", 2).uU;
		_ = Parse("0+1*2+,1");
		_ = _/**/	.h("S", (0, 5)).h("A", (0, 5)).u.N("S", (0, 7), err: -4);
		_ = _/**/	.n("A", (7, 8)).u;
		_ = _/**/.n("S", (6, 7), ',', -1);
		_ = _/**/	.H("A", (0, 6), "M", 2).uU;
	}

	[TestMethod]
	public void Recovery3()
	{
		NewSer("S=A|S,A? =*|\n A=M|A\\+M \n M=V|M\\*V \n V=(A)|N =|\n N=0|1|2|3|4|5");
		var _ = Parse("()");
		_ = _/**/	.h("A").h("M").h("V").H("V", (0, 2), err: -4).uuuu;
		_ = _/**/.n("S", (1, 2), ')', -1);
		_ = _/**/	.H("V", (0, 1), "A", 1).uU;
		_ = Parse("0+(),1");
		_ = _/**/	.h("S").h("A").h("A").h("M").u;
		_ = _/**/					.n("M").h("V").H("V", (2, 4), err: -4).uuuu;
		_ = _/**/	.n("A").u;
		_ = _/**/.n("S", (3, 4), ')', -1);
		_ = _/**/	.H("V", (2, 3), "A", 1).uU;
		_ = Parse("0,1+(2*),3#");
		_ = _/**/	.h("S").h("S").h("S", (0, 1));
		_ = _/**/				.n("A").h("A", d: "1");
		_ = _/**/						.n("M").h("V", (4, 8)).h("A", d: "2");
		_ = _/**/											.N("V", (4, 8), err: -4).uuuu;
		_ = _/**/			.n("A", d: "3").u;
		_ = _/**/	.N("S", (0, 11), err: -4).u;
		_ = _/**/.n("S", (7, 8), ')', -1);
		_ = _/**/	.H("M", (5, 7), "V", 2).u;
		_ = _/**/.n("S", (10, 11), '#', -1);
		_ = _/**/	.H("M", (9, 10), '*', 1).N("A", (9, 10), '+', 1).N("S", (0, 10), ',', 1).uU;
		_ = Parse("0,1+(2*)4,3+");
		_ = _/**/	.h("S").h("S").h("S", (0, 1));
		_ = _/**/				.n("A").h("A", d: "1");
		_ = _/**/						.n("M").h("V", (4, 8)).h("A", d: "2");
		_ = _/**/											.N("V", (4, 8), err: -4).uuuu;
		_ = _/**/			.N("S", (0, 10), err: -4).n("A", d: "3").u;
		_ = _/**/	.N("S", (0, 12), err: -4).u;
		_ = _/**/.n("S", (7, 8), ')', -1);
		_ = _/**/	.H("M", (5, 7), "V", 2).u;
		_ = _/**/.n("S", (8, 9), '4', -1);
		_ = _/**/	.H("M", (4, 8), '*', 1).N("A", (2, 8), '+', 1).N("S", (0, 8), ',', 1).u;
		_ = _/**/.n("S", (12, 12), null, -1);
		_ = _/**/	.H("A", (10, 12), "M", 2).uU;
	}

	[TestMethod]
	public void Recovery4()
	{
		NewSer("P=S+ \n S=V|{S+} =*|\n V=(N)|N =|\n N=0|1|2|3|4|5");
		var _ = Parse("{()");
		_ = _/**/	.h("S").h("S").h("V");
		_ = _/**/					.H("V", (1, 3), err: -4).uu;
		_ = _/**/			.N("S", (0, 3), err: -4).uu;
		_ = _/**/.n("P", (2, 3), ')', -1);
		_ = _/**/	.H("V", (1, 2), "N", 1).u;
		_ = _/**/.n("P", (3, 3), null, -1);
		_ = _/**/	.H("S", (0, 3), '}', 2).N("S", (0, 3), "S", 1).uU;
		ser.recover = 1;
		_ = Parse("{{").Eq("P", (2, 2), null, -1).H("S", (1, 2), "S", 1).uU;
		ser.recover = 2;
		_ = Parse("{(");
		_ = _/**/	.h("S").h("S").h("V");
		_ = _/**/					.H("V", (1, 2), err: -4).uu;
		_ = _/**/			.N("S", (0, 2), err: -4).uu;
		_ = _/**/.n("P", (2, 2), null, -1);
		_ = _/**/	.H("V", (1, 2), "N", 1).u;
		_ = _/**/.n("P", (2, 2), null, -1);
		_ = _/**/	.H("V", (1, 2), "N", 1).N("S", (0, 2), '}', 2).N("S", (0, 2), "S", 1).uU;
	}

	[TestMethod]
	public void Recovery5()
	{
		NewSer("P=S+ \n S=V|{S+} =*|\n|\\0?\\0} =-| \n V=0|1|2|3|4|5");
		var _ = Parse("{{0}}}}{1}");
		_ = _/**/	.h("S", (0, 5), "{{0}}").h("S", (1, 4), "{0}").u;
		_ = _/**/	.N("S", (5, 6), err: -4).N("S", (6, 7), err: -4).n("S", (7, 10), "{1}").u;
		_ = _/**/.n("P", (5, 6), '}', -1).n("P", (6, 7), '}', -1).U;
	}
}
