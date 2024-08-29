//
// Qutum 10 Compiler
// Copyright 2008-2024 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com  http://qutum.cn
//
using qutum.parser;
using qutum.parser.earley;
using System;
using System.Linq;

namespace qutum.syntax.earley;

public enum Esy
{
	all = 1, allii, alli, Block,
	block, nestr, nests, nest, line,
	e9, e8, e7, e6, e56, e53, e46, e43, e3, e2, e1, exp,
	F7, f8, f7, f6, f56, f53, f46, f43, f3, f2, f1, feed,
}

public class Esyn : Esyn<Esy, Esyn>
{
}

public class Earley : Earley<Lex, Esy, Esyn, Lexier>
{
	static readonly string grammar = """
	all   = allii? alli? Block*
	allii = INDR Block* DEDR
	alli  = IND Block* DED
	Block = block					=+

	block = line nestr? nests?
	line  = SP? exp EOL				=!||Blank
	nestr = INDR nest+ DEDR			=+		right nested blocks
	nests = IND nest+ DED			=!|IND	nested blocks
	nest  =	Bin block				=+_!	nested binary block
	      | block					=+		nested block

	e9    = LITERAL					=+_		literal
	      | LP exp RP				=+-!|LP	parenthesis
	e8    = POST | RUN				=+_		postfix
	      | feed
	e7    = e9 e8*					=		operand
	      | PRE e7					=+_!	prefix operator
	      | BINPRE e7				=+_!	binary prefix operator
	e56   = BIN56 e7							=+_!	bitwise operator
	e53   = BIN53 e7 e56*						=+_!	bitwise operator
	e46   = BIN46 e7 e56* e53*					=+_!	arithmetic operator
	e43   = BIN43 e7 e56* e53* e46*				=+_!	arithmetic operator
	e3    = BIN3  e7 e56* e53* e46* e43*		=+_!	comparison operator
	e2    = BIN2  e7 e56* e53* e46* e43* e3*	=+_!	logical operator
	exp  =       e7 e56* e53* e46* e43* e3* e2*	=*		expression == be greedy, whatever others
	
	f8    = POST								=+_		Postfix 
	f7    = e9 f8*
	      | PRE f7								=+_!	Prefix operator
	      | BINPRE f7							=+_!	Binary as prefix operator
	F7    = e9 f8*								=		Expression
	      | PRE f7								=+_!	Prefix operator
	f56   = BIN56 f7							=+_!	Bitwise operator
	f53   = BIN53 f7 f56*						=+_!	Bitwise operator
	f46   = BIN46 f7 f56* f53*					=+_!	Arithmetic operator
	f43   = BIN43 f7 f56* f53* f46*				=+_!	Arithmetic operator
	f3    = BIN3  f7 f56* f53* f46* f43*		=+_!	Comparison operator
	f2    = BIN2  f7 f56* f53* f46* f43* f3*	=+_!	Logical operator
	
	feed  =           F7 f56* f53* f46* f43* f3* f2*	=+!		Serial feed
	      | NAME BIND f7 f56* f53* f46* f43* f3* f2*	=+_!	Name feed
	""";

	public Earley(Lexier l) : base(grammar, l)
	{
		//greedy = true;
		tree = false;
		errExpect = 0;
		dump = 1;
		dumper = o => o is not ArraySegment<Lexi<Lex>> s ? null
			: s.Count == 0 ? "" : string.Join(" ", s.Select(t => t.ToString()).ToArray());
	}

	protected override Esy Name(string name) => Enum.Parse<Esy>(name);

	public override Esyn Parse()
	{
		var t = base.Parse();
		// add error lexis
		Esyn tail = t;
		foreach (var (err, x) in ler.errs.Each())
			tail.Append(tail = new Esyn {
				from = ~x, to = ~x - 1, err = -1, info = err.key, dump = "" + err
			});
		return t;
	}

	public override bool Check() => throw new NotImplementedException();
}
