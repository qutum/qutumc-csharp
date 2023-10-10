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
	public enum Lex
	{
		BLANK = 0x0010, // blanks
		DENSE = 0x0020, // dense before postfix
		LITERAL = 0x0040, // literals
		PRE = 0x0c00, // prefix operators
		PREPURE = 0x0400, // pure prefix
		PREBIN = 0x0800, // binary prefix
		BIN = 0xff000, // binary operators
		BIN3 = 0x01000,
		BIN43 = 0x02000,
		BIN46 = 0x04000,
		BIN53 = 0x08000,
		BIN56 = 0x10000,
		BIN6 = 0x20000,
		BIN7 = 0x40000,
		BIN8 = 0x80000,

		EOL = BLANK | 1, IND, DED, SP, COMM, COMMB, PATH, NUM,

		LP = 1, LSB, LCB, BIND,

		RP = DENSE | 1, RSB, RCB, RNAME1, RNAME,
		STR = LITERAL | 1, STRB, NAME, HEX, INT, FLOAT,

		// bitwise operators
		SHL = BIN43 | 1, SHR, BNOT = PREPURE | 1, BAND = BIN46 | 1, BXOR, BOR,
		// arithmetic operators
		ADD = BIN56 | PREBIN | 1, SUB, MUL = BIN53 | 1, DIV, MOD, DIVF, MODF,
		// comparison operators
		EQ = BIN6 | 1, UEQ, LEQ, GEQ, LT, GT,
		// logical operators
		NOT = PREPURE | 2, AND = BIN7 | 1, OR,
	}

	public class Lexer : Lexer<Lex>
	{
		static readonly string Grammar = """
		==APO   = '
		==BAPO  = ` == path
		==COM   = ,
		==DOT   = .
		==EXC   = ! == not
		==AT    = @
		==HASH  = #
		==DOL   = $
		==CIR   = ^
		==AMP   = & == and
		==COL   = :
		==SCOL  = ;
		==SEQ   = = == bind
		==QUE   = \?
		==BSL   = \\ == byte block
		==VER   = \| == or
		==TIL   = ~

		BIND  = =
		LP    = (
		RP    = )
		LSB   = \[
		RSB   = \]
		LCB   = {
		RCB   = }
		SHL   = <<
		SHR   = >>
		BNOT   = --
		BAND   = &&
		BXOR   = \+\+
		BOR    = \|\|
		ADD   = \+
		SUB   = -
		MUL   = \*
		DIV   = /
		MOD   = %
		DIVF  = //
		MODF  = %%
		EQ    = ==
		UEQ   = /=
		LEQ   = <=
		GEQ   = >=
		LT    = <
		GT    = >
		NOT   = !
		AND   = &
		OR    = \|

		EOL   = \n|\r\n
		SP    = \s|\t  |+\s+|+\t+
		COMM  = ##     |[\b^\n]+
		COMMB = \\+## +##\\+| +#| +[\b^#]+
		STRB  = \\+"   +"\\+| +"| +[\b^"]+
		STR   = "      *"|\n|     +[\t\s!-~\B^""\\]+| +\\[0tnr"".`\\]| +\\x\x\x| +\\u\x\x\x\x
		PATH  = `|.`   *`| \n|+.| +[\t\s!-~\B^.`\\]+| +\\[0tnr"".`\\]| +\\x\x\x| +\\u\x\x\x\x
		NAME  =     [\a_]|  [\a_][\d\a_]+
		RNAME = .| .[\a_]| .[\a_][\d\a_]+
		HEX   = 0[xX]  \x|_\x  |+\x+|+_\x+
		NUM   = 0|[1-9]  |+\d+|+_\d+  |.\d+  |+_\d+  |[eE]\d+|[eE][\+\-]\d+  |[fF]
		""";

		public override bool Is(Lex testee, Lex key)
		{
			if (testee == key) return true;
			// key as kind contains testee
			return ((int)key & 15) == 0 && (key & testee) != 0;
		}

		public Lexer() : base(BootLexer.Gram<Lex>(Grammar)) { }

		byte[] bs = new byte[4096]; // buffer used for some tokens
		int bn;
		int nn, nf, ne; // end of each number part
		int indent; // indent count of current line
		int indentNew = -1; // indent count at line start, -1 not line start
		int indentFrom, indentTo;
		bool crlf; // \r\n found
		List<string> path = new List<string>();
		public bool eof = true; // insert eol at scan end
		public bool allValue = false; // set all tokens value
		public bool allBlank = false; // keep all spaces, comments and empty lines

		public override void Dispose()
		{
			base.Dispose();
			indent = indentNew = 0; crlf = false; path.Clear();
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
			if (key == Lex.RNAME && tokenn > 0 && tokens[tokenn - 1].to == f
				&& (tokens[tokenn - 1].key & (Lex.DENSE | Lex.LITERAL)) != 0)
				key = Lex.RNAME1; // run name follows the previous token densely, high precedence
			base.Add(key, f, to, value);
		}

		protected override void Error(Lex key, int part, bool end, int b, int f, int to)
		{
			if (key == Lex.PATH)
				key = Lex.NAME;
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
				from = f; bn = 0; path.Clear();
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
				if (part == 1)
					return;
				if (end) {
					if (scan.Token(f) != '"') // \n found
						AddErr(key, f, to, "\" expected");
					break;
				}
				ScanBs(f, to, bn); Unesc(f, to);
				return; // inside string

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

			case Lex.NAME:
			case Lex.RNAME:
				f = key == Lex.NAME ? from : from + 1;
				if (to - f > 40) {
					AddErr(key, f, to, "too long");
					to = f + 40;
				}
				bn = ScanBs(f, to, 0);
				break;

			case Lex.PATH:
				if (part == 1)
					return;
				var split = end || scan.Token(f) == '.';
				if (split) {
					if (bn > 40) {
						AddErr(Lex.NAME, to - 1, to - 1, "too long");
						bn = 40;
					}
					path.Add(Encoding.UTF8.GetString(bs, 0, bn));
					bn = 0;
				}
				if (end) {
					if (scan.Token(f) != '`') // \n found
						AddErr(Lex.NAME, f, to, "` expected");
					key = scan.Token(from) != '.' ? Lex.NAME : Lex.RNAME;
					v = path.ToArray();
					break;
				}
				if (!split) {
					ScanBs(f, to, bn); Unesc(f, to);
				}
				return; // inside path

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

		void Unesc(int f, int to)
		{
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
		}

		int Hex(int x) => (bs[x] & 15) + (bs[x] < 'A' ? 0 : 9);

		object Hex()
		{
			uint v = 0;
			for (int x = 2; x < bn; x++)
				if (bs[x] != '_')
					if (v < 0x1000_0000)
						v = v << 4 | (uint)Hex(x);
					else {
						AddErr(Lex.INT, from, from + bn, "hexadecimal out of range");
						return 0;
					}
			return v;
		}

		object Num(ref Lex key)
		{
			uint v = 0; int x = 0, dot = 0, e = 0;
			if (nn == bn) {
				key = Lex.INT; // as INT
				for (; x < nn; x++)
					if (bs[x] != '_')
						if (v < 214748364 || v == 214748364 && bs[x] <= '8')
							v = v * 10 + bs[x] - '0';
						else {
							AddErr(key, from, from + nn, "integer out of range");
							return 0;
						}
				return v;
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
			if (nf < ne) {
				bool neg = bs[nf + 1] == '-';
				for (x = neg || bs[nf + 1] == '+' ? nf + 1 : nf; ++x < ne;)
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
