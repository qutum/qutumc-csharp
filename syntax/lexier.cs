//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace qutum.syntax;

using L = Lex;
using Set = CharSet;

// lexemes
public enum Lex
{
	// kinds
	LITERAL = 1,
	POST,  // postfix operators
	PRE,   // prefix operators
	BIN3,
	BIN43, // bitwise binary operators
	BIN46, // bitwise binary operators
	BIN53, // arithmetic binary operators
	BIN56, // arithmetic binary operators
	BIN6,  // comparison binary operators
	BIN7,  // logical binary operators
	BIN8,
	// singles
	BIND                          /**/, LP, LSB, LCB,
	RUN = (Right | LCB & 255) + 1 /**/, RP, RSB, RCB,
	EOL = (Blank | RCB & 255) + 1, IND, DED, INDR, DEDR,
	SP, COMM, COMMB, PATH, NUM,

	// inside kinds
	// literal
	STR = LITERAL << 8 | Other | 1, STRB, NAME, HEX, INT, FLOAT,
	// postfix
	RUNP = POST << 8 | Right | 1,
	// bitwise
	SHL = BIN43 << 8 | Bin | 1, SHR,
	ANDB = BIN46 << 8 | Bin | 1, ORB, XORB, NOTB = PRE << 8 | Other | 1,
	// arithmetic
	MUL = BIN53 << 8 | Bin | 1, DIV, MOD, DIVF, MODF,
	ADD = BIN56 << 8 | Bin | 1, SUB,
	// comparison
	EQ = BIN6 << 8 | Bin | 1, UEQ, LEQ, GEQ, LT, GT,
	// logical
	AND = BIN7 << 8 | Bin | 1, OR, XOR, NOT = PRE << 8 | Other | 2,
	// some arithmetic binarys as prefix
	BINPRE = BIN56,

	// groups
	Other  /**/= 0x800_0000, // other group 1<<24
	Right  /**/= 0x801_0000, // right-side group
	Bin    /**/= 0x802_0000, // binary group
	Blank  /**/= 0x880_0000, // blank group
}

// lexic parser
public sealed class Lexier : Lexier<L>, Lexer<L, Lexi<L>>
{
	/*	EXC   = ! == not
		QUO   = " == string
		HASH  = #
		DOL   = $
		AMP   = & == and
		APO   = ' == string?
		COM   = ,
		DOT   = . == run
		COL   = :
		SCOL  = ;
		EQ    = = == bind
		QUE   = \?
		AT    = @
		BSL   = \\ == byte block
		HAT   = ^
		BAPO  = ` == path
		VER   = \| == or
		TIL   = ~
	*/
	static readonly LexGram<L> Grammar = new LexGram<L>()
		.k(L.BIND).w["="]

		.k(L.LP).w["("].k(L.RP).w[")"]
		.k(L.LSB).w["["].k(L.RSB).w["]"]
		.k(L.LCB).w["{"].k(L.RCB).w["}"]

		.k(L.SHL).w["<<"].k(L.SHR).w[">>"]
		.k(L.ANDB).w["&&"].k(L.ORB).w["||"].k(L.XORB).w["++"].k(L.NOTB).w["--"]

		.k(L.MUL).w["*"].k(L.DIV).w["/"].k(L.MOD).w["%"]
		.k(L.DIVF).w["//"].k(L.MODF).w["%%"]
		.k(L.ADD).w["+"].k(L.SUB).w["-"]

		.k(L.EQ).w["=="].k(L.UEQ).w["/="]
		.k(L.LEQ).w["<="].k(L.GEQ).w[">="].k(L.LT).w["<"].k(L.GT).w[">"]

		.k(L.AND).w["&"].k(L.OR).w["|"].k(L.XOR).w["+|"].k(L.NOT).w["!"]

		.k(L.EOL).w["\n"]["\r\n"]
		.k(L.SP).w[" \t".Mem()] // [\s\t]  |+\s+|+\t+
				.w[""][" ", ..].loop["\t", ..].loop

		.k(L.COMM).w["##"] // ##  |[\A^\n]+
					.w[""][Set.All.Exc("\n"), ..]
		.k(L.COMMB).w["\\", .., "#"] // \\+#  +#\\+|+#|+[\A^#]+
					.w["#", "\\", ..].loop["#"].loop[Set.All.Exc("#"), ..].loop

