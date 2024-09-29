# Qutum compiler and interpreter running on .Net 8


## 词法

短词
- 换行、空白、注释
- 符号、十进制整数、十六进制整数、浮点数、词、字符串

长词
- 无冲突的注释块、无冲突的字符块
- 词串

转换
- 二元与前缀共用符，仅当左松右紧 为前缀符（左接换行、空白、注释、起始符为松，右非换行、空白、注释为紧）
- 后缀符 左接词组时，提升为高优先的紧密符

空白与缩进
- 按行首空白列处理缩进，tab和space不混用，tab视为4列
- 缩进 2 至 5 列为单缩进，至少 6 列为右缩进，两种缩进`Lexi`各自严格配对产生

行首连接符
- 二元连接符、后缀连接符、括号连接符
- 插入缩进，部分插入空行

其他
- 分类、分组，及相关判断
- 提示错误词，提示部分词无间隔


## 语块 是语法主要结构

- 语块：一行语句，可接右缩进的语串，可接单缩进的语串，可接连接块
	```
	语句
			语句最右词组_右缩进语串
		语句_单缩进语串
	连接块1
	连接块2
	...
	```
	```
	sentence
			indent_right_block_serie_of_sentence_rightmost_phrase
		indent_block_serie_of_sentence
	junction1
	junction2
	...
	```

- 语串：一或多个语块
	```
	语块1
	语块2
	...
	```
	```
	block1
	block2
	...
	```

- 连接块：连接符 接语串
	```
	连接符
		语串
	```
	```
	junct_lex
		block_serie
	...
	```

语块可嵌套
-	```
	语块1_语句
			语块1_语句最右词组_右缩进语块1
			语块1_语句最右词组_右缩进语块2
			...
		语块1_语句_单缩进语块1
		语块1_语句_单缩进语块2
		...
	语块1_连接块1_连接词
		连接块1_语块1_语句
				连接块1_语块1_语句最右词组_右缩进语块1
				连接块1_语块1_语句最右词组_右缩进语块2
				...
			连接块1_语块1_语句_单缩进语块1
			连接块1_语块1_语句_单缩进语块2
			...
		连接块1_连接块1
		连接块1_连接块2
		...
		连接块1_语块2
		...
	语块1_连接块2
	...
	语块2
	...
	```
-	```
	block1_sentence
			indent_right_block1_of_block1_sentence_rightmost_phrase
			indent_right_block2_of_block1_sentence_rightmost_phrase
			...
		indent_block1_of_block1_sentence
		indent_block2_of_block1_sentence
		...
	junction1_lex_of_block1
		block1_sentence_of_junction1
				indent_right_block1_of_block1_sentence_rightmost_phrase_of_junction1
				indent_right_block2_of_block1_sentence_rightmost_phrase_of_junction1
				...
			indent_block1_of_block1_sentence_of_junction1
			indent_block2_of_block1_sentence_of_junction1
			...
		junction1_of_junction1
		junction2_of_junction1
		...
		block2_of_junction1
		...
	junction2_of_block1
	...
	block2
	...
	```

多种连接块
- 二元连接块：二元符（含逗号符） 接语串，语串的首语句 可移至二元符之后
	```
	二元符
		语串
	二元符 语串首语句
		语串后续
	```
	```
	binary
		block_serie
	binary block_serie_sentence
		rest_of_block_serie
	```

- 后缀连接块：一或多个后缀符 可接语串，语串的首语句 可移至后缀符之后
	```
	后缀符1 后缀符2 ...
	后缀符1 后缀符2 ...
		语串
	后缀符1 后缀符2 ... 语串首语句
		语串后续
	```
	```
	postfix1 postfix2 ...
	postfix1 postfix2 ...
		block_serie
	postfix1 postfix2 ... block_serie_sentence
		rest_of_block_serie
	```

- 括号连接块：左括号 接语串首语句 接右括号 接语串后续
	```
	左括号 语串首语句 右括号
		语串后续
	```
	```
	left_bracket block_serie_sentence right_bracket
		rest_of_block_serie
	```

- 后接语串优先级高于连接符，连接符之间无优先级
	```
	1+2
	/	3 - 4
		/
			5
		* 6
	- 7
	```
	语义为 `( (1+2) /( (3-4)/5*6 ) )-7`
	```
	1+2
	[3*4]
		.a.b -5
	, a
		+ b
	, 1
	```
	语义为 `(1+2) [( (3*4).a.b (-5) )] (a+b) 1`


## 表达式 是语句主要结构

- 语句只在一行内
- 忽略语法空白（前缀符、数据传入的词法空白除外）
- `()` 在语句内调整优先级
- `+-` 等二元前缀共用符，按词法规则处理，`1-2` 为 1减2，`1 -2` 为 1 传入负2
- 错误尝试从右括号、行尾恢复，`( 1*[ 2 / [(3+4] - 5` 恢复为 `( 1* [ 2/[(3+4)] - 5 ] )`

词组为 表达式接后缀符、字面词、括号表达式、数据传入

高优先数据传入
- 空白分隔的多个表达式
- 高优先，但低于紧密符、括号
- `a b.c 2` 语义为 `( (a (b.c)) 2 )` 表示：数据a 传入b.c 再传入2
- `a b .c 2` 语义为 `( ((a b) .c) 2 )` 表示：数据a 传入b，其结果.c 传入2
- `1 * a -2 / b 3 4 == 5` 语义为 `1 / (a (-1)) / ((b 2) 3) == 4`

低优先数据传入
- 以词组开头，逗号分隔的多个表达式，最后可接逗号
- 低优先，因词组开头，故优先于词组左侧算符
- `-a,-1*2,` 语义为 `-(a (-1*2))` 表示：数据a 传入-1*2，其结果取负
- `1 * a, -2 / b, 3, 4 == 5` 语义为 `1 * (( (a (-2/b)) 3) (4 == 5))`
- `1 == a, -2 * b, 3, .d / 5, c .` 语义为 `1 == (a (-2*b) 3).d / (5 c).`

混合数据传入
- 空白、逗号可混合使用
- `a 1,2 3` 语义为 `((a 1) (2 3))`
- `a,1 2,3` 语义为 `(a (1 2) 3)`
- `1 * a b 2,3 * c 4,5` 语义为 `1 * ( (a b 2) (3*(c 4)) 5 )`
