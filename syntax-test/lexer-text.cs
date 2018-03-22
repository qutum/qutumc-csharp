//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using qutum.syntax;
using System;
using System.Linq;
using System.Text;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace qutum.test.syntax
{
	[TestClass]
	public class TestLexer
	{
		public TestLexer() { DebugWriter.ConsoleBegin(); l = new Lexer(); }

		Lexer l;

		void Check(string input, string s) => Check(Encoding.UTF8.GetBytes(input), s);

		void Check(byte[] input, string s)
		{
			l.Load(input);
			while (l.Next()) ;
			var z = string.Join(" ", l.Tokens(0, l.Loc()).Select(t => t.Dump()).ToArray());
			Console.WriteLine(z);
			l.Unload();
			AreEqual(s, z);
		}

		[TestMethod]
		public void Space()
		{
			Check(@"\####\\####\ ", "_=Commb _=");
		}

		[TestMethod]
		public void Eol()
		{
			Check("\\####\\\t \r\n\\####\\ \t \n", @"_=Commb _= Eol!use \n instead of \r\n Eol= _=Commb _= Eol=");
		}

		[TestMethod]
		public void Indent1()
		{
			Check("    \n\t\t\t\n\t\n", "Ind=1 Eol= Ind=2 Ind=3 Eol= Ded=2 Ded=1 Eol= Ded=0");
		}

		[TestMethod]
		public void Indent2()
		{
			Check(" \t", "Ind!do not mix tabs and spaces for indent _=");
			Check("\t    ", "Ind!do not mix tabs and spaces for indent _=");
			Check(" ", "Ind!4 spaces expected _="); Check("       ", "Ind!8 spaces expected _=");
		}

		[TestMethod]
		public void Indent3()
		{
			Check("\n\t\t\n\t\n\n", "Eol= Ind=1 Ind=2 Eol= Ded=1 Eol= Ded=0 Eol=");
			Check("\t\t####\n", "Ind=1 Ind=2 _=Comm Eol= Ded=1 Ded=0");
		}

		[TestMethod]
		public void Utf()
		{
			Check(@"好", "0!\xe5 0!\xa5 0!\xbd");
			Check(@"""abc你好def""", "Str=abc你好def");
		}

		[TestMethod]
		public void String1()
		{
			Check(@"""abc  def""", "Str=abc  def");
			Check(@"""a", "Str!"); Check("\"a\nb\"", "Str!\n Str=ab");
		}

		[TestMethod]
		public void String2()
		{
			Check(@"""\tabc\r\ndef""", "Str=\tabc\r\ndef");
			Check(@"""\x09abc\x0d\x0adef""", "Str=\tabc\r\ndef");
			Check(@"""\abc\\\0\x7edef\""\u597d吗""", "Str=abc\\\0~def\"好吗");
			Check(@"""\x0\uaa""", "Str!\\ Str!\\ Str=x0uaa");
		}

		[TestMethod]
		public void StringBlock1()
		{
			Check(@"\""abcdef""\", "Strb=abcdef"); Check(@"\\""abcdef""\\", "Strb=abcdef");
			Check("\\\"a\\tc\ndef\"\\", "Strb=a\\tc\ndef");
		}

		[TestMethod]
		public void StringBlock2()
		{
			Check(@"\""ab""cdef""\", "Strb=ab\"cdef");
			Check(@"\""""abcdef""\", "Strb=\"abcdef");
			Check(@"\""abcdef""""\", "Strb=abcdef\"");
		}

		[TestMethod]
		public void StringBlock3()
		{
			Check(@"\""""\\abc""\\def""\\""\", "Strb=\"\\\\abc\"\\\\def\"\\\\");
			Check(@"\\""""\abc""\def""\""\\", "Strb=\"\\abc\"\\def\"\\");
		}

		[TestMethod]
		public void Word()
		{
			Check("abc", "Word=abc"); Check("Abc123", "Word=Abc123");
			Check("_123", "Word=_123"); Check("__a4", "Word=__a4"); Check("_A__b_", "Word=_A__b_");
			Check("_123 __", "Word=_123 _= Word=__");
		}

		[TestMethod]
		public void Hex1()
		{
			Check("0x0", "Int=0"); Check("0xF", "Int=15");
			Check("+0x0A", "Int=10");
			Check("0x7fffffff", "Int=2147483647"); Check("0x8000_0000", "Int=-2147483648");
			Check("0xfffffffe", "Int=-2");
			Check("0x_0", "Int=0"); Check("0x__0", "Int=0 Word=__0"); Check("1x1", "Int=1 Word=x1");
		}

		[TestMethod]
		public void Hex2()
		{
			Check("-0x0", "Int=0"); Check("-0xF", "Int=-15");
			Check("-0x7fffffff", "Int=-2147483647"); Check("-0x80000000", "Int=-2147483648");
			Check("-0xff_ff_ff_fe", "Int=2");
		}

		[TestMethod]
		public void Int1()
		{
			Check("0", "Int=0"); Check("0a", "Int=0 Word=a");
			Check("+09", "Int=9"); Check("9876", "Int=9876");
			Check("2_", "Int=2 Word=_"); Check("23__3", "Int=23 Word=__3");
			Check("2_14_7483_647", "Int=2147483647");
			Check("+214_7483_648", "Int!integer out of range Int=0");
		}

		[TestMethod]
		public void Int2()
		{
			Check("-000", "Int=0"); Check("-1x1", "Int=-1 Word=x1"); Check("-0a", "Int=0 Word=a");
			Check("-09", "Int=-9"); Check("-2_", "Int=-2 Word=_");
			Check("-2_14_7483_647", "Int=-2147483647"); Check("-2147483648", "Int=-2147483648");
		}

		[TestMethod]
		public void Float1()
		{
			Check("0f", "Float=0"); Check("00.0x", "Float=0 Word=x"); Check("0.000f", "Float=0");
			Check("340282347999999999999999999999999999999.99999", "Float=3.402823E+38");
			Check("340282430000000000000000000000000000000.5", "Float!float out of range Float=0");
		}

		[TestMethod]
		public void Float2()
		{
			Check("1234.0", "Float=1234"); Check("553.2", "Float=553.2"); Check("34_8.5", "Float=348.5");
			Check("1.00_0_0100000000000000000000000000000000000000000000000000000000001", "Float=1.00001");
			Check("1.0__0", "Float=1 Word=__0");
		}

		[TestMethod]
		public void Float3()
		{
			Check("1e38", "Float=1E+38"); Check("1e+38", "Float=1E+38");
			Check("5e38", "Float!float out of range Float=0");
			Check("1e88", "Float!float out of range Float=0");
			Check("1e999999", "Float!float out of range Float=0");
			Check("0.0000034e44", "Float=3.4E+38");
			Check("0.0000035e44", "Float!float out of range Float=0");
			Check("1e-39", "Float=1E-39"); Check("1e-45", "Float=1.401298E-45");
			Check("1e-46", "Float=0"); Check("1e-83787", "Float=0");
		}

		[TestMethod]
		public void Symbol1()
		{
			Check("a`b.0'c", "Word=a In= Word=b Out= Int=0 Wire= Word=c");
		}

		[TestMethod]
		public void Symbol2()
		{
			Check("([{)]}", "Pl= Sbl= Cbl= Pr= Sbr= Cbr=");
			Check("*/%", "Mul= Div= Mod="); Check("<<>>", "Shl= Shr=");
			Check("+5-5+_5-_5", "Int=5 Int=-5 Add= Word=_5 Sub= Word=_5");
		}

		[TestMethod]
		public void Symbol3()
		{
			Check("==-=<<=<=<>=>", "Eq= Eq= Ineq= Shl= Eq= Leq= Less= Geq= Gre=");
			Check("---++&&||", "Not= Sub= Xor= And= Or=");
		}
	}
}
