using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System.Collections.Generic;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test
{
	[TestClass]
	public class TestParser
	{
		public TestParser()
		{
			DebugWriter.ConsoleBegin();
		}

		[TestMethod]
		public void Term()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "a" } },
			});
			IsTrue(p.Check("a"));
			IsFalse(p.Check("")); IsFalse(p.Check("aa")); IsFalse(p.Check("A"));
		}

		[TestMethod]
		public void Alt()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A" } },
				{ "A",     new[]{ "a", "1" } },
			});
			IsTrue(p.Check("a")); IsTrue(p.Check("1"));
			IsFalse(p.Check("")); IsFalse(p.Check("a1")); IsFalse(p.Check("A"));
		}

		[TestMethod]
		public void Con()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B", "B A" } },
				{ "A",     new[]{ "a", "1" } },
				{ "B",     new[]{ "b", "2" } },
			});
			IsFalse(p.Check("a")); IsFalse(p.Check("1")); IsFalse(p.Check("b")); IsFalse(p.Check("2"));
			IsTrue(p.Check("ab")); IsTrue(p.Check("ba"));
			IsTrue(p.Check("1b")); IsTrue(p.Check("b1"));
			IsTrue(p.Check("a2")); IsTrue(p.Check("2a"));
			IsTrue(p.Check("12")); IsTrue(p.Check("21"));
			IsFalse(p.Check("aba")); IsFalse(p.Check("12ab"));
		}

		[TestMethod]
		public void LeftRecu()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "Start s", "A" } },
				{ "A",     new[]{ "a" } },
			});
			IsTrue(p.Check("a")); IsTrue(p.Check("as")); IsTrue(p.Check("ass")); IsTrue(p.Check("asss"));
			IsFalse(p.Check("")); IsFalse(p.Check("s")); IsFalse(p.Check("aas")); IsFalse(p.Check("asssa"));
		}

		[TestMethod]
		public void RightRecu()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "a Start", "S" } },
				{ "S",     new[]{ "s" } },
			});
			IsTrue(p.Check("s")); IsTrue(p.Check("as")); IsTrue(p.Check("aas")); IsTrue(p.Check("aaaas"));
			IsFalse(p.Check("")); IsFalse(p.Check("a")); IsFalse(p.Check("ass")); IsFalse(p.Check("asssa"));
		}

		[TestMethod]
		public void RightRecuUnopt()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "aa B" } },
				{ "B",     new[]{ "A a" } },
				{ "A",     new[]{ "a A", "a" } },
			})
			{ treeDump = true };
			IsFalse(p.Check("aa")); IsFalse(p.Check("aaa"));
			IsTrue(p.Check("aaaa")); IsTrue(p.Check("aaaaa")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(11, p.largest);
			p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "aa B" } },
				{ "B",     new[]{ "A a" } },
				{ "A",     new[]{ "A a", "a" } },
			})
			{ treeDump = true };
			IsTrue(p.Check("aaaaaaa")); AreEqual(5, p.largest);
			p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "aa B" } },
				{ "B",     new[]{ "A a" } },
				{ "A",     new[]{ "a a~" } },
			})
			{ treeDump = true };
			IsTrue(p.Check("aaaaaaa")); AreEqual(5, p.largest);
		}

		[TestMethod]
		public void MidRecu()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "If", "X" } },
				{ "If",    new[]{ "if . Start . then . Start", "if . Start . then . Start . else . Start" } },
				{ "X",     new[]{ "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" } },
			});
			IsTrue(p.Check("if.a.then.b")); IsFalse(p.Check("if.a.else.b"));
			IsTrue(p.Check("if.a.then.b.else.c")); IsFalse(p.Check("if.a.then.b.then.c"));
			IsTrue(p.Check("if.a.then.if.b.then.c")); IsFalse(p.Check("if.a.then.if.b.else.c"));
			IsTrue(p.Check("if.a.then.if.b.then.c.else.d")); IsTrue(p.Check("if.a.then.if.b.then.c.else.d.else.e"));
			IsTrue(p.Check("if.a.then.if.b.then.c.else.if.d.then.e.else.f.else.g"));
			IsTrue(p.Check("if.a.then.if.b.then.if.c.then.d.else.e.else.if.g.then.h.else.i.else.j"));
		}

		[TestMethod]
		public void DoubleRecu()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "Start Start", "a" } },
			});
			IsFalse(p.Check(""));
			IsTrue(p.Check("a")); IsTrue(p.Check("aa")); IsTrue(p.Check("aaa")); IsTrue(p.Check("aaaa"));
		}

		[TestMethod]
		public void AddMul()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Expr", new[]{ "Expr + Mul", "Mul" } },
				{ "Mul",   new[]{ "Mul * Value", "Value" } },
				{ "Value", new[]{ "( Expr )", "Num" } },
				{ "Num",   new[]{ "Num Digi", "Digi" } },
				{ "Digi",  new[]{ "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" } },
			}, "Expr")
			{ treeDump = true };
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
			var t = p.Parse("").Dump(); IsNull(t.head);
			t = p.Parse("(1+2*").Dump();
			AreEqual("Mul", t.head.name); AreEqual(5, t.head.to); AreEqual(2, t.head.err);
			t = p.Parse("(*1*2+3)*4").Dump();
			AreEqual("Value", t.head.name); AreEqual(1, t.head.to); AreEqual(1, t.head.err);
			t = p.Parse("(1+2*3))*4").Dump();
			AreEqual("Mul", t.head.name); AreEqual(7, t.head.to); AreEqual(1, t.head.err);
			AreEqual(t.tail, t.head.next);
			AreEqual("Expr", t.tail.name); AreEqual(7, t.tail.to); AreEqual(1, t.tail.err);
			t = p.Parse("(1*2+3").Dump();
			AreEqual("Mul", t.head.name); AreEqual(6, t.head.to); AreEqual(1, t.head.err);
			AreEqual("Value", t.head.next.name); AreEqual(6, t.head.next.to); AreEqual(2, t.head.next.err);
			AreEqual(t.tail.prev, t.head.next.next);
			AreEqual("Expr", t.tail.prev.name); AreEqual(6, t.tail.prev.to); AreEqual(1, t.tail.prev.err);
			AreEqual("Num", t.tail.name); AreEqual(6, t.tail.to); AreEqual(1, t.tail.err);
		}

		[TestMethod]
		public void Greedy()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B" } },
				{ "A",     new[]{ "A 1", "1" } },
				{ "B",     new[]{ "1", "B 1" } },
			})
			{ treeDump = true };
			AreEqual(2, p.Parse("111").Dump().head.to);
			p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B" } },
				{ "A",     new[]{ "A 1", "1" } },
				{ "B",     new[]{ "1", "B 1" } },
			})
			{ greedy = false, treeDump = true };
			AreEqual(1, p.Parse("111").Dump().head.to);
		}

		[TestMethod]
		public void RepPlus1()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "a~" } },
			})
			{ treeDump = true };
			IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(2, p.largest);
		}

		[TestMethod]
		public void RepPlus2()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B" } },
				{ "A",     new[]{ "a P ~" } },
				{ "B",     new[]{ "P ~ b" } },
				{ "P",     new[]{ "pq" } },
			})
			{ treeDump = true };
			IsFalse(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
			var t = p.Parse("apqpqpqb").Dump();
			AreEqual(0, t.err); AreEqual(10, p.largest);
			AreEqual(1, t.head.head.from); AreEqual(3, t.head.head.to);
			AreEqual(3, t.head.tail.from); AreEqual(5, t.head.tail.to);
			AreEqual(5, t.tail.head.from); AreEqual(7, t.tail.head.to);
		}

		[TestMethod]
		public void Empty1()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "a A B" } },
				{ "A",     new[]{ "", "a~" } },
				{ "B",     new[]{ "A" } },
			})
			{ greedy = true, treeDump = true };
			IsTrue(p.Check("a")); IsTrue(p.Check("aa"));
			var t = p.Parse("aaa").Dump();
			AreEqual(0, t.err); AreEqual(1, t.head.from); AreEqual(3, t.head.to);
			AreEqual(3, t.tail.from); AreEqual(3, t.tail.to);
		}

		[TestMethod]
		public void Empty2()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B" } },
				{ "A",     new[]{ "a P" } },
				{ "B",     new[]{ "P b" } },
				{ "P",     new[]{ "", "pq" } },
			})
			{ treeDump = true };
			IsTrue(p.Check("ab")); IsTrue(p.Check("apqb")); IsTrue(p.Check("apqpqb")); IsFalse(p.Check("apqpqpqb"));
		}

		[TestMethod]
		public void Option1()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B ? a?" } },
				{ "A",     new[]{ "a", "aa" } },
				{ "B",     new[]{ "a" } },
			})
			{ treeDump = true };
			IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaa"));
			var t = p.Parse("aa").Dump();
			AreEqual(0, t.err); AreEqual(2, t.head.to);
			t = p.Parse("aaa").Dump();
			AreEqual(0, t.err); AreEqual(2, t.head.to); AreEqual(3, t.tail.to);
		}

		[TestMethod]
		public void Option2()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A ? B ?" } },
				{ "A",     new[]{ "B a" } },
				{ "B",     new[]{ "b" } },
			});
			IsTrue(p.Check("")); IsTrue(p.Check("ba")); IsTrue(p.Check("b"));
			IsTrue(p.Check("bab")); IsFalse(p.Check("ab"));
		}

		[TestMethod]
		public void RepStar1()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "a#" } },
			})
			{ treeDump = true };
			IsTrue(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(2, p.largest);
		}

		[TestMethod]
		public void RepStar2()
		{
			var p = new ParserStr(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B" } },
				{ "A",     new[]{ "a P #" } },
				{ "B",     new[]{ "P # b" } },
				{ "P",     new[]{ "p", "q" } },
			})
			{ treeDump = true };
			IsTrue(p.Check("ab")); IsTrue(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
			var t = p.Parse("apqpqpqb").Dump();
			AreEqual(0, t.err); AreEqual(20, p.largest);
			AreEqual(1, t.head.head.from); AreEqual(2, t.head.head.to);
			AreEqual(6, t.head.tail.from); AreEqual(7, t.head.tail.to);
			AreEqual(7, t.tail.from); AreEqual(8, t.tail.to);
		}
	}
}
