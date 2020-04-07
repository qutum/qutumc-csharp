//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using qutum.parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace qutum.syntax
{
	enum Lex : sbyte
	{
		BLANK = -3,         // SP or COMM,         bit 01
		CONTENT = -2,       // lexes for contents, bit 10
		CONTENT_BLANK = -1, // CONTENT or BLANK,   bit 11

		SP = 1, COMM, COMMB, EOL, IND, DED,

		STR, STRB, WORD, HEX, NUM, INT, FLOAT,

		LP, RP, LSB, RSB, LCB, RCB,
		APO, BAPO, COM, DOT,
		ADD, SUB, MUL, DIV, MOD, DIVF, MODF, SHL, SHR,
		EQ, IEQ, LEQ, GEQ, LT, GT,
		NOT, XOR, AND, OR,
		EXC, AT, HASH, DOL, CIR, AMP, COL, SCOL, SEQ, QUE, BSL, VER, TIL,
	}

	class Lexer : Lexer<Lex>
	{
		static readonly string Grammar = @"
		LP    = (
		RP    = )
		LSB   = \[
		RSB   = \]
		LCB   = {
		RCB   = }
		APO   = '
		BAPO  = `
		COM   = ,
		DOT   = .
		ADD   = \+
		SUB   = -
		MUL   = \*
		DIV   = /
		MOD   = %
		DIVF  = //
		MODF  = %%
		SHL   = <<
		SHR   = >>
		EQ    = ==
		IEQ   = \\=
		LEQ   = <=
		GEQ   = >=
		LT    = <
		GT    = >
		NOT   = --
		XOR   = \+\+
		AND   = &&
		OR    = \|\|
		EXC   = !
		AT    = @
		HASH  = #
		DOL   = $
		CIR   = ^
		AMP   = &
		COL   = :
		SCOL  = ;
		SEQ   = =
		QUE   = \?
		BSL   = \\
		VER   = \|
		TIL   = ~
		SP    = \s|\t  |+\s+|+\t+
		EOL   = \n|\r\n
		COMM  = ##     |[\u^\n]+
		COMMB = \\+##  *+##\\+| +#|  +[\u^#]+
		STRB  = \\+""  *+""\\+| +""| +[\u^""]+
		STR   = ""     *""|\n| +[\u^""\\\n\r]+| +\\[\s!-~^ux]| +\\x\x\x| +\\u\x\x\x\x
		WORD  = [\a_][\d\a_]*
		HEX   = 0[xX]|\+0[xX]|-0[xX]  \x|_\x  |+\x+|+_\x+
		NUM   = 0|\+0|-0|[1-9]|\+[1-9]|-[1-9] |+\d+|+_\d+ |.\d+ |+_\d+ |[eE]\d+|[eE][\+\-]\d+ |[fF]
		";

		public Lexer() : base(Grammar) { }

		byte[] bs = new byte[4096]; // buffer used for some tokens
		int bn;
		int nn, nf, ne; // end of each number part
		int indent; // indent count of last line
		bool crlf; // \r\n found
#pragma warning disable CS0649
		public bool allValue; // set all tokens value

		public override void Dispose() { base.Dispose(); indent = 0; crlf = false; }

		int ScanBs(int f, int to, int x)
		{
			var n = x + to - f;
			if (bs.Length < n)
				Array.Resize(ref bs, n + 4095 & ~4095);
			scan.Tokens(f, to, bs.AsSpan(x));
			return n;
		}

		protected override void Error(Lex key, int part, bool end, byte? b, int f, int to)
		{
			if (part < 0 && LineStart(f)) // EOL at scan end
				while (indent > 0) // clear indents
					Add(Lex.DED, f, f, --indent);
			base.Error(key, part, end, b, f, to);
		}

		bool LineStart(int f) => tokenn == 0 // scan start
			|| tokens[tokenn - 1].key == Lex.EOL && tokens[tokenn - 1].to == f; // follow a EOL token

		protected override void Token(Lex key, int part, ref bool end, int f, int to)
		{
			object v = null;
			if (from < 0) {
				from = f; bn = 0;
				if (key != Lex.SP && LineStart(f)) // no indent at line start
					while (indent > 0) // clear indents
						Add(Lex.DED, f, f, --indent);
			}
			switch (key) {

			case Lex.SP:
				if (part == 1) {
					// line start, save the byte to check indents
					bs[0] = LineStart(f) ? scan.Token(f) : (byte)0;
					return;
				}
				if (bs[0] != 0) // for line start
					if (f < to && (bs[1] = scan.Token(f)) != bs[0]) { // mix \s and \t
						bs[0] = 0; // error, not line start
						AddErr(Lex.SP, f, to, "do not mix tabs and spaces for indent");
					}
					else if (bs[0] == ' ' && (to - from & 3) != 0) { // check \s width of 4
						bs[0] = 0; // error, not line start
						AddErr(Lex.SP, f, to, $"{to - from + 3 >> 2 << 2} spaces expected");
					}
				if (!end)
					return;
				if (bs[0] != 0) { // for line start
					var ind = to - from >> (bs[0] == ' ' ? 2 : 0); // indent count
					while (ind > indent)
						Add(Lex.IND, from, to, ++indent); // more indents
					while (ind < indent)
						Add(Lex.DED, from, to, --indent); // less indents
				}
				else
					Add(key, from, to, null); // SP
				from = -1;
				return; // already make tokens

			case Lex.EOL:
				if (!crlf && to == f + 2) { // \r\n found
					AddErr(key, f, to, @"use LF \n eol instead of CRLF \r\n");
					crlf = true;
				}
				break;

			case Lex.COMMB:
				if (part == 1) {
					bn = to; // start part position
					return;
				}
				if (to - f != bn - from || scan.Token(f) != '#') // check end part length
					return;
				end = true; key = Lex.COMM; v = nameof(Lex.COMMB); // as COMM
				break;

			case Lex.STRB:
				if (part == 1) {
					bn = to; // start part position
					return;
				}
				if (to - f != bn - from || scan.Token(f) != '"') // check end part length
					return;
				end = true; bn = ScanBs(bn, f, 0);
				break;

			case Lex.STR:
				if (part == 1)
					return;
				if (end && scan.Token(f) != '"') { // \n found
					Error(key, part, true, (byte)'\n', f, to);
					return;
				}
				if (end) // " as end
					break;
				ScanBs(f, to, bn);
				if (bs[bn] != '\\')
					bn += to - f;
				else // unescape
					switch (bs[bn + 1]) {
					case (byte)'0': bs[bn++] = (byte)'\0'; break;
					case (byte)'t': bs[bn++] = (byte)'\t'; break;
					case (byte)'n': bs[bn++] = (byte)'\n'; break;
					case (byte)'r': bs[bn++] = (byte)'\r'; break;
					case (byte)'x': bs[bn++] = (byte)(Hex(bn + 1) << 4 | Hex(bn + 2)); break;
					case (byte)'u':
						Span<char> u = stackalloc char[1];
						u[0] = (char)(Hex(bn + 2) << 12 | Hex(bn + 3) << 8 | Hex(bn + 4) << 4 | Hex(bn + 5));
						bn += Encoding.UTF8.GetBytes(u, bs.AsSpan(bn)); break;
					default: bs[bn++] = bs[bn]; break;
					}
				return; // inside string

			case Lex.WORD:
				bn = ScanBs(from, to, 0);
				break;

			case Lex.HEX:
				if (!end)
					return;
				bn = ScanBs(from, to, 0);
				key = Lex.INT; v = Hex(); // as INT
				break;

			case Lex.NUM:
				if (part == 2) nn = to - from; // end of integer part
				else if (part == 4) nf = to - from; // end of fraction part
				else if (part == 5) ne = to - from; // end of exponent part
				if (!end)
					return;
				bn = ScanBs(from, to, 0);
				v = Num(ref key);
				break;

			default:
				if (allValue)
					bn = ScanBs(from, to, 0);
				break;
			}
			if (end) {
				Add(key, from, to, v ?? (bn > 0 ? Encoding.UTF8.GetString(bs, 0, bn) : null));
				from = -1;
			}
		}

		int Hex(int x) => (bs[x] & 15) + (bs[x] < 'A' ? 0 : 9);

		object Hex()
		{
			var neg = bs[0] == '-';
			uint v = 0;
			for (int x = neg || bs[0] == '+' ? 3 : 2; x < bn; x++)
				if (bs[x] != '_')
					if (v < 0x1000_0000)
						v = v << 4 | (uint)Hex(x);
					else {
						AddErr(Lex.INT, from, from + bn, "hexadecimal out of range");
						return 0;
					}
			return neg ? (int)-v : (int)v;
		}

		object Num(ref Lex key)
		{
			var neg = bs[0] == '-';
			int x = neg || bs[0] == '+' ? 1 : 0;
			int v = 0, dot = 0, e = 0;
			if (nn == bn) {
				key = Lex.INT; // as INT
				for (; x < nn; x++)
					if (bs[x] != '_')
						if (v < 214748364 || v == 214748364 && bs[x] <= (neg ? '8' : '7'))
							v = v * 10 + bs[x] - '0';
						else {
							AddErr(key, from, from + nn, "integer out of range");
							return 0;
						}
				return neg ? -v : v;
			}
			key = Lex.FLOAT; // as FLOAT
			for (; x < nn; x++)
				if (bs[x] != '_')
					if (v <= 9999_9999) v = v * 10 + bs[x] - '0';
					else {
						dot = nn - x;
						break;
					}
			if (nn < nf)
				for (x = nn + 1; x < nf; x++)
					if (bs[x] != '_')
						if (v <= 9999_9999) {
							v = v * 10 + bs[x] - '0'; dot--;
						}
						else
							break;
			if (v == 0)
				return 0f;
			if (neg)
				v = -v;
			if (nf < ne) {
				for (x = (neg = bs[nf + 1] == '-') || bs[nf + 1] == '+' ? nf + 1 : nf; ++x < ne;)
					e = e * 10 + bs[x] - '0';
				if (neg)
					e = -e;
			}
			e += dot;
			if (e <= 0)
				return e < -54 ? 0f : e < -37 ? v / Fes[37] / Fes[-e - 37] : v / Fes[-e];
			float w = v * Fes[e < 39 ? e : 39];
			if (float.IsInfinity(w)) {
				AddErr(key, from, from + bn, "float out of range");
				return 0f;
			}
			return w;
		}

		static readonly float[] Fes = new[] {
			1e00f, 1e01f, 1e02f, 1e03f, 1e04f, 1e05f, 1e06f, 1e07f, 1e08f, 1e09f,
			1e10f, 1e11f, 1e12f, 1e13f, 1e14f, 1e15f, 1e16f, 1e17f, 1e18f, 1e19f,
			1e20f, 1e21f, 1e22f, 1e23f, 1e24f, 1e25f, 1e26f, 1e27f, 1e28f, 1e29f,
			1e30f, 1e31f, 1e32f, 1e33f, 1e34f, 1e35f, 1e36f, 1e37f, 1e38f, float.PositiveInfinity,
		};

		static Lexer() => Eq = new LexEq();

		class LexEq : EqualityComparer<Lex>
		{
			public override bool Equals(Lex x, Lex y)
			{
				if (x == y) return true;
				if ((x ^ y) >= 0) return false; // both lexes or kinds
												// kind contains lex
				if (y < 0) (x, y) = (y, x);
				return y <= Lex.COMM ? (Lex.BLANK & x) == Lex.BLANK
					: y > Lex.DED && (Lex.CONTENT & x) == Lex.CONTENT;
			}

			public override int GetHashCode(Lex v) => (int)v;
		}
	}
}
