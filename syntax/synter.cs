//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//

using qutum.parser;
using System;
using System.Linq;

namespace qutum.syntax;

public enum Syn
{
	all = 1, allii, alli, Block,
	block, nestr, nests, nest, line,
	exp, feed, run, e0, e1, f1, F2,
	e2, e3, e43, e46, e53, e56, e6, e7, e8,
	E2, E3, E43, E46, E53, E56, E6, E7, E8,
	f2, f3, f43, f46, f53, f56, f6, f7, f8,
}

public class Synt : Synt<Syn, Synt>
{
}

public class Synter : Synter<Lex, Syn, Synt, Lexier>
{
	static readonly string grammar = """
	all   = allii? alli? Block*
	allii = INDR Block* DEDR
	alli  = IND Block* DED
	Block = block					=+

	block = line nestr? nests?
	nestr = INDR nest+ DEDR			=+		right nested blocks
	nests = IND nest+ DED			=!|IND	nested blocks

	nest  =	BIN block				=^+_!	nested binary block
	      | block					=+		nested block

	line  = SP? exp EOL				=*!||BLANK

	run   = RNAME					=+_
	e0    = LITERAL					=+_		literal
	      | LP exp RP				=+-!|LP	parenthesis
	e1    = RNAME1 					=+_		postfix
	      | feed* run				=		run
	e2    = e0 e1*					=		expression
	      | PRE e2					=+_!	prefix operator
	      | BINPRE e2				=+_!	binary prefix operator
	E2    = e0 feed+				=		feed Expression
	      | PRE E2					=+_!	prefix Operator
	      | BINPRE E2				=+_!	binary prefix Operator

	e43   = BIN43 e2							=+_!	bitwise operator
	e46   = BIN46 e2 e43*						=+_!	bitwise operator
	e53   = BIN53 e2 e43* e46*					=+_!	arithmetic operator
	e56   = BIN56 e2 e43* e46* e53*				=+_!	arithmetic operator
	e6    = BIN6  e2 e43* e46* e53* e56*		=+_!	comparison operator
	e7    = BIN7  e2 e43* e46* e53* e56* e6*	=+_!	logical operator
	E43   = BIN43 E2							=+_!	bitwise Operator
	E46   = BIN46 E2							=+_!	bitwise Operator
	E53   = BIN53 E2							=+_!	arithmetic Operator
	E56   = BIN56 E2							=+_!	arithmetic Operator
	E6    = BIN6  E2							=+_!	comparison Operator
	E7    = BIN7  E2							=+_!	logical Operator

	exp   = E2									=		feed Expression
	      | e2 e43* E43							=		feed Expression
	      | e2 e43* e46* E46					=		feed Expression
	      | e2 e43* e46* e53* E53				=		feed Expression
	      | e2 e43* e46* e53* e56* E56			=		feed Expression
	      | e2 e43* e46* e53* e56* e6* E6		=		feed Expression
	      | e2 e43* e46* e53* e56* e6* e7* E7	=		feed Expression
	      | e2 e43* e46* e53* e56* e6* e7*		=		whole expression

	f1    = RNAME1								=+_		Postfix 
	f2    = e0 f1*
	      | PRE f2								=+_!	Prefix operator
	      | BINPRE f2							=+_!	Binary as prefix operator
	F2    = e0 f1*								=*		Expression
	      | PRE f2								=+_!	Prefix operator
	f43   = BIN43 f2							=*+_!	Bitwise operator
	f46   = BIN46 f2 f43*						=*+_!	Bitwise operator
	f53   = BIN53 f2 f43* f46*					=*+_!	Arithmetic operator
	f56   = BIN56 f2 f43* f46* f53*				=*+_!	Arithmetic operator
	f6    = BIN6  f2 f43* f46* f53* f56*		=*+_!	Comparison operator
	f7    = BIN7  f2 f43* f46* f53* f56* f6*	=*+_!	Logical operator

	feed  =           F2 f43* f46* f53* f56* f6* f7*	=+		Serial feed
	      | NAME BIND f2 f43* f46* f53* f56* f6* f7*	=+_!	Name feed
	""";
	/* 

		f1    = RNAME1										=+_		Postfix
		F2    = e0 f1*										=*		Expression
				| PRE f2										=+_!	Prefix operator
		feed  =
				| NAME BIND f2 f43* f46* f53* f56* f6* f7*	=*+_!|	name feed
	*/
	public Synter(Lexier l) : base(grammar, l)
	{
		tree = false;
		errExpect = 0;
		dump = 1;
		dumper = o => o is not ArraySegment<Lexi<Lex>> s ? null
			: s.Count == 0 ? "" : string.Join(" ", s.Select(t => t.ToString()).ToArray());
	}

	protected override Syn Name(string name) => Enum.Parse<Syn>(name);

	public override Synt Parse()
	{
		var t = base.Parse();
		// add error lexis
		Synt tail = t;
		foreach (var (err, x) in ler.errs.Each())
			tail.AddNext(tail = new Synt {
				from = ~x, to = ~x - 1, err = -1, info = err.key, dump = "" + err
			});
		return t;
	}

	public override bool Check() => throw new NotImplementedException();
}
