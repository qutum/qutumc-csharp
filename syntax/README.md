<!--
Copyright 2008-2025 Qianyan Cai  
Under the terms of the GNU General Public License version 3  
http://qutum.com  http://qutum.cn
-->

## qutum.syntax.Lexier

qutum语言的词法解析器，读取byte流，产生`Lexi`流

- 数字解析，无冲突词块
- 单缩进、右缩进，各自严格配对
- 词转换，连接符
- 分类分组 及判断
- 读取尾添加换行、减缩进，空行、空白、注释分开产生
- 错误提示

## qutum.syntax.Synter

qutum语言的语法解析器，读取`Lexier`，产生语法树

语块结构
- 单缩进、右缩进 嵌套
- 语串
- 多种连接块

表达式
- 行内括号
- 高、低优先级 数据传入

错误恢复
