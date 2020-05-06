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
		line, Line,
		pre, b3, b43, b46, b53, b56, b6, b7,
		e1, e3, e43, e46, e53, e56, e6, e9,
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
		stat  =	BIN right				=^+_!	statement == prior than prefix expression
		      | Line stats?				=+-		statement
		line  = SP? e9 EOL				=^!||BLANK
		Line  = SP? e9 EOL				=!||BLANK

		e1    = LITERAL					=+_		literal
		      | LP e9 RP				=+-!|LP	parenthesis
		pre   = e1						=		datum
		      | PRE pre					=+_!	prefix operator
		b43   = BIN43 pre				=+_!	binary operator
		b46   = BIN46 e43				=+_!	binary operator
		b53   = BIN53 e46				=+_!	binary operator
		b56   = BIN56 e53				=+_!	binary operator
		b6    = BIN6 e56				=+_!	binary operator
		b7    = BIN7 e6					=+_!	binary operator
		e43   = pre b43*							=*+-expression
		e46   = pre b43* b46*						=*+-expression
		e53   = pre b43* b46* b53*					=*+-expression
		e56   = pre b43* b46* b53* b56*				=*+-expression
		e6    = pre b43* b46* b53* b56* b6*			=*+-expression
		e9    = pre b43* b46* b53* b56* b6* b7*		=*	expression
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
