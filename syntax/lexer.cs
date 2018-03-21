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
		_ = 1, Eol, Ind, Ded, Comm, Bcomm,
		Str, Bstr,
		Word,
	}

	class Lexer : LexerEnum<Lex>
	{
		static string Grammar = @"
		_     = \s|\t ?+\s+|+\t+
		Eol   = \n|\r\n
		Comm  = ## ?+[^\n]+|+\U+
		Bcomm = \\+## *+##\\+|+#|+[^#]+|+\U+
		Str   = "" *""|+[^""\\\n\r]+|+\U+|+\\[\s!-~^ux]|+\\x[0-9a-fA-F][0-9a-fA-F]|+\\u[0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F]
		Bstr  = \\+"" *+""\\+|+""|+[^""]+|+\U+
		Word  = [a-zA-Z_]|[a-zA-Z_][a-zA-Z0-9_]+
		";

		public Lexer() : base(Grammar, null) { }

		byte[] bs = new byte[4096];
		int bn, indLast;
		char[] us = new char[1];

		public override void Unload() { base.Unload(); indLast = 0; }

		protected override void Token(Lex key, int step, ref bool end, int f, int to)
		{
			object v = null;
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
				case Lex.Comm:
					key = Lex._; v = nameof(Lex.Comm); break;
				case Lex.Bcomm:
					if (step == 1) { bn = to - from; return; }
					if (to - f != bn || scan.Tokens(f, f + 1, bs)[0] != '#') return;
					end = true; key = Lex._; v = nameof(Lex.Bcomm); break;
				case Lex.Str:
					if (step == 1 || end) break;
					ScanBs(f, to, bn);
					if (bs[bn] != '\\') bn += to - f; else Escape(); return;
				case Lex.Bstr:
					if (step == 1) { bn = to; return; }
					if (to - f != bn - from || scan.Tokens(f, f + 1, bs)[0] != '"') return;
					end = true; bn = ScanBs(bn, f, 0); break;
				case Lex.Word:
					bn = ScanBs(from, to, 0); break;
			}
			if (end) { Add(key, from, to, v ?? (bn > 0 ? Encoding.UTF8.GetString(bs, 0, bn) : null)); from = -1; }
		}

		protected override void Error(Lex key, int step, bool end, byte? b, int f, int to)
		{
			if (step < 0 && LineStart(f))
				while (indLast > 0) Add(Lex.Ded, f, f, --indLast);
			base.Error(key, step, end, b, f, to);
		}

		bool LineStart(int f) => f == 0 || tokens.Last().key == Lex.Eol && tokens.Last().to == f;

		int ScanBs(int f, int to, int x)
		{
			var n = x + to - f;
			if (bs.Length < n) Array.Resize(ref bs, n + 4095 & ~4095);
			scan.Tokens(f, to, bs, x);
			return n;
		}

		void Escape()
		{
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
			bs[bn++] = bs[bn];
		}

		int Hex(int x) => (bs[x] & 15) + (bs[x] < 'A' ? 0 : 9);
	}
}
