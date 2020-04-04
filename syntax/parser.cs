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
		blocks, block,
		line, parse, inner,
		skips, skip, empty, comm
	}

	class Tree : Tree<Syn, Tree>
	{
	}

	class Parsers : Parser<Lex, Syn, Tree, Lexer>
	{
		static readonly string grammer =
		@"|= EOL DED
		blocks = block+ skips?
		block = skips? line
		line = parse EOL inner? =+
		parse = BLANK* CONTENT CONTENT_BLANK* =+
		inner = skips? IND block+ skips? DED
		skips = skip+
		skip = empty|comm | IND skip+ DED | DED skip+ IND
		empty = SP* EOL =+
		comm = SP* COMM BLANK* EOL
		";

		public Parsers(Lexer l) : base(grammer, l)
		{
			treeKeep = false;
			treeExpect = 0;
			treeDumper = o => !(o is ArraySegment<Token<Lex>> s) ? null
				: s.Count == 0 ? "" : string.Join(" ", s.Select(t => t.ToString()).ToArray());
		}

		protected override Syn Name(string name) => Enum.Parse<Syn>(name);

		public override Tree Parse()
		{
			var t = base.Parse();
			if (t.head is Tree x) {
			Loop:
				if (x.err == 0 && x.name == Syn.empty &&
						x.up != t && x.next?.err == 0 && x.next?.name == Syn.empty) {
					t.Add(new Tree {
						name = x.name, from = x.from, to = x.next.to,
						err = 1, dump = "too many empty lines"
					});
					while ((x = x.next).next?.err == 0 && x.next?.name == Syn.empty)
						;
				}
				if (x.head != null) {
					x = x.head;
					goto Loop;
				}
				do
					if (x.next != null) {
						x = x.next;
						goto Loop;
					}
				while ((x = x.up) != null);
			}
			foreach (var k in scan.errs)
				t.Add(new Tree {
					name = (Syn)(-(int)k.key), from = k.from, to = k.to, err = 1, expect = k.value
				});
			return t;
		}

		public override bool Check() => throw new NotImplementedException();
	}
}
