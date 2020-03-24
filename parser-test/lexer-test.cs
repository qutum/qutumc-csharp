//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.parser;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.parser
{
	public class TestException : ExpectedExceptionBaseAttribute
	{
		readonly Regex reg;
		readonly Type type;

		public TestException(string regex = null, Type type = null)
		{
			if (regex != null)
				reg = new Regex(regex);
			this.type = type ?? typeof(Exception);
		}

		protected override void Verify(Exception ex)
		{
			if (!type.IsAssignableFrom(ex.GetType()))
				throw new Exception($"Expected: {type}, Actual: {ex.GetType()}");
			if (reg != null && !reg.IsMatch(ex.Message))
				throw new Exception($"Expected Regex: {reg}, Actual: {ex.Message}");
		}
	}

	[TestClass]
	public class TestLexer
	{
		public TestLexer() { DebugWriter.ConsoleBegin(); }

		enum Tag { A = 1, B, BB, C, CC, D };

		void Check(LexerEnum<Tag> l, string input, string s) => Check(l, Encoding.UTF8.GetBytes(input), s);

		void Check(LexerEnum<Tag> l, byte[] input, string s)
		{
			l.Load(input);
			while (l.Next()) ;
			var z = string.Join(" ", l.Tokens(0, l.Loc()).Select(t => t.Dump()).ToArray());
			Console.WriteLine(z);
			l.Unload();
			AreEqual(s, z);
		}

		[TestMethod]
		public void Lex1()
		{
			var l = new LexerEnum<Tag>("A=a \n B=b\n");
			Check(l, "a", "A=a"); Check(l, "b", "B=b"); Check(l, "ab", "A=a B=b");
			Check(l, "", ""); Check(l, "c", "0!c");
		}

		[TestMethod]
		public void Lex12()
		{
			var l = new LexerEnum<Tag>("A=a \n B=ab \n BB=bb \n C=cc");
			Check(l, "a", "A=a"); Check(l, "ab", "B=ab");
			Check(l, "abbb", "B=ab BB=bb"); Check(l, "ccbb", "C=cc BB=bb");
			Check(l, "aabb", "A=a B=ab 0!b");
		}

		[TestMethod]
		public void Lex13()
		{
			var l = new LexerEnum<Tag>("A=a \n B=abc \n BB=bc \n C=ccc");
			Check(l, "abc", "B=abc"); Check(l, "aabcccc", "A=a B=abc C=ccc");
			Check(l, "bcb", "BB=bc 0!b");
		}

		[TestMethod]
		public void Lex23()
		{
			var l = new LexerEnum<Tag>("A=ab \n B=abc \n C=cc \n CC=ccc");
			Check(l, "ab", "A=ab"); Check(l, "abc", "B=abc"); Check(l, "ababc", "A=ab B=abc");
			Check(l, "abcc", "B=abc 0!c"); Check(l, "abccc", "B=abc C=cc");
			Check(l, "ababcccc", "A=ab B=abc CC=ccc"); Check(l, "a", "0!a");
		}

		[TestMethod]
		public void Lex24()
		{
			var l = new LexerEnum<Tag>("A=ab \n B=abcd \n C=cccd");
			Check(l, "a", "0!a"); Check(l, "ab", "A=ab"); Check(l, "abc", "A=ab 0!c");
			Check(l, "abcdab", "B=abcd A=ab"); Check(l, "abcccd", "A=ab C=cccd");
		}

		[TestMethod]
		public void Lex123()
		{
			var l = new LexerEnum<Tag>("A=a \n B=ab \n BB=ac \n C=abc");
			Check(l, "a", "A=a"); Check(l, "ab", "B=ab"); Check(l, "ac", "BB=ac");
			Check(l, "abc", "C=abc"); Check(l, "aabcc", "A=a C=abc 0!c");
		}

		[TestMethod]
		public void Lex1234()
		{
			var l = new LexerEnum<Tag>("A=a \n B=ab \n C=abc \n CC=abd \n D=abcd");
			Check(l, "abcd", "D=abcd"); Check(l, "ababd", "B=ab CC=abd");
			Check(l, "abd", "CC=abd"); Check(l, "abcab", "C=abc B=ab");
		}

		[TestMethod]
		[TestException("15")]
		public void Lex16()
		{
			new LexerEnum<Tag>("A=0123456789abcdefg");
		}

		[TestMethod]
		[TestException("Prefix of B.1 and A.1")]
		public void LexRange1()
		{
			new LexerEnum<Tag>("A=a[^ab] \n B=ac");
		}

		[TestMethod]
		public void LexRange2()
		{
			var l = new LexerEnum<Tag>("A=[ab][0-2] \n B=[a-b][0-9^3-9]d");
			Check(l, "a0", "A=a0"); Check(l, "a0d", "B=a0d");
			Check(l, "b2", "A=b2"); Check(l, "b2d", "B=b2d");
		}

		[TestMethod]
		public void LexRange3()
		{
			var l = new LexerEnum<Tag>("A=[^abAB]c \n B=[AB][]");
			Check(l, "ac", "0!a 0!c"); Check(l, "bc", "0!b 0!c");
			Check(l, "cccc", "A=cc A=cc"); Check(l, "\nc", "A=\nc"); Check(l, "A~", "B=A~");
			Check(l, "acc", "0!a A=cc");
		}

		[TestMethod]
		public void LexRange4()
		{
			var l = new LexerEnum<Tag>("A=[a-z0-9]_");
			Check(l, "3_", "A=3_"); Check(l, "b_", "A=b_");
			Check(l, "A_", "0!A 0!_"); Check(l, "_", "0!_");
		}

		[TestMethod]
		public void LexRange5()
		{
			var l = new LexerEnum<Tag>("A=[^a-z0-9]_");
			Check(l, "A_", "A=A_"); Check(l, ".._", "0!. A=._");
			Check(l, "a_", "0!a 0!_"); Check(l, "2_", "0!2 0!_");
		}

		[TestMethod]
		public void LexRange6()
		{
			var l = new LexerEnum<Tag>("A=[abc^b]_");
			Check(l, "a_", "A=a_"); Check(l, "c_", "A=c_"); Check(l, "b_", "0!b 0!_");
		}

		[TestMethod]
		public void LexRange7()
		{
			var l = new LexerEnum<Tag>("A=[!-~^0-9A-Za-z]");
			Check(l, "!_^/+", "A=! A=_ A=^ A=/ A=+");
			Check(l, " 2Ab.", "0!  0!2 0!A 0!b A=.");
		}

		[TestMethod]
		[TestException("No byte in A.1")]
		public void LexRange8()
		{
			new LexerEnum<Tag>("A=[abc^0-9A-Za-z]");
		}

		[TestMethod]
		[TestException("Prefix of B.1 and A.1")]
		public void LexRange9()
		{
			new LexerEnum<Tag>("A=a[ac] \n B=a[bc]");
		}

		[TestMethod]
		[TestException("Prefix of B.1 and A.1")]
		public void LexRange10()
		{
			new LexerEnum<Tag>("A=a[abc] \n B=a[bc]");
		}

		[TestMethod]
		[TestException("B.1 and A.1 conflict")]
		public void LexAlt1()
		{
			new LexerEnum<Tag>("A=ab|ac \n B=ab");
		}

		[TestMethod]
		public void LexAlt2()
		{
			var l = new LexerEnum<Tag>("A=ab|ac");
			Check(l, "ab", "A=ab"); Check(l, "ac", "A=ac");
			Check(l, "a", "0!a"); Check(l, "ad", "0!a 0!d"); Check(l, "abc", "A=ab 0!c");
		}

		[TestMethod]
		public void LexAlt3()
		{
			var l = new LexerEnum<Tag>("A=ab|ac \n B=abc");
			Check(l, "ab", "A=ab"); Check(l, "ac", "A=ac"); Check(l, "abc", "B=abc");
		}

		[TestMethod]
		[TestException("A.1 and A.1 conflict")]
		public void LexAlt4()
		{
			new LexerEnum<Tag>("A=a[a-c^ac]|a[b-b]");
		}

		[TestMethod]
		[TestException("Prefix of A.1 and A.1")]
		public void LexAlt5()
		{
			new LexerEnum<Tag>("A=a[a-c^a]|ab");
		}

		[TestMethod]
		public void LexPlus1()
		{
			var l = new LexerEnum<Tag>("A=a+");
			Check(l, "a", "A=a"); Check(l, "aa", "A=aa"); Check(l, "aaa", "A=aaa");
			Check(l, "baa", "0!b A=aa"); Check(l, "ab", "A=a 0!b");
		}

		[TestMethod]
		public void LexPlus2()
		{
			var l = new LexerEnum<Tag>("A=aa+");
			Check(l, "a", "0!a"); Check(l, "aa", "A=aa"); Check(l, "aaa", "A=aaa");
			Check(l, "baa", "0!b A=aa"); Check(l, "ab", "0!a 0!b");
		}

		[TestMethod]
		public void LexPlus3()
		{
			var l = new LexerEnum<Tag>("A=ab+c");
			Check(l, "abc", "A=abc"); Check(l, "abbc", "A=abbc"); Check(l, "abbbc", "A=abbbc");
			Check(l, "ac", "0!a 0!c"); Check(l, "abb", "A!"); Check(l, "abbda", "A!d 0!a");
		}

		[TestMethod]
		[TestException("A.1 and A.1 .*repeat")]
		public void LexPlus4()
		{
			new LexerEnum<Tag>("A=a+a");
		}

		[TestMethod]
		[TestException("B.1 and A.1 conflict")]
		public void LexPlus5()
		{
			new LexerEnum<Tag>("A=a+c \n B=aa");
		}

		[TestMethod]
		[TestException("B.1 and A.1 .*repeat")]
		public void LexPlus6()
		{
			new LexerEnum<Tag>("A=aa \n B=a+");
		}

		[TestMethod]
		[TestException("B.1 and A.1 conflict")]
		public void LexPlus7()
		{
			new LexerEnum<Tag>("A=a+b \n B=abc");
		}

		[TestMethod]
		public void LexPlus8()
		{
			var l = new LexerEnum<Tag>("A=a+ \n B=a+b");
			Check(l, "a", "A=a"); Check(l, "ab", "B=ab");
			Check(l, "aa", "A=aa"); Check(l, "aab", "B=aab");
			Check(l, "aaa", "A=aaa"); Check(l, "aaab", "B=aaab");
		}

		[TestMethod]
		public void LexPlus9()
		{
			var l = new LexerEnum<Tag>("A=a+b \n B=a+c");
			Check(l, "abaac", "A=ab B=aac"); Check(l, "b", "0!b"); Check(l, "c", "0!c");
		}

		[TestMethod]
		public void LexStep1()
		{
			var l = new LexerEnum<Tag>("A=aa b c \n B=ab \n C=bc");
			Check(l, "aabcd", "A=aabc 0!d"); Check(l, "abbc", "B=ab C=bc");
			Check(l, "aa", "A!"); Check(l, "aacab", "A!c B=ab");
		}

		[TestMethod]
		[TestException()]
		public void LexStep2()
		{
			new LexerEnum<Tag>("A=*a b c");
		}

		[TestMethod]
		public void LexStep3()
		{
			var l = new LexerEnum<Tag>("A=a *b c \n B=d \n C=cd");
			Check(l, "abcd", "A=abc B=d"); Check(l, "acbc", "A!c A=acbc");
			Check(l, "acdd", "A!c A!d A!d A!"); Check(l, "adbccd", "A!d A=adbc C=cd");
		}

		[TestMethod]
		public void LexStep4()
		{
			var l = new LexerEnum<Tag>("A=a b+|c d");
			Check(l, "abd", "A=abd"); Check(l, "abbd", "A=abbd");
			Check(l, "ad", "A!d"); Check(l, "abbc", "A!c"); Check(l, "abbcd", "A!c 0!d");
			Check(l, "acd", "A=acd"); Check(l, "acbd", "A!b 0!d");
		}

		[TestMethod]
		public void LexStep5()
		{
			var l = new LexerEnum<Tag>("A=a b+e|c d");
			Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
			Check(l, "aecd", "A!e 0!c 0!d"); Check(l, "abbcd", "A!c 0!d");
			Check(l, "acd", "A=acd"); Check(l, "acbd", "A!b 0!d");
		}

		[TestMethod]
		public void LexStep6()
		{
			var l = new LexerEnum<Tag>("A=a *c|b+e d");
			Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
			Check(l, "aecd", "A!e A=aecd"); Check(l, "abbcbed", "A!c A=abbcbed");
			Check(l, "acd", "A=acd"); Check(l, "acbed", "A!b 0!e 0!d");
		}

		[TestMethod]
		public void LexStep7()
		{
			var l = new LexerEnum<Tag>("A=a *+c|b+e d");
			Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
			Check(l, "aebed", "A!e A=aebed"); Check(l, "abbcbed", "A!c A=abbcbed");
			Check(l, "acde", "A!d A!e A!"); Check(l, "acbed", "A=acbed");
		}

		[TestMethod]
		public void LexStep8()
		{
			var l = new LexerEnum<Tag>("A=a ?+c|b+e d");
			Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
			Check(l, "aebed", "A!e 0!b 0!e 0!d"); Check(l, "abbcbed", "A!c A=abbcbed");
			Check(l, "acd", "A=acd"); Check(l, "acbed", "A=acbed");
			Check(l, "ad", "A=ad");
		}

		[TestMethod]
		public void LexEsc1()
		{
			var l = new LexerEnum<Tag>(@"A=[\^\-\[\]\\\U]");
			Check(l, "^", "A=^"); Check(l, "-", "A=-"); Check(l, "[", "A=["); Check(l, "]", "A=]");
			Check(l, "\\", "A=\\"); Check(l, "U", "A=U");
		}

		[TestMethod]
		public void LexEsc2()
		{
			var l = new LexerEnum<Tag>(@"A=\[\]");
			Check(l, "[]", "A=[]");
		}

		[TestMethod]
		[TestException("B.1 and A.1 conflict")]
		public void LexUtf1()
		{
			new LexerEnum<Tag>("A=\\U+ \n B=\\U\\U");
		}

		[TestMethod]
		public void LexUtf2()
		{
			var l = new LexerEnum<Tag>("A=a\\U\\Uz \n B=a");
			Check(l, "a你好za很好z", "A=a你好z A=a很好z");
			Check(l, "a\x80\x100z", "A=a\x80\x100z");
			Check(l, "a\u0080aa", "A!a B=a");
		}

		[TestMethod]
		public void LexUtf3()
		{
			var l = new LexerEnum<Tag>("A=a\\U+z");
			Check(l, "a好za大家都好z", "A=a好z A=a大家都好z");
			var bs = Encoding.UTF8.GetBytes("a好"); bs[2] = 0;
			Check(l, bs, "A!\0 0!\xbd");
		}
	}
}
