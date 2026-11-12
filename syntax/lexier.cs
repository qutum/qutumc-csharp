//
// Qutum 10 Compiler
// Copyright 2008-2027 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
global using Bytex = qutum.parser.X<byte>;
global using Lexix = qutum.parser.X<qutum.parser.Lexi<qutum.syntax.Lek>>;
global using JovBx = qutum.parser.Jov<qutum.parser.X<byte>>;

using qutum.parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qutum.syntax;

using B = ByteSet;
using K = Lek;
using Kord = char;
using One = ByteSet.One;

/*	EXC		!	NOT
	QUO		"	STR?
	NUM		#	COM (HASH)
	DOL		$
	PER		%	MOD
	AMP		&	AND
	APO		'	STR?
	LP		(	LP
	RP		)	RP
	AST		*	MUL
	PLUS	+	ADD,POSI
	COM		,	INP
	HYP		-	SUB,NEGA
	DOT		.	RUN
	SLA		/	DIV
	COL		:
	SCOL	;
	LT		<	LT
	EQ		=	quote
	GT		>	GT
	QUE		?
	AT		@
	LSB		[	LSB
	BSLA	\	byte block
	RSB		]	RSB
	HAT		^
	UND		_	NAME
	BAPO	`	PATH
	LCB		{	LCB
	VER		|	OR
	RCB		}	RCB
	TIL		~
*/

public static class LexIs
{
	extension(K aim)
	{
		public IEnumerable<K> GroupEach(bool kinds = false)
		{
			HashSet<K> ks = kinds ? [] : null;
			foreach (var key in Enum.GetValues<K>())
				if (key.InGroupAll(aim)) {
					yield return key;
					if (kinds && key.InKind() is var k and > 0)
						ks.Add((K)k);
				}
			if (kinds)
				foreach (var k in ks)
					yield return k;
		}
		public bool IsGroup() => (byte)aim == 0;
		public bool IsKind() => (ushort)aim is > 0 and < 16;
		public bool IsSingle() => (ushort)aim is >= 16 and <= 255;
	}

	extension(K key)
	{
		public bool InGroup(K aim) => (int)(key & aim) != 0;
		public bool InGroupAll(K aim) => (int)(key & aim) == (int)aim;
		public byte InKind() => (byte)((ushort)key >> 8);
		public bool InKind(K aim) => (byte)((int)key >> ((int)key >> 24)) == (byte)aim;

		public bool Is(K aim) =>
			aim.IsGroup() ? key.InGroup(aim) : aim.IsKind() ? key.InKind(aim) : key == aim;

		// key ordinal byte for single or which kind
		public Kord Ordin() => (Kord)(byte)((int)key >> ((int)key >> 24));
	}
}

// lexemes
public enum Lek : int
{
	// 0 for end of read
	// kinds, 255<<8
	LIT = 1, // literals
	BIN1,
	BIN2,
	BIN3,  // logical binary operators
	BIN4,  // comparison binary operators
	BIN53, // arithmetic binary operators
	BIN57, // arithmetic binary operators
	BIN67, // bitwise binary operators
	BIN7,
	PRE,   // prefix operators
	POST,  // postfix operators
	POSTD, // dense postfix operators

	// singles
	QUO = 16,
	// singles in proem, phrase, binary groups
	INP = Bin | QUO + 1 & 255, ORB, XORB,
	LP = Proem | XORB + 1 & 255, LCB, LSB = Junct | LCB + 1,
	RP = Phr | LSB + 1 & 255, RCB, RSB,
	// singles of blank group
	EOL = Blank | RSB + 1 & 255, IND, INDR, DED, DEDR, INDJ,
	SP, COM, COMB, PATH, DEC, HEX,

	// inside one kind
	// literal
	STR = Phr | Kind | LIT << 8 | 1, STRB, NAME, INT, FLOAT,
	// logical
	OR = Bin | Kind | BIN3 << 8 | 1, XOR, AND,
	// comparison
	EQ = Bin | Kind | BIN4 << 8 | 1, UEQ, LEQ, GEQ, LT, GT,
	// arithmetic
	ADD = Bin | Kind | BIN53 << 8 | 1, SUB,
	MUL = Bin | Kind | BIN57 << 8 | 1, DIV, MOD, DIVF, MODF,
	// bitwise
	SHL = Bin | Kind | BIN67 << 8 | 1, SHR, ANDB,
	// prefix: logical, arithmetic, bitwise
	NOT = Proem | Kind | PRE << 8 | 1, POSI, NEGA, NOTB,
	// postfix
	RUN = Phr | Junct | Kind | POST << 8 | 1,
	// dense postfix
	RUND = POSTD << 8 | POST << 8 ^ RUN,

