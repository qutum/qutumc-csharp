//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using qutum.syntax;
using System;
using System.Linq;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax
{
	using Trees = Tree<ArraySegment<Token<Lex>>>;

	[TestClass]
	public class TestParser
	{
		public TestParser()
		{
			DebugWriter.ConsoleBegin();
			l = new Lexer();
			ps = new Parsers(l) { treeDump = true };
		}

		Lexer l;
		Parsers ps;

		Trees Parses(string input, bool ok)
		{
			Console.WriteLine(input);
			byte[] bs = Encoding.UTF8.GetBytes(input);
			ps.treeDumper = o => !(o is ArraySegment<Token<Lex>> s) ? null : s.Count == 0 ? ""
				: string.Join(" ", s.Select(k => k.Dump()).ToArray());
			var t = ps.Parse(bs);
			Simple(t); t.Dump();
			if (ok != (t.err == 0 && (t.tail == null || t.tail.err == 0)))
				Fail(ok ? "error" : "no error");
			return t;
		}

		void Simple(Trees t)
		{
			if (t.err > 0) return;
			if (t.err < 0 || t.name == "empty") { t.Remove(false); return; }
			if (t.name == "line")
			{
				t.name = t.head.name; t.from = t.head.from; t.to = t.head.to;
				t.tokens = t.head.tokens; t.err = t.head.err; t.expect = t.head.expect;
				t.dump = t.head.dump;
				t.head.Remove();
			}
			for (var x = t.head; x != null; x = x.next)
				Simple(x);
		}

		[TestMethod]
		public void Eof()
		{
			var t = Parses("a\nb", false);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(Lex.Eol, t.tail.tail.expect);
			t = Parses("a\n\tb", false);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(Lex.Eol, t.tail.tail.expect);
		}

		[TestMethod]
		public void Inner1()
		{
			var t = Parses("a\n\taa\n\tab\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(3, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(5, t.head.tail.from); AreEqual(6, t.head.tail.to);
		}

		[TestMethod]
		public void Inner2()
		{
			var t = Parses("a\n\taa\n        aaa\n\tab\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(3, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(0, t.head.head.head.err); AreEqual(6, t.head.head.head.from); AreEqual(7, t.head.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(9, t.head.tail.from); AreEqual(10, t.head.tail.to);
		}

		[TestMethod]
		public void Inner3()
		{
			var t = Parses("a\n\taa\n        aaa\n\tab\nb\n\tbb\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(3, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(0, t.head.head.head.err); AreEqual(6, t.head.head.head.from); AreEqual(7, t.head.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(9, t.head.tail.from); AreEqual(10, t.head.tail.to);
			AreEqual(0, t.tail.err); AreEqual(12, t.tail.from); AreEqual(13, t.tail.to);
			AreEqual(0, t.tail.head.err); AreEqual(15, t.tail.head.from); AreEqual(16, t.tail.head.to);
		}

		[TestMethod]
		public void Inner4()
		{
			var t = Parses("\ta\nb\n", false);
			AreEqual(0, t.head.err); AreEqual(4, t.head.from); AreEqual(5, t.head.to);
			AreEqual(Lex.Eol, t.tail.head.expect); AreEqual(1, t.tail.head.to);
			t = Parses("##\n\ta\nb\n", false);
			AreEqual(0, t.head.err); AreEqual(6, t.head.from); AreEqual(7, t.head.to);
			AreEqual(Lex.Eol, t.tail.head.expect); AreEqual(3, t.tail.head.to);
			t = Parses("##\n\t##\n\ta\nb\n", false);
			AreEqual(0, t.head.err); AreEqual(8, t.head.from); AreEqual(9, t.head.to);
			AreEqual(Lex.Eol, t.tail.head.next.expect); AreEqual(5, t.tail.head.next.to);
			t = Parses("\t##\n##\n\ta\nb\n", false);
			AreEqual(0, t.head.err); AreEqual(10, t.head.from); AreEqual(11, t.head.to);
			AreEqual(Lex.Eol, t.tail.head.next.expect); AreEqual(7, t.tail.head.next.to);
		}

		[TestMethod]
		public void Inner5()
		{
			var t = Parses("a\n\t\taa\n\tab\n", false);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(7, t.head.head.from); AreEqual(8, t.head.head.to);
			AreEqual(Lex.Eol, t.tail.head.expect); AreEqual(4, t.tail.head.to);
			t = Parses("a\n##\n\n\t\taa\n\tab\n", false);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(10, t.head.head.from); AreEqual(11, t.head.head.to);
			AreEqual(Lex.Eol, t.tail.head.expect); AreEqual(7, t.tail.head.to);
		}

		[TestMethod]
		public void Empty1()
		{
			var t = Parses("\n\na\n", true);
			AreEqual(0, t.head.err); AreEqual(2, t.head.from); AreEqual(3, t.head.to);
			t = Parses("\n\na\n\n\nb\n", true);
			AreEqual(0, t.head.err); AreEqual(2, t.head.from); AreEqual(3, t.head.to);
			AreEqual(0, t.tail.err); AreEqual(6, t.tail.from); AreEqual(7, t.tail.to);
		}

		[TestMethod]
		public void Empty2()
		{
			var t = Parses("a\n\n\taa\n\tab\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(4, t.head.head.from); AreEqual(5, t.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(6, t.head.tail.from); AreEqual(7, t.head.tail.to);
		}

		[TestMethod]
		public void Empty3()
		{
			var t = Parses("a\n\taa\n\t\n\tab\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(3, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(6, t.head.tail.from); AreEqual(7, t.head.tail.to);
		}

		[TestMethod]
		public void Empty4()
		{
			var t = Parses("a\n\taa\n\n\tab\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(3, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(8, t.head.tail.from); AreEqual(9, t.head.tail.to);
		}

		[TestMethod]
		public void Empty5()
		{
			var t = Parses("a\n\n\n\n\taa\n\n\n\n\tab\n", false);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(6, t.head.head.from); AreEqual(7, t.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(13, t.head.tail.from); AreEqual(14, t.head.tail.to);
			AreEqual("empty", t.tail.prev.name); AreEqual(3, t.tail.prev.from); AreEqual(4, t.tail.prev.to);
			AreEqual("empty", t.tail.name); AreEqual(10, t.tail.from); AreEqual(11, t.tail.to);
		}

		[TestMethod]
		public void Empty6()
		{
			var t = Parses("a\n\taa\n\t\n\t\taaa\n\t\t\t\n\tab\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(3, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(0, t.head.head.head.err); AreEqual(7, t.head.head.head.from); AreEqual(8, t.head.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(13, t.head.tail.from); AreEqual(14, t.head.tail.to);
		}

		[TestMethod]
		public void Empty7()
		{
			var t = Parses("a\n\taa\n\n\n\t\taaa\n\n\n\tab\n", false);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(3, t.head.head.from); AreEqual(4, t.head.head.to);
			AreEqual(0, t.head.head.head.err); AreEqual(10, t.head.head.head.from); AreEqual(11, t.head.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(17, t.head.tail.from); AreEqual(18, t.head.tail.to);
			AreEqual("empty", t.tail.prev.name); AreEqual(7, t.tail.prev.from); AreEqual(8, t.tail.prev.to);
			AreEqual("empty", t.tail.name); AreEqual(15, t.tail.from); AreEqual(16, t.tail.to);
		}

		[TestMethod]
		public void Comm1()
		{
			var t = Parses("##\n\n\na\n", true);
			AreEqual(0, t.head.err); AreEqual(4, t.head.from); AreEqual(5, t.head.to);
			t = Parses("\n\n##\na\n\\##\n##\\ \n\nb\n\n##\nc\n", true);
			AreEqual(0, t.head.err); AreEqual(4, t.head.from); AreEqual(5, t.head.to);
			AreEqual(0, t.head.next.err); AreEqual(10, t.head.next.from); AreEqual(11, t.head.next.to);
			AreEqual(0, t.tail.err); AreEqual(15, t.tail.from); AreEqual(16, t.tail.to);
		}

		[TestMethod]
		public void Comm2()
		{
			var t = Parses("\t##\na\n    \\##\n##\\ \n\n\n\nb\n\n##\nc\n", true);
			AreEqual(0, t.head.err); AreEqual(4, t.head.from); AreEqual(5, t.head.to);
			AreEqual(0, t.head.next.err); AreEqual(14, t.head.next.from); AreEqual(15, t.head.next.to);
			AreEqual(0, t.tail.err); AreEqual(19, t.tail.from); AreEqual(20, t.tail.to);
		}

		[TestMethod]
		public void Comm3()
		{
			var t = Parses("a\n##\n\n\tb\n\n##\n\tc\n\t\t##\n\t\t\t\nd\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(6, t.head.head.from); AreEqual(7, t.head.head.to);
			AreEqual(0, t.head.tail.err); AreEqual(13, t.head.tail.from); AreEqual(14, t.head.tail.to);
			AreEqual(0, t.tail.err); AreEqual(23, t.tail.from); AreEqual(24, t.tail.to);
		}

		[TestMethod]
		public void Comm4()
		{
			var t = Parses("a\n\n##\n\n\tb\n\n\t\t  ##\n\t\tc\n  \\##\n..##\\  \\####\\\n\n\t\td\n", false);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(1, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(7, t.head.head.from); AreEqual(8, t.head.head.to);
			AreEqual(0, t.head.head.head.err); AreEqual(17, t.head.head.head.from); AreEqual(18, t.head.head.head.to);
			AreEqual(0, t.head.head.tail.err); AreEqual(30, t.head.head.tail.from); AreEqual(31, t.head.head.tail.to);
			AreEqual("empty", t.head.next.name); AreEqual(5, t.head.next.from); AreEqual(6, t.head.next.to);
			AreEqual("_", t.tail.prev.name); AreEqual(13, t.tail.prev.from); AreEqual(15, t.tail.prev.to);
			AreEqual("_", t.tail.name); AreEqual(23, t.tail.from); AreEqual(24, t.tail.to);
		}

		[TestMethod]
		public void Comm5()
		{
			var t = Parses("\\##\\##2##\\a\n\t\\##\n\t##\\ab##\n", true);
			AreEqual(0, t.head.err); AreEqual(0, t.head.from); AreEqual(2, t.head.to);
			AreEqual(0, t.head.head.err); AreEqual(4, t.head.head.from); AreEqual(7, t.head.head.to);
		}
	}
}