		.k(L.STRB).w["\\", .., "\""] // \\+"  +"\\+|+"|+[\A^"]+
					.w["\"", "\\", ..].loop["\""].loop[Set.All.Exc("\""), ..].loop
		.k(L.STR).w["\""] // "  *"|\n|\r\n|+[\L^"\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo["\"\n".Mem()]["\r\n"]
					[Set.Line.Exc("\"\\"), ..].loop
					["\\", "0tnr\".`\\".Mem()].loop
					["\\x", Set.Hex, Set.Hex].loop["\\u", Set.Hex, Set.Hex, Set.Hex, Set.Hex].loop

		.k(L.PATH).w["`"][".`"] // .?`  *`|\n|\r\n|+.|+[\L^.`\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo["`\n".Mem()]["\r\n"]["."].loop
					[Set.Line.Exc(".`\\"), ..].loop
					["\\", "0tnr\".`\\".Mem()].loop
					["\\x", Set.Hex, Set.Hex].loop["\\u", Set.Hex, Set.Hex, Set.Hex, Set.Hex].loop

		.k(L.NAME).w[Set.Alpha.Inc("_")][Set.Alpha.Inc("_"), Set.Word, ..] // [\a_]\w*
		.k(L.RUN).w["."] // .|.[\a_]\w*
						[".", Set.Alpha.Inc("_")][".", Set.Alpha.Inc("_"), Set.Word, ..]

		.k(L.HEX).w["0x"]["0X"] // 0[xX]  _*\x  |+_*\x+
				.w[Set.Hex]["_", .., Set.Hex]
				.w[""][Set.Hex, ..].loop["_", .., Set.Hex, ..].loop
		.k(L.NUM) // 0|[1-9]  |+_*\d+  |.\d+  |+_+\d+  |[eE][\+\-]?\d+  |[fF]
				.w["0"][Set.Dec.Exc("0")]
				.w[""][Set.Dec, ..].loop["_", .., Set.Dec, ..].loop
				.w[""][".", Set.Dec, ..]
				.w[""]["_", .., Set.Dec, ..].loop
				.w[""]["eE".Mem(), Set.Dec, ..]["eE".Mem(), "+-".Mem(), Set.Dec, ..]
				.w[""]["fF".Mem()]
	;

	// key oridinal that is single or kind value, useful for syntax parser
	public static ushort Ordin(Lexier ler)
		=> (byte)((int)ler.lexs[ler.loc].key >> ((int)ler.lexs[ler.loc].key >> 24));

	public static bool IsGroup(L key, L aim) => (int)(key & aim) << 8 != 0;
	public static bool IsKind(L key, L aim) => (byte)((int)key >> ((int)key >> 24)) == (byte)aim;
	public override bool Is(L key, L aim) =>
		(byte)aim == 0 ? IsGroup(key, aim) : aim <= L.BIN8 ? IsKind(key, aim) : key == aim;

	// check each keys distinct from others, otherwise throw exception
	public static void Distinct(IEnumerable<L> keys)
	{
		L kind = 0;
		StringBuilder err = new();
		foreach (var k in keys)
			if (((int)k & 15) == 0 && (kind ^ k) != (kind |= k))
				err.Append(k + " ");
		foreach (var k in keys)
			if (((int)k & 15) != 0 && (kind & k) != 0)
				err.Append(k + " ");
		if (err.Length > 0)
			throw new(err.ToString());
	}

	public Lexier() : base(Grammar) { }

	byte[] bs = new byte[4096]; // buffer used by some lexi
	int bn;
	int nn, nf, ne; // end of each number wad
	int indb; // indent byte, unknown: -1
	int indn; // indent count
	int[] inds = new int[100]; // [0, indent column...]
	int ind, indf, indt; // indent column 0 based, from input loc to loc excluded
	bool crlf; // \r\n found
	readonly List<string> path = [];
	public bool eof = true; // insert eol at input end
	public bool allValue = false; // set all lexis value
	public bool allBlank = false; // keep spaces without indent and offset, comments and empty lines