	// multi groups, 255<<16
	Proem  /**/= 0x_01_0000, // proem
	Phr    /**/= 0x_02_0000, // phrase
	Junct  /**/= 0x_04_0000, // junct
	Bin    /**/= 0x_08_0000 | Junct, // binary, also junct
	Blank  /**/= 0x_80_0000, // blank
	Kind   /**/= 0x800_0000, // inside one kind, 8<<24
}

// lexic parser
public sealed partial class Lexier : LexierArr<K>
{
	static readonly LexGram<K> Grammar = new LexGram<K>()
		.k(K.INP).w[","].k(K.QUO).w["="]

		.k(K.OR).w["|"].k(K.XOR).w["!="].k(K.AND).w["&"].k(K.NOT).w["!"]

		.k(K.EQ).w["=="].k(K.UEQ).w["/="]
		.k(K.LEQ).w["<="].k(K.GEQ).w[">="].k(K.LT).w["<"].k(K.GT).w[">"]

		.k(K.ADD).w["+"].k(K.SUB).w["-"]
		.k(K.MUL).w["*"].k(K.DIV).w["/"].k(K.MOD).w["%"]
		.k(K.DIVF).w["//"].k(K.MODF).w["%%"]

		.k(K.ORB).w["||"].k(K.XORB).w["++"].k(K.ANDB).w["&&"].k(K.NOTB).w["--"]
		.k(K.SHL).w["<<"].k(K.SHR).w[">>"]

		.k(K.LP).w["("].k(K.LSB).w["["].k(K.LCB).w["{"]
		.k(K.RP).w[")"].k(K.RSB).w["]"].k(K.RCB).w["}"]

		.k(K.EOL).w["\n"]["\r\n"]
		.k(K.SP).w[(One)" \t"] // [\s\t]  |+\s+|+\t+
				.w[[]][" ", ..].loop["\t", ..].loop

		.k(K.COM).w["##"] // ##  |[\A^\n]+
				.w[[]][B.All - "\n", ..]
		.k(K.COMB).w["\\", .., "#"] // \\+#  +#\\+|+#|+[\A^#]+
				.w["#", "\\", ..].loop["#"].loop[B.All - "#", ..].loop

		.k(K.STRB).w["\\", .., "\""] // \\+"  +"\\+|+"|+[\A^"]+
				.w["\"", "\\", ..].loop["\""].loop[B.All - "\"", ..].loop
		.k(K.STR).w["\""] // "  *"|\n|\r\n|+[\L^"\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo[(One)"\"\n"]["\r\n"]
					[B.Line - "\"\\", ..].loop
					["\\", (One)"0tnr\".`\\"].loop
					["\\x", B.Hex, B.Hex].loop["\\u", B.Hex, B.Hex, B.Hex, B.Hex].loop

		.k(K.PATH).w["`"][".`"] // .?`  *`|\n|\r\n|+.|+[\L^.`\\]+|+\\[0tnr".`\\]|+\\x\x\x|+\\u\x\x\x\x
				.redo[(One)"`\n"]["\r\n"]["."].loop
					[B.Line - ".`\\", ..].loop
					["\\", (One)"0tnr\".`\\"].loop
					["\\x", B.Hex, B.Hex].loop["\\u", B.Hex, B.Hex, B.Hex, B.Hex].loop

		.k(K.NAME).w[B.Alpha + "_"][B.Alpha + "_", B.Word, ..] // [\a_]\w*
		.k(K.RUN).w["."] // .|.[\a_]\w*
					[".", B.Alpha + "_"][".", B.Alpha + "_", B.Word, ..]

		.k(K.HEX).w["0x"]["0X"] // 0[xX]  _*\x+  |+_*\x+  |.\x+  |+_+\x+  |[pP][\+\-]?\d+|[xX][\+\-]?\x+
				.w[B.Hex]["_", .., B.Hex]
				.w[[]][B.Hex, ..].loop["_", .., B.Hex, ..].loop
				.w[[]][".", B.Hex, ..]
				.w[[]]["_", .., B.Hex, ..].loop
				.w[[]][(One)"pP", B.Dec, ..][(One)"pP", (One)"+-", B.Dec, ..]
					[(One)"xX", B.Hex, ..][(One)"xX", (One)"+-", B.Hex, ..]
		.k(K.DEC) // 0|[1-9]  |+_*\d+  |.\d+  |+_+\d+  |[eE][\+\-]?\d+  |[fF]
				.w["0"][B.Dec - "0"]
				.w[[]][B.Dec, ..].loop["_", .., B.Dec, ..].loop
				.w[[]][".", B.Dec, ..]
				.w[[]]["_", .., B.Dec, ..].loop
				.w[[]][(One)"eE", B.Dec, ..][(One)"eE", (One)"+-", B.Dec, ..]
				.w[[]][(One)"fF"]
	;

