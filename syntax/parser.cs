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
		block, nest, stats, right, stat,
		bin,
		opb, opr, exp,
	}

	class Tree : Tree<Syn, Tree>
	{
	}

	class Parser : Parser<Lex, Syn, Tree, Lexer>
	{
		static readonly string grammar = @"
		all   = block+ | IND all DED block*
		block = exp stats?					=+ block
		nest  = IND block DED				= nested block
		stats = IND right? stat+ DED		= statements
		right = IND stat+ DED				= statements of right datum
		stat  = bin							= statment
		      | EFFECT SP EOL				=| statement expected
		bin   = opb block | opb EOL nest	=|| binary statement

		opb = ADD|SUB|MUL|DIV	=+ binary operator
		opr = EFFECT			=+ postfix operator
		exp = EFFECT+ EOL		=+| expression
		";

		public Parser(Lexer l) : base(grammar, l)
		{
			keep = false;
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