	public override void Clear()
	{
		base.Clear();
		indb = ind = -1; indn = indf = indt = 0; inds[0] = 0;
		crlf = false; path.Clear();
	}

	int Input(int f, int to, int x)
	{
		var n = x + to - f;
		if (bs.Length < n)
			Array.Resize(ref bs, n + 4095 & ~4095);
		input.Lexs(f, to, bs.AsSpan(x));
		return n;
	}

	public const int IndLoc = 2, IndrLoc = 6;

	void Indent()
	{
		if (ind < 0)
			return;
		int i = 1, c = ind;
		if (indn > 0) {
			i = Array.BinarySearch<int>(inds, 0, indn + 1, ind + IndLoc); // min indent as offset
			i ^= i >> 31;
			c = ind - inds[i - 1];
			if (i <= indn) { // drop these indents
				for (var x = indn; x >= i; indn = --x)
					if (x > i || c < IndrLoc)
						base.Lexi(inds[x] - inds[x - 1] < IndrLoc ? L.DED : L.DEDR,
							indf, indf, inds[x]);
					else { // still INDR
						Error(L.INDR, indf, indt, "indent same as upper lines expected");
						goto Done;
					}
			}
		}
		if (c >= IndLoc) {
			base.Lexi(c < IndrLoc ? L.IND : L.INDR, indf, indt, ind);
			indn = i;
			if (indn >= inds.Length) Array.Resize(ref inds, inds.Length << 1);
			inds[i] = ind;
		}
	Done: ind = -1;
	}

	protected override void InputEnd(int bn)
	{
		var end = true;
		if (eof)
			Wad(L.EOL, 1, ref end, bn, bn);
		ind = 0; indf = indt = bn; Indent();
	}

	protected override void Lexi(L key, int f, int to, object value)
	{
		Indent();
		Lexi<L> p;
		if (lexn > 0 && (p = lexs[lexn - 1]).to == f)
			if (key == L.RUN && (IsKind(p.key, L.LITERAL) || IsGroup(p.key, L.Right)))
				key = L.RUNP; // run follows previous lexi densely, high precedence
			else if (IsKind(key, L.LITERAL) && IsKind(p.key, L.LITERAL))
				Error(key, f, to, "literal can not densely follow literal");
			else if (IsKind(key, L.PRE) && !(IsKind(p.key, L.PRE) || IsGroup(p.key, L.Blank | L.Bin)))
				Error(key, f, to, "prefix operator can densely follow blank and prefix and binary only");
		base.Lexi(key, f, to, value);
	}

	protected override void WadErr(L key, int wad, bool end, int b, int f, int to)
	{
		if (key == L.PATH)
			key = L.NAME;
		base.WadErr(key, wad, end, b, f, to);
	}