	public const int IndMin = 2, IndrMin = 6;
	public List<Lexi<K>> blanks; // [blank except single eol and indent], merged into lexis at err
	public bool dump = false;

	public Lexier() { G = new(Grammar); }

	// results keep available
	public override void Dispose()
	{
		G.bufz = (Lexix)(dump ? 65535 : 7);
		G.lines = lines; G.errs = errs; G.blanks = blanks;
		base.Dispose(); G.Dispose();
		errs.Clear();
	}
	public IDisposable Begin(Lexer<byte, byte> read)
	{
		Dispose();
		G.Begin(read); G.Next(); gloc = G.Loc();
		Add(K.IND, default, inds[indz = 0] = 0); // whole read is indent 0
		indl = lexs.size; loc = default;
		return this;
	}

	readonly LexierGet G;
	Lexix gloc;
	int indz; // inds size from 1
	int[] inds = new int[50]; // {0, indent column...}
	Lexix indl; // indent start lexi

	public override bool Next()
	{
		if (loc.IncLess(+lexs.size)) return true;
		var siz = lexs.size;
	Next:
		if (siz < lexs.size || G.Get(ref gloc, out var ok) is var (k, j, v) && !ok)
			return loc < lexs.size;

		switch (k) {

		case default(K):
			Indent(0, (G.left.j.via, G.left.j.via)); // eor
			break;
		case K.IND:
			break;

		case K.EOL:
			if (Left.key is >= K.EOL and <= K.INDR)
				G.Blank(k, j, null); // no eol for empty line, so no empty indent block
			else
				Add(G.lex);
			break;

		case K.ADD:
		case K.SUB:
			if ((Left.j.via < j.on || Left.key.InGroup(K.Proem | K.Blank))
				&& j.via == G.right.j.on && !G.right.key.InGroup(K.Blank))
				// left not dense and right dense, binary as prefix
				k = k switch { K.ADD => K.POSI, K.SUB => K.NEGA, _ => throw new() };
			Lexi(k, j, v);
			break;

		default:
			Lexi(k, j, v);
			break;
		}
		goto Next;
	}

	void Lexi(K k, JovBx j, object v)
	{
		// indent at start of line
		if (G.left.key == K.EOL)
			Indent(0, (j.on, j.on));
		else if (G.left.key == K.IND)
			Indent((int)G.left.value, G.left.j);

		// left dense
		if (Left.j.via == j.on)
			if (k.InKind(K.POST) && Left.key.InGroup(K.Phr)) // postfix densely follows phrase, higher precedence
				k = (K)((int)k ^ (int)K.POST << 8 | (int)K.POSTD << 8);
			else if (k.InKind(K.LIT) && Left.key.InKind(K.LIT))
				G.Error(k, j, "literal can not densely follow literal");
			else if (k.InKind(K.PRE) && !Left.key.InGroup(K.Proem | K.Blank))
				G.Error(k, j, "prefix operator can densely follow proem or blank only");

		// junct at start of line
		if (G.left.key is K.EOL or K.IND && k.InGroup(K.Junct)) {
			Indent((G.left.key == K.IND ? (int)G.left.value : 0) + 3, (j.on, j.on), true); // between 2 and 4 column
			do
				Add(k, j, v);
			while (G.right.key.InKind(K.POST) // multi post as junct
				&& ((k, j, v) = G.Get(ref gloc, out var _)) is var _);
			if (!k.InGroup(K.Proem))
				Add(K.EOL, (j.via, j.via), null);
			indl = lexs.size; // junct indent start lexi
		}
		else
			Add(k, j, v);
	}
	void Add(K k, JovBx j, object v) => Add(new() { key = k, j = j, value = v });

