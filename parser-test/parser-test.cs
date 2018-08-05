//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser
{
	[TestClass]
	public class TestParser
	{
		public TestParser() { DebugWriter.ConsoleBegin(); }

		[TestMethod]
		public void Term()
		{
			var p = new ParserStr("S=\\u0061");
			IsTrue(p.Check("a"));
			IsFalse(p.Check("")); IsFalse(p.Check("aa")); IsFalse(p.Check("A"));
		}

		[TestMethod]
		public void Alt()
		{
			var p = new ParserStr("S=A\nA=a|1");
			IsTrue(p.Check("a")); IsTrue(p.Check("1"));
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
			IsTrue(p.Check(" s\tt\r\n\\/")); IsTrue(p.Check("|or=eq*s+p?q"));
		}

		[TestMethod]
		public void ErrHint()
		{
			var p = new ParserStr("S=A B =start \n A=1|2 =A12 ==oh||no\n |3 =A3 \n B= =empty \n |4 =B4") { treeDump = true };
			IsNull(p.Parse("").Dump().head);
			IsNull(p.Parse("4").Dump().head);
			AreEqual("empty", p.Parse("15").Dump().head.expect);
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
			var p = new ParserStr("S= aa B \nB= A a \nA= a A|a") { treeDump = true };
			IsFalse(p.Check("aa")); IsFalse(p.Check("aaa"));
			IsTrue(p.Check("aaaa")); IsTrue(p.Check("aaaaa")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(11, p.largest);
			p = new ParserStr("S= aa B \nB= A a \nA= A a|a") { treeDump = true };
			IsTrue(p.Check("aaaaaaa")); AreEqual(5, p.largest);
			p = new ParserStr("S= aa B \nB= A a \nA= a a+") { treeDump = true };
			IsTrue(p.Check("aaaaaaa")); AreEqual(5, p.largest);
		}

		[TestMethod]
		public void MidRecu()
		{
			var p = new ParserStr(@"
				S	= If|X
				If	= if \s S \s then \s S
					| if \s S \s then \s S \s else \s S
				X	= a|b|c|d|e|f|g|h|i|j");
			IsTrue(p.Check("if a then b")); IsFalse(p.Check("if a else b"));
			IsTrue(p.Check("if a then b else c")); IsFalse(p.Check("if a then b then c"));
			IsTrue(p.Check("if a then if b then c")); IsFalse(p.Check("if a then if b else c"));
			IsTrue(p.Check("if a then if b then c else d")); IsTrue(p.Check("if a then if b then c else d else e"));
			IsTrue(p.Check("if a then if b then c else if d then e else f else g"));
			IsTrue(p.Check("if a then if b then if c then d else e else if g then h else i else j"));
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
				Digi  = 0|1|2|3|4|5|6|7|8|9") { treeDump = true };
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
				Digi  = 0|1|2|3|4|5|6|7|8|9 = digit") { treeDump = true };
			var t = p.Parse("(1+2*").Dump();
			AreEqual("Mul", t.head.name); AreEqual(5, t.head.to); AreEqual(2, t.head.err);
			IsNull(t.head.next);
			t = p.Parse("(*1*2+3)*4").Dump();
			AreEqual("Value", t.head.name); AreEqual(1, t.head.to); AreEqual(1, t.head.err);
			IsNull(t.head.next);
			t = p.Parse("(1+2*3))*4").Dump();
			AreEqual("Mul", t.head.name); AreEqual(7, t.head.to); AreEqual(1, t.head.err);
			AreSame(t.tail, t.head.next);
			AreEqual("Expr", t.tail.name); AreEqual(7, t.tail.to); AreEqual(1, t.tail.err);
			t = p.Parse("(1*2+3").Dump();
			AreEqual("Mul", t.head.name); AreEqual(6, t.head.to); AreEqual(1, t.head.err);
			AreEqual("Value", t.head.next.name); AreEqual(6, t.head.next.to); AreEqual(2, t.head.next.err);
			AreSame(t.tail.prev, t.head.next.next);
			AreEqual("Expr", t.tail.prev.name); AreEqual(6, t.tail.prev.to); AreEqual(1, t.tail.prev.err);
			AreEqual("Num", t.tail.name); AreEqual(6, t.tail.to); AreEqual(1, t.tail.err);
			t = p.Parse("(1*2+)").Dump();
			AreEqual("Expr", t.head.name); AreEqual(5, t.head.to); AreEqual(2, t.head.err);
			IsNull(t.head.next);
			t = p.Parse("()").Dump();
			AreEqual("Value", t.head.name); AreEqual(1, t.head.to); AreEqual(1, t.head.err);
			IsNull(t.head.next);
		}

		[TestMethod]
		public void Greedy1()
		{
			var p = new ParserStr("S = A B \n A = A 1|1 \n B = 1|B 1") { treeDump = true };
			AreEqual(1, p.Parse("111").Dump().head.to);
			p.greedy = true;
			AreEqual(2, p.Parse("111").Dump().head.to);
		}

		[TestMethod]
		public void Greedy2()
		{
			var p = new ParserStr("S =A B C D \nA =1|12 \nB =234|3 \nC =5|456 \nD =67|7") { treeDump = true };
			AreEqual(1, p.Parse("1234567").Dump().head.to);
			p.greedy = true;
			AreEqual(2, p.Parse("1234567").Dump().head.to);
		}

		[TestMethod]
		public void GreedyHint()
		{
			var p = new ParserStr("S = A B =*\n A = A 1|1 \n B = 1|B 1") { treeDump = true };
			AreEqual(2, p.Parse("111").Dump().head.to);
			p.greedy = true;
			AreEqual(2, p.Parse("111").Dump().head.to);
		}

		[TestMethod]
		public void Empty1()
		{
			var p = new ParserStr("S=a A B \n A=|a+ \n B=A") { greedy = true, treeDump = true };
			IsTrue(p.Check("a")); IsTrue(p.Check("aa"));
			var t = p.Parse("aaa").Dump();
			AreEqual(0, t.err); AreEqual(1, t.head.from); AreEqual(3, t.head.to);
			AreEqual(3, t.tail.from); AreEqual(3, t.tail.to);
		}

		[TestMethod]
		public void Empty2()
		{
			var p = new ParserStr("S=A B \n A=a P \n B=P b \n P=|pq") { treeDump = true };
			IsTrue(p.Check("ab")); IsTrue(p.Check("apqb")); IsTrue(p.Check("apqpqb")); IsFalse(p.Check("apqpqpqb"));
		}

		[TestMethod]
		public void Option1()
		{
			var p = new ParserStr("S=A B?a? \n A=a|aa \n B=a") { treeDump = true };
			IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaa"));
			var t = p.Parse("aa").Dump();
			AreEqual(0, t.err); AreEqual(1, t.head.to);
			t = p.Parse("aaa").Dump();
			AreEqual(0, t.err); AreEqual(1, t.head.to); AreEqual(2, t.tail.to);
			p.greedy = true;
			t = p.Parse("aaa").Dump();
			AreEqual(0, t.err); AreEqual(2, t.head.to); AreEqual(3, t.tail.to);
		}

		[TestMethod]
		public void Option2()
		{
			var p = new ParserStr("S=A?B? \n A=B a \n B=b");
			IsTrue(p.Check("")); IsTrue(p.Check("ba")); IsTrue(p.Check("b"));
			IsTrue(p.Check("bab")); IsFalse(p.Check("ab"));
		}

		[TestMethod]
		public void Plus1()
		{
			var p = new ParserStr("S=a+") { treeDump = true };
			IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(2, p.largest);
		}

		[TestMethod]
		public void Plus2()
		{
			var p = new ParserStr("S=A B \n A=a P+ \n B=P+b \n P=pq") { treeDump = true };
			IsFalse(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
			var t = p.Parse("apqpqpqb").Dump();
			AreEqual(0, t.err); AreEqual(10, p.largest);
			AreEqual(1, t.head.head.from); AreEqual(3, t.head.head.to);
			AreEqual(3, t.tail.head.from); AreEqual(5, t.tail.head.to);
			AreEqual(5, t.tail.tail.from); AreEqual(7, t.tail.tail.to);
			p.greedy = true;
			t = p.Parse("apqpqpqb").Dump();
			AreEqual(3, t.head.tail.from); AreEqual(5, t.head.tail.to);
		}

		[TestMethod]
		public void Star1()
		{
			var p = new ParserStr("S=a*") { treeDump = true };
			IsTrue(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(2, p.largest);
		}

		[TestMethod]
		public void Star2()
		{
			var p = new ParserStr("S=A B \n A=a P* \n B=P* b \n P=p|q") { treeDump = true };
			IsTrue(p.Check("ab")); IsTrue(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
			var t = p.Parse("apqpqpqb").Dump();
			AreEqual(0, t.err); AreEqual(20, p.largest);
			AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(1, t.tail.head.from); AreEqual(2, t.tail.head.to);
			AreEqual(6, t.tail.tail.from); AreEqual(7, t.tail.tail.to);
			p.greedy = true;
			t = p.Parse("apqpqpqb").Dump();
			AreEqual(0, t.err); AreEqual(20, p.largest);
			AreEqual(1, t.head.head.from); AreEqual(2, t.head.head.to);
			AreEqual(6, t.head.tail.from); AreEqual(7, t.head.tail.to);
		}

		[TestMethod]
		public void KeepHint()
		{
			var p = new ParserStr("S=A B C\n A=1 =+\n B=1 =-\n C=1") { treeKeep = true, treeDump = true };
			var t = p.Parse("111").Dump();
			AreEqual("A", t.head.name); AreEqual("C", t.head.next.name);
			p.treeKeep = false;
			t = p.Parse("111").Dump();
			AreEqual("A", t.head.name); AreEqual(null, t.head.next);
		}

		[TestMethod]
		public void Recovery1()
		{
			var p = new ParserStr("|=,\n S=A|S,A? \n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") { treeDump = true };
			var t = p.Parse("0").Dump(); AreEqual(0, t.err);
			t = p.Parse("0+").Dump();
			AreEqual(0, t.head.err); AreEqual("S", t.head.name); AreEqual("A", t.head.head.name);
			AreEqual(-1, t.tail.prev.err); AreEqual(1, t.tail.err);
			AreEqual(2, t.tail.head.err); AreEqual("A", t.tail.head.name); AreEqual("M", t.tail.head.expect);
			t = p.Parse("0*").Dump();
			AreEqual(0, t.head.err); AreEqual("S", t.head.name); AreEqual("A", t.head.head.name);
			AreEqual(-1, t.tail.prev.err); AreEqual(1, t.tail.err);
			AreEqual(2, t.tail.head.err); AreEqual("M", t.tail.head.name); AreEqual("V", t.tail.head.expect);
		}

		[TestMethod]
		public void Recovery2()
		{
			var p = new ParserStr("|=,\n S=A|S,A? \n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") { treeDump = true };
			var t = p.Parse("+").Dump(); AreEqual(1, t.err);
			t = p.Parse("+0").Dump(); AreEqual(1, t.err);
			t = p.Parse("0#").Dump(); AreEqual(0, t.err);
			AreEqual(0, t.head.err); AreEqual("S", t.head.name); AreEqual("A", t.head.head.name);
			AreEqual(-1, t.tail.prev.err); AreEqual(1, t.tail.err);
			AreEqual("M", t.tail.head.name); AreEqual('*', t.tail.head.expect);
			t = p.Parse("0+1*2+").Dump(); AreEqual(0, t.err);
			AreEqual(0, t.head.err); AreEqual("S", t.head.name); AreEqual(5, t.head.head.to);
			AreEqual(-1, t.tail.prev.err); AreEqual(1, t.tail.err);
			AreEqual("A", t.tail.head.name); AreEqual("M", t.tail.head.expect);
		}

		[TestMethod]
		public void Recovery3()
		{
			var p = new ParserStr("|=,\n S=A|S,A? \n A=M|A\\+M \n M=V|M\\*V \n V=0|1|2|3|4|5") { treeDump = true };
			var t = p.Parse("0+1*2+,1").Dump(); AreEqual(0, t.err);
			AreEqual(0, t.head.err); AreEqual("S", t.head.name); AreEqual(5, t.head.head.to);
			AreEqual(-1, t.head.next.err); AreEqual(7, t.head.next.to);
			AreEqual(0, t.tail.prev.err); AreEqual(1, t.tail.err);
			AreEqual("A", t.tail.head.name); AreEqual("M", t.tail.head.expect); AreEqual(6, t.tail.head.to);
		}

		[TestMethod]
		public void Recovery4()
		{
			var p = new ParserStr("|=),\n S=A|S,A? \n A=M|A\\+M \n M=V|M\\*V \n V=(A)|N \n N=0|1|2|3|4|5")
			{ treeDump = true };
			var t = p.Parse("()").Dump(); AreEqual(0, t.err);
			AreEqual(0, t.head.err); AreEqual("A", t.head.name); AreEqual(-1, t.head.head.head.head.err);
			AreEqual(0, t.tail.prev.err); AreEqual(1, t.tail.err);
			AreEqual(1, t.tail.from); AreEqual("V", t.tail.head.name); AreEqual(1, t.tail.head.err);
			t = p.Parse("0+(),1").Dump(); AreEqual(0, t.err);
			AreEqual(0, t.head.head.head.err); AreEqual(1, t.head.head.head.to);
			AreEqual(0, t.head.head.tail.err); AreEqual(4, t.head.head.tail.to);
			AreEqual(-1, t.head.head.tail.head.head.err); AreEqual(4, t.head.head.tail.head.head.to);
			AreEqual(0, t.tail.prev.err); AreEqual(1, t.tail.err);
			AreEqual(3, t.tail.from); AreEqual("V", t.tail.head.name); AreEqual(1, t.tail.head.err);
			t = p.Parse("0,1+(1*),2+3").Dump(); AreEqual(0, t.err);
			AreEqual(8, t.head.to); AreEqual(2, t.head.tail.from); AreEqual(8, t.head.tail.tail.to);
			AreEqual(0, t.head.tail.tail.head.head.err); AreEqual(6, t.head.tail.tail.head.head.to);
			AreEqual(-1, t.head.tail.tail.head.tail.err);
			AreEqual(9, t.head.next.from); AreEqual(12, t.head.next.tail.to);
			AreEqual(0, t.tail.prev.err); AreEqual(2, t.tail.head.err);
			AreEqual(7, t.tail.from); AreEqual(7, t.tail.head.to); AreEqual("M", t.tail.head.name);
		}

		[TestMethod]
		public void Recovery5()
		{
			var p = new ParserStr("|=)}\n P=S+ \n S={A}|{S+} \n A=V|A\\+V \n V=(A)|N \n N=0|1|2|3|4|5")
			{ treeDump = true };
			var t = p.Parse("{()").Dump(); AreEqual(0, t.err);
			AreEqual(0, t.head.err); AreEqual(1, t.head.head.from); AreEqual(-1, t.head.head.head.head.err);
			AreEqual(1, t.tail.prev.head.err); AreEqual(2, t.tail.prev.from); AreEqual("V", t.tail.prev.head.name);
			AreEqual(2, t.tail.head.err); AreEqual(3, t.tail.from); AreEqual("S", t.tail.head.name);
			p.recovery = 1;
			t = p.Parse("{{").Dump(); AreEqual(1, t.err);
			p.recovery = 3;
			t = p.Parse("{{0}{").Dump(); AreEqual(0, t.err);
			AreEqual(0, t.head.head.err); AreEqual(1, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(0, t.head.head.next.err); AreEqual(5, t.head.head.next.to);
			AreEqual(-1, t.head.tail.err); AreEqual(5, t.head.tail.to);
			AreEqual(1, t.tail.prev.tail.err); AreEqual(4, t.tail.prev.tail.from); AreEqual("S", t.tail.prev.tail.name);
			AreEqual(2, t.tail.tail.prev.err); AreEqual(0, t.tail.tail.prev.from);
			t = p.Parse("{{0}}}{1}").Dump(); AreEqual(0, t.err);
			AreEqual(0, t.head.err); AreEqual(6, t.head.to);
			AreEqual(1, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(-1, t.head.tail.err); AreEqual(6, t.head.tail.to);
			AreEqual(0, t.head.next.err); AreEqual(6, t.head.next.from); AreEqual(9, t.head.next.to);
			AreEqual(0, t.tail.prev.err); AreEqual(1, t.tail.err);
			AreEqual(5, t.tail.from); AreEqual(6, t.tail.to); IsNull(t.tail.head);
		}
	}
}