	protected override void Wad(L key, int wad, ref bool end, int f, int to)
	{
		object v = null;
		if (from < 0) {
			from = f; bn = 0; path.Clear();
		}
		switch (key) {

		case L.EOL:
			if (!crlf && to == f + 2) { // \r\n found
				Error(key, f, to, @"use LF \n eol instead of CRLF \r\n");
				crlf = true;
			}
			if (allBlank
				|| lexn > 0 && lexs[lexn - 1].key != L.EOL) // no EOL for empty line
				base.Lexi(key, from, to, null);
			ind = 0; indf = indt = to;
			goto End;

		case L.SP:
			if (wad == 1)
				bs[0] = (byte)(f < 1 || input.Lex(f - 1) == '\n' ? 1 : 0); // maybe line start
			if (bs[0] != 0)
				if (indb < 0)
					indb = input.Lex(f); // first line start, save the indent byte
				else if (f < to && input.Lex(f) != indb) { // mix \s and \t
					bs[0] = 0; // as not line start and indent unchanged
					Error(key, f, to, "do not mix tabs and spaces for indent"); // TODO delayed to omit this if COMM
				}
			if (!end)
				return;
			if (bs[0] != 0) {
				ind = to - from << (indb == '\t' ? 2 : 0); // 4 column each \t
				indf = from; indt = to;
			}
			else if (allBlank)
				Lexi(key, from, to, null);
			goto End; // lexis already made

		case L.COMM:
			if (!allBlank)
				goto End;
			break;

		case L.COMMB:
			if (wad == 1) {
				bn = to; // begin wad loc
				return;
			}
			if (to - f != bn - from || input.Lex(f) != '#') // check end wad length
				return;
			end = true;
			if (!allBlank)
				goto End;
			key = L.COMM; v = nameof(L.COMMB); // as COMM
			break;

		case L.STRB:
			if (wad == 1) {
				bn = to; // begin wad loc
				return;
			}
			if (to - f != bn - from || input.Lex(f) != '"') // check end wad length
				return;
			end = true; bn = Input(bn, f, 0);
			break;

		case L.STR:
			if (wad == 1)
				return;
			if (end) {
				if (input.Lex(to - 1) == '\n') {
					Error(key, f, to, "eol unexpected");
					BackByte(to = f); // next lexi will be eol
				}
				break;
			}
			Input(f, to, bn); Unesc(f, to);
			return; // inside string

		case L.HEX:
			if (!end)
				return;
			bn = Input(from, to, 0);
			key = L.INT; v = Hex(); // as INT
			break;

		case L.NUM:
			if (wad == 2) nn = to - from; // end of integer wad
			else if (wad == 4) nf = to - from; // end of fraction wad
			else if (wad == 5) ne = to - from; // end of exponent wad
			if (!end)
				return;
			bn = Input(from, to, 0);
			v = Num(ref key);
			break;

		case L.NAME:
		case L.RUN:
			f = key == L.NAME ? from : from + 1;
			if (to - f > 40) {
				Error(key, f, to, "too long");
				to = f + 40;
			}
			bn = Input(f, to, 0);
			break;

		case L.PATH:
			if (wad == 1)
				return;
			var split = end || input.Lex(f) == '.';
			if (split) {
				if (bn > 40) {
					Error(L.NAME, to - 1, to - 1, "too long");
					bn = 40;
				}
				path.Add(Encoding.UTF8.GetString(bs, 0, bn));
				bn = 0;
			}
			if (end) {
				if (input.Lex(to - 1) == '\n') {
					Error(L.NAME, f, to, "eol unexpected");
					BackByte(to = f); // next lexi will be eol
				}
				key = input.Lex(from) != '.' ? L.NAME : L.RUN;
				v = path.ToArray();
				break;
			}
			if (!split) {
				Input(f, to, bn); Unesc(f, to);
			}
			return; // inside path

		default:
			if (allValue)
				bn = Input(from, to, 0);
			break;
		}
		if (!end)
			return;
		Lexi(key, from, to, v ?? (bn > 0 ? Encoding.UTF8.GetString(bs, 0, bn) : null));
	End: // lexi already made
		from = -1;
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
				Span<char> u = [
					(char)(Hex(bn + 2) << 12 | Hex(bn + 3) << 8 | Hex(bn + 4) << 4 | Hex(bn + 5)) ];
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
					Error(L.INT, from, from + bn, "hexadecimal out of range");
					return 0;
				}
		return v;
	}

	object Num(ref L key)
	{
		uint v = 0; int x = 0, dot = 0, e = 0;
		if (nn == bn) {
			key = L.INT; // as INT
			for (; x < nn; x++)
				if (bs[x] != '_')
					if (v < 214748364 || v == 214748364 && bs[x] <= '8')
						v = v * 10 + bs[x] - '0';
					else {
						Error(key, from, from + nn, "integer out of range");
						return 0;
					}
			return v;
		}
		key = L.FLOAT; // as FLOAT
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
			Error(key, from, from + bn, "float out of range");
			return 0f;
		}
		return w;
	}

	static readonly float[] Exps = [
		1e00f, 1e01f, 1e02f, 1e03f, 1e04f, 1e05f, 1e06f, 1e07f, 1e08f, 1e09f,
		1e10f, 1e11f, 1e12f, 1e13f, 1e14f, 1e15f, 1e16f, 1e17f, 1e18f, 1e19f,
		1e20f, 1e21f, 1e22f, 1e23f, 1e24f, 1e25f, 1e26f, 1e27f, 1e28f, 1e29f,
		1e30f, 1e31f, 1e32f, 1e33f, 1e34f, 1e35f, 1e36f, 1e37f, 1e38f, float.PositiveInfinity,
	];
}