	// indent column from 0
	void Indent(int ind, JovBx j, bool junct = false)
	{
		var i = Array.BinarySearch(inds, 1, indz, ind + IndMin); // indent with offset
		i ^= i >> 31; // >= 1
		var c = ind - inds[i - 1];
		// for i <= indz, drop these indents
		for (var x = indz; i <= x; indz = --x)
			if (x > i || c < IndrMin)
				Add(inds[x] - inds[x - 1] < IndrMin ? K.DED : K.DEDR,
					(j.on, j.on), inds[x]);
			else { // indent-right remains
				G.Error(K.INDR, j, "indent-right expected same as upper lines");
				inds[i] = ind;
				return;
			}
		// add indent
		if (c >= IndMin) {
			if (indl == lexs.size) // indent at start lexi, insert eol
				Add(K.EOL, (j.on, j.on), null);
			Add(c < IndrMin ? junct ? K.INDJ : K.IND : K.INDR, j, ind);
			indz = i;
			if (indz >= inds.Length) Array.Resize(ref inds, inds.Length << 1);
			inds[i] = ind;
			indl = lexs.size;
		}
	}

	Lexi<K> Left => lexs.last;

	public static Kord Ordin(Lexier ler) => ler.lexs[ler.loc].key.Ordin();
	public override bool Is(K key, K aim) => key.Is(aim);
}

// lexic parser base
internal sealed class LexierGet : Lexier<K>, Lexer<K, Lexi<K>>
{
	public Lexix bufz;
	ref Lexi<K> Buf(Lexix x) => ref lexs.s[+x & +bufz];
	public List<Lexi<K>> blanks; // [blank except single eol and indent], merged into lexis at err

	public LexierGet(LexGram<K> gram) : base(gram) { errs = null; }

	public override void Dispose()
	{
		base.Dispose();
		lexs = new(bufz + 1, default);
		Buf(lexs.size - 1).key = K.EOL; // eol at start of read
		crlf = false; indb = -1; path.Clear();
	}

	public Lexi<K> left, lex, right;
	public (K k, JovBx j, object v) Get(ref Lexix l, out bool ok)
	{
		ok = Next() || l < loc;
		if (!ok)
			return default;
		left = Buf(l - 1); lex = Buf(l); l += 1; right = Buf(l);
		return (lex.key, lex.j, lex.value);
	}

	protected override void Add(Lexi<K> lexi) { Buf(lexs.size) = lexi; lexs.j.via += 1; }
	public new void Error(K k, JovBx j, object value) => base.Error(k, j, value);
	public void Blank(K k, JovBx j, object value) => blanks?.Add(
		new() { key = k, j = j, value = value, err = 1 });

	protected override void WadErr(K k, int wad, bool end, int b, JovBx j)
	{
		if (k == K.PATH)
			k = K.NAME; // TODO
		base.WadErr(k, wad, end, b, j);
	}
	protected override void Eor(Bytex via)
	{
		if (lexs.size.zero || Buf(lexs.size - 1).key != K.EOL)
			Lexi(K.EOL, (via, via), null); // eol at end of read
		Lexi(default, (via, via), null);
	}

	Bytex bz; // buffer size
	Arr<Bytex, byte> bs = new((Bytex)4096); // long read buffer
	bool crlf; // \r\n found
	bool leadSp; // leading space
	short indb; // indent byte, unknown: -1
	Bytex ni, nf, ne; // end of each number wad
	readonly List<string> path = [];

