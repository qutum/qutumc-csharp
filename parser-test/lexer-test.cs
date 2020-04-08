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
	[TestClass]
	public class TestLexer : IDisposable
	{
		readonly EnvWriter env = EnvWriter.Begin();

		public void Dispose() => env.Dispose();

		enum Tag { A = 1, B, BB, C, CC, D };

		void Check(Lexer<Tag> l, string input, string s) => Check(l, Encoding.UTF8.GetBytes(input), s);

		void Check(Lexer<Tag> l, byte[] input, string s)
		{
			l.errMerge = true;
			using var __ = l.Load(new ScanByte(input));
			while (l.Next()) ;
			var z = string.Join(" ", l.Tokens(0, l.Loc()).Select(t => t.ToString()).ToArray());
			env.WriteLine(z);
			AreEqual(s, z);
		}

		void Throw(Action a, string reg)
		{
			try {
				a();
			}
			catch (Exception e) {
				if (new Regex(reg).IsMatch(e.Message))
					return;
				Fail($"Expected Regex: {reg}, Actual: {e.Message}");
			}
			Fail($"Expected Regex: {reg}, Actually No Excpetion");
		}

		[TestMethod]
		public void Lex1()
		{
			var l = new Lexer<Tag>("A=a \n B=b\n ==comm\n");
			Check(l, "a", "A=a"); Check(l, "b", "B=b"); Check(l, "ab", "A=a B=b");
			Check(l, "", ""); Check(l, "c", "0!c");
		}

		[TestMethod]
		public void Lex12()
		{
			var l = new Lexer<Tag>("A=a \n B=ab \n BB=bb \n C=cc");
			Check(l, "a", "A=a"); Check(l, "ab", "B=ab");
			Check(l, "abbb", "B=ab BB=bb"); Check(l, "ccbb", "C=cc BB=bb");
			Check(l, "aabb", "A=a B=ab 0!b");
		}

		[TestMethod]
		public void Lex13()
		{
			var l = new Lexer<Tag>("A=a \n B=abc \n BB=bc \n C=ccc \n D=cd");
			Check(l, "abc", "B=abc"); Check(l, "aabcccc", "A=a B=abc C=ccc");
			Check(l, "bcb", "BB=bc 0!b"); Check(l, "ccd", "0!c D=cd");
		}

		[TestMethod]
		public void Lex23()
		{
			var l = new Lexer<Tag>("A=ab \n B=abc \n C=cc \n CC=ccc");
			Check(l, "ab", "A=ab"); Check(l, "abc", "B=abc");
			Check(l, "ababc", "A=ab B=abc"); Check(l, "abdcc", "A=ab 0!d C=cc");
			Check(l, "abcc", "B=abc 0!c"); Check(l, "abccc", "B=abc C=cc");
			Check(l, "ababcccc", "A=ab B=abc CC=ccc"); Check(l, "a", "0!a");
		}

		[TestMethod]
		public void Lex24()
		{
			var l = new Lexer<Tag>("A=ab \n B=abcd \n C=cccd");
			Check(l, "a", "0!a"); Check(l, "ab", "A=ab"); Check(l, "abc", "A=ab 0!c");
			Check(l, "abcdab", "B=abcd A=ab"); Check(l, "abcccd", "A=ab C=cccd");
		}

		[TestMethod]
		public void Lex123()
		{
			var l = new Lexer<Tag>("A=a \n B=ab \n BB=ac \n C=abc");
			Check(l, "a", "A=a"); Check(l, "ab", "B=ab"); Check(l, "ac", "BB=ac");
			Check(l, "abc", "C=abc"); Check(l, "aabcc", "A=a C=abc 0!c");
		}

		[TestMethod]
		public void Lex1234()
		{
			var l = new Lexer<Tag>("A=a \n B=ab \n C=abc \n CC=abd \n D=abcd");
			Check(l, "abcd", "D=abcd"); Check(l, "ababd", "B=ab CC=abd");
			Check(l, "abd", "CC=abd"); Check(l, "abcab", "C=abc B=ab");
		}

		[TestMethod]
		public void Lex16()
		{
			Throw(() => new Lexer<Tag>("A=0123456789abcdefg"), "15");
			Throw(() => new Lexer<Tag>("A=[abc^0-9A-Za-z]"), "No byte in A.1");
		}

		[TestMethod]
		public void LexRange1()
		{
			Throw(() => new Lexer<Tag>("A=a[^ab] \n B=ac"), "Prefix of B.1 and A.1");
		}

		[TestMethod]
		public void LexRange2()
		{
			var l = new Lexer<Tag>("A=[ab][0-2] \n B=[a-b][0-9^3-9]d");
			Check(l, "a0", "A=a0"); Check(l, "a0d", "B=a0d");
			Check(l, "b2", "A=b2"); Check(l, "b2d", "B=b2d");
		}

		[TestMethod]
		public void LexRange3()
		{
			var l = new Lexer<Tag>("A=[^abAB]c \n B=[AB][]");
			Check(l, "ac", "0!a 0!c"); Check(l, "bc", "0!b 0!c");
			Check(l, "cccc", "A=cc A=cc"); Check(l, "\nc", "A=\nc"); Check(l, "A~", "B=A~");
			Check(l, "acc", "0!a A=cc");
		}

		[TestMethod]
		public void LexRange4()
		{
			var l = new Lexer<Tag>("A=\\x_");
			Check(l, "3_", "A=3_"); Check(l, "b_", "A=b_");
			Check(l, "G_", "0!G 0!_"); Check(l, "_", "0!_");
		}

		[TestMethod]
		public void LexRange5()
		{
			var l = new Lexer<Tag>("A=[^a-z0-9]_");
			Check(l, "A_", "A=A_"); Check(l, ".._", "0!. A=._");
			Check(l, "a_", "0!a 0!_"); Check(l, "2_", "0!2 0!_");
		}

		[TestMethod]
		public void LexRange6()
		{
			var l = new Lexer<Tag>("A=[abc^b]_");
			Check(l, "a_", "A=a_"); Check(l, "c_", "A=c_"); Check(l, "b_", "0!b 0!_");
		}

		[TestMethod]
		public void LexRange7()
		{
			var l = new Lexer<Tag>("A=[!-~^\\d\\a]");
			Check(l, "!_^/+", "A=! A=_ A=^ A=/ A=+");
			Check(l, " 2Ab.", "0!  0!2 0!A 0!b A=.");
		}

		[TestMethod]
		public void LexRange8()
		{
			Throw(() => new Lexer<Tag>("A=a[ac] \n B=a[bc]"), "Prefix of B.1 and A.1");
			Throw(() => new Lexer<Tag>("A=a[abc] \n B=a[bc]"), "Prefix of B.1 and A.1");
		}

		[TestMethod]
		public void LexAlt1()
		{
			Throw(() => new Lexer<Tag>("A=ab| ac \n B=ab"), "B.1 and A.1 conflict");
		}

		[TestMethod]
		public void LexAlt2()
		{
			var l = new Lexer<Tag>("A=ab|ac");
			Check(l, "ab", "A=ab"); Check(l, "ac", "A=ac");
			Check(l, "a", "0!a"); Check(l, "ad", "0!a 0!d"); Check(l, "abc", "A=ab 0!c");
		}

		[TestMethod]
		public void LexAlt3()
		{
			var l = new Lexer<Tag>("A=abc|abd \n B=abcd \n C=acd");
			Check(l, "abc", "A=abc"); Check(l, "abd", "A=abd");
			Check(l, "abcd", "B=abcd"); Check(l, "acd", "C=acd");
		}

		[TestMethod]
		public void LexAlt4()
		{
			Throw(() => new Lexer<Tag>("A=a[a-c^ac]|a[b-b]"), "A.1 and A.1 conflict");
			Throw(() => new Lexer<Tag>("A=a[a-c^a]|ab"), "Prefix of A.1 and A.1");
		}

		[TestMethod]
		public void LexRepeat1()
		{
			var l = new Lexer<Tag>("A=a+");
			Check(l, "a", "A=a"); Check(l, "aa", "A=aa"); Check(l, "aaa", "A=aaa");
			Check(l, "baa", "0!b A=aa"); Check(l, "ab", "A=a 0!b");
		}

		[TestMethod]
		public void LexRepeat2()
		{
			var l = new Lexer<Tag>("A=aa+");
			Check(l, "a", "0!a"); Check(l, "aa", "A=aa"); Check(l, "aaa", "A=aaa");
			Check(l, "baa", "0!b A=aa"); Check(l, "ab", "0!a 0!b");
		}

		[TestMethod]
		public void LexRepeat3()
		{
			var l = new Lexer<Tag>("A=ab+c");
			Check(l, "abc", "A=abc"); Check(l, "abbc", "A=abbc"); Check(l, "abbbc", "A=abbbc");
			Check(l, "ac", "0!a 0!c"); Check(l, "abb", "A!"); Check(l, "abbda", "A!d 0!a");
		}

		[TestMethod]
		public void LexRepeat4()
		{
			Throw(() => new Lexer<Tag>("A=a+a"), "A.1 and A.1 .*repeat");
		}

		[TestMethod]
		public void LexRepeat5()
		{
			Throw(() => new Lexer<Tag>("A=a+c \n B=aa"), "B.1 and A.1 conflict");
			Throw(() => new Lexer<Tag>("A=a+b \n B=abc"), "B.1 and A.1 conflict");
			Throw(() => new Lexer<Tag>("A=ab+c \n B=abcd"), "B.1 and A.1 conflict");
			Throw(() => new Lexer<Tag>("A=abc \n B=a+b"), "B.1 and A.1 conflict");
		}

		[TestMethod]
		public void LexRepeat6()
		{
			Throw(() => new Lexer<Tag>("A=aa \n B=a+"), "B.1 and A.1 .*repeat");
			Throw(() => new Lexer<Tag>("A=a \n B=a+"), "B.1 and A.1 conflict");
		}

		[TestMethod]
		public void LexRepeat7()
		{
			var l = new Lexer<Tag>("A=a+ \n B=a+b");
			Check(l, "a", "A=a"); Check(l, "ab", "B=ab");
			Check(l, "aa", "A=aa"); Check(l, "aab", "B=aab");
			Check(l, "aaa", "A=aaa"); Check(l, "aaab", "B=aaab");
		}

		[TestMethod]
		public void LexRepeat8()
		{
			var l = new Lexer<Tag>("A=a+b \n B=a+c");
			Check(l, "abaac", "A=ab B=aac"); Check(l, "b", "0!b"); Check(l, "c", "0!c");
		}

		[TestMethod]
		public void LexRepeat9()
		{
			Throw(() => new Lexer<Tag>("A=[ab]+ [bc]"), "A.2 and A.1 .*repeat");
			Throw(() => new Lexer<Tag>("A=a[ab]+|b[bc]+ b"), "A.2 and A.1 .*repeat");
		}

		[TestMethod]
		public void LexStep1()
		{
			var l = new Lexer<Tag>("A=aa b cde \n B=ab \n C=bc");
			Check(l, "aabcdee", "A=aabcde 0!e"); Check(l, "abbc", "B=ab C=bc");
			Check(l, "aa", "A!"); Check(l, "aabcdf", "A!c 0!d 0!f");
			Check(l, "aacab", "A!c B=ab");
		}

		[TestMethod]
		public void LexStep2()
		{
			var l = new Lexer<Tag>("A=a *b c \n B=d \n C=cd");
			Check(l, "abcd", "A=abc B=d"); Check(l, "acbc", "A!c A=acbc");
			Check(l, "acdd", "A!c A!d A!d A!"); Check(l, "adebccd", "A!d A!e A=adebc C=cd");
		}

		[TestMethod]
		public void LexStep3()
		{
			var l = new Lexer<Tag>("A=a bcd|bce f \n B=abcf");
			Check(l, "abcdf", "A=abcdf"); Check(l, "abcef", "A=abcef");
			Check(l, "abcf", "B=abcf");
		}

		[TestMethod]
		public void LexStep4()
		{
			var l = new Lexer<Tag>("A=a b+|c d");
			Check(l, "abd", "A=abd"); Check(l, "abbd", "A=abbd");
			Check(l, "ad", "A!d"); Check(l, "abbc", "A!c"); Check(l, "abbcd", "A!c 0!d");
			Check(l, "acd", "A=acd"); Check(l, "acbd", "A!b 0!d");
		}

		[TestMethod]
		public void LexStep5()
		{
			var l = new Lexer<Tag>("A=a b+e|c d");
			Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
			Check(l, "aecd", "A!e 0!c 0!d"); Check(l, "abbcd", "A!c 0!d");
			Check(l, "acd", "A=acd"); Check(l, "acbd", "A!b 0!d");
		}

		[TestMethod]
		public void LexStep6()
		{
			var l = new Lexer<Tag>("A=a *c|b+e d");
			Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
			Check(l, "aecd", "A!e A=aecd"); Check(l, "abbcbed", "A!c A=abbcbed");
			Check(l, "acd", "A=acd"); Check(l, "acbed", "A!b 0!e 0!d");
		}

		[TestMethod]
		public void LexStep7()
		{
			var l = new Lexer<Tag>("A=a *+c|b+e d");
			Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
			Check(l, "aebed", "A!e A=aebed"); Check(l, "abbcbed", "A!c A=abbcbed");
			Check(l, "acde", "A!d A!e A!"); Check(l, "acbed", "A=acbed");
		}

		[TestMethod]
		public void LexStep8()
		{
			var l = new Lexer<Tag>("A=a | +c|b+e d");
			Check(l, "abed", "A=abed"); Check(l, "abbed", "A=abbed");
			Check(l, "aebed", "A!e 0!b 0!e 0!d"); Check(l, "abbcbed", "A!c A=abbcbed");
			Check(l, "acd", "A=acd"); Check(l, "acbed", "A=acbed");
			Check(l, "ad", "A=ad");
		}

		[TestMethod]
		public void LexStep9()
		{
			var l = new Lexer<Tag>("A=a |+c|b+e");
			Check(l, "abed", "A=abe 0!d"); Check(l, "abbe", "A=abbe");
			Check(l, "aebe", "A=a 0!e 0!b 0!e"); Check(l, "abbcbe", "A!c A=abbcbe");
			Check(l, "ac", "A=ac"); Check(l, "acbe", "A=acbe");
			Check(l, "a", "A=a");
		}

		[TestMethod]
		public void LexStep10()
		{
			var l = new Lexer<Tag>("A=a *bc d \n B=d \n C=cd");
			Check(l, "abcdd", "A=abcd B=d"); Check(l, "acbcd", "A!c A=acbcd");
			Check(l, "abedd", "A!b A!e A!d A!d A!"); Check(l, "abebcdcd", "A!b A!e A=abebcd C=cd");
		}

		[TestMethod]
		public void LexStep11()
		{
			Throw(() => new Lexer<Tag>("A=*a b c"), "");
		}

		[TestMethod]
		public void LexEsc1()
		{
			var l = new Lexer<Tag>(@"A=[\^\-\[\]\\\Z]");
			Check(l, "^", "A=^"); Check(l, "-", "A=-"); Check(l, "[", "A=["); Check(l, "]", "A=]");
			Check(l, "\\", "A=\\"); Check(l, "Z", "A=Z");
		}

		[TestMethod]
		public void LexEsc2()
		{
			var l = new Lexer<Tag>(@"A=\[\]");
			Check(l, "[]", "A=[]");
		}

		[TestMethod]
		public void LexUtf1()
		{
			Throw(() => new Lexer<Tag>("A=\\U+ \n B=\\U\\u"), "Prefix of B.1 and A.1");
			Throw(() => new Lexer<Tag>("A=\\U+ \n B=\\U\\U"), "B.1 and A.1 conflict");
			Throw(() => new Lexer<Tag>("A=\\u+ \n B=\\u"), "B.1 and A.1 conflict");
		}

		[TestMethod]
		public void LexUtf3()
		{
			var l = new Lexer<Tag>("A=a\\U\\Uz \n B=a");
			Check(l, "a你好za很好z", "A=a你好z A=a很好z");
			Check(l, "a\x80\x100z", "A=a\x80\x100z");
			Check(l, "a\u0080aa", "A!a B=a");
		}

		[TestMethod]
		public void LexUtf4()
		{
			var l = new Lexer<Tag>(@"A=a[\U\u^z]+z");
			Check(l, "a好za大家\t都好z", "A=a好z A=a大家\t都好z");
			var bs = Encoding.UTF8.GetBytes("a好"); bs[2] = 0;
			Check(l, bs, "A!\0 0!\xbd");
		}

		[TestMethod]
		public void LexUtf5()
		{
			var l = new Lexer<Tag>("A=[a\\U^\\U] \n B=a\\U+ b");
			Check(l, "a", "A=a");
			Check(l, "a你好b", "B=a你好b");
			Check(l, new byte[] { (byte)'a', 0xc0 }, "B!");
			Check(l, new byte[] { (byte)'a', 0xe0, 0xc0 }, "B!\xc0");
			Check(l, new byte[] { (byte)'a', 0xc0, (byte)'b', (byte)'a' }, "B!b A=a");
			Check(l, new byte[] { (byte)'a', 0xe0, 0xc0, (byte)'a' }, "B!\xc0 A=a");
		}
	}
}
