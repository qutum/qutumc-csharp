//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using qutum.parser;
using System;
using System.Linq;
using System.Text;

namespace qutum.syntax
{
	enum Lex
	{
		_ = 1, Eol, Ind, Ded, Comm, Bcomm, Str, Bstr,
	}

	class Lexer : LexerEnum<Lex>
	{
		static string Grammar = @"
		_ = \s|\t ?+\s+|+\t+
		Eol = \n|\r\n
		Comm = ## ?+[^\n]+|+\U+
		Bcomm = \\+## *+##\\+|+#|+[^#]+|+\U+
		Str = "" *""|+[^""\\\n\r]+|+\U+|+\\[ -~^ux]|+\\x[0-9a-fA-F][0-9a-fA-F]|+\\u[0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F]
		Bstr = \\+"" *+""\\+|+""|+[^""]+|+\U+
		";

		public Lexer() : base(Grammar, null) { }

		byte[] bs = new byte[4096];
		int bn, indLast;
		char[] us = new char[1];

		public override void Unload() { base.Unload(); indLast = 0; }

		protected override void Token(Lex key, int step, ref bool end, int f, int to)
		{
			if (from < 0) { from = f; bn = 0; }
			switch (key)
			{
				case Lex._:
					if (step == 1)
						if (LineStart(f)) { scan.Tokens(f, to, bs); return; }
						else { bs[0] = 0; return; }
					if (bs[0] != 0)
						if (f < to && scan.Tokens(f, f + 1, bs, 1)[1] != bs[0])
						{ bs[0] = 0; Add(Lex.Ind, f, to, "do not mix tabs and spaces for indent", true); }
						else if (bs[0] == ' ' && (to - from & 3) != 0)
						{ bs[0] = 0; Add(Lex.Ind, f, to, $"{to - from + 3 >> 2 << 2} spaces expected", true); }
					if (end)
						if (bs[0] == 0) { Add(key, from, to, null); from = -1; return; }
						else
						{
							bn = to - from >> (bs[0] == ' ' ? 2 : 0);
							while (bn > indLast) Add(Lex.Ind, from, to, ++indLast);
							while (bn < indLast) Add(Lex.Ded, from, to, --indLast);
							from = -1;
						}
					return;
				case Lex.Eol:
					if (to == f + 2) Add(key, f, to, @"use \n instead of \r\n", true);
					if (LineStart(f)) while (indLast > 0) Add(Lex.Ded, f, f, --indLast);
					break;
				case Lex.Comm: if (end) { Add(Lex._, from, to, nameof(Lex.Comm)); from = -1; } return;
				case Lex.Bcomm:
					if (step == 1) { bn = to; return; }
					if (to - f != bn - from || scan.Tokens(f, f + 1, bs)[0] != '#') return;
					Add(Lex._, from, to, nameof(Lex.Bcomm));
					end = true; from = -1; return;
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
			}
			if (end) { Add(key, from, to, ""); from = -1; }
		}

		protected override void Error(Lex key, int step, bool end, byte? b, int f, int to)
		{
			if (step < 0 && LineStart(f))
				while (indLast > 0) Add(Lex.Ded, f, f, --indLast);
			base.Error(key, step, end, b, f, to);
		}

		bool LineStart(int f) => f == 0 || tokens.Last().key == Lex.Eol && tokens.Last().to == f;

		int Hex(int x) => (bs[x] & 15) + (bs[x] < 'A' ? 0 : 9);
	}
}
