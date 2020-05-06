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
		bpre, right, stat,
		line, linep,
		e1, pre, prep, b3, b43, b46, b53, b56, b6, b7, e9,
		E1, Pre, Prep, B3, B43, B46, B53, B56, B6, B7, E9,
	}

	public class Tree : Tree<Syn, Tree>
	{
	}

	public class Parser : Parser<Lex, Syn, Tree, Lexer>
	{
		static readonly string grammar = @"
		all   = Block* | IND all DED Block*
		Block = block					=+

		block = line stats?				=		block
		      | bpre stat* DED			=		prefix block
		stats = IND headr? stat* DED	=		statements
		headr = IND stat+ DED			=+		statements of head right datum
		      | IND headr DED			=!|IND	statements of head right datum
		right = block					=		right side
		      | EOL nest				=!|		right side
		nest  = IND block DED			=!|IND	nested block

		bpre  = PRE EOL IND block		=+_!	prefix
		stat  =	BIN right				=+_!	statement
		      | linep stats?			=+-		statement
		line  = SP? pre e9 EOL			=!||BLANK
		linep = SP? prep e9 EOL			=!||BLANK == no leading prefix operator

		e1    = LITERAL E9*				=+_		literal
		      | LP pre e9 RP E9*		=+-!|LP	parenthesis
		E1    = LITERAL					=+_		literal
		      | LP pre e9 RP			=+-!|LP	parenthesis
		pre   = e1						=		expression
		      | PRE pre					=+_!	prefix operator
		prep  = e1						=		expression
		      | PREPURE pre				=+_!	prefix operator
		Pre   = E1						=		expression
		      | PRE Pre					=+_!	prefix operator
		Prep  = E1						=		expression
		      | PREPURE Pre				=+_!	prefix operator
		b43   = BIN43 pre							=+_!	binary operator
		b46   = BIN46 pre b43*						=+_!	binary operator
		b53   = BIN53 pre b43* b46*					=+_!	binary operator
		b56   = BIN56 pre b43* b46* b53*			=+_!	binary operator
		b6    = BIN6  pre b43* b46* b53* b56*		=+_!	binary operator
		b7    = BIN7  pre b43* b46* b53* b56* b6*	=+_!	binary operator
		e9    =      b43* b46* b53* b56* b6* b7*
		B43   = BIN43 Pre							=+_!	binary operator
		B46   = BIN46 Pre B43*						=+_!	binary operator
		B53   = BIN53 Pre B43* B46*					=+_!	binary operator
		B56   = BIN56 Pre B43* B46* B53*			=+_!	binary operator
		B6    = BIN6  Pre B43* B46* B53* B56*		=+_!	binary operator
		B7    = BIN7  Pre B43* B46* B53* B56* B6*	=+_!	binary operator
		E9    = Prep B43* B46* B53* B56* B6* B7*	=+-		feed
		";

		public Parser(Lexer l) : base(grammar, l)
		{
			tree = false;
			errExpect = 0;
			dump = 1;
			dumper = o => !(o is ArraySegment<Token<Lex>> s) ? null
				: s.Count == 0 ? "" : string.Join(" ", s.Select(t => t.ToString()).ToArray());
		}

		protected override Syn Name(string name) => Enum.Parse<Syn>(name);

		public override Tree Parse()
		{
			var t = base.Parse();
			Tree tail = t;
			scan.errs.Each((err, x) =>
				tail.AddNext(tail = new Tree {
					name = (Syn)(-(int)err.key), from = ~x, to = ~x, err = -1, info = err.value
				}));
			return t;
		}

		public override bool Check() => throw new NotImplementedException();
	}
}