	protected override void Wad(K k, int wad, ref bool end, JovBx j)
	{
		object d = null;
		if (!lon.yes) {
			lon = j.on; bz = default; path.Clear();
		}
		JovBx lj = (lon, j.via);
		switch (k) {

		case K.EOL:
			if (!crlf && +j.size == 2) { // \r\n found
				Error(k, j, @"use LF \n eol instead of CRLF \r\n");
				crlf = true;
			}
			Lexi(k, lj, null);
			goto End;

		case K.SP:
			if (wad == 1)
				leadSp = +j.on < 1 || read.Lex(j.on - 1) == '\n'; // maybe leading space
			if (leadSp)
				if (indb < 0)
					indb = read.Lex(j.on); // first leading space, save the indent byte
				else if (j.any && read.Lex(j.on) != indb) { // mix \s and \t
					leadSp = false; // not leading and indent unchanged
					Error(k, j, "do not mix tabs and spaces for indent"); // TODO omit this before comment ?
				}
			if (!end)
				return;
			if (leadSp)
				Lexi(K.IND, lj, +lj.size << (indb == '\t' ? 2 : 0)); // 4 column each \t
			else
				Blank(k, lj, null);
			goto End;

		case K.COM:
			if (end)
				Blank(k, lj, null);
			goto End;

		case K.COMB:
			if (wad == 1) {
				bz = j.via; // begin wad loc
				return;
			}
			if (j.size != bz - lon || read.Lex(j.on) != '#') // check end wad size
				return;
			end = true; bz = default;
			Blank(k, lj, null);
			goto End;

		case K.STRB:
			if (wad == 1) {
				bz = j.via; // begin wad loc
				return;
			}
			if (j.size != bz - lon || read.Lex(j.on) != '"') // check end wad size
				return;
			end = true; bz = Read((bz, j.on));
			break;

		case K.STR:
			if (wad == 1)
				return;
			if (end) {
				if (read.Lex(j.via - 1) == '\n') {
					Error(k, j, "eol unexpected");
					BackByte(lj.via = j.via = j.on); // next lexi will be eol
				}
				break;
			}
			Read(j, bz); Unesc(j);
			return; // inside string

		case K.DEC:
			if (wad == 2) ni = lj.size; // end of integer wad
			else if (wad == 4) nf = lj.size; // end of fraction wad
			else if (wad == 5) ne = lj.size; // end of exponent wad
			if (!end)
				return;
			bz = Read(lj);
			d = Dec(ref k);
			break;

		case K.HEX:
			if (wad == 3) ni = lj.size; // end of integer wad
			else if (wad == 5) nf = lj.size; // end of fraction wad
			else if (wad == 6) ne = lj.size; // end of exponent wad
			if (!end)
				return;
			bz = Read(lj);
			d = Hex(ref k);
			break;

		case K.NAME:
		case K.RUN:
			j.on = k == K.NAME ? lon : lon + 1;
			if (+j.size > 40) {
				Error(k, j, "too long");
				lj.via = j.via = j.on + 40;
			}
			bz = Read(j);
			break;

		case K.PATH:
			if (wad == 1)
				return;
			var split = end || read.Lex(j.on) == '.';
			if (split) {
				if (+bz > 40) {
					Error(K.NAME, (j.via - 1, j.via - 1), "too long");
					bz = (Bytex)40;
				}
				path.Add(Encoding.UTF8.GetString(bs.Span[..+bz]));
				bz = default;
			}
			if (end) {
				if (read.Lex(j.via - 1) == '\n') {
					Error(K.NAME, j, "eol unexpected");
					BackByte(lj.via = j.via = j.on); // next lexi will be eol
				}
				k = read.Lex(lon) != '.' ? K.NAME : K.RUN;
				d = path.ToArray();
				break;
			}
			if (!split) {
				Read(j, bz); Unesc(j);
			}
			return; // inside path
		}
		if (!end)
			return;
		Lexi(k, lj, d ?? (bz.pos ? Encoding.UTF8.GetString(bs.Span[..+bz]) : null));
	End: // lexi already made
		lon = Bytex.No;
	}

	Bytex Read(JovBx j, Bytex bon = default)
	{
		var n = bon + j.size;
		if (bs.size < n) bs.Resize(n + 4095 & (Bytex)~4095);
		read.Lexs(j, bs.Span[+bon..]);
		return n;
	}

	void Unesc(JovBx j)
	{
		if (bs[bz] != '\\')
			bz += j.size;
		else // unescape
			switch (bs[bz + 1]) {
			case (byte)'0': bs[bz] = (byte)'\0'; bz += 1; break;
			case (byte)'t': bs[bz] = (byte)'\t'; bz += 1; break;
			case (byte)'n': bs[bz] = (byte)'\n'; bz += 1; break;
			case (byte)'r': bs[bz] = (byte)'\r'; bz += 1; break;
			case (byte)'x': bs[bz] = (byte)(Hex(bz + 2) << 4 | Hex(bz + 3)); bz += 1; break;
			case (byte)'u':
				Span<char> u = [
					(char)(Hex(bz + 2) << 12 | Hex(bz + 3) << 8 | Hex(bz + 4) << 4 | Hex(bz + 5)) ];
				bz += Encoding.UTF8.GetBytes(u, bs.Span[+bz..]); break;
			default: bs[bz] = bs[bz + 1]; bz += 1; break;
			}
	}

