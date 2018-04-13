//
// Qutum 10 Compiler
// Copyright 2008-2018 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using qutum.parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qutum.syntax
{
	enum Lex : sbyte
	{
		__ = -3, Parse = -2, Parse__ = -1,
		_ = 1, Comm, Commb, Eol, Ind, Ded,
		Str, Strb,
		Word, Hex, Num, Int, Float,
		In, Out, Hub, Wire,
		Pl, Pr, Sbl, Sbr, Cbl, Cbr,
		Mul, Div, Mod, Divf, Modf, Shl, Shr, Add, Sub,
		Eq, Ineq, Leq, Geq, Less, Gre,
		Not, Xor, And, Or,
	}

	class Lexer : LexerEnum<Lex>
	{
		static string Grammar = @"
		_     = \s|\t ?+\s+|+\t+
		Eol   = \n|\r\n
		Comm  = ## ?+[^\n]+|+\U+
		Commb = \\+## *+##\\+|+#|+[^#]+|+\U+
		Str   = "" *""|+[^""\\\n\r]+|+\U+|+\\[\s!-~^ux]|+\\x[0-9a-fA-F][0-9a-fA-F]|+\\u[0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F]
		Strb  = \\+"" *+""\\+|+""|+[^""]+|+\U+
		Word  = [a-zA-Z_]|[a-zA-Z_][0-9a-zA-Z_]+
		Hex   = 0[xX]|\+0[xX]|-0[xX] ?+[0-9a-fA-F]+|+_[0-9a-fA-F]+
		Num   = 0|\+0|-0|[1-9]|\+[1-9]|-[1-9] ?+[0-9]+|+_[0-9]+ ?.[0-9]+ ?+_[0-9]+ ?[eE][0-9]+|[eE][\+\-][0-9]+ ?[fF]
		In    = `
		Out   = .
		Wire  = '
		Pl    = (
		Pr    = )
		Sbl   = \[
		Sbr   = \]
		Cbl   = {
		Cbr   = }
		Mul   = \*
		Div   = /
		Mod   = %
		Divf  = //
		Modf  = %%
		Shl   = <<
		Shr   = >>
		Add   = \+
		Sub   = -
		Eq    = =
		Ineq  = -=
		Leq   = <=
		Geq   = >=
		Less  = <
		Gre   = >
		Not   = --
		Xor   = \+\+
		And   = &&
		Or    = \|\|
		";

		public Lexer() : base(Grammar, null) { }

		byte[] bs = new byte[4096];
		int bn, nn, nf, ne, indLast;
		char[] us = new char[1];

		public override void Unload() { base.Unload(); indLast = 0; }

		protected override void Token(Lex key, int step, ref bool end, int f, int to)
		{
			object v = null;
			if (from < 0)
			{
				from = f; bn = 0;
				if (key != Lex._ && LineStart(f)) while (indLast > 0) Add(Lex.Ded, f, f, --indLast);
			}
			if (key == Lex._)
			{
				if (step == 1)
					if (LineStart(f)) { scan.Tokens(f, to, bs); return; }
					else { bs[0] = 0; return; }
				if (bs[0] != 0)
					if (f < to && scan.Tokens(f, f + 1, bs, 1)[1] != bs[0])
					{ bs[0] = 0; Add(Lex._, f, to, "do not mix tabs and spaces for indent", true); }
					else if (bs[0] == ' ' && (to - from & 3) != 0)
					{ bs[0] = 0; Add(Lex._, f, to, $"{to - from + 3 >> 2 << 2} spaces expected", true); }
				if (!end) return;
				if (bs[0] == 0) { Add(key, from, to, null); from = -1; return; }
				bn = to - from >> (bs[0] == ' ' ? 2 : 0);
				while (bn > indLast) Add(Lex.Ind, from, to, ++indLast);
				while (bn < indLast) Add(Lex.Ded, from, to, --indLast);
				from = -1; return;
			}
			switch (key)
			{
				case Lex.Eol:
					if (to == f + 2) Add(key, f, to, @"use \n instead of \r\n", true);
					break;
				case Lex.Commb:
					if (step == 1) { bn = to - from; return; }
					if (to - f != bn || scan.Tokens(f, f + 1, bs)[0] != '#') return;
					end = true; key = Lex.Comm; v = nameof(Lex.Commb); break;
				case Lex.Str:
					if (step == 1 || end) break;
					ScanBs(f, to, bn);
					if (bs[bn] != '\\') bn += to - f; else Escape(); return;
				case Lex.Strb:
					if (step == 1) { bn = to; return; }
					if (to - f != bn - from || scan.Tokens(f, f + 1, bs)[0] != '"') return;
					end = true; bn = ScanBs(bn, f, 0); break;
				case Lex.Word:
					bn = ScanBs(from, to, 0); break;
				case Lex.Hex:
					if (!end) return;
					bn = ScanBs(from, to, 0); key = Lex.Int; v = Hex(); break;
				case Lex.Num:
					if (step == 2) nn = to - from;
					else if (step == 4) nf = to - from;
					else if (step == 5) ne = to - from;
					if (!end) return;
					bn = ScanBs(from, to, 0); v = Num(ref key); break;
			}
			if (end) { Add(key, from, to, v ?? (bn > 0 ? Encoding.UTF8.GetString(bs, 0, bn) : null)); from = -1; }
		}

		protected override void Error(Lex key, int step, bool end, byte? b, int f, int to)
		{
			if (step < 0 && LineStart(f))
				while (indLast > 0) Add(Lex.Ded, f, f, --indLast);
			base.Error(key, step, end, b, f, to);
		}

		bool LineStart(int f) => tokenn == 0 || tokens[tokenn - 1].key == Lex.Eol && tokens[tokenn - 1].to == f;

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

		object Hex()
		{
			var neg = bs[0] == '-'; uint v = 0;
			for (int x = neg || bs[0] == '+' ? 3 : 2; x < bn; x++)
				if (bs[x] == '_') continue;
				else if (v < 0x10000000) v = v << 4 | (uint)Hex(x);
				else { Add(Lex.Int, from, from + bn, "integer out of range", true); return 0; }
			return neg ? (int)-v : (int)v;
		}

		object Num(ref Lex key)
		{
			var neg = bs[0] == '-'; int x = neg || bs[0] == '+' ? 1 : 0; uint v = 0;
			if (nn == bn)
			{
				key = Lex.Int;
				for (; x < nn; x++)
					if (bs[x] == '_') continue;
					else if (v < 214748364 || v == 214748364 && bs[x] < (neg ? '9' : '8')) v = v * 10 + bs[x] - '0';
					else { Add(key, from, from + nn, "integer out of range", true); return 0; }
				return neg ? (int)-v : (int)v;
			}
			key = Lex.Float; float d = bs[x] - '0';
			while (++x < nn)
				if (bs[x] != '_')
					if (bs[x] > '9') { Add(key, from, from + nn, "invalid float", true); return 0; }
					else if (float.IsInfinity(d = d * 10 + (bs[x] - '0')))
					{ Add(key, from, from + nn, "float out of range", true); return 0f; }
			if (nn < nf)
				for (float f = 1; ++x < nf;)
					if (bs[x] != '_') d = d + (bs[x] - '0') * (f /= 10);
			if (neg) d = -d;
			if (d != 0 && nf < ne)
			{
				if ((neg = bs[++x] == '-') || bs[x] == '+') ++x;
				for (; x < ne; x++)
					if (v <= 8) v = v * 10 + bs[x] - '0';
					else if (neg) { v = 89; break; }
					else { Add(key, from, from + bn, "float out of range", true); return 0f; }
				float a = Exs[v < 38 ? v : 38], b = Exs[v < 38 ? 0 : v < 76 ? v - 38 : 38], c = Exs[v < 76 ? 0 : v - 76];
				if (neg) d = d / a / b / c;
				else if (float.IsInfinity(d = d * a * b * c))
				{ Add(key, from, from + bn, "float out of range", true); return 0f; }
			}
			return d;
		}

		static float Ex = 1;
		static float[] Exs = Enumerable.Range(1, 38).Select(x => Ex *= 10).Prepend(1).ToArray();

		static Lexer() => Eq = new LexEq();
	}

	class LexEq : EqualityComparer<Lex>
	{
		public override bool Equals(Lex x, Lex y)
		{
			if (x == y) return true;
			if ((x ^ y) >= 0) return false;
			return x > 0 ?
				x <= Lex.Comm ? (Lex.__ & y) == Lex.__ : x > Lex.Ded && (Lex.Parse & y) == Lex.Parse :
				y <= Lex.Comm ? (Lex.__ & x) == Lex.__ : y > Lex.Ded && (Lex.Parse & x) == Lex.Parse;
		}

		public override int GetHashCode(Lex v) => (int)v;
	}
}
