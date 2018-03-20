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
		public TestLexer() { DebugWriter.ConsoleBegin(); }

		Lexer l = new Lexer();

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
			Check(@"\####\ ", "_=Bcomm _=");
		}

		[TestMethod]
		public void Eol()
		{
			Check("\\####\\\t \r\n\\####\\ \t \n", @"_=Bcomm _= Eol!use \n instead of \r\n Eol= _=Bcomm _= Eol=");
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
		public void BlockString1()
		{
			Check(@"\""abcdef""\", "Bstr=abcdef"); Check(@"\\""abcdef""\\", "Bstr=abcdef");
			Check("\\\"a\\tc\ndef\"\\", "Bstr=a\\tc\ndef");
		}

		[TestMethod]
		public void BlockString2()
		{
			Check(@"\""ab""cdef""\", "Bstr=ab\"cdef");
			Check(@"\""""abcdef""\", "Bstr=\"abcdef");
			Check(@"\""abcdef""""\", "Bstr=abcdef\"");
		}

		[TestMethod]
		public void BlockString3()
		{
			Check(@"\""""\\abc""\\def""\\""\", "Bstr=\"\\\\abc\"\\\\def\"\\\\");
			Check(@"\\""""\abc""\def""\""\\", "Bstr=\"\\abc\"\\def\"\\");
		}
	}
}
