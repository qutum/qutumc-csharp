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
			});
			IsFalse(p.Check("aa")); IsFalse(p.Check("aaa"));
			IsTrue(p.Check("aaaa")); IsTrue(p.Check("aaaaa")); IsTrue(p.Check("aaaaaa")); IsTrue(p.Check("aaaaaaa"));
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
		public void AddMul()
		{
			var p = new Earley<char>(new Dictionary<string, string[]>
			{
				{ "Start", new[]{ "Start + Mul", "Mul" } },
				{ "Mul",   new[]{ "Mul * Value", "Value" } },
				{ "Value", new[]{ "( Start )", "Num" } },
				{ "Num",   new[]{ "Num Digi", "Digi" } },
				{ "Digi",  new[]{ "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" } },
			})
			{ treeText = true };
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
			p.Parse("(7+58)*((23+882))*7+(5*23)+(15*33+89)*0").Dump();
		}
	}
}
