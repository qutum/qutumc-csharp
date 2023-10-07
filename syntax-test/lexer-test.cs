//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
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
	[TestClass]
	public class TestLexer : IDisposable
	{
		readonly EnvWriter env = EnvWriter.Begin();

		public void Dispose() => env.Dispose();

		readonly Lexer l = new Lexer { eof = false };

		void Check(string input, string s)
		{
			env.WriteLine(input);
			l.mergeErr = true;
			using var __ = l.Load(new ScanByte(Encoding.UTF8.GetBytes(input)));
			while (l.Next())
				;
			var z = string.Join(" ", l.Tokens(0, l.Loc()).Select(t => t.ToString(Dump)).ToArray());
			env.WriteLine(z);
			AreEqual(s, z);
		}

		static string Dump(object v) => v is object[] s ? string.Join(',', s) + "," : v?.ToString() ?? "";

		void CheckSp(string input, string s)
		{
			l.allBlank = true;
			try {
				Check(input, s);
			}
			finally {
				l.allBlank = false;
			}
		}

		[TestMethod]
		public void LexComm()
		{
			CheckSp("\\####\\\\####\\ ", "COMM=COMMB SP=");
			CheckSp("\\### \\## ###\\ ###\\ ab", "COMM=COMMB SP= COMM=");
			CheckSp("\\### \\## ###\\ ###\\ \nab", "COMM=COMMB SP= COMM= EOL= NAME=ab");
			Check("\\### \\## ###\\ ###\\ \nab", "NAME=ab");
			Check("\\\\", "COMMB!");
		}

		[TestMethod]
		public void LexEol()
		{
			CheckSp("\\####\\\t \r\n\r\n\\####\\ \t \n",
				@"COMM=COMMB SP= EOL!use LF \n eol instead of CRLF \r\n EOL= EOL= COMM=COMMB SP= EOL=");
			Check("\\####\\\t \r\n\r\n\\####\\ \t \n", @"EOL!use LF \n eol instead of CRLF \r\n");
		}

		[TestMethod]
		public void LexIndent1()
		{
			CheckSp("    \n\t\t\t\n\t\n    \n", "IND=1 EOL= IND=2 IND=3 EOL= DED=2 DED=1 EOL= EOL= DED=0");
			CheckSp("\ta\n\t\t\tb\n\t\tc\nd\ne",
				"IND=1 NAME=a EOL= IND=2 IND=3 NAME=b EOL= DED=2 NAME=c EOL= DED=1 DED=0 NAME=d EOL= NAME=e");
		}

		[TestMethod]
		public void LexIndent2()
		{
			CheckSp("\t\t####\n", "IND=1 IND=2 COMM= EOL= DED=1 DED=0");
			CheckSp("\\####\\\t\t\n", "COMM=COMMB SP= EOL=");
		}

		[TestMethod]
		public void LexIndent3()
		{
			Check("\n\ta\n\t\t\n\n\tb\n\t\t\t\n\t\tc\n\n\t\nd\ne",
				"IND=1 NAME=a EOL= NAME=b EOL= IND=2 NAME=c EOL= DED=1 DED=0 NAME=d EOL= NAME=e");
			Check("\t\t#### \n\ta \n\\####\\  b\n\t\t\\####\\\tc",
				"IND=1 NAME=a EOL= DED=0 NAME=b EOL= IND=1 IND=2 NAME=c");
		}

		[TestMethod]
		public void LexIndent4()
		{
			CheckSp("a \t", "NAME=a SP=");
			CheckSp(" \t", "SP!do not mix tabs and spaces for indent SP=");
			CheckSp("\t    ", "SP!do not mix tabs and spaces for indent SP=");
			CheckSp(" ", "SP!4 spaces expected SP="); CheckSp("       ", "SP!8 spaces expected SP=");
		}

		[TestMethod]
		public void LexEof1()
		{
			CheckSp("a\t", "NAME=a SP=");
			CheckSp("\ta\t", "IND=1 NAME=a SP=");
			CheckSp("a\n\t", "NAME=a EOL= IND=1");
			CheckSp("\ta\n", "IND=1 NAME=a EOL= DED=0");
			Check("a\t", "NAME=a");
			Check("\ta\t", "IND=1 NAME=a");
			Check("a\n\t\t\n\t", "NAME=a EOL=");
			Check("\ta\n\t\t\n", "IND=1 NAME=a EOL=");
		}

		[TestMethod]
		public void LexEof2()
		{
			l.eof = true;
			CheckSp("a\n", "NAME=a EOL= EOL=");
			CheckSp("a\t", "NAME=a SP= EOL=");
			CheckSp("\ta\t", "IND=1 NAME=a SP= EOL= DED=0");
			CheckSp("a\n\t", "NAME=a EOL= IND=1 EOL= DED=0");
			CheckSp("\ta\n", "IND=1 NAME=a EOL= DED=0 EOL=");
			Check("a\n", "NAME=a EOL=");
			Check("a\t", "NAME=a EOL=");
			Check("\ta\t", "IND=1 NAME=a EOL= DED=0");
			Check("a\n\t\t\n\t", "NAME=a EOL=");
			Check("\ta\n\t\t\n", "IND=1 NAME=a EOL= DED=0");
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
			Check("\"a", "STR!"); Check("\"a\nb\"", "STR!\" expected STR=a NAME=b STR!");
			var s = string.Join("", Enumerable.Range(1000, 5000).ToArray());
			CheckSp($"\"{s}\"", $"STR={s}");
		}

		[TestMethod]
		public void LexString2()
		{
			Check(@"""\tabc\r\ndef""", "STR=\tabc\r\ndef");
			Check(@"""\x09abc\x0d\x0adef""", "STR=\tabc\r\ndef");
			Check(@"""abc\\\0\x7edef\""\u597d吗""", "STR=abc\\\0~def\"好吗");
			Check(@"""\a\x0\uaa""", "STR!\\ STR!\\ STR!\\ STR=ax0uaa");
		}

		[TestMethod]
		public void LexStringBlock1()
		{
			Check("""\"abcdef"\""", "STRB=abcdef"); Check("""\\"abcdef"\\""", "STRB=abcdef");
			Check("\\\"a\\tc\ndef\"\\", "STRB=a\\tc\ndef");
		}

		[TestMethod]
		public void LexStringBlock2()
		{
			Check("""\"ab"cdef"\""", "STRB=ab\"cdef");
			Check("""\""abcdef"\""", "STRB=\"abcdef");
			Check("""\"abcdef""\""", "STRB=abcdef\"");
		}

		[TestMethod]
		public void LexStringBlock3()
		{
			Check("""\""\\abc"\\def"\\"\""", "STRB=\"\\\\abc\"\\\\def\"\\\\");
			Check("""\\""\abc"\def"\"\\""", "STRB=\"\\abc\"\\def\"\\");
		}

		[TestMethod]
		public void LexName1()
		{
			Check("abc", "NAME=abc"); Check("Abc123", "NAME=Abc123");
			Check("_123", "NAME=_123"); Check("__a4", "NAME=__a4"); Check("_A__b_", "NAME=_A__b_");
			Check("_123 __", "NAME=_123 NAME=__");
		}

		[TestMethod]
		public void LexName2()
		{
			Check("a..", "NAME=a RNAME1= RNAME1=");
			Check("1.).", "INT=1 RNAME1= RP= RNAME1=");
			Check("A1.b_..c!__.__", "NAME=A1 RNAME1=b_ RNAME1= RNAME1=c NOT= NAME=__ RNAME1=__");
		}

		[TestMethod]
		public void LexName3()
		{
			Check("a ..).b", "NAME=a RNAME= RNAME1= RP= RNAME1=b");
			Check("A1 .b_..c .d", "NAME=A1 RNAME=b_ RNAME1= RNAME1=c RNAME=d");
		}

		[TestMethod]
		public void LexPath1()
		{
			Check("``", "NAME=,"); Check("`a", "NAME!");
			Check("`a\nb`", "NAME!` expected NAME=a, NAME=b NAME!");
			Check("`abc  def`", "NAME=abc  def,");
			Check(@"`\tabc\`..\.\ndef.`", "NAME=\tabc`,,.\ndef,,");
			Check(@"`..\x09abc\x0adef`", "NAME=,,\tabc\ndef,");
			Check(@"`abc\\\0\x7edef\u597d吗\x2e`", "NAME=abc\\\0~def好吗.,");
			Check(@"`1.2\a\x0\uaa`", "NAME!\\ NAME!\\ NAME!\\ NAME=1,2ax0uaa,");
		}

		[TestMethod]
		public void LexPath2()
		{
			Check(".``", "RNAME=,");
			Check(".`a\n", "NAME!` expected RNAME=a,");
			Check(".`a`.b.`c`", "RNAME=a, RNAME1=b RNAME1=c,");
			Check("a .`b.b`.`c` .`d`.``", "NAME=a RNAME=b,b, RNAME1=c, RNAME=d, RNAME1=,");
		}

		[TestMethod]
		public void LexHex1()
		{
			Check("0x0", "INT=0"); Check("0xF", "INT=15"); Check("0xx", "HEX!x");
			Check("+0x0A", "ADD= INT=10"); Check("-0x0A", "SUB= INT=10");
			Check("0x_0", "INT=0"); Check("0x__0", "HEX!_ NAME=_0"); Check("1x1", "INT=1 NAME=x1");
		}

		[TestMethod]
		public void LexHex2()
		{
			Check("0x7fffffff", "INT=2147483647"); Check("0x80_00_00_00", "INT=2147483648");
			Check("0xffff_fffe", "INT=4294967294"); Check("0xffff_fffe_", "INT=4294967294 NAME=_");
		}

		[TestMethod]
		public void LexInt1()
		{
			Check("0", "INT=0"); Check("0a", "INT=0 NAME=a");
			Check("+09", "ADD= INT=9"); Check("-9876", "SUB= INT=9876");
			Check("2_", "INT=2 NAME=_"); Check("23__3", "INT=23 NAME=__3");
		}

		[TestMethod]
		public void LexInt2()
		{
			Check("2_14_7483_648", "INT=2147483648");
			Check("+214_7483_649", "ADD= INT!integer out of range INT=0");
		}

		[TestMethod]
		public void LexFloat1()
		{
			Check("-0f", "SUB= FLOAT=0"); Check("00.0x", "FLOAT=0 NAME=x"); Check("0.000f", "FLOAT=0");
			Check("340282347999999999999999999999999999999.99999", "FLOAT=3.4028235E+38");
			Check("340282430000000000000000000000000000000.5", "FLOAT!float out of range FLOAT=0");
		}

		[TestMethod]
		public void LexFloat2()
		{
			Check("1234.0", "FLOAT=1234"); Check("553.2", "FLOAT=553.2"); Check("34_8.5", "FLOAT=348.5");
			Check("1.00_0_01000000000000000000000000000000000000000000000001", "FLOAT=1.00001");
			Check("1.0__0", "FLOAT=1 NAME=__0");
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
			Check("',@#$^:;?~", "0!' 0!, 0!@ 0!# 0!$ 0!^ 0!: 0!; 0!? 0!~");
		}

		[TestMethod]
		public void LexSymbol2()
		{
			Check("([{)]}", "LP= LSB= LCB= RP= RSB= RCB=");
			Check("*/%//%%", "MUL= DIV= MOD= DIVF= MODF=");
			Check("<<>>---&&+++||", "SHL= SHR= BNOT= SUB= BAND= BXOR= ADD= BOR=");
		}

		[TestMethod]
		public void LexSymbol3()
		{
			Check("===/=<<=<=<>=>", "EQ= BIND= UEQ= SHL= BIND= LEQ= LT= GEQ= GT=");
			Check("!!&|", "NOT= NOT= AND= OR=");
		}
	}
}
