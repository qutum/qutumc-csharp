//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
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
		EFFECT = 0x1FFE0, // effective lexes
		LITERAL = 0x0020, // literals
		OPARI = 0x0040, // arithmetic operators
		OPCOM = 0x0080, // comparison operators
		OPLOG = 0x0100, // logical operators
		OPBIN = 0x8000, // binary operators
		OPPRE = 0x4000, // prefix operators
		OPPOST = 0x2000, // postfix operators
		OTHER = 0x10000,

		EOL = 1, IND, DED, SP, COMM, COMMB,

		LP = OTHER | 1, RP, LSB, RSB, LCB, RCB,
		APO, BAPO, COM, DOT,
		EXC, AT, HASH, DOL, CIR, AMP, COL, SCOL, SEQ, QUE, BSL, VER, TIL,

		SUB = OPBIN | OPPRE | OPARI | 2,
		ADD = OPBIN | OPARI | 1, MUL, DIV, MOD, DIVF, MODF, SHL, SHR,
		EQ = OPBIN | OPCOM | 1, UEQ, LEQ, GEQ, LT, GT,
		NOT = OPPRE | OPLOG | 1, AND = OPBIN | OPLOG | 2, OR,

		STR = LITERAL | 1, STRB, WORD, WORDS, HEX, NUM, INT, FLOAT,
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
		==BAPO  = `
		COM   = ,
		DOT   = .
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
		UEQ   = \\=
		LEQ   = <=
		GEQ   = >=
		LT    = <
		GT    = >
		NOT   = --
		AND   = &&
		OR    = \|\|

		EOL   = \n|\r\n
		SP    = \s|\t  |+\s+|+\t+
		COMM  = ##     |[\u^\n]+
		COMMB = \\+##  *+##\\+| +#|  +[\u^#]+
		STRB  = \\+""  *+""\\+| +""| +[\u^""]+
		STR   = ""     *""|\n| +[\t\s!-~\U^""\\]+| +\\[0tnr"".`\\]| +\\x\x\x| +\\u\x\x\x\x
		WORDS = `      *`| \n| +[\t\s!-~\U^`\\]+|  +\\[0tnr"".`\\]| +\\x\x\x| +\\u\x\x\x\x
		WORD  = [\a_]|[\a_][\d\a_]+  |+.+| +.+[\a_]|+.+[\a_][\d\a_]+
		HEX   = 0[xX]|\+0[xX]|-0[xX]  \x|_\x  |+\x+|+_\x+
		NUM   = 0|\+0|-0|[1-9]|\+[1-9]|-[1-9] |+\d+|+_\d+ |.\d+ |+_\d+ |[eE]\d+|[eE][\+\-]\d+ |[fF]
		";

		public Lexer() : base(Grammar) { }

		byte[] bs = new byte[4096]; // buffer used for some tokens
		int bn;
		int nn, nf, ne; // end of each number part
		int indent; // indent count of current line
		int indentNew = -1; // indent count at line start, -1 not line start
		int indentFrom, indentTo;
		bool crlf; // \r\n found
		public bool eof = true; // insert eol at scan end
		public bool allValue = false; // set all tokens value
		public bool allBlank = false; // keep all spaces, comments and empty lines

		public override void Dispose()
		{
			base.Dispose();
			indent = indentNew = 0; crlf = false;
		}

		public override bool Is(Lex testee, Lex key)
		{
			if (testee == key) return true;
			// key as kind contains testee
			return ((int)key & 0x1f) == 0 && (key & testee) != 0;
		}

		void Indent()
		{
			if (indentNew >= 0) {
				while (indentNew > indent)
					base.Add(Lex.IND, indentFrom, indentTo, ++indent); // more indents
				while (indentNew < indent)
					base.Add(Lex.DED, indentFrom, indentTo, --indent); // less indents
			}
			indentNew = -1;
		}

		protected override void Add(Lex key, int f, int to, object value)
		{
			Indent();
			base.Add(key, f, to, value);
		}

		protected override void Error(Lex key, int part, bool end, int b, int f, int to)
		{
			base.Error(key, part, end, b, f, to);
			if (part < 0) { // scan end
				end = true;
				from = -1;
				if (eof)
					Token(Lex.EOL, 1, ref end, f, f);
				if (eof || allBlank)
					Indent();
			}
		}

		int ScanBs(int f, int to, int x)
		{
			var n = x + to - f;
			if (bs.Length < n)
				Array.Resize(ref bs, n + 4095 & ~4095);
			scan.Tokens(f, to, bs.AsSpan(x));
			return n;
		}

		protected override void Token(Lex key, int part, ref bool end, int f, int to)
		{
			object v = null;
			if (from < 0) {
				from = f; bn = 0;
			}
			switch (key) {

			case Lex.EOL:
				if (!crlf && to == f + 2) { // \r\n found
					AddErr(key, f, to, @"use LF \n eol instead of CRLF \r\n");
					crlf = true;
				}
				if (allBlank
					|| tokenn > 0 && tokens[tokenn - 1].key != Lex.EOL) // not empty line
					Add(key, from, to, null);
				indentNew = 0; indentFrom = indentTo = to; // EOL or clear indents of empty lines
				goto End;

			case Lex.SP:
				if (part == 1) {
					bs[0] = 0;
					if (f < 1 || scan.Token(f - 1) == '\n')
						bs[0] = scan.Token(f); // line start, save the byte to check indents
					return;
				}
				if (bs[0] != 0) // for line start
					if (f < to && (bs[1] = scan.Token(f)) != bs[0]) { // mix \s and \t
						bs[0] = 0; // to be not line start
						AddErr(Lex.SP, f, to, "do not mix tabs and spaces for indent");
					}
					else if (bs[0] == ' ' && (to - from & 3) != 0) { // check \s width of 4
						bs[0] = 0; // to be not line start
						AddErr(Lex.SP, f, to, $"{to - from + 3 >> 2 << 2} spaces expected");
					}
				if (!end)
					return;
				if (bs[0] != 0) { // for line start
					indentNew = to - from; // indent count
					if (bs[0] == ' ') indentNew = indentNew + 2 >> 2;
					indentFrom = from; indentTo = to;
				}
				else if (allBlank)
					Add(key, from, to, null); // SP
				from = -1;
				goto End; // tokens already made

			case Lex.COMM:
				if (!allBlank)
					goto End;
				break;

			case Lex.COMMB:
				if (part == 1) {
					bn = to; // start part position
					return;
				}
				if (to - f != bn - from || scan.Token(f) != '#') // check end part length
					return;
				end = true;
				if (!allBlank)
					goto End;
				key = Lex.COMM; v = nameof(Lex.COMMB); // as COMM
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
			case Lex.WORDS:
				if (part == 1)
					return;
				if (end && scan.Token(f) != (key == Lex.STR ? '"' : '`')) { // \n found
					Error(key, part, true, (byte)'\n', f, to);
					goto End;
				}
				if (end) // " or ` as end
					break;
				// TODO split words
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
				return; // inside string or word

			case Lex.WORD:
				// TODO check length
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
			if (!end)
				return;
			Add(key, from, to, v ?? (bn > 0 ? Encoding.UTF8.GetString(bs, 0, bn) : null));
		End: from = -1;
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
				return e < -54 ? 0f : e < -37 ? v / Exps[37] / Exps[-e - 37] : v / Exps[-e];
			float w = v * Exps[e < 39 ? e : 39];
			if (float.IsInfinity(w)) {
				AddErr(key, from, from + bn, "float out of range");
				return 0f;
			}
			return w;
		}

		static readonly float[] Exps = new[] {
			1e00f, 1e01f, 1e02f, 1e03f, 1e04f, 1e05f, 1e06f, 1e07f, 1e08f, 1e09f,
			1e10f, 1e11f, 1e12f, 1e13f, 1e14f, 1e15f, 1e16f, 1e17f, 1e18f, 1e19f,
			1e20f, 1e21f, 1e22f, 1e23f, 1e24f, 1e25f, 1e26f, 1e27f, 1e28f, 1e29f,
			1e30f, 1e31f, 1e32f, 1e33f, 1e34f, 1e35f, 1e36f, 1e37f, 1e38f, float.PositiveInfinity,
		};
	}
}
