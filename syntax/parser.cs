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
	enum Syn
	{
		all = 1, Block,
		block, stats, headr, nest,
		pre, right, stat,
		exp,
	}

	class Tree : Tree<Syn, Tree>
	{
	}

	class Parser : Parser<Lex, Syn, Tree, Lexer>
	{
		static readonly string grammar = @"
		all   = Block* | IND all DED Block*
		Block = block					=+

		block = exp stats?				=	block
		      | pre	stat* DED			=	prefix and statements
		stats = IND headr? stat* DED	=	statements
		headr = IND stat+ DED			=+	statements of head right datum
		      | IND headr DED			=|!	statements of head right datum
		right = block					=	right side
		      | EOL nest				=||!right side
		nest  = IND block DED			=	block

		pre   = OPPRE EOL IND block		=+_!prefix statement
		stat  =	EFFECT SP EOL			=|!	statement == leading DED exclusive
		      | OPBIN right				=+_	binary statement

		exp = EFFECT+ EOL		=+|! expression
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
