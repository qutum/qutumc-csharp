using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using System.Linq;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test
{
	[TestClass]
	public class TestLexer
	{
		public TestLexer() { DebugWriter.ConsoleBegin(); }

		enum Tag { Utf = 1, A, B, BB, C, CC, D };

		void Check(LexerEnum<Tag> l, string input, string s)
		{
			l.Load(Encoding.UTF8.GetBytes(input));
			while (l.Next()) ;
			var z = string.Join(", ", l.Tokens(0, l.Loc()).Select(t => t.Dump()).ToArray());
			Console.WriteLine(z);
			l.Unload();
			AreEqual(s, z);
		}

		[TestMethod]
		public void Lex1()
		{
			var l = new LexerEnum<Tag>("A=a \n B=b\n");
			Check(l, "a", "A: a"); Check(l, "b", "B: b");
			Check(l, "ab", "A: a, B: b");
			Check(l, "c", "0! c");
		}

		[TestMethod]
		public void Lex12()
		{
			var l = new LexerEnum<Tag>("A=a \n B=ab \n BB=bb \n C=cc");
		}

		[TestMethod]
		public void Lex13()
		{
			var l = new LexerEnum<Tag>("A=a \n B=abc \n BB=bc \n C=ccc");
		}

		[TestMethod]
		public void Lex23()
		{
			var l = new LexerEnum<Tag>("A=ab \n B=abc \n C=cc \n CC=ccc");
		}

		[TestMethod]
		public void Lex24()
		{
			var l = new LexerEnum<Tag>("A=ab \n B=abcd \n C=cccd");
		}

		[TestMethod]
		public void Lex123()
		{
			var l = new LexerEnum<Tag>("A=a \n B=ab \n BB=ac \n C=abc");
		}

		[TestMethod]
		public void Lex1234()
		{
			var l = new LexerEnum<Tag>("A=a \n B=ab \n C=abc \n CC=abd \n D=abcd");
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void LexRange1()
		{
			new LexerEnum<Tag>("A=a[^ab] \n B=ac");
		}

		[TestMethod]
		public void LexRange2()
		{
			var l = new LexerEnum<Tag>("A=[ab][0-2] \n B=[a-b][0-9^3-9]d");
		}

		[TestMethod]
		public void LexRange3()
		{
			var l = new LexerEnum<Tag>("A=[^ab]c");
		}

		[TestMethod]
		public void LexRange4()
		{
			var l = new LexerEnum<Tag>("A=[a-z0-9]_");
		}

		[TestMethod]
		public void LexRange5()
		{
			var l = new LexerEnum<Tag>("A=[^a-z0-9]_");
		}

		[TestMethod]
		public void LexRange6()
		{
			var l = new LexerEnum<Tag>("A=[abc^b]_");
		}

		[TestMethod]
		public void LexRange7()
		{
			var l = new LexerEnum<Tag>("A=[!-~^0-9A-Za-z]");
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void LexRange8()
		{
			var l = new LexerEnum<Tag>("A=[abc^0-9A-Za-z]");
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void LexAlt1()
		{
			new LexerEnum<Tag>("A=ab|ac \n B=ab");
		}

		[TestMethod]
		public void LexAlt2()
		{
			var l = new LexerEnum<Tag>("A=ab|ac");
		}

		[TestMethod]
		public void LexAlt3()
		{
			var l = new LexerEnum<Tag>("A=ab|ac \n B=abc");
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void LexAlt4()
		{
			var l = new LexerEnum<Tag>("A=ab|ab");
		}

		[TestMethod]
		public void LexPlus1()
		{
			var l = new LexerEnum<Tag>("A=a+");
		}

		[TestMethod]
		public void LexPlus2()
		{
			var l = new LexerEnum<Tag>("A=aa+");
		}

		[TestMethod]
		public void LexPlus3()
		{
			var l = new LexerEnum<Tag>("A=ab+c");
		}

		[TestMethod]
		public void LexStep1()
		{
			var l = new LexerEnum<Tag>("A=a b c");
		}

		[TestMethod]
		[ExpectedException(typeof(Exception))]
		public void LexStep2()
		{
			var l = new LexerEnum<Tag>("A=?a b c");
		}

		[TestMethod]
		public void LexStep3()
		{
			var l = new LexerEnum<Tag>("A=a ?b+ c");
		}

		[TestMethod]
		public void LexStep4()
		{
			var l = new LexerEnum<Tag>("A=a +b+ c");
		}
	}
}