	object Dec(ref K key)
	{
		uint v = 0; var x = Bytex.Z;
		int dot = 0, e = 0;
		if (ni == bz) {
			key = K.INT; // as INT
			for (; x < ni; x += 1)
				if (bs[x] != '_')
					if (v < 21_4748_364 || v == 21_4748_364 && bs[x] <= '8') v = v * 10 + bs[x] - '0';
					else {
						Error(key, (lon, lon + ni), "integer out of range");
						return 0;
					}
			return v;
		}
		key = K.FLOAT; // as FLOAT
		if (+bz > 65535) {
			Error(key, (lon, lon + bz), "float out of length");
			return 0f;
		}
		for (; x < ni; x += 1)
			if (bs[x] != '_')
				if (v <= 9_9999_999) v = v * 10 + bs[x] - '0';
				else dot++;
		for (x = ni + 1; x < nf; x += 1)
			if (bs[x] != '_')
				if (v <= 9_9999_999) (v, dot) = (v * 10 + bs[x] - '0', dot - 1);
				else break;
		if (v == 0)
			return 0f;
		if (nf < ne) {
			bool neg = bs[nf + 1] == '-';
			for (x = neg || bs[nf + 1] == '+' ? nf + 1 : nf; (x += 1) < ne;)
				if (e <= 9_9999_999) e = e * 10 + bs[x] - '0';
				else {
					Error(key, (lon, lon + ne), "exponent out of range");
					return 0f;
				}
			if (neg)
				e = -e;
		}
		e += dot;
		if (e <= 0)
			return e < -54 ? 0f : e < -38 ? v / Exps[-e - 38] / Exps[38] : v / Exps[-e];
		float d = v * Exps[e < 39 ? e : 39];
		if (float.IsInfinity(d)) {
			Error(key, (lon, lon + bz), "float out of range");
			return 0f;
		}
		return d;
	}

	static readonly float[] Exps = [
		1e00f, 1e01f, 1e02f, 1e03f, 1e04f, 1e05f, 1e06f, 1e07f, 1e08f, 1e09f,
		1e10f, 1e11f, 1e12f, 1e13f, 1e14f, 1e15f, 1e16f, 1e17f, 1e18f, 1e19f,
		1e20f, 1e21f, 1e22f, 1e23f, 1e24f, 1e25f, 1e26f, 1e27f, 1e28f, 1e29f,
		1e30f, 1e31f, 1e32f, 1e33f, 1e34f, 1e35f, 1e36f, 1e37f, 1e38f, float.PositiveInfinity,
	];

	byte Hex(Bytex x) => (byte)((bs[x] & 15) + (bs[x] << 25 >> 31 & 9));

	object Hex(ref K key)
	{
		uint v = 0; var x = (Bytex)2;
		int dot = 0, e = 0;
		if (ni == bz) {
			key = K.INT; // as INT
			for (; x < ni; x += 1)
				if (bs[x] != '_')
					if (v <= 0xffff_fff) v = v << 4 | Hex(x);
					else {
						Error(key, (lon, lon + ni), "hexadecimal out of range");
						return 0;
					}
			return v;
		}
		key = K.FLOAT; // as FLOAT
		if (+bz > 65535) {
			Error(key, (lon, lon + bz), "float out of length");
			return 0f;
		}
		for (; x < ni; x += 1)
			if (bs[x] != '_')
				if (v <= 0xffff_fff) v = v << 4 | Hex(x);
				else dot++;
		for (x = ni + 1; x < nf; x += 1)
			if (bs[x] != '_')
				if (v <= 0xffff_fff) (v, dot) = (v << 4 | Hex(x), dot - 1);
				else break;
		if (v == 0)
			return 0f;
		if (nf < ne) {
			bool hex = (char)bs[nf] is 'x' or 'X';
			bool neg = bs[nf + 1] == '-';
			for (x = neg || bs[nf + 1] == '+' ? nf + 1 : nf; (x += 1) < ne;)
				if (hex ? e <= 0x6fff_fff : e <= 9_9999_999)
					e = hex ? e << 4 | Hex(x) : e * 10 + bs[x] - '0';
				else {
					Error(key, (lon, lon + ni), "exponent out of range");
					return 0f;
				}
			if (neg)
				e = -e;
		}
		e += dot << 2;
		if (e <= 0)
			return e < -181 ? 0f : e < -126 ? v / Pows[-e - 126] / Pows[126] : v / Pows[-e];
		float d = v * Pows[e < 128 ? e : 128];
		if (float.IsInfinity(d)) {
			Error(key, (lon, lon + bz), "float out of range");
			return 0f;
		}
		return d;
	}

	static readonly float[] Pows = [.. Enumerable.Range(0, 129)
		.Select(x => BitConverter.Int32BitsToSingle(x + 127 << 23))];
}
