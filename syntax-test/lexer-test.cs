//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
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

		void Check(string input, string s)
		{
			l.errMerge = true;
			l.Load(Encoding.UTF8.GetBytes(input));
			while (l.Next()) ;
			var z = string.Join(" ", l.Tokens(0, l.Loc()).Select(t => t.Dump()).ToArray());
			Console.WriteLine(z);
			l.Unload();
			AreEqual(s, z);
		}

		[TestMethod]
		public void LexComm()
		{
			Check(@"\####\\####\ ", "COMM=COMMB SP=");
			Check(@"\### \## ###\ ###\ ab", "COMM=COMMB SP= COMM=");
		}

		[TestMethod]
		public void LexEol()
		{
			Check("\\####\\\t \r\n\\####\\ \t \n", @"COMM=COMMB SP= EOL!use \n instead of \r\n EOL= COMM=COMMB SP= EOL=");
		}

		[TestMethod]
		public void LexIndent1()
		{
			Check("    \n\t\t\t\n\t\n    \n", "IND=1 EOL= IND=2 IND=3 EOL= DED=2 DED=1 EOL= EOL= DED=0");
		}

		[TestMethod]
		public void LexIndent2()
		{
			Check("\n\t\ta\n\ta\na\na", "EOL= IND=1 IND=2 WORD=a EOL= DED=1 WORD=a EOL= DED=0 WORD=a EOL= WORD=a");
		}

		[TestMethod]
		public void LexIndent3()
		{
			Check("\t\t####\n", "IND=1 IND=2 COMM= EOL= DED=1 DED=0");
			Check("\\####\\\t\t\n", "COMM=COMMB SP= EOL=");
		}

		[TestMethod]
		public void LexIndent4()
		{
			Check(" \t", "SP!do not mix tabs and spaces for indent SP=");
			Check("\t    ", "SP!do not mix tabs and spaces for indent SP=");
			Check(" ", "SP!4 spaces expected SP="); Check("       ", "SP!8 spaces expected SP=");
		}

		[TestMethod]
		public void LexUtf()
		{
			Check(@"好", "0!\xe5 0!\xa5 0!\xbd");
			Check(@"""abc你好def""", "STR=abc你好def");
		}

		[TestMethod]
		public void LexString1()
		{
			Check("\"abc  def\"", "STR=abc  def");
			Check("\"a", "STR!"); Check("\"a\nb\"", "STR!\n WORD=b STR!");
		}

		[TestMethod]
		public void LexString2()
		{
			Check(@"""\tabc\r\ndef""", "STR=\tabc\r\ndef");
			Check(@"""\x09abc\x0d\x0adef""", "STR=\tabc\r\ndef");
			Check(@"""\abc\\\0\x7edef\""\u597d吗""", "STR=abc\\\0~def\"好吗");
			Check(@"""\x0\uaa""", "STR!\\ STR!\\ STR=x0uaa");
		}

		[TestMethod]
		public void LexStringBlock1()
		{
			Check(@"\""abcdef""\", "STRB=abcdef"); Check(@"\\""abcdef""\\", "STRB=abcdef");
			Check("\\\"a\\tc\ndef\"\\", "STRB=a\\tc\ndef");
		}

		[TestMethod]
		public void LexStringBlock2()
		{
			Check(@"\""ab""cdef""\", "STRB=ab\"cdef");
			Check(@"\""""abcdef""\", "STRB=\"abcdef");
			Check(@"\""abcdef""""\", "STRB=abcdef\"");
		}

		[TestMethod]
		public void LexStringBlock3()
		{
			Check(@"\""""\\abc""\\def""\\""\", "STRB=\"\\\\abc\"\\\\def\"\\\\");
			Check(@"\\""""\abc""\def""\""\\", "STRB=\"\\abc\"\\def\"\\");
		}

		[TestMethod]
		public void LexWord()
		{
			Check("abc", "WORD=abc"); Check("Abc123", "WORD=Abc123");
			Check("_123", "WORD=_123"); Check("__a4", "WORD=__a4"); Check("_A__b_", "WORD=_A__b_");
			Check("_123 __", "WORD=_123 SP= WORD=__");
		}

		[TestMethod]
		public void LexHex1()
		{
			Check("0x0", "INT=0"); Check("0xF", "INT=15"); Check("0xx", "HEX!x");
			Check("+0x0A", "INT=10");
			Check("0x7fffffff", "INT=2147483647"); Check("0x80_00_00_00", "INT=-2147483648");
			Check("0xffff_fffe", "INT=-2"); Check("0xffff_fffe_", "INT=-2 WORD=_");
			Check("0x_0", "INT=0"); Check("0x__0", "HEX!_ WORD=_0"); Check("1x1", "INT=1 WORD=x1");
		}

		[TestMethod]
		public void LexHex2()
		{
			Check("-0x0", "INT=0"); Check("-0xF", "INT=-15");
			Check("-0x7fffffff", "INT=-2147483647"); Check("-0x80000000", "INT=-2147483648");
			Check("-0xff_ff_ff_fe", "INT=2");
		}

		[TestMethod]
		public void LexInt1()
		{
			Check("0", "INT=0"); Check("0a", "INT=0 WORD=a");
			Check("+09", "INT=9"); Check("9876", "INT=9876");
			Check("2_", "INT=2 WORD=_"); Check("23__3", "INT=23 WORD=__3");
			Check("2_14_7483_647", "INT=2147483647");
			Check("+214_7483_648", "INT!integer out of range INT=0");
		}

		[TestMethod]
		public void LexInt2()
		{
			Check("-000", "INT=0"); Check("-1x1", "INT=-1 WORD=x1"); Check("-0a", "INT=0 WORD=a");
			Check("-09", "INT=-9"); Check("-2_", "INT=-2 WORD=_");
			Check("-2_14_7483_647", "INT=-2147483647"); Check("-2147483648", "INT=-2147483648");
		}

		[TestMethod]
		public void LexFloat1()
		{
			Check("0f", "FLOAT=0"); Check("00.0x", "FLOAT=0 WORD=x"); Check("0.000f", "FLOAT=0");
			Check("340282347999999999999999999999999999999.99999", "FLOAT=3.4028235E+38");
			Check("340282430000000000000000000000000000000.5", "FLOAT!float out of range FLOAT=0");
		}

		[TestMethod]
		public void LexFloat2()
		{
			Check("1234.0", "FLOAT=1234"); Check("553.2", "FLOAT=553.2"); Check("34_8.5", "FLOAT=348.5");
			Check("1.00_0_01000000000000000000000000000000000000000000000001", "FLOAT=1.00001");
			Check("1.0__0", "FLOAT=1 WORD=__0");
		}

		[TestMethod]
		public void LexFloat3()
		{
			Check("1e38", "FLOAT=1E+38"); Check("1e+38", "FLOAT=1E+38");
			Check("5e38", "FLOAT!float out of range FLOAT=0");
			Check("1e88", "FLOAT!float out of range FLOAT=0");
			Check("1e999999", "FLOAT!float out of range FLOAT=0");
			Check("0.0000034e44", "FLOAT=3.4E+38");
			Check("0.0000035e44", "FLOAT!float out of range FLOAT=0");
			Check("3402.823479E35", "FLOAT=3.4028235E+38");
		}

		[TestMethod]
		public void LexFloat4()
		{
			Check("1.234567891e-39", "FLOAT=1.234568E-39");
			Check("1.999999999e-45", "FLOAT=1E-45");
			Check("1e-46", "FLOAT=0"); Check("1e-83787", "FLOAT=0");
			Check("0.0000000000000000000000000000000000000000000000000034e89f", "FLOAT=3.4E+38");
			Check("0.0000000000000000000000000000000000000000000000000034e90f", "FLOAT!float out of range FLOAT=0");
		}

		[TestMethod]
		public void LexSymbol1()
		{
			Check("a`b.0'c", "WORD=a BAPO= WORD=b DOT= INT=0 APO= WORD=c");
		}

		[TestMethod]
		public void LexSymbol2()
		{
			Check("([{)]}", "PL= SBL= CBL= PR= SBR= CBR=");
			Check("*/%//%%", "MUL= DIV= MOD= DIVF= MODF="); Check("<<>>", "SHL= SHR=");
			Check("+5-5+_5-_5", "INT=5 INT=-5 ADD= WORD=_5 SUB= WORD=_5");
		}

		[TestMethod]
		public void LexSymbol3()
		{
			Check("==-=<<=<=<>=>", "EQ= EQ= IEQ= SHL= EQ= LEQ= LES= GEQ= GRE=");
			Check("---++&&||", "NOT= SUB= XOR= AND= OR=");
		}
	}
}
