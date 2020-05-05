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
		pre, right, stat,
		line, Line,
		b2, b33, b35, b43, b45, feed, b6, b7,
		e1, e2, e33, e35, e43, e45, e5, e6, e9,
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
		      | pre	stat* DED			=		prefix and statements
		stats = IND headr? stat* DED	=		statements
		headr = IND stat+ DED			=+		statements of head right datum
		      | IND headr DED			=!|IND	statements of head right datum
		right = block					=		right side
		      | EOL nest				=!|		right side
		nest  = IND block DED			=!|IND	nested block

		pre   = PRE EOL IND block		=+_!	prefix statement
		stat  =	BIN right				=^+_!	statement == prior than prefix expression
		      | Line stats?				=+-		statement
		line  = SP? e9 EOL				=^!||BLANK
		Line  = SP? e9 EOL				=!||BLANK

		e1    = LITERAL					=+_		literal
		      | LP e9 RP				=+-!|LP	parenthesis
		      | PRE e1					=+_!	prefix operator
		b2    = BIN2 e1					=+_!	binary operator
		b33   = BIN33 e2				=+_!	binary operator
		b35   = BIN35 e33				=+_!	binary operator
		b43   = BIN43 e35				=+_!	binary operator
		b45   = BIN45 e43				=+_!	binary operator
		feed  = e45+					=+!		datum feed
		b6    = BIN6 e5					=+_!	binary operator
		b7    = BIN7 e6					=+_!	binary operator
		e2    = e1 b2*										=+-	expression
		e33   = e1 b2* b33*									=+-	expression
		e35   = e1 b2* b33* b35*							=+-	expression
		e43   = e1 b2* b33* b35* b43*						=+-	expression
		e45   = e1 b2* b33* b35* b43* b45*					=+-	expression
		e5    = e1 b2* b33* b35* b43* b45* feed?			=*+-expression
		e6    = e1 b2* b33* b35* b43* b45* feed? b6*		=*+-expression
		e9    = e1 b2* b33* b35* b43* b45* feed? b6* b7*	=*	expression
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
