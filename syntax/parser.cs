//
// Qutum 10 Compiler
// Copyright 2008-2020 Qianyan Cai
// Under the terms of the GNU General Public License version 3
// http://qutum.com
//

using qutum.parser;
using System;
using System.Linq;

namespace qutum.syntax
{
	public enum Syn
	{
		all = 1, Block,
		block, stats, headr, nest,
		pre, right, stat, line, linep,
		E1, E2, E2p, B3, B43, B46, B53, B56, B6, B7, B8, E9,
		e1, e2, e2p, b3, b43, b46, b53, b56, b6, b7, b8, e9,
		e0,
	}

	public class Synt : Synt<Syn, Synt>
	{
	}

	public class Parser : Parser<Lex, Syn, Synt, Lexer>
	{
		static readonly string grammar = """
		all   = Block* | IND all DED Block*
		Block = block					=+

		block = line stats?				=		block
		      | pre stat* DED			=		prefix block
		stats = IND headr? stat* DED	=		statements
		headr = IND stat+ DED			=+		statements of head right datum
		      | IND headr DED			=!|IND	statements of head right datum
		right = block					=		right side
		      | EOL nest				=!|		right side
		nest  = IND block DED			=!|IND	nested block

		pre  = PRE EOL IND block		=+_!	prefix
		stat  =	BIN right				=+_!	statement
		      | linep stats?			=+-		statement
		line  = SP? e2 e9 EOL			=*!||BLANK
		linep = SP? e2p e9 EOL			=*!||BLANK == no leading binary prefix operator

		e0    = LITERAL					=+_		literal
		      | LP e2 e9 RP				=+-!|LP	parenthesis

		e1    = RNAME1					=+_		postfix
		      | RNAME					=+_		run
		      | E9+						=+		feed
		E1    = RNAME1					=+_		postfix

		e2    = e0 e1*								=*		expression
		      | PRE e2								=+_!	prefix operator
		e2p   = e0 e1*								=*		expression
		      | PREPURE e2							=+_!	prefix operator
		b43   = BIN43 e2							=*+_!	binary operator
		b46   = BIN46 e2 b43*						=*+_!	binary operator
		b53   = BIN53 e2 b43* b46*					=*+_!	binary operator
		b56   = BIN56 e2 b43* b46* b53*				=*+_!	binary operator
		b6    = BIN6  e2 b43* b46* b53* b56*		=*+_!	binary operator
		b7    = BIN7  e2 b43* b46* b53* b56* b6*	=*+_!	binary operator
		e9    =     b43* b46* b53* b56* b6* b7*

		E2    = e0 E1*										=		expression
		      | PRE E2										=+_!	prefix operator
		E2p   = e0 E1*										=		expression
		      | PREPURE E2									=+_!	prefix operator
		B43   = BIN43 E2									=+_!	binary operator
		B46   = BIN46 E2 B43*								=+_!	binary operator
		B53   = BIN53 E2 B43* B46*							=+_!	binary operator
		B56   = BIN56 E2 B43* B46* B53*						=+_!	binary operator
		B6    = BIN6  E2 B43* B46* B53* B56*				=+_!	binary operator
		B7    = BIN7  E2 B43* B46* B53* B56* B6*			=+_!	binary operator
		E9    =          E2p B43* B46* B53* B56* B6* B7*	=+-		serial feed
		      | NAME BIND E2 B43* B46* B53* B56* B6* B7*	=+_!|	name feed
		""";

		public Parser(Lexer l) : base(grammar, l)
		{
			tree = false;
			errExpect = 0;
			dump = 1;
			dumper = o => o is not ArraySegment<Token<Lex>> s ? null
				: s.Count == 0 ? "" : string.Join(" ", s.Select(t => t.ToString()).ToArray());
		}

		protected override Syn Name(string name) => Enum.Parse<Syn>(name);

		public override Synt Parse()
		{
			var t = base.Parse();
			Synt tail = t;
			scan.errs.Each((err, x) =>
				tail.AddNext(tail = new Synt {
					name = (Syn)(-(int)err.key), from = ~x, to = ~x, err = -1, info = err.value
				}));
			return t;
		}

		public override bool Check() => throw new NotImplementedException();
	}
}
