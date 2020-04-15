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
		all = 1,
		block, nest, inest, ileft, right, istat, headr,
		opb, opr, exp,
	}

	class Tree : Tree<Syn, Tree>
	{
		public override int dumpOrder => err == 0 && info is Token<Lex> ? 0 : 1; // is binary operator
	}

	class Parser : Parser<Lex, Syn, Tree, Lexer>
	{
		static readonly string grammar = @"
		all   = block+ | IND all DED block*
		block = exp | istat DED
		nest  = IND block DED			= nested block
		inest = IND block				= nested block

		ileft = istat					= left side
		      | exp IND					= expression statement
		      | exp IND IND headr DED	= expression with head right statement
		right = block					=		right side
		      | EOL nest				=||!	right side
		istat = ileft EOL				=|		statement expected == forward to recover
		      | OPPRE EOL inest			=+_!	prefix statement
		      | ileft OPBIN right		=+_!	binary statement
		headr = headr OPBIN right		=+_||!	head right statement
		      |

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
