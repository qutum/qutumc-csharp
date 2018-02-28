using Microsoft.VisualStudio.TestTools.UnitTesting;
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
			var p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "a" } },
			});
			IsTrue(p.Check("a"));
			IsFalse(p.Check("")); IsFalse(p.Check("aa")); IsFalse(p.Check("A"));
		}

		[TestMethod]
		public void Alt()
		{
			var p = new Earley<char>(new Dictionary<string, string[]>
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
			var p = new Earley<char>(new Dictionary<string, string[]>
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
			var p = new Earley<char>(new Dictionary<string, string[]>
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
			var p = new Earley<char>(new Dictionary<string, string[]>
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
			var p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "aa B" } },
				{ "B",     new[]{ "A a" } },
				{ "A",     new[]{ "a A", "a" } },
			})
			{ treeText = true };
			IsFalse(p.Check("aa")); IsFalse(p.Check("aaa"));
			IsTrue(p.Check("aaaa")); IsTrue(p.Check("aaaaa")); IsTrue(p.Check("aaaaaa"));
			AreEqual(p.Parse("aaaaaaa").Dump().err, 0); AreEqual(p.largest, 11);
			p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "aa B" } },
				{ "B",     new[]{ "A a" } },
				{ "A",     new[]{ "A a", "a" } },
			})
			{ treeText = true };
			AreEqual(p.Parse("aaaaaaa").Dump().err, 0); AreEqual(p.largest, 5);
		}

		[TestMethod]
		public void MidRecu()
		{
			var p = new Earley<char>(new Dictionary<string, string[]>
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
			var p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "Start Start", "a" } },
			});
			IsFalse(p.Check(""));
			IsTrue(p.Check("a")); IsTrue(p.Check("aa")); IsTrue(p.Check("aaa")); IsTrue(p.Check("aaaa"));
		}

		[TestMethod]
		public void Greedy()
		{
			var p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B" } },
				{ "A",    new[]{ "A 1", "1" } },
				{ "B",    new[]{ "1", "B 1" } },
			})
			{ treeText = true };
			AreEqual(p.Parse("111").Dump().head.to, 2);
			p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B" } },
				{ "A",    new[]{ "A 1", "1" } },
				{ "B",    new[]{ "1", "B 1" } },
			})
			{ treeGreedy = false, treeText = true };
			AreEqual(p.Parse("111").Dump().head.to, 1);
		}


		[TestMethod]
		public void RepPlus1()
		{
			var p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "a~" } },
			})
			{ treeText = true };
			IsFalse(p.Check("")); IsTrue(p.Check("a")); IsTrue(p.Check("aaaaaa"));
			IsTrue(p.Check("aaaaaaa")); AreEqual(p.largest, 2);
		}

		[TestMethod]
		public void RepPlus2()
		{
			var p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "A B" } },
				{ "A",     new[]{ "a P ~" } },
				{ "B",     new[]{ "P ~ b" } },
				{ "P",     new[]{ "pq" } },
			})
			{ treeText = true };
			IsFalse(p.Check("apqb")); IsTrue(p.Check("apqpqb"));
			var t = p.Parse("apqpqpqb").Dump();
			AreEqual(t.err, 0); AreEqual(p.largest, 10);
			AreEqual(t.head.head.from, 1); AreEqual(t.head.head.to, 3);
			AreEqual(t.head.tail.from, 3); AreEqual(t.head.tail.to, 5);
			AreEqual(t.tail.head.from, 5); AreEqual(t.tail.head.to, 7);
		}
		[TestMethod]
		public void AddMul()
		{
			var p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Expr", new[]{ "Expr + Mul", "Mul" } },
				{ "Mul",   new[]{ "Mul * Value", "Value" } },
				{ "Value", new[]{ "( Expr )", "Num" } },
				{ "Num",   new[]{ "Num Digi", "Digi" } },
				{ "Digi",  new[]{ "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" } },
			})
			{ start = "Expr", treeText = true };
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
			var e = p.Parse("").Dump(); IsNull(e.head);
			e = p.Parse("(1+2*").Dump();
			AreEqual(e.head.name, "Mul"); AreEqual(e.head.to, 5); AreEqual(e.head.err, 2);
			e = p.Parse("(*1*2+3)*4").Dump();
			AreEqual(e.head.name, "Value"); AreEqual(e.head.to, 1); AreEqual(e.head.err, 1);
			e = p.Parse("(1+2*3))*4").Dump();
			AreEqual(e.head.name, "Mul"); AreEqual(e.head.to, 7); AreEqual(e.head.err, 1);
			AreEqual(e.head.next, e.tail);
			AreEqual(e.tail.name, "Expr"); AreEqual(e.tail.to, 7); AreEqual(e.tail.err, 1);
			e = p.Parse("(1*2+3").Dump();
			AreEqual(e.head.name, "Mul"); AreEqual(e.head.to, 6); AreEqual(e.head.err, 1);
			AreEqual(e.head.next.name, "Value"); AreEqual(e.head.next.to, 6); AreEqual(e.head.next.err, 2);
			AreEqual(e.head.next.next, e.tail.prev);
			AreEqual(e.tail.prev.name, "Expr"); AreEqual(e.tail.prev.to, 6); AreEqual(e.tail.prev.err, 1);
			AreEqual(e.tail.name, "Num"); AreEqual(e.tail.to, 6); AreEqual(e.tail.err, 1);
		}
	}
}
