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
using System.Linq;
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
		return (t, s);
	}

	public static Ser Eq(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
	{
		AreNotEqual(null, s.t);
		AreEqual(err, s.t.err);
		if (name != null) AreEqual(name, s.t.name);
		if (from != null) AreEqual(from, s.t.from);
		if (to != null) AreEqual(to, s.t.to);
		if (v != null) AreEqual(v, s.t.err == 0 ? s.s.ler.Lexs(s.t.from, s.t.to) : s.t.info);
		return s;
	}

	public static Ser h(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (s.t.head, s.s).Eq(name, from, to, v, err);
	public static Ser t(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (s.t.tail, s.s).Eq(name, from, to, v, err);
	public static Ser n(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (s.t.next, s.s).Eq(name, from, to, v, err);
	public static Ser p(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> (s.t.prev, s.s).Eq(name, from, to, v, err);

	public static Ser H(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> h(s, name, from, to, v, err).Leaf();
	public static Ser T(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> t(s, name, from, to, v, err).Leaf();
	public static Ser N(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> n(s, name, from, to, v, err).Leaf();
	public static Ser P(this Ser s,
		string name = null, int? from = null, int? to = null, object v = null, int err = 0)
		=> p(s, name, from, to, v, err).Leaf();

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
}

[TestClass]
public class TestSynter : IDisposable
{
	readonly EnvWriter env = EnvWriter.Begin();

	public void Dispose() => env.Dispose();

	public static SynterStr NewSer(string Alts, char[] keys, ushort[] names, SynForm<char, string>[] forms)
	{
		var alts = Alts.Split('\n', ' ').Select(alt => new SynAlt<string> {
			name = alt[0..1],
			len = alt.Length - 2, // N=
			lex = alt.IndexOfAny(keys, 2) - 2,
		}).ToArray();
		var ks = keys.Select(k => (ushort)k).Prepend(default); // { other... }
		foreach (var form in forms)
			if (form != null) {
				form.modes = form.modes.Prepend(default).ToArray();
				Array.Sort(form.keys = ks.ToArray(), form.modes, 1, keys.Length);
				if (form.pushs != null)
					Array.Sort(form.names = names[0..], form.pushs);
			}
		return new(ler => ler.Lex(), name => name[0], alts, forms);
	}

	[TestMethod]
	public void TigerF325()
	{
		var s = NewSer("S=E E=T+E E=T T=a", ['a', 'b', '+', '\0'], ['E', 'T'], [ null,
			new() { modes = [5, 5, 0, 0],      pushs=[2, 3] },
			new() { modes = [0, 0, 0, -2-0]                 },
			new() { modes = [0, 0, 4, -2-2]                 },
			new() { modes = [5, 5, 0, 0],      pushs=[6, 3] },
			new() { modes = [0, 0, -2-3, -2-3]              },
			new() { modes = [0, 0, 0, -2-1]                 },
		]);
		IsTrue(s.Check("a")); IsTrue(s.Check("b"));
		IsTrue(s.Check("a+b")); IsTrue(s.Check("a+b+a"));
		IsFalse(s.Check("c")); IsFalse(s.Check("+")); IsFalse(s.Check("ab"));
		IsFalse(s.Check("a+")); IsFalse(s.Check("+b"));
		IsFalse(s.Check("a+b+")); IsFalse(s.Check("+a+b")); IsFalse(s.Check("a++b"));
		s.dump = 3;
		var t = s.Parse("a+b+a");
		t = t/**/.Eq("S", 0, 5).h("E");
		t = t/**/				.H("T", 0, 1, v: "a");
		t = t/**/				.n("E", 2, 5, v: "b+a").H("T", 2, 3, v: "b");
		t = t/**/									.n("E").H("T", 4, 5, v: "a").uuuuU();
	}

	/*
	[TestMethod]
	public void Term1()
	{
		var s = new SynterStr("S=k");
		IsTrue(s.Check("k"));
		IsFalse(s.Check("")); IsFalse(s.Check("kk")); IsFalse(s.Check("K"));
	}

	[TestMethod]
	public void Term2()
	{
		var s = new SynterStr("S=");
		IsTrue(s.Check("")); IsFalse(s.Check("a"));
	}

	[TestMethod]
	public void Alt1()
	{
		var s = new SynterStr("S=|a");
		IsTrue(s.Check("")); IsTrue(s.Check("a")); IsFalse(s.Check("b"));
	}

	[TestMethod]
	public void Alt2()
	{
		var s = new SynterStr("S=A\nA=a|1|#");
		IsTrue(s.Check("a")); IsTrue(s.Check("1")); IsTrue(s.Check("#"));
		IsFalse(s.Check("")); IsFalse(s.Check("a1")); IsFalse(s.Check("A"));
	}

	[TestMethod]
	public void Con()
	{
		var s = new SynterStr("S = A B|B A \nA = a|1 \nB = b|2");
		IsFalse(s.Check("a")); IsFalse(s.Check("1")); IsFalse(s.Check("b")); IsFalse(s.Check("2"));
		IsTrue(s.Check("ab")); IsTrue(s.Check("ba"));
		IsTrue(s.Check("1b")); IsTrue(s.Check("b1"));
		IsTrue(s.Check("a2")); IsTrue(s.Check("2a"));
		IsTrue(s.Check("12")); IsTrue(s.Check("21"));
		IsFalse(s.Check("aba")); IsFalse(s.Check("12ab"));
	}

	[TestMethod]
	public void AltPrior1()
	{
		var s = new SynterStr("S=.E =^ \n | E \n E=W|P \n W=a \n P=.W") { dump = 3 };
		s.Parse(".a").h("E").H("W").uuU();
	}

	[TestMethod]
	public void AltPrior2()
	{
		var s = new SynterStr("S=.E \n | E =^ \n E=W|P \n W=a \n P=.W") { dump = 3 };
		s.Parse(".a").h("E").h("P").H("W").uuuU();
	}

	[TestMethod]
	public void Esc()
	{
		var s = new SynterStr(@"S = \ss\tt\r\n\\/ | \|or\=eq\*s\+p\?q");
		IsTrue(s.Check(" s\tt\r\n\\/")); IsFalse(s.Check(" s\tt\r \n\\/"));
		IsTrue(s.Check("|or=eq*s+p?q"));
	}

	[TestMethod]
	public void ErrHint()
	{
		var s = new SynterStr("S=A B =start \n A=1|2 =A12 ==oh||no\n |3 =A3 \n B= =\tempty \n |4 =B4") {
			dump = 3
		};
		IsNull(s.Parse("").t.head);
		IsNull(s.Parse("4").t.head);
		s.Parse("15").H("S", 0, 1, "empty", 1);
	}

	[TestMethod]
	public void LeftRecu()
	{
		var s = new SynterStr("S = S b|A \nA = a");
		IsTrue(s.Check("a")); IsTrue(s.Check("ab")); IsTrue(s.Check("abb")); IsTrue(s.Check("abbb"));
		IsFalse(s.Check("")); IsFalse(s.Check("b")); IsFalse(s.Check("aab")); IsFalse(s.Check("abbba"));
	}

	[TestMethod]
	public void RightRecu()
	{
		var s = new SynterStr("S = a S|B \nB = b");
		IsTrue(s.Check("b")); IsTrue(s.Check("ab")); IsTrue(s.Check("aab")); IsTrue(s.Check("aaaab"));
		IsFalse(s.Check("")); IsFalse(s.Check("a")); IsFalse(s.Check("abb")); IsFalse(s.Check("abbba"));
	}

	[TestMethod]
	public void RightRecuUnopt()
	{
		var s = new SynterStr("S= aa B \nB= A a \nA= a A|a") { dump = 3 };
		IsFalse(s.Check("aa")); IsFalse(s.Check("aaa"));
		IsTrue(s.Check("aaaa")); IsTrue(s.Check("aaaaa")); IsTrue(s.Check("aaaaaa"));
		IsTrue(s.Check("aaaaaaa")); AreEqual(49, s.matchn);
		s = new SynterStr("S= aa B \nB= A a \nA= A a|a") { dump = 3 };
		IsTrue(s.Check("aaaaaaa")); AreEqual(29, s.matchn);
		s = new SynterStr("S= aa B \nB= A a \nA= a+") { dump = 3 };
		IsTrue(s.Check("aaaaaaa")); AreEqual(28, s.matchn);
	}
*/
	//[TestMethod]
	//public void MidRecu()
	//{
	//	var s = new SynterStr("""
	//		S	= If|X =-
	//		If	= if \s S \s then \s S
	//			| if \s S \s then \s S \s else \s S
	//		X	= a|b|c|d|e|f|g|h|i|j
	//		""") { dump = 3 };
	//	s.Parse("if a then b").h("If").H(v: "a").N(v: "b").uuU();
	//	s.Parse("if a then b else c").h().H(v: "a").N(v: "b").N(v: "c").uuU();
	//	var t = s.Parse("if a then if b then c").h().H(v: "a");
	//	t = t/**/									.n().H(v: "b").N(v: "c").uuuU();
	//	t = s.Parse("if a then if b then c else d").h().H(v: "a");
	//	t = t/**/									   .n().H(v: "b").N(v: "c").u();
	//	t = t/**/									   .N(v: "d").uuU();
	//	t = s.Parse("if a then if b then c else d else e").h().H(v: "a");
	//	t = t/**/											  .n().H(v: "b").N(v: "c").N(v: "d").u();
	//	t = t/**/											  .N(v: "e").uuU();
	//	t = s.Parse("if a then if b then c else if d then e else f else g");
	//	t = t/**/.h().H(v: "a");
	//	t = t/**/	.n().H(v: "b").N(v: "c");
	//	t = t/**/		 .n().H(v: "d").N(v: "e").N(v: "f").uu();
	//	t = t/**/	.N(v: "g").uuU();
	//	t = s.Parse("if a then if b then if c then d else e else if g then h else i else j");
	//	t = t/**/.h().H(v: "a");
	//	t = t/**/	.n().H(v: "b");
	//	t = t/**/		.n().H(v: "c").N(v: "d").N(v: "e").u();
	//	t = t/**/		.n().H(v: "g").N(v: "h").N(v: "i").uu();
	//	t = t/**/	.N(v: "j").uuU();
	//	IsFalse(s.Check("if a else b"));
	//	IsFalse(s.Check("if a then b then c"));
	//	IsFalse(s.Check("if a then if b else c"));
	//}
	/*
	[TestMethod]
	public void DoubleRecu()
	{
		var s = new SynterStr("S=S S|a");
		IsFalse(s.Check(""));
		IsTrue(s.Check("a")); IsTrue(s.Check("aa")); IsTrue(s.Check("aaa")); IsTrue(s.Check("aaaa"));
	}

	[TestMethod]
	public void AddMul()
	{
		var s = new SynterStr("""
			Expr  = Expr\+Mul | Mul
			Mul   = Mul\*Value | Value
			Value = (Expr) | Num
			Num   = Num Digi | Digi
			Digi  = 0|1|2|3|4|5|6|7|8|9
			""") { dump = 3 };
		IsTrue(s.Check("1")); IsTrue(s.Check("07")); IsTrue(s.Check("(3)")); IsTrue(s.Check("(298)"));
		IsFalse(s.Check("1 3")); IsFalse(s.Check("(2")); IsFalse(s.Check("39210)"));
		IsTrue(s.Check("1*2")); IsTrue(s.Check("073*32")); IsTrue(s.Check("86*1231*787*99"));
		IsFalse(s.Check("1*")); IsFalse(s.Check("*3")); IsFalse(s.Check("1*2*"));
		IsTrue(s.Check("1+2")); IsTrue(s.Check("073+32")); IsTrue(s.Check("86+1231+787+99"));
		IsFalse(s.Check("1+")); IsFalse(s.Check("+3")); IsFalse(s.Check("1+2+"));
		IsTrue(s.Check("1*2+3")); IsTrue(s.Check("1+2*3")); IsTrue(s.Check("7+58*23+882*152*33+89*6"));
		IsFalse(s.Check("1*+3")); IsFalse(s.Check("+3*2")); IsFalse(s.Check("+1*2+6+"));
		IsTrue(s.Check("1*(2+3)")); IsTrue(s.Check("(1+2)*3")); IsTrue(s.Check("(7+58)*(23+882)*152*(33+89)*6"));
		IsTrue(s.Check("(1*2)+3")); IsTrue(s.Check("(1+2*3)")); IsTrue(s.Check("7+(5*23)*(15*33+89)*0"));
		IsTrue(s.Check("(1*(2+5)+2)+3")); IsTrue(s.Check("53+((((1))))+2*3"));
		IsFalse(s.Check("1*(3+)")); IsFalse(s.Check("(3))*2")); IsFalse(s.Check("(5*(2)(6)+8)"));
		IsFalse(s.Check("1*()3+5")); IsFalse(s.Check("(3))*2")); IsFalse(s.Check("(5*(2)(6)+8)"));
	}

	[TestMethod]
	public void AddMulErr()
	{
		var s = new SynterStr("""
			Expr  = Expr\+Mul | Mul     = expression
			Mul   = Mul\*Value | Value  = expression
			Value = (Expr) | Num        = value
			Num   = Num Digi | Digi     = number
			Digi  = 0|1|2|3|4|5|6|7|8|9 = digit
			""") { dump = 3 };
		s.Parse("(1+2*").H("Mul", 3, 5, "value", 2).uU();
		s.Parse("(*1*2+3)*4").H("Value", 0, 1, "expression", 1).uU();
		s.Parse("(1+2*3))*4").H("Mul", 0, 7, '*', 1).N("Expr", 0, 7, '+', 1).uU();
		s.Parse("(1*2+3").H("Mul", 5, 6, '*', 1).N("Value", 0, 6, ')', 2)
						.N("Expr", 1, 6, '+', 1).N("Num", 5, 6, "digit", 1).uU();
		s.Parse("(1*2+)").H("Expr", 1, 5, "expression", 2).uU();
		s.Parse("()").H("Value", 0, 1, "expression", 1).uU();
	}

	[TestMethod]
	public void Greedy1()
	{
		var s = new SynterStr("S = A B \n A = A 1|1 \n B = 1|B 1") { dump = 3 };
		s.Parse("111").H("A", v: "1").n("B", v: "11").H("B", v: "1").uuU();
		s.greedy = true;
		s.Parse("111").h("A", v: "11").H("A", v: "1").u().N("B", v: "1").uU();
	}

	[TestMethod]
	public void Greedy2()
	{
		var s = new SynterStr("S =A B C D \nA =1|12 \nB =234|3 \nC =5|456 \nD =67|7") {
			dump = 3
		};
		s.Parse("1234567").H("A", v: "1").N("B", v: "234").N("C", v: "5").N("D", v: "67").uU();
		s.greedy = true;
		s.Parse("1234567").H("A", v: "12").N("B", v: "3").N("C", v: "456").N("D", v: "7").uU();
	}

	[TestMethod]
	public void GreedyHint()
	{
		var s = new SynterStr("S = A B =*\n A = A 1|1 \n B = 1|B 1") { dump = 3 };
		s.Parse("111").h("A", v: "11").H("A", v: "1").u().N("B", v: "1").uU();
		s.greedy = true;
		s.Parse("111").h("A", v: "11").H("A", v: "1").u().N("B", v: "1").uU();
	}

	[TestMethod]
	public void Empty1()
	{
		var s = new SynterStr("S=a A B \n A=|a+ \n B=A") { greedy = true, dump = 3 };
		IsTrue(s.Check("a")); IsTrue(s.Check("aa"));
		s.Parse("aaa").H("A", v: "aa").n("B", v: "").H("A", v: "").uuU();
	}

	[TestMethod]
	public void Empty2()
	{
		var s = new SynterStr("S=A B \n A=a P \n B=P b \n P=|pq") { dump = 3 };
		IsTrue(s.Check("ab")); IsTrue(s.Check("apqb"));
		IsTrue(s.Check("apqpqb")); IsFalse(s.Check("apqpqpqb"));
	}

	[TestMethod]
	public void Option1()
	{
		var s = new SynterStr("S=A B?a? \n A=a|aa \n B=a") { dump = 3 };
		IsFalse(s.Check("")); IsTrue(s.Check("a")); IsTrue(s.Check("aaaa"));
		s.Parse("aa").H("A", v: "a").uU();
		s.Parse("aaa").H("A", v: "a").N("B", v: "a").uU();
		s.greedy = true;
		s.Parse("aaa").H("A", v: "aa").N("B", v: "a").uU();
	}

	[TestMethod]
	public void Option2()
	{
		var s = new SynterStr("S=A?B? \n A=B a \n B=b");
		IsTrue(s.Check("")); IsTrue(s.Check("ba")); IsTrue(s.Check("b"));
		IsTrue(s.Check("bab")); IsFalse(s.Check("ab"));
	}

	[TestMethod]
	public void More1()
	{
		var s = new SynterStr("S=a+") { dump = 3 };
		IsFalse(s.Check("")); IsTrue(s.Check("a")); IsTrue(s.Check("aaaaaa"));
		IsTrue(s.Check("aaaaaaa")); AreEqual(15, s.matchn);
	}

	[TestMethod]
	public void More2()
	{
		var s = new SynterStr("S=A B \n A=a P+ \n B=P+b \n P=pq") { dump = 3 };
		IsFalse(s.Check("apqb")); IsTrue(s.Check("apqpqb"));
		s.Parse("apqpqpqb").h("A", v: "apq").n("B", v: "pqpqb").uU();
		s.greedy = true;
		s.Parse("apqpqpqb").h("A", v: "apqpq").n("B", v: "pqb").uU();
		AreEqual(34, s.matchn);
	}

	[TestMethod]
	public void Any1()
	{
		var s = new SynterStr("S=a*") { dump = 3 };
		IsTrue(s.Check("")); IsTrue(s.Check("a")); IsTrue(s.Check("aaaaaa"));
		IsTrue(s.Check("aaaaaaa")); AreEqual(16, s.matchn);
	}

	[TestMethod]
	public void Any2()
	{
		var s = new SynterStr("S=A B \n A=a P* \n B=P* b \n P=p|q") { dump = 3 };
		IsTrue(s.Check("ab")); IsTrue(s.Check("apqb")); IsTrue(s.Check("apqpqb"));
		s.Parse("apqpqpqb").H("A", 0, 1).n("B").H("P", 1).N().N().N().N().N("P", 6).uuU();
		AreEqual(107, s.matchn);
		s.greedy = true;
		s.Parse("apqpqpqb").h("A").H("P", 1).N().N().N().N().N("P", 6).u().N("B", v: "b").uU();
		AreEqual(107, s.matchn);
	}

	[TestMethod]
	public void HintSynt1()
	{
		var s = new SynterStr("S=A B C\n A=1 =+\n B=1 =-\n C=1") {
			tree = true, dump = 3
		};
		s.Parse("111").H("A").N("C").uU();
		s.tree = false;
		s.Parse("111").H("A").uU();
	}
*/
	//[TestMethod]
	//public void HintSynt2()
	//{
	//	var s = new SynterStr("S=Z*U* \n U=A*V* =+-\n V=B*C* =+-\n Z=z \n A=a \n B=b \n C=c") {
	//		tree = true, dump = 3
	//	};
	//	var t = s.Parse("zabc");
	//	t = t/**/.H("Z");
	//	t = t/**/.n("U").H("A");
	//	t = t/**/		.n("V").H("B");
	//	t = t/**/				.N("C").uuuU();
	//	t = s.Parse("zab");
	//	t = t/**/.H("Z");
	//	t = t/**/.n("U").H("A");
	//	t = t/**/		.N("B").uuU();
	//	t = s.Parse("zbc");
	//	t = t/**/.H("Z");
	//	t = t/**/.n("U").H("B");
	//	t = t/**/		.N("C").uuU();
	//	t = s.Parse("abc");
	//	t = t/**/.H("A");
	//	t = t/**/.n("V").H("B");
	//	t = t/**/		.N("C").uuU();
	//	s.Parse("ab").H("A").N("B").uU();
	//	s.Parse("bc").H("B").N("C").uU();
	//}

	//[TestMethod]
	//public void HintSynt3()
	//{
	//	var s = new SynterStr("S=Z*U* \n U=A*V* =+-\n V=B*C* =+-\n Z=z =-\n A=a \n B=b \n C=c") {
	//		tree = true, dump = 3
	//	};
	//	var t = s.Parse("zabc");
	//	t = t/**/.H("A");
	//	t = t/**/.n("V").H("B");
	//	t = t/**/		.N("C").uuU();
	//	s.Parse("zab").H("A").N("B").uU();
	//	s.Parse("zbc").H("B").N("C").uU();
	//	t = s.Parse("abc");
	//	t = t/**/.H("A");
	//	t = t/**/.n("V").H("B");
	//	t = t/**/		.N("C").uuU();
	//	s.Parse("ab").H("A").N("B").uU();
	//	s.Parse("bc").H("B").N("C").uU();
	//}

	//[TestMethod]
	//public void Recovery1()
	//{
	//	var s = new SynterStr("S=A|S,A? =|\n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") {
	//		dump = 3
	//	};
	//	s.Parse("0,").h("S").h("A").h("M").H("V").uuuuU();
	//	s.Parse("+").Eq(v: '+', err: -1).Leaf().U();
	//	s.Parse("+0").Eq(v: '+', err: -1).Leaf().U();
	//	s.Parse("+,+0").Eq(v: '+', err: -1).Leaf().U();
	//	s.Parse("0+").h("S", 0, 1).N("S", err: -4).u().n(to: 2, err: -1).H("A", 0, 2, "M", 2).uU();
	//	s.Parse("0*").h("S", 0, 1).N("S", err: -4).u().n(to: 2, err: -1).H("M", 0, 2, "V", 2).uU();
	//	var t = s.Parse("0#&$");
	//	t = t/**/	.h("S", 0, 1).N("S", 0, 4, err: -4).u();
	//	t = t/**/.n("S", 1, 2, '#', -1);
	//	t = t/**/	.h("M", 0, 1, '*', 1).N("A", 0, 1, '+', 1).N("S", 0, 1, ',', 1).uU();
	//}

	//[TestMethod]
	//public void Recovery2()
	//{
	//	var s = new SynterStr("S=A|S,A? =|\n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") {
	//		dump = 3
	//	};
	//	var t = s.Parse("0+,1*,2#");
	//	t = t/**/	.h("S", 0, 7).h("S", 0, 4).h("S", v: "0");
	//	t = t/**/								.N("S", 0, 3, err: -4).n("A", v: "1").u();
	//	t = t/**/				.N("S", 0, 6, err: -4).n("A", v: "2").u();
	//	t = t/**/	.N("S", 0, 8, err: -4).u();
	//	t = t/**/.n("S", 2, 3, ',', -1);
	//	t = t/**/	.H("A", 0, 2, "M", 2).u();
	//	t = t/**/.n("S", 5, 6, ',', -1);
	//	t = t/**/	.H("M", 3, 5, "V", 2).u();
	//	t = t/**/.n("S", 7, 8, '#', -1);
	//	t = t/**/	.H("M", 6, 7, '*', 1).N("A", 6, 7, '+', 1).N("S", 0, 7, ',', 1).uU();
	//	t = s.Parse("0+1*2+");
	//	t = t/**/	.h("S", 0, 5).h("A", 0, 5).u().N("S", 0, 6, err: -4).u();
	//	t = t/**/.n("S", 6, 6, null, -1);
	//	t = t/**/	.H("A", 0, 6, "M", 2).uU();
	//	t = s.Parse("0+1*2+,1");
	//	t = t/**/	.h("S", 0, 5).h("A", 0, 5).u().N("S", 0, 7, err: -4);
	//	t = t/**/	.n("A", 7, 8).u();
	//	t = t/**/.n("S", 6, 7, ',', -1);
	//	t = t/**/	.H("A", 0, 6, "M", 2).uU();
	//}

	//[TestMethod]
	//public void Recovery3()
	//{
	//	var s = new SynterStr("S=A|S,A? =*|\n A=M|A\\+M \n M=V|M\\*V \n V=(A)|N =|\n N=0|1|2|3|4|5") {
	//		dump = 3
	//	};
	//	var t = s.Parse("()");
	//	t = t/**/	.h("A").h("M").h("V").H("V", 0, 2, err: -4).uuuu();
	//	t = t/**/.n("S", 1, 2, ')', -1);
	//	t = t/**/	.H("V", 0, 1, "A", 1).uU();
	//	t = s.Parse("0+(),1");
	//	t = t/**/	.h("S").h("A").h("A").h("M").u();
	//	t = t/**/					.n("M").h("V").H("V", 2, 4, err: -4).uuuu();
	//	t = t/**/	.n("A").u();
	//	t = t/**/.n("S", 3, 4, ')', -1);
	//	t = t/**/	.H("V", 2, 3, "A", 1).uU();
	//	t = s.Parse("0,1+(2*),3#");
	//	t = t/**/	.h("S").h("S").h("S", 0, 1);
	//	t = t/**/				.n("A").h("A", v: "1");
	//	t = t/**/						.n("M").h("V", 4, 8).h("A", v: "2");
	//	t = t/**/											.N("V", 4, 8, err: -4).uuuu();
	//	t = t/**/			.n("A", v: "3").u();
	//	t = t/**/	.N("S", 0, 11, err: -4).u();
	//	t = t/**/.n("S", 7, 8, ')', -1);
	//	t = t/**/	.H("M", 5, 7, "V", 2).u();
	//	t = t/**/.n("S", 10, 11, '#', -1);
	//	t = t/**/	.H("M", 9, 10, '*', 1).N("A", 9, 10, '+', 1).N("S", 0, 10, ',', 1).uU();
	//	t = s.Parse("0,1+(2*)4,3+");
	//	t = t/**/	.h("S").h("S").h("S", 0, 1);
	//	t = t/**/				.n("A").h("A", v: "1");
	//	t = t/**/						.n("M").h("V", 4, 8).h("A", v: "2");
	//	t = t/**/											.N("V", 4, 8, err: -4).uuuu();
	//	t = t/**/			.N("S", 0, 10, err: -4).n("A", v: "3").u();
	//	t = t/**/	.N("S", 0, 12, err: -4).u();
	//	t = t/**/.n("S", 7, 8, ')', -1);
	//	t = t/**/	.H("M", 5, 7, "V", 2).u();
	//	t = t/**/.n("S", 8, 9, '4', -1);
	//	t = t/**/	.H("M", 4, 8, '*', 1).N("A", 2, 8, '+', 1).N("S", 0, 8, ',', 1).u();
	//	t = t/**/.n("S", 12, 12, null, -1);
	//	t = t/**/	.H("A", 10, 12, "M", 2).uU();
	//}

	//[TestMethod]
	//public void Recovery4()
	//{
	//	var s = new SynterStr("P=S+ \n S=V|{S+} =*|\n V=(N)|N =|\n N=0|1|2|3|4|5") {
	//		dump = 3
	//	};
	//	var t = s.Parse("{()");
	//	t = t/**/	.h("S").h("S").h("V");
	//	t = t/**/					.H("V", 1, 3, err: -4).uu();
	//	t = t/**/			.N("S", 0, 3, err: -4).uu();
	//	t = t/**/.n("P", 2, 3, ')', -1);
	//	t = t/**/	.H("V", 1, 2, "N", 1).u();
	//	t = t/**/.n("P", 3, 3, null, -1);
	//	t = t/**/	.H("S", 0, 3, '}', 2).N("S", 0, 3, "S", 1).uU();
	//	s.recover = 1;
	//	s.Parse("{{").Eq("P", 2, 2, null, -1).H("S", 1, 2, "S", 1).uU();
	//	s.recover = 2;
	//	t = s.Parse("{(");
	//	t = t/**/	.h("S").h("S").h("V");
	//	t = t/**/					.H("V", 1, 2, err: -4).uu();
	//	t = t/**/			.N("S", 0, 2, err: -4).uu();
	//	t = t/**/.n("P", 2, 2, null, -1);
	//	t = t/**/	.H("V", 1, 2, "N", 1).u();
	//	t = t/**/.n("P", 2, 2, null, -1);
	//	t = t/**/	.H("V", 1, 2, "N", 1).N("S", 0, 2, '}', 2).N("S", 0, 2, "S", 1).uU();
	//}

	//[TestMethod]
	//public void Recovery5()
	//{
	//	var s = new SynterStr("P=S+ \n S=V|{S+} =*|\n|\\0?\\0} =-| \n V=0|1|2|3|4|5") {
	//		dump = 3
	//	};
	//	var t = s.Parse("{{0}}}}{1}");
	//	t = t/**/	.h("S", 0, 5, "{{0}}").h("S", 1, 4, "{0}").u();
	//	t = t/**/	.N("S", 5, 6, err: -4).N("S", 6, 7, err: -4).n("S", 7, 10, "{1}").u();
	//	t = t/**/.n("P", 5, 6, '}', -1).n("P", 6, 7, '}', -1).U();
	//}
}
