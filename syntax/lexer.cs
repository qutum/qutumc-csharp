//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using qutum.parser;
using System;
using System.Text;

namespace qutum.syntax
{
	enum Lex
	{
		_ = 1, Eol, Indent, Dedent, Str, Bstr, Comm, Bcomm,
	}

	class Lexer : LexerEnum<Lex>
	{
		static string Grammar = @"
		_ = [ \t]+
		Eol = \n|\r\n
		Str = "" ?""|+[^""\\\n\r]+|+\U+|+\\[ -~^ux]|+\\x[0-9a-fA-F][0-9a-fA-F]|+\\u[0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F]
		Bstr = \\+"" ?+""\\+|+""|+[^""]+|+\U+
		Comm = ## ?\n|+[^\n]+|+\U+
		Bcomm = \\+## ?+##\\+|+#|+[^#]+|+\U+
		";

		public Lexer() : base(Grammar, null) { }

		byte[] bs = new byte[4096];
		int bn = 0;
		char[] us = new char[1];

		protected override void Token(Lex key, int step, ref bool end, int f, int to)
		{
			if (from < 0) { from = f; bn = 0; }
			switch (key)
			{
				case Lex.Str:
					if (step == 1) return;
					if (end) { Add(key, from, to, Encoding.UTF8.GetString(bs, 0, bn)); from = -1; return; }
					if (bs.Length < bn + to - f) Array.Resize(ref bs, bn + to - f + 4095 & ~4095);
					scan.Tokens(f, to, bs, bn);
					if (bs[bn] != '\\') { bn += to - f; return; }
					switch (bs[bn + 1])
					{
						case (byte)'0': bs[bn++] = (byte)'\0'; return;
						case (byte)'t': bs[bn++] = (byte)'\t'; return;
						case (byte)'n': bs[bn++] = (byte)'\n'; return;
						case (byte)'r': bs[bn++] = (byte)'\r'; return;
						case (byte)'x': bs[bn++] = (byte)(Hex(bn + 1) << 4 | Hex(bn + 2)); return;
						case (byte)'u':
							us[0] = (char)(Hex(bn + 2) << 12 | Hex(bn + 3) << 8 | Hex(bn + 4) << 4 | Hex(bn + 5));
							bn += Encoding.UTF8.GetBytes(us, 0, 1, bs, bn); return;
					}
					bs[bn++] = bs[bn]; return;
				case Lex.Bstr:
					if (step == 1) { bn = to; return; }
					if (to - f != bn - from || scan.Tokens(f, f + 1, bs)[0] != '"') return;
					if (bs.Length < f - bn) Array.Resize(ref bs, f - bn + 4095 & ~4095);
					scan.Tokens(bn, f, bs);
					Add(key, from, to, Encoding.UTF8.GetString(bs, 0, f - bn));
					end = true; from = -1; return;
				case Lex.Comm: if (end) { Add(key, from, to, null); from = -1; } return;
				case Lex.Bcomm:
					if (step == 1) { bn = to; return; }
					if (to - f != bn - from || scan.Tokens(f, f + 1, bs)[0] != '#') return;
					Add(key, from, to, null); end = true; from = -1; return;
			}
			base.Token(key, step, ref end, f, to);
		}

		int Hex(int x) => (bs[x] & 15) + (bs[x] < 'A' ? 0 : 9);
	}
}
